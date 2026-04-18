using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// Editor 区域顶部 24px mini header bar（区别于 <see cref="BlenderWindow"/> 的 30px 标题栏）。
    /// 三槽位：左 EditorTypeIcon / 中 LeftContent / 右 RightContent。
    /// 典型场景：每个 Editor 顶部的「类型选择 + 菜单 + 视图模式」。
    /// </summary>
    public class EditorHeader : ContentControl
    {
        static EditorHeader()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(EditorHeader),
                new FrameworkPropertyMetadata(typeof(EditorHeader)));
        }

        public static readonly DependencyProperty EditorTypeIconProperty = DependencyProperty.Register(
            nameof(EditorTypeIcon), typeof(object), typeof(EditorHeader),
            new PropertyMetadata(null));

        /// <summary>最左侧 Editor 类型图标（任意 UIElement，典型为 Path）。</summary>
        public object EditorTypeIcon
        {
            get => GetValue(EditorTypeIconProperty);
            set => SetValue(EditorTypeIconProperty, value);
        }

        public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register(
            nameof(LeftContent), typeof(object), typeof(EditorHeader),
            new PropertyMetadata(null));

        /// <summary>左侧（图标右侧）槽位：典型放 menu 链 / 操作按钮。</summary>
        public object LeftContent
        {
            get => GetValue(LeftContentProperty);
            set => SetValue(LeftContentProperty, value);
        }

        public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register(
            nameof(RightContent), typeof(object), typeof(EditorHeader),
            new PropertyMetadata(null));

        /// <summary>右侧槽位：典型放 view mode / shading / overlay 切换。</summary>
        public object RightContent
        {
            get => GetValue(RightContentProperty);
            set => SetValue(RightContentProperty, value);
        }
    }
}
