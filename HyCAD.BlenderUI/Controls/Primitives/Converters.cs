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
}
