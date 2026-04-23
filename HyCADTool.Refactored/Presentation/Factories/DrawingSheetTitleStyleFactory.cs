#nullable enable

using HyCADTool.Refactored.Domain.ValueObjects.Drawing;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Presentation.Factories
{
    /// <summary>
    /// 从设置面板 + 默认图层名生成 <see cref="DrawingSheetTitleSpec"/>；出图与 WPF 共用。
    /// </summary>
    public static class DrawingSheetTitleStyleFactory
    {
        private const string ScaleFormat = "1:{0}";

        public static DrawingSheetTitleSpec FromSettings(SettingsPanelViewModel? settings)
        {
            if (settings == null)
                return WithDefaults(
                    showCrosshair: false,
                    showScale: true,
                    scaleH: 0.55,
                    topW: 0.07,
                    botW: 0.02,
                    textToLine: 0.12,
                    dblGap: 0.05,
                    scaleGap: 0.12);

            return new DrawingSheetTitleSpec(
                showCrosshair: settings.SheetTitleShowCrosshair,
                showScale: settings.SheetTitleShowScale,
                titleTextLayerName: HyRoadLayers.CrossSectionTitleLayer,
                titleDecorationLayerName: HyRoadLayers.CrossSectionTitleDecorationLayer,
                scaleFormat: ScaleFormat,
                mainTextStyleName: settings.SheetTitleMainTextStyleName,
                scaleTextStyleName: settings.SheetTitleScaleTextStyleName,
                mainTextHeightModel: settings.SheetTitleMainTextHeight,
                scaleTextHeightModel: settings.SheetTitleScaleTextHeight,
                textToUpperLineGapFactor: settings.SheetTitleTextToLinesGapFactor,
                upperLineWidthFactor: settings.SheetTitleTopLineWidthFactor,
                doubleLineSpacingFactor: settings.SheetTitleDoubleLineSpacingFactor,
                lowerLineWidthFactor: settings.SheetTitleBottomLineWidthFactor,
                scaleTextHeightFactor: settings.SheetTitleScaleTextHeightRatio,
                scaleGapFromTextRightFactor: settings.SheetTitleScaleGapFromTextFactor,
                crosshairArmLengthFactor: 0.32,
                crosshairCoreHalfFactor: 0.06,
                crosshairOffsetFromTextLeftFactor: 0.28,
                crosshairCenterLiftFactor: 0.06);
        }

        private static DrawingSheetTitleSpec WithDefaults(
            bool showCrosshair,
            bool showScale,
            double scaleH,
            double topW,
            double botW,
            double textToLine,
            double dblGap,
            double scaleGap)
        {
            return new DrawingSheetTitleSpec(
                showCrosshair: showCrosshair,
                showScale: showScale,
                titleTextLayerName: HyRoadLayers.CrossSectionTitleLayer,
                titleDecorationLayerName: HyRoadLayers.CrossSectionTitleDecorationLayer,
                scaleFormat: ScaleFormat,
                mainTextStyleName: "0-hy-说明-T",
                scaleTextStyleName: "0-hy-说明-T",
                mainTextHeightModel: 5.0,
                scaleTextHeightModel: 3.0,
                textToUpperLineGapFactor: textToLine,
                upperLineWidthFactor: topW,
                doubleLineSpacingFactor: dblGap,
                lowerLineWidthFactor: botW,
                scaleTextHeightFactor: scaleH,
                scaleGapFromTextRightFactor: scaleGap,
                crosshairArmLengthFactor: 0.32,
                crosshairCoreHalfFactor: 0.06,
                crosshairOffsetFromTextLeftFactor: 0.28,
                crosshairCenterLiftFactor: 0.06);
        }
    }
}
