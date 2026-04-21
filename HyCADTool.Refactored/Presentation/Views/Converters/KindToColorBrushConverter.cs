using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using HyCADTool.Refactored.Domain.Models.Road;

namespace HyCADTool.Refactored.Presentation.Views.Converters
{
    /// <summary>
    /// 把 <see cref="TemplateComponentKind"/> 枚举映射到 Outliner 色条 <see cref="SolidColorBrush"/>。
    ///
    /// <para>
    /// 颜色表（rCs 面板 v2）：
    /// <list type="bullet">
    ///   <item>Pavement       → 深灰（#4A4A4A）</item>
    ///   <item>NonMotorized   → 褐色（#8B6A3E）</item>
    ///   <item>Sidewalk       → 米色（#C2B280）</item>
    ///   <item>GreenStrip     → 绿（#5A8F3A）</item>
    ///   <item>MedianStrip    → 黄（#C8A73A）</item>
    ///   <item>Kerb           → 粉（#C77E9E）</item>
    ///   <item>Shoulder/Slope → 浅蓝（#5C85B0）</item>
    ///   <item>Unknown        → 暗灰（#6D6D6D）</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 避坑提醒（参考 <c>wpf-paletteset-resource-pitfalls</c>）：返回的 SolidColorBrush 会在
    /// ResourceDictionary Seal 后被强冻；所以本类每次 Convert 都返回**已 Freeze** 的 Brush，
    /// 允许跨线程使用且避免"resource seal" 相关异常。
    /// </para>
    /// </summary>
    public sealed class KindToColorBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush PavementBrush = CreateFrozen(0x4A, 0x4A, 0x4A);
        private static readonly SolidColorBrush NonMotorBrush = CreateFrozen(0x8B, 0x6A, 0x3E);
        private static readonly SolidColorBrush SidewalkBrush = CreateFrozen(0xC2, 0xB2, 0x80);
        private static readonly SolidColorBrush GreenBrush = CreateFrozen(0x5A, 0x8F, 0x3A);
        private static readonly SolidColorBrush MedianBrush = CreateFrozen(0xC8, 0xA7, 0x3A);
        private static readonly SolidColorBrush KerbBrush = CreateFrozen(0xC7, 0x7E, 0x9E);
        private static readonly SolidColorBrush ShoulderBrush = CreateFrozen(0x5C, 0x85, 0xB0);
        private static readonly SolidColorBrush UnknownBrush = CreateFrozen(0x6D, 0x6D, 0x6D);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TemplateComponentKind k)
            {
                switch (k)
                {
                    case TemplateComponentKind.Pavement: return PavementBrush;
                    case TemplateComponentKind.NonMotorized: return NonMotorBrush;
                    case TemplateComponentKind.Sidewalk: return SidewalkBrush;
                    case TemplateComponentKind.GreenStrip: return GreenBrush;
                    case TemplateComponentKind.MedianStrip: return MedianBrush;
                    case TemplateComponentKind.Kerb: return KerbBrush;
                    case TemplateComponentKind.Shoulder:
                    case TemplateComponentKind.Slope: return ShoulderBrush;
                    default: return UnknownBrush;
                }
            }
            return UnknownBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;

        private static SolidColorBrush CreateFrozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
