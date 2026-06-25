using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HyCADTool.Shared.UI.Converters
{
    /// <summary>
    /// 布尔值转可见性转换器
    /// True → Visible, False → Collapsed；
    /// ConverterParameter="Invert" 时取反（True → Collapsed, False → Visible）。
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var invert = parameter is string s
                && string.Equals(s, "Invert", StringComparison.OrdinalIgnoreCase);

            if (value is bool boolValue)
            {
                if (invert)
                    boolValue = !boolValue;
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return invert ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }
}


