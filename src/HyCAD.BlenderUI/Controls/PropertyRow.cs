using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// Blender 风格属性行：左侧 LabelWidth 固定宽度、右对齐 Label；右侧为 Content（通常是 TextBox / ComboBox / NumericSlider）
    /// </summary>
    public class PropertyRow : ContentControl
    {
        static PropertyRow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(PropertyRow),
                new FrameworkPropertyMetadata(typeof(PropertyRow)));
        }

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(PropertyRow),
            new PropertyMetadata(string.Empty));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty LabelWidthProperty = DependencyProperty.Register(
            nameof(LabelWidth), typeof(GridLength), typeof(PropertyRow),
            new PropertyMetadata(new GridLength(100)));

        public GridLength LabelWidth
        {
            get => (GridLength)GetValue(LabelWidthProperty);
            set => SetValue(LabelWidthProperty, value);
        }

        public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
            nameof(RowSpacing), typeof(Thickness), typeof(PropertyRow),
            new PropertyMetadata(new Thickness(0, 1, 0, 1)));

        public Thickness RowSpacing
        {
            get => (Thickness)GetValue(RowSpacingProperty);
            set => SetValue(RowSpacingProperty, value);
        }
    }
}
