using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using UTF.Logging;

namespace UTF.UI.Converters
{
    public class LogLevelToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Debug => new SolidColorBrush(Color.FromRgb(108, 117, 125)),    // 灰色
                    LogLevel.Info => new SolidColorBrush(Color.FromRgb(0, 123, 255)),       // 蓝色
                    LogLevel.Warning => new SolidColorBrush(Color.FromRgb(255, 193, 7)),    // 黄色
                    LogLevel.Error => new SolidColorBrush(Color.FromRgb(220, 53, 69)),      // 红色
                    LogLevel.Critical => new SolidColorBrush(Color.FromRgb(138, 43, 226)),  // 紫色
                    _ => new SolidColorBrush(Color.FromRgb(33, 37, 41))                        // 默认深灰色
                };
            }
            return new SolidColorBrush(Color.FromRgb(33, 37, 41));
        }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // 反向转换不支持
        return System.Windows.DependencyProperty.UnsetValue;
    }
    }
}
