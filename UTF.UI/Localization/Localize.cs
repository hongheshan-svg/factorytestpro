using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace UTF.UI.Localization;

/// <summary>
/// 本地化绑定源，实现动态语言切换
/// </summary>
public class LocalizationBindingSource : INotifyPropertyChanged
{
    private static LocalizationBindingSource? _instance;
    public static LocalizationBindingSource Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new LocalizationBindingSource();
                Services.LocalizationService.CultureChanged += (culture) =>
                {
                    System.Console.WriteLine($"LocalizationBindingSource: CultureChanged event received for culture: {culture}");
                    _instance.OnPropertyChanged("Item[]");
                    System.Console.WriteLine($"LocalizationBindingSource: PropertyChanged event raised for Item[]");
                };
            }
            return _instance;
        }
    }

    public string this[string key]
    {
        get => Services.LocalizationService.GetText(key, key);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 本地化标记扩展，用于XAML中的动态文本本地化
/// </summary>
public class LocalizeExtension : MarkupExtension
{
    /// <summary>
    /// 本地化键
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 默认值，当找不到本地化文本时使用
    /// </summary>
    public string DefaultValue { get; set; } = string.Empty;

    /// <summary>
    /// 构造函数
    /// </summary>
    public LocalizeExtension()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="key">本地化键</param>
    public LocalizeExtension(string key)
    {
        Key = key;
    }

    /// <summary>
    /// 提供值 - 返回一个绑定以支持动态语言切换
    /// </summary>
    /// <param name="serviceProvider">服务提供程序</param>
    /// <returns>本地化后的文本或绑定</returns>
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return DefaultValue;

        // 创建绑定到 LocalizationBindingSource 的 Binding
        var binding = new Binding($"[{Key}]")
        {
            Source = LocalizationBindingSource.Instance,
            Mode = BindingMode.OneWay,
            FallbackValue = DefaultValue,
            TargetNullValue = DefaultValue
        };

        // 如果可以获取到目标对象，返回绑定
        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget provideValueTarget)
        {
            if (provideValueTarget.TargetObject is DependencyObject &&
                provideValueTarget.TargetProperty is DependencyProperty)
            {
                // 对于 DependencyProperty，返回绑定以支持动态更新
                return binding.ProvideValue(serviceProvider);
            }
        }

        // 对于非 DependencyProperty 的情况，返回当前值
        return LocalizationHelper.GetString(Key, DefaultValue);
    }
}

/// <summary>
/// 简化的别名，便于在XAML中使用 {loc:Localize}
/// </summary>
public class Localize : LocalizeExtension
{
    public Localize() : base()
    {
    }

    public Localize(string key) : base(key)
    {
    }
}
