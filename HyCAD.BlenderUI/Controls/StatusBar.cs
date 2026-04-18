using System.Windows;
using System.Windows.Controls;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// Editor 内底部 22px 状态条（区别于 <see cref="BlenderWindow"/> 的 ~36px 命令栏 Footer）。
    /// 三槽位：左 LeftContent / 中 CenterContent / 右 RightContent。
    /// 典型场景：3D Viewport 底部「场景统计 + 当前操作 + 帧率」。
    /// </summary>
    public class StatusBar : ContentControl
    {
        static StatusBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(StatusBar),
                new FrameworkPropertyMetadata(typeof(StatusBar)));
        }

        public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register(
            nameof(LeftContent), typeof(object), typeof(StatusBar),
            new PropertyMetadata(null));

        public object LeftContent
        {
            get => GetValue(LeftContentProperty);
            set => SetValue(LeftContentProperty, value);
        }

        public static readonly DependencyProperty CenterContentProperty = DependencyProperty.Register(
            nameof(CenterContent), typeof(object), typeof(StatusBar),
            new PropertyMetadata(null));

        public object CenterContent
        {
            get => GetValue(CenterContentProperty);
            set => SetValue(CenterContentProperty, value);
        }

        public static readonly DependencyProperty RightContentProperty = DependencyProperty.Register(
            nameof(RightContent), typeof(object), typeof(StatusBar),
            new PropertyMetadata(null));

        public object RightContent
        {
            get => GetValue(RightContentProperty);
            set => SetValue(RightContentProperty, value);
        }
    }
}
