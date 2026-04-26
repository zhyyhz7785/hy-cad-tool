using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_COLOR：色块 + 点击可选中（宿主可弹出拾色器）。</summary>
    [TemplatePart(Name = PartSwatch, Type = typeof(Border))]
    public class BlenderColorField : Control
    {
        public const string PartSwatch = "PART_Swatch";

        private Border _swatch;

        static BlenderColorField()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderColorField),
                new FrameworkPropertyMetadata(typeof(BlenderColorField)));
        }

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(
            nameof(SelectedColor), typeof(Color), typeof(BlenderColorField),
            new FrameworkPropertyMetadata(Colors.Gray, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));

        public Color SelectedColor
        {
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BlenderColorField cf) cf.ApplySwatch();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _swatch = GetTemplateChild(PartSwatch) as Border;
            ApplySwatch();
        }

        private void ApplySwatch()
        {
            if (_swatch == null) return;
            _swatch.Background = new SolidColorBrush(SelectedColor);
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            RaiseEvent(new RoutedEventArgs(ColorPickedEvent, this));
        }

        public static readonly RoutedEvent ColorPickedEvent = EventManager.RegisterRoutedEvent(
            nameof(ColorPicked), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(BlenderColorField));

        public event RoutedEventHandler ColorPicked
        {
            add => AddHandler(ColorPickedEvent, value);
            remove => RemoveHandler(ColorPickedEvent, value);
        }
    }
}
