using System;
using System.Collections.Generic;
using UTF.UI.Services;

namespace UTF.UI.Localization;

public static class LocalizationHelper
{
    private static ILanguageManager? _languageManager;

    public static string GetString(string key, string defaultValue = "")
    {
        var text = LocalizationService.GetText(key);
        return string.IsNullOrEmpty(text) || text == key ? defaultValue : text;
    }

    public static string GetStringFormatted(string key, params object[] args)
    {
        var format = GetString(key, key);
        return string.Format(format, args);
    }

    public static string CurrentCulture => LocalizationService.CurrentCulture;

    public static void SetCulture(string culture)
    {
        LocalizationService.CurrentCulture = culture;
        _languageManager?.SetLanguage(culture);
    }

    public static ILanguageManager GetLanguageManager()
    {
        _languageManager ??= new LanguageManager();
        return _languageManager;
    }

    public static void SetLanguage(string culture)
    {
        GetLanguageManager().SetLanguage(culture);
        LocalizationService.CurrentCulture = culture;
    }

    public static event Action<string>? CultureChanged
    {
        add => LocalizationService.CultureChanged += value;
        remove => LocalizationService.CultureChanged -= value;
    }

    public static void Initialize(ILanguageManager? languageManager = null)
    {
        _languageManager = languageManager ?? _languageManager ?? new LanguageManager();

        // 将语言管理器注册到 LocalizationService，服务会自动同步当前文化并触发事件
        LocalizationService.SetLanguageManager(_languageManager);
    }

    public static async System.Threading.Tasks.Task LoadTranslationsAsync(string filePath)
    {
        await LocalizationService.LoadTranslationsFromFileAsync(filePath);
    }
}
