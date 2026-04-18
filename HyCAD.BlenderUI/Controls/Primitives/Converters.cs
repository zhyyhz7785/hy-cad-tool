using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace HyCAD.BlenderUI.Controls.Primitives
{
    /// <summary>bool -> Visibility：true=Visible，false=Collapsed</summary>
    public class BoolToVisibility : MarkupExtension, IValueConverter
    {
        public static readonly BoolToVisibility Instance = new BoolToVisibility();
        public override object ProvideValue(IServiceProvider sp) => Instance;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>bool -> Visibility 反转：true=Collapsed，false=Visible</summary>
    public class InverseBoolToVisibility : MarkupExtension, IValueConverter
    {
        public static readonly InverseBoolToVisibility Instance = new InverseBoolToVisibility();
        public override object ProvideValue(IServiceProvider sp) => Instance;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v != Visibility.Visible;
    }

    /// <summary>
    /// 整数索引 + ConverterParameter(int 字面量) -> Visibility：
    /// value == int.Parse(parameter) 时 Visible，否则 Collapsed。
    /// 用于 PropertyEditor 多 Tab 内容互斥切换。
    ///
    /// 注意（pitfall B6）：调用时务必走 <c>Converter="{StaticResource IndexToVisibility}"</c> 命名资源，
    /// 不要写 <c>Converter="{prim:IndexToVisibility}"</c>，否则 MarkupExtension 当 Converter 会触发递归 SO。
    /// </summary>
    public class IndexToVisibility : MarkupExtension, IValueConverter
    {
        public static readonly IndexToVisibility Instance = new IndexToVisibility();
        public override object ProvideValue(IServiceProvider sp) => Instance;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int v = value is int i ? i : 0;
            int p = 0;
            if (parameter is int pi) p = pi;
            else if (parameter is string ps) int.TryParse(ps, NumberStyles.Integer, CultureInfo.InvariantCulture, out p);
            return v == p ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
