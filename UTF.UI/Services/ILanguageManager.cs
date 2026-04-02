using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace UTF.UI.Services;

/// <summary>
/// 语言管理器接口
/// </summary>
public interface ILanguageManager : INotifyPropertyChanged
{
    string CurrentLanguage { get; }
    CultureInfo CurrentCulture { get; }
    List<LanguageInfo> SupportedLanguages { get; }
    string GetString(string key, string defaultValue = "");
    string GetString(string key, params object[] args);
    void SetLanguage(string languageCode);
    void LoadLanguagePack(string languageFilePath);
    void SaveLanguageSettings();
    event EventHandler<LanguageChangedEventArgs> LanguageChanged;
}

/// <summary>
/// 语言信息
/// </summary>
public class LanguageInfo
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string NativeName { get; set; } = "";
    public string Flag { get; set; } = "";
    public bool IsDefault { get; set; } = false;
    public bool IsUserDefined { get; set; } = false;
}

/// <summary>
/// 语言改变事件参数
/// </summary>
public class LanguageChangedEventArgs : EventArgs
{
    public string OldLanguage { get; set; } = "";
    public string NewLanguage { get; set; } = "";
    public CultureInfo NewCulture { get; set; } = CultureInfo.InvariantCulture;
}
