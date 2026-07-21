using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UTF.UI.Converters;

/// <summary>
/// 将空引用映射为 <see cref="Visibility.Collapsed"/>、非空引用映射为 <see cref="Visibility.Visible"/>。
/// 用于将面板的 <c>Visibility</c> 绑定到 <c>SelectedStep</c> 等可能为 null 的视图模型属性，
/// 在未选中任何项时自动隐藏面板，避免空引用绑定错误与视觉残留。
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// 将绑定值转换为 <see cref="Visibility"/>。
    /// 非空 -> <see cref="Visibility.Visible"/>；null -> <see cref="Visibility.Collapsed"/>。
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// 反向转换不支持（单向使用）。返回 <see cref="Visibility.Visible"/> 占位。
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Visibility.Visible;
}
