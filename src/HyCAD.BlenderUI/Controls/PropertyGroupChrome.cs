using System.Windows;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// 三级 PropertyGroup（Expander）头条/装饰：头条背景与面板内容背景解耦，以贴近 Blender 侧栏层级。
    /// </summary>
    public static class PropertyGroupChrome
    {
        public static readonly DependencyProperty HeaderBackgroundProperty =
            DependencyProperty.RegisterAttached(
                "HeaderBackground",
                typeof(Brush),
                typeof(PropertyGroupChrome),
                new FrameworkPropertyMetadata(null));

        public static void SetHeaderBackground(DependencyObject element, Brush value) =>
            element.SetValue(HeaderBackgroundProperty, value);

        public static Brush GetHeaderBackground(DependencyObject element) =>
            (Brush)element.GetValue(HeaderBackgroundProperty);

        public static readonly DependencyProperty ShowGripProperty =
            DependencyProperty.RegisterAttached(
                "ShowGrip",
                typeof(bool),
                typeof(PropertyGroupChrome),
                new FrameworkPropertyMetadata(true));

        public static void SetShowGrip(DependencyObject element, bool value) =>
            element.SetValue(ShowGripProperty, value);

        public static bool GetShowGrip(DependencyObject element) =>
            (bool)element.GetValue(ShowGripProperty);
    }
}
