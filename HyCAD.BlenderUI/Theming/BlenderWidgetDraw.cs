using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace HyCAD.BlenderUI.Theming
{
    /// <summary>
    /// WPF 等价于 Blender <c>widget_draw_backdrop</c> / <c>ui_widgetbase_draw</c> 的渐变与容器工厂。
    /// 使用主题中的 <c>Brush_Shadetop_*</c> / <c>Brush_Shadedown_*</c> 作为竖向渐变端点，
    /// <c>Brush_WidgetOutlineLight</c> / <c>Brush_WidgetOutlineDark</c> 作为 1px 浮雕描边。
    /// PaletteSet 约束：不使用 <see cref="AllowsTransparency"/>；阴影可选（默认关闭以避免大面积 Effect 开销）。
    /// </summary>
    public static class BlenderWidgetDraw
    {
        /// <summary>默认圆角（与 <see cref="Themes.Metrics"/> 中 Metric_CornerRadius 一致）。</summary>
        public const double DefaultCornerRadius = 3.0;

        /// <summary>
        /// 由顶/底内色构造竖向线性渐变画刷（不冻结，便于主题切换时仍通过 Resource 引用同一实例时由 Facade 更新）。
        /// </summary>
        public static LinearGradientBrush CreateVerticalGradientBrush(Color top, Color bottom)
        {
            var g = new LinearGradientBrush
            {
                StartPoint = new Point(0.5, 0),
                EndPoint = new Point(0.5, 1),
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
            };
            g.GradientStops.Add(new GradientStop(top, 0));
            g.GradientStops.Add(new GradientStop(bottom, 1));
            return g;
        }

        /// <summary>
        /// 创建带圆角、渐变背景、可选 1px 内嵌浮雕边的控件底板。
        /// </summary>
        /// <param name="innerTop">渐变顶色（通常 shadetop）。</param>
        /// <param name="innerBottom">渐变底色（通常 shadedown）。</param>
        /// <param name="outline">外轮廓色（1px）。</param>
        /// <param name="cornerRadius">圆角半径。</param>
        /// <param name="dropShadow">是否叠加 <see cref="DropShadowEffect"/>（默认 false）。</param>
        public static Border CreateWidgetBackdrop(
            Color innerTop,
            Color innerBottom,
            Color outline,
            double cornerRadius = DefaultCornerRadius,
            bool dropShadow = false)
        {
            var border = new Border
            {
                CornerRadius = new CornerRadius(cornerRadius),
                BorderBrush = new SolidColorBrush(outline),
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                Background = CreateVerticalGradientBrush(innerTop, innerBottom),
            };

            if (dropShadow)
            {
                border.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 1,
                    Opacity = 0.35,
                    BlurRadius = 3,
                };
            }

            return border;
        }

        /// <summary>
        /// 在已有 <see cref="Border"/> 上设置渐变背景（用于 Template 内代码后置或单元测试）。
        /// </summary>
        public static void ApplyGradientBackground(Border border, Color innerTop, Color innerBottom)
        {
            if (border == null) throw new ArgumentNullException(nameof(border));
            border.Background = CreateVerticalGradientBrush(innerTop, innerBottom);
        }

        /// <summary>
        /// 将 <paramref name="baseRgb"/> 向白/黑各偏移一定比例，得到 Blender 式 shadetop / shadedown（与 theme.c 中偏移思想一致）。
        /// </summary>
        public static void ComputeShadePair(Color baseRgb, out Color shadetop, out Color shadedown, double topLift = 0.12, double downDarken = 0.12)
        {
            shadetop = Color.FromRgb(
                ClampByte(baseRgb.R + baseRgb.R * topLift),
                ClampByte(baseRgb.G + baseRgb.G * topLift),
                ClampByte(baseRgb.B + baseRgb.B * topLift));

            shadedown = Color.FromRgb(
                ClampByte(baseRgb.R * (1 - downDarken)),
                ClampByte(baseRgb.G * (1 - downDarken)),
                ClampByte(baseRgb.B * (1 - downDarken)));
        }

        private static byte ClampByte(double v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)Math.Round(v);
        }
    }
}
