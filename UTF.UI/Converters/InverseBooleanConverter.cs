using System;
using System.Globalization;
using System.Windows.Data;

namespace UTF.UI.Converters;

/// <summary>
/// 反转布尔值转换器。用于 IsEnabled="{Binding IsRunning, Converter={...}}"。
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
}
