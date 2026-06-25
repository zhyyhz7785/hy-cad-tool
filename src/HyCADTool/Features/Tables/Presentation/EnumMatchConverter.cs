using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// 枚举值与 ConverterParameter 比较：相等→true。
    /// ConvertBack 仅在置为 true 时回写对应枚举，置 false 时忽略（用于 ToggleButton 互斥组）。
    /// </summary>
    public sealed class EnumMatchConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter != null)
            {
                try
                {
                    return Enum.Parse(targetType, parameter.ToString());
                }
                catch (ArgumentException)
                {
                    return Binding.DoNothing;
                }
            }

            return Binding.DoNothing;
        }
    }
}
