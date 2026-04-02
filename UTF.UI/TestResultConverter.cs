using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace UTF.UI;

/// <summary>
/// 测试结果到颜色的转换器
/// </summary>
public class TestResultToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string result)
        {
            return result.ToLower() switch
            {
                "通过" or "pass" or "成功" => new SolidColorBrush(Colors.Green),
                "失败" or "fail" or "错误" => new SolidColorBrush(Colors.Red),
                "执行中" or "running" => new SolidColorBrush(Colors.Orange),
                "等待" or "waiting" => new SolidColorBrush(Colors.Gray),
                "暂停" or "paused" => new SolidColorBrush(Colors.Blue),
                _ => new SolidColorBrush(Colors.Black)
            };
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 反向转换不支持
        return System.Windows.DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// 测试结果到背景色的转换器
/// </summary>
public class TestResultToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string result)
        {
            return result.ToLower() switch
            {
                "通过" or "pass" or "成功" => new SolidColorBrush(Color.FromArgb(30, 0, 255, 0)),
                "失败" or "fail" or "错误" => new SolidColorBrush(Color.FromArgb(30, 255, 0, 0)),
                "执行中" or "running" => new SolidColorBrush(Color.FromArgb(30, 255, 165, 0)),
                _ => new SolidColorBrush(Colors.Transparent)
            };
        }
        return new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 反向转换不支持
        return System.Windows.DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// 测试状态到颜色的转换器
/// </summary>
public class TestStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status.ToLower() switch
            {
                "已连接" or "connected" or "就绪" or "ready" => new SolidColorBrush(Colors.Green),
                "测试中" or "testing" or "执行中" or "running" => new SolidColorBrush(Colors.Orange),
                "故障" or "error" or "失败" or "failed" => new SolidColorBrush(Colors.Red),
                "未连接" or "disconnected" or "离线" or "offline" => new SolidColorBrush(Colors.Gray),
                "完成" or "completed" or "完成" => new SolidColorBrush(Colors.Blue),
                "等待" or "waiting" or "pending" => new SolidColorBrush(Colors.Purple),
                _ => new SolidColorBrush(Colors.Black)
            };
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 反向转换不支持
        return System.Windows.DependencyProperty.UnsetValue;
    }
}

/// <summary>
/// DUT数量到颜色的转换器（用于统计显示）
/// </summary>
public class DutCountToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text && parameter is string type)
        {
            return type.ToLower() switch
            {
                "pass" or "success" => new SolidColorBrush(Colors.Green),
                "fail" or "error" => new SolidColorBrush(Colors.Red),
                "active" or "running" => new SolidColorBrush(Colors.Orange),
                "total" => new SolidColorBrush(Colors.Blue),
                _ => new SolidColorBrush(Colors.Black)
            };
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 反向转换不支持
        return System.Windows.DependencyProperty.UnsetValue;
    }
}


