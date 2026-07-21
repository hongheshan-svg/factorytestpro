using System;
using System.Globalization;
using System.Windows.Data;

namespace UTF.UI.Converters;

/// <summary>
/// 在 TextBox 文本与 <c>int?</c> 之间双向转换。
/// 空字符串 -> null；非数字文本 -> null（避免抛异常打断输入）。
/// 用于配置中心绑定 <c>GlobalSettings.DefaultMaxConcurrent</c> 等 nullable int 字段。
/// </summary>
public sealed class StringToNullableIntConverter : IValueConverter
{
    /// <summary>将 <c>int?</c> 转换为字符串（null -> 空串）。</summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            return i.ToString(culture);
        }

        return string.Empty;
    }

    /// <summary>将输入文本解析为 <c>int?</c>；解析失败返回 null。</summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s && int.TryParse(s, NumberStyles.Integer, culture, out var result))
        {
            return result;
        }

        // WPF ConvertBack 约定：解析失败时返回 null 表示无值。
        // IValueConverter.ConvertBack 返回类型为非 null 的 object，但 null 是约定语义，用 null! 抑制 CS8603。
        return null!;
    }
}
