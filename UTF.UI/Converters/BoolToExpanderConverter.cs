using System;
using System.Globalization;
using System.Windows.Data;

namespace UTF.UI.Converters;

/// <summary>
/// 布尔值到展开器图标的转换器
/// </summary>
public class BoolToExpanderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? "▼" : "▶";
        }
        return "▶";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.DependencyProperty.UnsetValue;
    }
}
