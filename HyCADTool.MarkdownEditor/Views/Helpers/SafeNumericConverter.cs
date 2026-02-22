using System;
using System.Globalization;
using System.Windows.Data;

namespace HyCADTool.MarkdownEditor.Views.Helpers
{
    /// <summary>
    /// 安全数值转换器：无效输入时返回 Binding.DoNothing，避免绑定异常。
    /// </summary>
    public sealed class SafeNumericConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string s = value as string;
            if (s == null) return Binding.DoNothing;

            if (targetType == typeof(string))
                return s;

            if (targetType == typeof(double))
            {
                if (string.IsNullOrWhiteSpace(s)) return Binding.DoNothing;
                if (double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                    return d;
                return Binding.DoNothing;
            }

            if (targetType == typeof(int))
            {
                if (string.IsNullOrWhiteSpace(s)) return Binding.DoNothing;
                if (int.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out int i))
                    return i;
                return Binding.DoNothing;
            }

            return Binding.DoNothing;
        }
    }
}
