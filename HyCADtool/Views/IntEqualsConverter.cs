using System;
using System.Globalization;
using System.Windows.Data;

namespace HyCADTool.Views
{
    public class IntEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            int intValue = System.Convert.ToInt32(value);
            int paramValue = System.Convert.ToInt32(parameter);
            return intValue == paramValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if ((bool)value && parameter != null)
            {
                return System.Convert.ToInt32(parameter);
            }
            return Binding.DoNothing;
        }
    }
}
