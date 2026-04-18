using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// 左侧图标 Tab 项；通常以 Path Geometry 作为图标
    /// </summary>
    public class IconTabItem : ContentControl
    {
        static IconTabItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(IconTabItem),
                new FrameworkPropertyMetadata(typeof(IconTabItem)));
        }

        public static readonly DependencyProperty IsSelectedProperty = Selector.IsSelectedProperty
            .AddOwner(typeof(IconTabItem),
                new FrameworkPropertyMetadata(false,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal));

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(object), typeof(IconTabItem),
            new PropertyMetadata(null));

        public object Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty TooltipTextProperty = DependencyProperty.Register(
            nameof(TooltipText), typeof(string), typeof(IconTabItem),
            new PropertyMetadata(string.Empty));

        public string TooltipText
        {
            get => (string)GetValue(TooltipTextProperty);
            set => SetValue(TooltipTextProperty, value);
        }
    }
}
