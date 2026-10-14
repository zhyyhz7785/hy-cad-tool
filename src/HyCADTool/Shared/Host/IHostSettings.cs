using System.ComponentModel;
using HyCADTool.Shared.Drawing.Models;
using HyCADTool.Shared.Drawing.ValueObjects;

namespace HyCADTool.Shared.Host
{
    /// <summary>
    /// 稳定的宿主设置面。Features 只通过本接口读比例、精度、图层相关参数，
    /// 不再引用 <c>HyCADTool.Shell.ViewModels.SettingsPanelViewModel</c>。
    /// 工厂方法若返回业务类型，走 <see cref="CadHost"/> 上的委托，避免 Shared 依赖 Features。
    /// </summary>
    public interface IHostSettings : INotifyPropertyChanged
    {
        double ActualMLeaderArrowSize { get; }
        double ActualMLeaderLandingGap { get; }
        double ActualTextHeight { get; }
        double AlignmentDefaultRadius { get; set; }
        double AlignmentDefaultSpiralIn { get; set; }
        double AlignmentDefaultSpiralOut { get; set; }
        double AlignmentDefaultStartStation { get; set; }
        double AnchorageJoinLength { get; set; }
        double AnchorageLength { get; set; }
        string AppliedDimStyleName { get; }
        bool AutoSaveEnabled { get; set; }
        double BendingLineMinLength { get; set; }
        string BigFontFileName { get; set; }
        double CompBeamMaxHeight { get; set; }
        double CompBeamMaxWidth { get; set; }
        double CompBeamRebarDiameter { get; set; }
        double CompBeamRebarSpacing { get; set; }
        bool CompBeamSkipRein { get; set; }
        double CompBottomRebarDiameter { get; set; }
        double CompBottomRebarSpacing { get; set; }
        double CompBottomSlabMarchStep { get; set; }
        double CompBottomSlabMaxThickness { get; set; }
        double CompBumpMaxHeight { get; set; }
        double CompBumpRebarDiameter { get; set; }
        double CompBumpRebarSpacing { get; set; }
        double CompEmbedmentDepth { get; set; }
        double CompFoundationBottomElevation { get; set; }
        double CompGroupClusterDistance { get; set; }
        double CompLocalBumpMaxHeight { get; set; }
        double CompLocalConcreteMaxHeight { get; set; }
        double CompMassConstructDiameter { get; set; }
        double CompMassConstructMaxSpacing { get; set; }
        double CompMassMinSize { get; set; }
        double CompMassRebarDiameter { get; set; }
        double CompMassRebarSpacing { get; set; }
        bool CompMergeBumpsIntoBottomSlab { get; set; }
        double CompParallelAngleThreshold { get; set; }
        double CompParallelLineRatio { get; set; }
        double CompSlabMaxThickness { get; set; }
        double CompSlabRebarDiameter { get; set; }
        double CompSlabRebarSpacing { get; set; }
        double CompStripMinAspectRatio { get; set; }
        double CompWallMaxThickness { get; set; }
        double CompWallRebarDiameter { get; set; }
        double CompWallRebarSpacing { get; set; }
        string DimArrowName { get; set; }
        double DimDistanceTolerance { get; set; }
        string DimStyleName { get; }
        double Dimasz { get; set; }
        double Dimdle { get; set; }
        double DimensionDistanceInside { get; set; }
        double DimensionDistanceOutside { get; set; }
        double DimensionDistanceWithDim { get; set; }
        double Dimexe { get; set; }
        double Dimexo { get; set; }
        double Dimgap { get; set; }
        double Dimtxt { get; set; }
        double DotReinOffset { get; set; }
        double DotSeparation { get; set; }
        bool EnableHyEdDoubleClick { get; set; }
        string EquipmentDataFilePath { get; set; }
        string FontFileName { get; set; }
        double HookLength { get; set; }
        double HyEdAboveOffsetFactor { get; set; }
        string MLeaderArrowName { get; set; }
        double MLeaderArrowSize { get; set; }
        double MLeaderLandingGap { get; set; }
        string MLeaderStyleName { get; }
        int MLeaderTextColorIndex { get; set; }
        double MleaderDistance { get; set; }
        double PolylineWidth { get; set; }
        int Precision { get; }
        double ProtectionThickness { get; set; }
        double RebarDiameter { get; set; }
        double RebarSpacing { get; set; }
        double ReinforcementDiameter { get; set; }
        bool RoadCrossSectionOrientationUseWestEast { get; set; }
        double RoadCrossSectionPlanStripVerticalOffsetM { get; set; }
        double RoadCrosswalkWidth { get; set; }
        double RoadGapWidth { get; set; }
        double RoadStopLineDistance { get; set; }
        double RoadStripeSpacing { get; set; }
        double Scale { get; }
        double SheetTitleBottomLineWidthFactor { get; set; }
        double SheetTitleDoubleLineSpacingFactor { get; set; }
        double SheetTitleMainTextHeight { get; set; }
        string SheetTitleMainTextStyleName { get; set; }
        double SheetTitleScaleGapFromTextFactor { get; set; }
        double SheetTitleScaleTextHeight { get; set; }
        double SheetTitleScaleTextHeightRatio { get; set; }
        string SheetTitleScaleTextStyleName { get; set; }
        bool SheetTitleShowCrosshair { get; set; }
        bool SheetTitleShowScale { get; set; }
        double SheetTitleTextToLinesGapFactor { get; set; }
        double SheetTitleTopLineWidthFactor { get; set; }
        double StationMainInterval { get; set; }
        bool StationRotateTextAlongTangent { get; set; }
        double StationSubInterval { get; set; }
        double StationTextHeight { get; set; }
        double StationTextMargin { get; set; }
        string StationTextSide { get; set; }
        double StationTickLengthMain { get; set; }
        double StationTickLengthSub { get; set; }
        string StatusMessage { get; set; }
        string StyleSBigFont { get; }
        string StyleSFont { get; }
        string StyleSName { get; set; }
        double StyleSXScale { get; }
        string StyleTFont { get; }
        string StyleTName { get; set; }
        double StyleTXScale { get; set; }
        double SubScale { get; }
        string TableStyleName { get; }
        double TextSize { get; set; }
        string TextStyleName { get; }
        double TextXScale { get; set; }
        string Theme { get; }
        double UiDensityScale { get; }
        double UiFontScale { get; }
        double UiInputWidthScale { get; }
        DrawingUnit Unit { get; set; }
        DrawingUnit[] UnitOptions { get; }
        bool UseSubScale { get; }

        ScaleContext BuildScaleContext();
        DrawingSheetTitleSpec CreateDrawingSheetTitleSpec();
        void EnsureStylesApplied();
        string TryResolveLayerName(string semanticId, string defaultName);
        void CommitTextAndMLeaderStylesToActiveDocument();
    }
}
