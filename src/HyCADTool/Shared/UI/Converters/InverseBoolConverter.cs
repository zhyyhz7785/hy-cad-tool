using System;
using System.Globalization;
using System.Windows.Data;

namespace HyCADTool.Shared.UI.Converters
{
    /// <summary>
    /// 布尔值取反转换器
    /// True → False, False → True
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return false;
        }
    }
}


