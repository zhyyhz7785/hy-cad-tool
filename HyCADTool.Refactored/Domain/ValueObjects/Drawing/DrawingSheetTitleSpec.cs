#nullable enable

using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Drawing
{
    /// <summary>
    /// 图题/图纸名称 的几何与图层规则（与 AutoCAD / WPF 无关的快照，由
    /// <c>DrawingSheetTitleStyleFactory</c> 从设置与 <see cref="HyRoadLayers"/> 合并生成）。
    /// </summary>
    public sealed class DrawingSheetTitleSpec
    {
        /// <summary>与 <c>HyRoadLayers.CrossSectionTitleLayer</c> 一致，Factory 会合并为同值。</summary>
        public const string DefaultTitleTextLayer = "05_hy_道路_横断面_图题";

        /// <summary>与 <c>HyRoadLayers.CrossSectionTitleDecorationLayer</c> 一致。</summary>
        public const string DefaultTitleDecorationLayer = "05_hy_道路_横断面_图题_装饰";

        public bool ShowCrosshair { get; }
        public bool ShowScale { get; }

        /// <summary>主图题、比例文字所用人图层。</summary>
        public string TitleTextLayerName { get; }

        /// <summary>双下划线、十字装饰线人图层（可与 <see cref="TitleTextLayerName"/> 相同）。</summary>
        public string TitleDecorationLayerName { get; }

        /// <summary>比例串格式，<c>{0}</c> 为分母，例如 "1:{0}"。</summary>
        public string ScaleFormat { get; }

        /// <summary>主图名文字样式名（如 0-hy-说明-T），在数据库中解析为 <c>TextStyleId</c>。</summary>
        public string MainTextStyleName { get; }

        /// <summary>右侧比例文字样式名（可与主图名相同）。</summary>
        public string ScaleTextStyleName { get; }

        /// <summary>
        /// 主图名<strong>纸面</strong>字高（mm），与设置「横断面图题」主字高及
        /// <c>TextSize</c>→<c>ActualTextHeight</c> 的纸面语义一致；AutoCAD 出图时按
        /// <c>paperMm × UnitFactor × MainScale</c> 换为模型单位。≤0 时由出图端按注记样式回退。
        /// </summary>
        public double MainTextHeightModel { get; }

        /// <summary>
        /// 比例文字<strong>纸面</strong>字高（mm），换算同 <see cref="MainTextHeightModel"/>；≤0 时 = <see cref="ScaleTextHeightFactor"/>×主字高（模型高）。
        /// </summary>
        public double ScaleTextHeightModel { get; }

        // —— 以下系数均相对主图题字高 H（0~1 为小数比） ——

        public double TextToUpperLineGapFactor { get; }
        public double UpperLineWidthFactor { get; }
        public double DoubleLineSpacingFactor { get; }
        public double LowerLineWidthFactor { get; }
        public double ScaleTextHeightFactor { get; }
        public double ScaleGapFromTextRightFactor { get; }
        public double CrosshairArmLengthFactor { get; }
        public double CrosshairCoreHalfFactor { get; }
        public double CrosshairOffsetFromTextLeftFactor { get; }
        public double CrosshairCenterLiftFactor { get; }

        /// <summary>无用户设置/无法访问 Settings 时的横断面图题缺省，与
        /// <c>DrawingSheetTitleStyleFactory.FromSettings(null)</c> 数值一致。
        /// </summary>
        public static DrawingSheetTitleSpec RoadCrossSectionDefault { get; } = new DrawingSheetTitleSpec(
            showCrosshair: false,
            showScale: true,
            titleTextLayerName: DefaultTitleTextLayer,
            titleDecorationLayerName: DefaultTitleDecorationLayer,
            scaleFormat: "1:{0}",
            mainTextStyleName: "0-hy-说明-T",
            scaleTextStyleName: "0-hy-说明-T",
            mainTextHeightModel: 5.0,
            scaleTextHeightModel: 3.0,
            textToUpperLineGapFactor: 0.12,
            upperLineWidthFactor: 0.07,
            doubleLineSpacingFactor: 0.05,
            lowerLineWidthFactor: 0.02,
            scaleTextHeightFactor: 0.55,
            scaleGapFromTextRightFactor: 0.12,
            crosshairArmLengthFactor: 0.32,
            crosshairCoreHalfFactor: 0.06,
            crosshairOffsetFromTextLeftFactor: 0.28,
            crosshairCenterLiftFactor: 0.06);

        public DrawingSheetTitleSpec(
            bool showCrosshair,
            bool showScale,
            string? titleTextLayerName,
            string? titleDecorationLayerName,
            string? scaleFormat,
            string? mainTextStyleName,
            string? scaleTextStyleName,
            double mainTextHeightModel,
            double scaleTextHeightModel,
            double textToUpperLineGapFactor,
            double upperLineWidthFactor,
            double doubleLineSpacingFactor,
            double lowerLineWidthFactor,
            double scaleTextHeightFactor,
            double scaleGapFromTextRightFactor,
            double crosshairArmLengthFactor,
            double crosshairCoreHalfFactor,
            double crosshairOffsetFromTextLeftFactor,
            double crosshairCenterLiftFactor)
        {
            ShowCrosshair = showCrosshair;
            ShowScale = showScale;
            TitleTextLayerName = string.IsNullOrWhiteSpace(titleTextLayerName)
                ? DefaultTitleTextLayer
                : titleTextLayerName!.Trim();
            TitleDecorationLayerName = string.IsNullOrWhiteSpace(titleDecorationLayerName)
                ? DefaultTitleDecorationLayer
                : titleDecorationLayerName!.Trim();
            ScaleFormat = string.IsNullOrWhiteSpace(scaleFormat) ? "1:{0}" : scaleFormat!.Trim();
            MainTextStyleName = string.IsNullOrWhiteSpace(mainTextStyleName) ? "0-hy-说明-T" : mainTextStyleName!.Trim();
            ScaleTextStyleName = string.IsNullOrWhiteSpace(scaleTextStyleName) ? MainTextStyleName : scaleTextStyleName!.Trim();
            MainTextHeightModel = mainTextHeightModel;
            ScaleTextHeightModel = scaleTextHeightModel;
            TextToUpperLineGapFactor = ClampNonNeg(textToUpperLineGapFactor, 0.12);
            UpperLineWidthFactor = ClampPos(upperLineWidthFactor, 0.07);
            DoubleLineSpacingFactor = ClampNonNeg(doubleLineSpacingFactor, 0.05);
            LowerLineWidthFactor = ClampPos(lowerLineWidthFactor, 0.02);
            ScaleTextHeightFactor = ClampPos(scaleTextHeightFactor, 0.55);
            ScaleGapFromTextRightFactor = ClampNonNeg(scaleGapFromTextRightFactor, 0.12);
            CrosshairArmLengthFactor = ClampPos(crosshairArmLengthFactor, 0.32);
            CrosshairCoreHalfFactor = ClampPos(crosshairCoreHalfFactor, 0.06);
            CrosshairOffsetFromTextLeftFactor = ClampPos(crosshairOffsetFromTextLeftFactor, 0.28);
            CrosshairCenterLiftFactor = ClampNonNeg(crosshairCenterLiftFactor, 0.06);
        }

        private static double ClampPos(double v, double d) => v > 1e-9 ? v : d;
        private static double ClampNonNeg(double v, double d) => v >= 0 ? v : d;
    }
}
