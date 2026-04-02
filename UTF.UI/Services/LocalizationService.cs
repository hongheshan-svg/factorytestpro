using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace UTF.UI.Services;

public class LocalizedText
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; } = new();

    public void SetTranslation(string culture, string value)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return;
        Translations[culture] = value ?? string.Empty;
    }

    public string GetText(string culture = "")
    {
        if (string.IsNullOrEmpty(culture))
        {
            culture = CultureInfo.CurrentUICulture.Name;
        }

        if (Translations.TryGetValue(culture, out var exactMatch))
        {
            return exactMatch;
        }

        var languageCode = culture.Split('-')[0];
        if (Translations.TryGetValue(languageCode, out var languageMatch))
        {
            return languageMatch;
        }

        if (Translations.TryGetValue("en", out var englishMatch))
        {
            return englishMatch;
        }

        if (Translations.TryGetValue("zh", out var chineseMatch))
        {
            return chineseMatch;
        }

        foreach (var translation in Translations.Values)
        {
            return translation;
        }

        return Key;
    }
}

public static class LocalizationService
{
    private static readonly Dictionary<string, LocalizedText> _localizedTexts = new();
    private static ILanguageManager? _languageManager;
    private static string _currentCulture = CultureInfo.CurrentUICulture.Name;
    private static bool _isSyncingWithLanguageManager;
    
    public static event Action<string>? CultureChanged;
    
    static LocalizationService()
    {
        // 不加载语言包，直接使用中文
    }

    private static void LoadTranslationsFromDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(directoryPath, "*.json"))
        {
            try
            {
                LoadTranslationsFromFileAsync(file).GetAwaiter().GetResult();
            }
            catch
            {
                // 忽略加载错误
            }
        }
    }

    public static async Task LoadTranslationsFromFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return;

            var jsonContent = await File.ReadAllTextAsync(filePath);
            using var document = JsonDocument.Parse(jsonContent);

            if (!document.RootElement.TryGetProperty("Translations", out var translationsElement))
            {
                // 如果不是LanguageInfo/Translations结构，尝试当作扁平结构
                var flatTranslations = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(jsonContent);
                if (flatTranslations != null)
                {
                    foreach (var kvp in flatTranslations)
                    {
                        RegisterText(kvp.Key, kvp.Value);
                    }
                }
                return;
            }

            foreach (var category in translationsElement.EnumerateObject())
            {
                foreach (var entry in category.Value.EnumerateObject())
                {
                    var key = $"{category.Name}.{entry.Name}";

                    if (!_localizedTexts.TryGetValue(key, out var localizedText))
                    {
                        localizedText = new LocalizedText { Key = key };
                        _localizedTexts[key] = localizedText;
                    }

                    switch (entry.Value.ValueKind)
                    {
                        case JsonValueKind.String:
                            localizedText.SetTranslation(Path.GetFileNameWithoutExtension(filePath), entry.Value.GetString() ?? string.Empty);
                            break;
                        case JsonValueKind.Object:
                            foreach (var translation in entry.Value.EnumerateObject())
                            {
                                localizedText.SetTranslation(translation.Name, translation.Value.GetString() ?? string.Empty);
                            }
                            break;
                    }
                }
            }
        }
        catch
        {
            // 忽略加载错误
        }
    }
    
    public static string CurrentCulture
    {
        get => _currentCulture;
        set => UpdateCurrentCulture(value, raiseEvent: true, syncToLanguageManager: true);
    }
    
    public static string GetText(string key, string? culture = null)
    {
        if (_localizedTexts.TryGetValue(key, out var localizedText))
        {
            return localizedText.GetText(culture ?? _currentCulture);
        }
        return key;
    }
    
    public static void RegisterText(string key, Dictionary<string, string> translations)
    {
        var localizedText = new LocalizedText { Key = key };
        foreach (var kvp in translations)
        {
            localizedText.SetTranslation(kvp.Key, kvp.Value);
        }
        _localizedTexts[key] = localizedText;
    }
    
    public static void SetLanguageManager(ILanguageManager languageManager)
    {
        if (languageManager == null)
            throw new ArgumentNullException(nameof(languageManager));

        if (_languageManager != null)
        {
            _languageManager.LanguageChanged -= OnLanguageManagerChanged;
        }

        _languageManager = languageManager;
        _languageManager.LanguageChanged += OnLanguageManagerChanged;

        // 同步当前文化
        UpdateCurrentCulture(_languageManager.CurrentLanguage, raiseEvent: true, syncToLanguageManager: false);
    }

    public static ILanguageManager GetLanguageManager()
    {
        if (_languageManager == null)
        {
            // 创建一个简化的语言管理器，固定为中文
            _languageManager = new LanguageManager();
        }
        return _languageManager;
    }

    private static void OnLanguageManagerChanged(object? sender, LanguageChangedEventArgs e)
    {
        if (_isSyncingWithLanguageManager)
            return;

        UpdateCurrentCulture(e.NewLanguage, raiseEvent: true, syncToLanguageManager: false);
    }

    private static void UpdateCurrentCulture(string culture, bool raiseEvent, bool syncToLanguageManager)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return;

        if (_currentCulture == culture)
            return;

        _currentCulture = culture;

        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
        }
        catch (CultureNotFoundException)
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        }

        if (syncToLanguageManager && _languageManager != null && _languageManager.CurrentLanguage != culture)
        {
            try
            {
                _isSyncingWithLanguageManager = true;
                _languageManager.SetLanguage(culture);
            }
            finally
            {
                _isSyncingWithLanguageManager = false;
            }
        }

        if (raiseEvent)
        {
            CultureChanged?.Invoke(culture);
        }
    }

    public static IEnumerable<string> GetSupportedCultures()
    {
        var set = new HashSet<string>(_localizedTexts.Values.SelectMany(t => t.Translations.Keys))
        {
            "zh-CN", "en-US", "zh", "en"
        };
        return set;
    }

}
