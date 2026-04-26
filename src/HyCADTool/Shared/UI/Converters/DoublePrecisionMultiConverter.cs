using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HyCADTool.Shared.UI.Converters
{
    /// <summary>
    /// 根据界面小数位设置格式化 double。
    /// values[0] = 数值，values[1] = 小数位。
    /// </summary>
    public class DoublePrecisionMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length == 0 || values[0] == null)
            {
                return string.Empty;
            }

            double number;
            try
            {
                number = System.Convert.ToDouble(values[0], culture);
            }
            catch
            {
                return values[0]?.ToString() ?? string.Empty;
            }

            int precision = 2;
            if (values.Length > 1 && values[1] != null)
            {
                try
                {
                    precision = System.Convert.ToInt32(values[1], culture);
                }
                catch
                {
                    precision = 2;
                }
            }

            if (precision < 0) precision = 0;
            if (precision > 6) precision = 6;
            return number.ToString("F" + precision, culture);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            var text = value as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                // 失焦时空串不写回 0，避免误清空；源值不变后 Convert 会恢复格式化显示。
                return new object[] { DependencyProperty.UnsetValue, Binding.DoNothing };
            }

            double parsed;
            if (!double.TryParse(text, NumberStyles.Float, culture, out parsed))
            {
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    return new object[] { DependencyProperty.UnsetValue, Binding.DoNothing };
                }
            }

            return new object[] { parsed, Binding.DoNothing };
        }
    }
}
