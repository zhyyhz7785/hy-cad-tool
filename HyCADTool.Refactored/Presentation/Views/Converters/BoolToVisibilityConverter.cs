using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HyCADTool.Refactored.Presentation.Views.Converters
{
    /// <summary>
    /// 布尔值转可见性转换器
    /// True → Visible, False → Collapsed
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
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


