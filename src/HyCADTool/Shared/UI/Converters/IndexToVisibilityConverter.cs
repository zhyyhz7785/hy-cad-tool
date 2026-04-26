using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HyCADTool.Shared.UI.Converters
{
    /// <summary>
    /// 把 int 与 ConverterParameter 比较，相等返回 Visible，否则 Collapsed。
    /// 用于 PropertyEditor.SelectedTabIndex 驱动多 Tab 内容互斥可见。
    /// </summary>
    public class IndexToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int current = ToInt(value);
            int target = ToInt(parameter);
            return current == target ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }

        private static int ToInt(object o)
        {
            if (o is int i) return i;
            if (o == null) return -1;
            return int.TryParse(o.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : -1;
        }
    }
}
