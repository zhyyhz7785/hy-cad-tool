using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// Blender 风格面板标题栏：
    ///  - 左侧 Title
    ///  - 中间 SearchBox（可选）
    ///  - 右侧 PinButton（可选）
    /// </summary>
    public class PanelHeader : Control
    {
        static PanelHeader()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(PanelHeader),
                new FrameworkPropertyMetadata(typeof(PanelHeader)));
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(PanelHeader),
            new PropertyMetadata(string.Empty));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
            nameof(SearchText), typeof(string), typeof(PanelHeader),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public static readonly DependencyProperty ShowSearchProperty = DependencyProperty.Register(
            nameof(ShowSearch), typeof(bool), typeof(PanelHeader),
            new PropertyMetadata(true));

        public bool ShowSearch
        {
            get => (bool)GetValue(ShowSearchProperty);
            set => SetValue(ShowSearchProperty, value);
        }

        public static readonly DependencyProperty ShowPinProperty = DependencyProperty.Register(
            nameof(ShowPin), typeof(bool), typeof(PanelHeader),
            new PropertyMetadata(true));

        public bool ShowPin
        {
            get => (bool)GetValue(ShowPinProperty);
            set => SetValue(ShowPinProperty, value);
        }

        public static readonly DependencyProperty IsPinnedProperty = DependencyProperty.Register(
            nameof(IsPinned), typeof(bool), typeof(PanelHeader),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public bool IsPinned
        {
            get => (bool)GetValue(IsPinnedProperty);
            set => SetValue(IsPinnedProperty, value);
        }
    }
}
