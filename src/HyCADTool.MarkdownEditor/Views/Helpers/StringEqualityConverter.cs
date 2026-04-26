using System;
using System.Globalization;
using System.Windows.Data;

namespace HyCADTool.MarkdownEditor.Views.Helpers
{
    /// <summary>
    /// 将字符串值与 ConverterParameter 比较，相等返回 true，否则返回 false。
    /// 用于 IsEnabled 绑定：仅当模式匹配时启用控件。
    /// </summary>
    public class StringEqualityConverter : IValueConverter
    {
        public static readonly StringEqualityConverter Instance = new StringEqualityConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
