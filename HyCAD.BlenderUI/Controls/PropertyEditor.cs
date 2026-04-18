using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// Blender 风格属性编辑器组合壳：
    ///  顶部 PanelHeader / 左侧 IconTabBar / 右侧 ScrollViewer 包 Content
    /// </summary>
    public class PropertyEditor : ContentControl
    {
        static PropertyEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(PropertyEditor),
                new FrameworkPropertyMetadata(typeof(PropertyEditor)));
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(PropertyEditor),
            new PropertyMetadata(string.Empty));
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
            nameof(SearchText), typeof(string), typeof(PropertyEditor),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public static readonly DependencyProperty IsPinnedProperty = DependencyProperty.Register(
            nameof(IsPinned), typeof(bool), typeof(PropertyEditor),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public bool IsPinned
        {
            get => (bool)GetValue(IsPinnedProperty);
            set => SetValue(IsPinnedProperty, value);
        }

        public static readonly DependencyProperty ShowSearchProperty = DependencyProperty.Register(
            nameof(ShowSearch), typeof(bool), typeof(PropertyEditor),
            new PropertyMetadata(true));
        public bool ShowSearch
        {
            get => (bool)GetValue(ShowSearchProperty);
            set => SetValue(ShowSearchProperty, value);
        }

        public static readonly DependencyProperty ShowPinProperty = DependencyProperty.Register(
            nameof(ShowPin), typeof(bool), typeof(PropertyEditor),
            new PropertyMetadata(true));
        public bool ShowPin
        {
            get => (bool)GetValue(ShowPinProperty);
            set => SetValue(ShowPinProperty, value);
        }

        public static readonly DependencyProperty TabsProperty = DependencyProperty.Register(
            nameof(Tabs), typeof(object), typeof(PropertyEditor),
            new PropertyMetadata(null));
        public object Tabs
        {
            get => GetValue(TabsProperty);
            set => SetValue(TabsProperty, value);
        }

        public static readonly DependencyProperty SelectedTabIndexProperty = DependencyProperty.Register(
            nameof(SelectedTabIndex), typeof(int), typeof(PropertyEditor),
            new FrameworkPropertyMetadata(-1,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
        public int SelectedTabIndex
        {
            get => (int)GetValue(SelectedTabIndexProperty);
            set => SetValue(SelectedTabIndexProperty, value);
        }

        public static readonly DependencyProperty ShowIconBarProperty = DependencyProperty.Register(
            nameof(ShowIconBar), typeof(bool), typeof(PropertyEditor),
            new PropertyMetadata(true));
        public bool ShowIconBar
        {
            get => (bool)GetValue(ShowIconBarProperty);
            set => SetValue(ShowIconBarProperty, value);
        }
    }
}
