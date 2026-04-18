using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HyCADTool.Refactored.Presentation.Views.Converters
{
    /// <summary>
    /// 对象 null/非 null 转可见性。null → Collapsed，非 null → Visible。
    /// ConverterParameter == "Inverse"（不区分大小写）时反向：null → Visible，非 null → Collapsed。
    /// 用于 CrossSectionDrawWindow 右侧 PropertyEditor 在 SelectedBand=null 时隐藏条带相关 Expander。
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool inverse = parameter is string s && string.Equals(s, "Inverse", StringComparison.OrdinalIgnoreCase);
            bool isNull = value == null;
            return (isNull ^ inverse) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
