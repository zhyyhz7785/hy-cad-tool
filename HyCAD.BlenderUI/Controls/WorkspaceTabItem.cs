using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// <see cref="WorkspaceTabBar"/> 的子项：水平显示「Icon + Title」，被选中时高亮底部细线。
    /// </summary>
    public class WorkspaceTabItem : ContentControl
    {
        static WorkspaceTabItem()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(WorkspaceTabItem),
                new FrameworkPropertyMetadata(typeof(WorkspaceTabItem)));
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
            nameof(Title), typeof(string), typeof(WorkspaceTabItem),
            new PropertyMetadata(string.Empty));

        /// <summary>显示文字（如 Layout / Modeling / Sculpting）。</summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            nameof(Icon), typeof(object), typeof(WorkspaceTabItem),
            new PropertyMetadata(null));

        /// <summary>左侧小图标（任意 UIElement，典型为 Path）。</summary>
        public object Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
            nameof(IsSelected), typeof(bool), typeof(WorkspaceTabItem),
            new PropertyMetadata(false));

        /// <summary>由 <see cref="WorkspaceTabBar"/> 同步设置。</summary>
        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        public static readonly DependencyProperty TooltipTextProperty = DependencyProperty.Register(
            nameof(TooltipText), typeof(string), typeof(WorkspaceTabItem),
            new PropertyMetadata(string.Empty));

        public string TooltipText
        {
            get => (string)GetValue(TooltipTextProperty);
            set => SetValue(TooltipTextProperty, value);
        }
    }
}
