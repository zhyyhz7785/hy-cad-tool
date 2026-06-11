using HyCADTool.Shell.Configuration.Global;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;

namespace HyCADTool.Shared.AutoCAD.Xdata
{
    /// <summary>
    /// 道路模块使用的 AutoCAD 标准图层名（经 <see cref="UserLayerNameResolver"/> 解析，可在「设置—图层」中改名）+ 颜色索引常量。
    ///
    /// 默认名与 <see cref="LayerCatalogFactory"/> 内置层表一致。
    /// <see cref="GetAll"/> 返回默认名与颜色、线型，供层表工厂与首次建层使用（不调用解析器，避免循环依赖）。
    /// </summary>
    public static class HyRoadLayers
    {
        public static string AlignmentLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadPlaneAlignment, LayerBuiltinDefaults.RoadPlaneAlignment);
        public const short AlignmentColor = 6;

        public static string ProfileLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadProfile, LayerBuiltinDefaults.RoadProfile);
        public const short ProfileColor = 2;

        public static string CorridorLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCorridor, LayerBuiltinDefaults.RoadCorridor);
        public const short CorridorColor = 8;

        public static string MarkingLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadMarking, LayerBuiltinDefaults.RoadMarking);
        public const short MarkingColor = 3;

        public static string StationLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadStation, LayerBuiltinDefaults.RoadStation);
        public const short StationColor = 7;

        public static string GeometryPointLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadGeometryPoint, LayerBuiltinDefaults.RoadGeometryPoint);
        public const short GeometryPointColor = 4;

        public static string OffsetLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadOffset, LayerBuiltinDefaults.RoadOffset);
        public const short OffsetColor = 30;

        public static string UserPickPreviewLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadUserPickPreview, LayerBuiltinDefaults.RoadUserPickPreview);
        public const short UserPickPreviewLayerColor = 7;

        public static string RawPolylineLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadRawPolyline, LayerBuiltinDefaults.RoadRawPolyline);
        public const short RawPolylineColor = 252;

        public static string LivePreviewLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadLivePreview, LayerBuiltinDefaults.RoadLivePreview);
        public const short LivePreviewColor = 2;

        public static string IntersectionLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadIntersection, LayerBuiltinDefaults.RoadIntersection);
        public const short IntersectionColor = 1;

        public static string CurbRampLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCurbRamp, LayerBuiltinDefaults.RoadCurbRamp);
        public const short CurbRampColor = 11;

        public static string TactilePavingLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadTactilePaving, LayerBuiltinDefaults.RoadTactilePaving);
        public const short TactilePavingColor = 42;

        public static string CrosswalkLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrosswalk, LayerBuiltinDefaults.RoadCrosswalk);
        public const short CrosswalkColor = 7;

        public static string StopLineLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadStopLine, LayerBuiltinDefaults.RoadStopLine);
        public const short StopLineColor = 7;

        public static string CrossSectionOutlineLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionOutline, LayerBuiltinDefaults.RoadCrossSectionOutline);
        public const short CrossSectionOutlineColor = 7;

        public static string CrossSectionCenterlineLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionCenterline, LayerBuiltinDefaults.RoadCrossSectionCenterline);
        public const short CrossSectionCenterlineColor = 1;

        public static string CrossSectionPavementLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionPavement, LayerBuiltinDefaults.RoadCrossSectionPavement);
        public const short CrossSectionPavementColor = 5;

        public static string CrossSectionSidewalkLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionSidewalk, LayerBuiltinDefaults.RoadCrossSectionSidewalk);
        public const short CrossSectionSidewalkColor = 52;

        public static string CrossSectionKerbLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionKerb, LayerBuiltinDefaults.RoadCrossSectionKerb);
        public const short CrossSectionKerbColor = 8;

        public static string CrossSectionGreenLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionGreen, LayerBuiltinDefaults.RoadCrossSectionGreen);
        public const short CrossSectionGreenColor = 92;

        public static string CrossSectionDimensionLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionDimension, LayerBuiltinDefaults.RoadCrossSectionDimension);
        public const short CrossSectionDimensionColor = 4;

        public static string CrossSectionAnnotationLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionAnnotation, LayerBuiltinDefaults.RoadCrossSectionAnnotation);
        public const short CrossSectionAnnotationColor = 7;

        public static string CrossSectionTitleLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionTitle, LayerBuiltinDefaults.RoadCrossSectionTitle);
        public const short CrossSectionTitleColor = 3;

        public static string PlanRedLineLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadPlanRedLine, LayerBuiltinDefaults.RoadPlanRedLine);
        public const short PlanRedLineColor = 1;

        public static string PlanBandDividerLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadPlanBandDivider, LayerBuiltinDefaults.RoadPlanBandDivider);
        public const short PlanBandDividerColor = 30;

        public static string PlanMarkingLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadPlanMarking, LayerBuiltinDefaults.RoadPlanMarking);
        public const short PlanMarkingColor = 7;

        /// <summary>经 <see cref="UserLayerNameResolver"/> 解析后的层名 + 色（供落图前兜底建层，与实体 Layer 赋值同源）。</summary>
        public static (string layerName, short colorIndex)[] GetAllResolved()
        {
            return new[]
            {
                (AlignmentLayer, AlignmentColor),
                (ProfileLayer, ProfileColor),
                (CorridorLayer, CorridorColor),
                (MarkingLayer, MarkingColor),
                (StationLayer, StationColor),
                (GeometryPointLayer, GeometryPointColor),
                (OffsetLayer, OffsetColor),
                (IntersectionLayer, IntersectionColor),
                (CurbRampLayer, CurbRampColor),
                (TactilePavingLayer, TactilePavingColor),
                (CrosswalkLayer, CrosswalkColor),
                (StopLineLayer, StopLineColor),
                (CrossSectionOutlineLayer, CrossSectionOutlineColor),
                (CrossSectionCenterlineLayer, CrossSectionCenterlineColor),
                (CrossSectionPavementLayer, CrossSectionPavementColor),
                (CrossSectionSidewalkLayer, CrossSectionSidewalkColor),
                (CrossSectionKerbLayer, CrossSectionKerbColor),
                (CrossSectionGreenLayer, CrossSectionGreenColor),
                (CrossSectionDimensionLayer, CrossSectionDimensionColor),
                (CrossSectionAnnotationLayer, CrossSectionAnnotationColor),
                (CrossSectionTitleLayer, CrossSectionTitleColor),
                (PlanRedLineLayer, PlanRedLineColor),
                (PlanBandDividerLayer, PlanBandDividerColor),
                (PlanMarkingLayer, PlanMarkingColor),
                (UserPickPreviewLayer, UserPickPreviewLayerColor),
                (RawPolylineLayer, RawPolylineColor),
                (LivePreviewLayer, LivePreviewColor),
            };
        }

        /// <summary>默认名 + 色 + 线型（供层表工厂；不调用 <see cref="UserLayerNameResolver"/>）。</summary>
        public static (string layerName, short colorIndex, string linetypeName)[] GetAll()
        {
            var c = LayerBuiltinDefaults.LinetypeContinuous;
            var hyCenter = HyLinetypeNames.Center;
            return new[]
            {
                (LayerBuiltinDefaults.RoadPlaneAlignment, AlignmentColor, c),
                (LayerBuiltinDefaults.RoadProfile, ProfileColor, c),
                (LayerBuiltinDefaults.RoadCorridor, CorridorColor, c),
                (LayerBuiltinDefaults.RoadMarking, MarkingColor, c),
                (LayerBuiltinDefaults.RoadStation, StationColor, c),
                (LayerBuiltinDefaults.RoadGeometryPoint, GeometryPointColor, c),
                (LayerBuiltinDefaults.RoadOffset, OffsetColor, c),
                (LayerBuiltinDefaults.RoadIntersection, IntersectionColor, c),
                (LayerBuiltinDefaults.RoadCurbRamp, CurbRampColor, c),
                (LayerBuiltinDefaults.RoadTactilePaving, TactilePavingColor, c),
                (LayerBuiltinDefaults.RoadCrosswalk, CrosswalkColor, c),
                (LayerBuiltinDefaults.RoadStopLine, StopLineColor, c),
                (LayerBuiltinDefaults.RoadCrossSectionOutline, CrossSectionOutlineColor, c),
                (LayerBuiltinDefaults.RoadCrossSectionCenterline, CrossSectionCenterlineColor, hyCenter),
                (LayerBuiltinDefaults.RoadCrossSectionPavement, CrossSectionPavementColor, c),
                (LayerBuiltinDefaults.RoadCrossSectionSidewalk, CrossSectionSidewalkColor, c),
                (LayerBuiltinDefaults.RoadCrossSectionKerb, CrossSectionKerbColor, c),
                (LayerBuiltinDefaults.RoadCrossSectionGreen, CrossSectionGreenColor, c),
                (LayerBuiltinDefaults.RoadCrossSectionDimension, CrossSectionDimensionColor, c),
                (LayerBuiltinDefaults.RoadCrossSectionAnnotation, CrossSectionAnnotationColor, c),
                (LayerBuiltinDefaults.RoadCrossSectionTitle, CrossSectionTitleColor, c),
                (LayerBuiltinDefaults.RoadPlanRedLine, PlanRedLineColor, c),
                (LayerBuiltinDefaults.RoadPlanBandDivider, PlanBandDividerColor, c),
                (LayerBuiltinDefaults.RoadPlanMarking, PlanMarkingColor, c),
                (LayerBuiltinDefaults.RoadUserPickPreview, UserPickPreviewLayerColor, c),
                (LayerBuiltinDefaults.RoadRawPolyline, RawPolylineColor, c),
                (LayerBuiltinDefaults.RoadLivePreview, LivePreviewColor, c),
            };
        }
    }
}
