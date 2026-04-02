using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace UTF.UI.Services;

public class LanguageManager : ILanguageManager, INotifyPropertyChanged
{
    private string _currentLanguage = "zh-CN";
    private CultureInfo _currentCulture = new CultureInfo("zh-CN");
    private Dictionary<string, Dictionary<string, string>> _languageResources = new();
    private readonly string _languageDirectory = "";
    private readonly string _settingsFile = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

    public string CurrentLanguage
    {
        get => _currentLanguage;
        private set
        {
            if (_currentLanguage != value)
            {
                var oldLanguage = _currentLanguage;
                _currentLanguage = value;
                _currentCulture = new CultureInfo(value);

                OnPropertyChanged(nameof(CurrentLanguage));
                OnPropertyChanged(nameof(CurrentCulture));

                LanguageChanged?.Invoke(this, new LanguageChangedEventArgs
                {
                    OldLanguage = oldLanguage,
                    NewLanguage = value,
                    NewCulture = _currentCulture
                });
            }
        }
    }

    public CultureInfo CurrentCulture => _currentCulture;
    public List<LanguageInfo> SupportedLanguages { get; private set; } = new();

    public LanguageManager()
    {
        try
        {
            _languageDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages");
            _settingsFile = Path.Combine(_languageDirectory, "language-settings.json");

            InitializeLanguageDirectory();
            InitializeDefaultLanguages();
            LoadUserDefinedLanguages();
            LoadLanguageSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LanguageManager initialization failed: {ex.Message}");
        }
    }

    public string GetString(string key, string defaultValue = "")
    {
        if (_languageResources.TryGetValue(_currentLanguage, out var resources))
        {
            if (resources.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        if (_currentLanguage != "zh-CN" && _languageResources.TryGetValue("zh-CN", out var defaultResources))
        {
            if (defaultResources.TryGetValue(key, out var defaultVal))
            {
                return defaultVal;
            }
        }

        return string.IsNullOrEmpty(defaultValue) ? key : defaultValue;
    }

    public string GetString(string key, params object[] args)
    {
        var template = GetString(key);
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public void SetLanguage(string languageCode)
    {
        if (SupportedLanguages.Any(l => l.Code == languageCode))
        {
            CurrentLanguage = languageCode;
            Thread.CurrentThread.CurrentUICulture = _currentCulture;
            Thread.CurrentThread.CurrentCulture = _currentCulture;
            SaveLanguageSettings();
        }
    }

    public void LoadLanguagePack(string languageFilePath)
    {
        try
        {
            if (!File.Exists(languageFilePath))
            {
                throw new FileNotFoundException($"Language file not found: {languageFilePath}");
            }

            var json = File.ReadAllText(languageFilePath);
            var languagePack = JsonSerializer.Deserialize<LanguagePack>(json);

            if (languagePack != null && !string.IsNullOrEmpty(languagePack.LanguageCode))
            {
                _languageResources[languagePack.LanguageCode] = languagePack.Resources;

                if (!SupportedLanguages.Any(l => l.Code == languagePack.LanguageCode))
                {
                    var languageInfo = new LanguageInfo
                    {
                        Code = languagePack.LanguageCode,
                        Name = languagePack.LanguageName,
                        NativeName = languagePack.NativeName,
                        Flag = languagePack.Flag,
                        IsUserDefined = true
                    };

                    SupportedLanguages.Add(languageInfo);
                    SaveUserDefinedLanguages();
                }
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load language pack: {ex.Message}", ex);
        }
    }

    public void SaveLanguageSettings()
    {
        try
        {
            var settings = new LanguageSettings
            {
                CurrentLanguage = _currentLanguage,
                LastUpdated = DateTime.Now
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFile)!);
            File.WriteAllText(_settingsFile, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save language settings: {ex.Message}");
        }
    }

    private void InitializeLanguageDirectory()
    {
        try
        {
            if (!Directory.Exists(_languageDirectory))
            {
                Directory.CreateDirectory(_languageDirectory);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create language directory: {ex.Message}");
        }
    }

    private void InitializeDefaultLanguages()
    {
        LoadLanguagePacksFromJson();
        if (_languageResources.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine("No language packs were loaded from JSON. UI will fall back to default keys.");
        }
    }

    private void LoadLanguagePacksFromJson()
    {
        try
        {
            var languagesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages");
            if (!Directory.Exists(languagesPath))
            {
                System.Diagnostics.Debug.WriteLine($"Languages directory not found: {languagesPath}");
                return;
            }

            var jsonFiles = Directory.GetFiles(languagesPath, "*.json");
            foreach (var jsonFile in jsonFiles)
            {
                try
                {
                    LoadLanguagePackFromFile(jsonFile);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load language pack {jsonFile}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load language packs from JSON: {ex.Message}");
        }
    }

    private void LoadLanguagePackFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        var languagePack = JsonSerializer.Deserialize<LanguagePackJson>(json);

        if (languagePack?.LanguageInfo != null && languagePack.Translations.ValueKind != JsonValueKind.Undefined)
        {
            var languageCode = languagePack.LanguageInfo.Code;
            var resources = new Dictionary<string, string>();

            ExtractTranslations(languagePack.Translations, "", resources);

            _languageResources[languageCode] = resources;

            var languageInfo = new LanguageInfo
            {
                Code = languagePack.LanguageInfo.Code,
                Name = languagePack.LanguageInfo.Name,
                NativeName = languagePack.LanguageInfo.NativeName,
                Flag = languagePack.LanguageInfo.Flag,
                IsDefault = languagePack.LanguageInfo.IsDefault
            };

            if (!SupportedLanguages.Any(l => l.Code == languageCode))
            {
                SupportedLanguages.Add(languageInfo);
            }

            if (languageInfo.IsDefault)
            {
                SetLanguage(languageCode);
            }
        }
    }

    private void ExtractTranslations(object obj, string prefix, Dictionary<string, string> resources)
    {
        if (obj is JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                        ExtractTranslations(property.Value, key, resources);
                    }
                    break;
                case JsonValueKind.String:
                    resources[prefix] = element.GetString() ?? string.Empty;
                    break;
            }
        }
    }

    private void LoadUserDefinedLanguages()
    {
        try
        {
            var userLanguagesFile = Path.Combine(_languageDirectory, "user-languages.json");
            if (File.Exists(userLanguagesFile))
            {
                var json = File.ReadAllText(userLanguagesFile);
                var userLanguages = JsonSerializer.Deserialize<List<LanguageInfo>>(json);

                if (userLanguages != null)
                {
                    foreach (var lang in userLanguages)
                    {
                        if (!SupportedLanguages.Any(l => l.Code == lang.Code))
                        {
                            SupportedLanguages.Add(lang);

                            var langFile = Path.Combine(_languageDirectory, $"{lang.Code}.json");
                            if (File.Exists(langFile))
                            {
                                LoadLanguagePack(langFile);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load user-defined languages: {ex.Message}");
        }
    }

    private void SaveUserDefinedLanguages()
    {
        try
        {
            var userLanguages = SupportedLanguages.Where(l => l.IsUserDefined).ToList();
            var json = JsonSerializer.Serialize(userLanguages, new JsonSerializerOptions { WriteIndented = true });
            var filePath = Path.Combine(_languageDirectory, "user-languages.json");
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save user-defined languages: {ex.Message}");
        }
    }

    private void LoadLanguageSettings()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var json = File.ReadAllText(_settingsFile);
                var settings = JsonSerializer.Deserialize<LanguageSettings>(json);

                if (settings != null && !string.IsNullOrEmpty(settings.CurrentLanguage))
                {
                    if (SupportedLanguages.Any(l => l.Code == settings.CurrentLanguage))
                    {
                        CurrentLanguage = settings.CurrentLanguage;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load language settings: {ex.Message}");
        }
    }

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class LanguagePack
{
    public string LanguageCode { get; set; } = "";
    public string LanguageName { get; set; } = "";
    public string NativeName { get; set; } = "";
    public string Flag { get; set; } = "";
    public Dictionary<string, string> Resources { get; set; } = new();
}

public class LanguageSettings
{
    public string CurrentLanguage { get; set; } = "zh-CN";
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}

public class LanguagePackJson
{
    public LanguageInfoJson? LanguageInfo { get; set; }
    public JsonElement Translations { get; set; }
}

public class LanguageInfoJson
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string NativeName { get; set; } = "";
    public string Flag { get; set; } = "";
    public bool IsDefault { get; set; } = false;
}
