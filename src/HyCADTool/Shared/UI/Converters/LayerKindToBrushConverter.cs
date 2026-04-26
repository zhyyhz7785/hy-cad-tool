using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using HyCADTool.Domain.Models.Road;

namespace HyCADTool.Shared.UI.Converters
{
    /// <summary>
    /// 把 <see cref="StructureLayerKind"/> 映射到「路面结构层」ListBox 的色条 <see cref="SolidColorBrush"/>。
    ///
    /// <list type="bullet">
    ///   <item>Surface → 近黑（#1F1F1F）—— 面层（沥青）</item>
    ///   <item>Base    → 灰（#6D6D6D）—— 基层（水稳碎石）</item>
    ///   <item>Subbase → 浅灰（#A0A0A0）—— 垫层（砂砾）</item>
    ///   <item>Custom  → 蓝（#4070A5）—— 自定义</item>
    /// </list>
    ///
    /// Brush 预先 Freeze，可安全跨 ResourceDictionary seal 使用。
    /// </summary>
    public sealed class LayerKindToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush SurfaceBrush = CreateFrozen(0x1F, 0x1F, 0x1F);
        private static readonly SolidColorBrush BaseBrush = CreateFrozen(0x6D, 0x6D, 0x6D);
        private static readonly SolidColorBrush SubbaseBrush = CreateFrozen(0xA0, 0xA0, 0xA0);
        private static readonly SolidColorBrush CustomBrush = CreateFrozen(0x40, 0x70, 0xA5);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is StructureLayerKind k)
            {
                switch (k)
                {
                    case StructureLayerKind.Surface: return SurfaceBrush;
                    case StructureLayerKind.Base: return BaseBrush;
                    case StructureLayerKind.Subbase: return SubbaseBrush;
                    default: return CustomBrush;
                }
            }
            return CustomBrush;
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
