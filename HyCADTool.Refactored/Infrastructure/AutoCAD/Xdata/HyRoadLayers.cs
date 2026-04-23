using HyCADTool.Refactored.Domain.ValueObjects.Configuration.User;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata
{
    /// <summary>
    /// 道路模块使用的 AutoCAD 标准图层名（经 <see cref="UserLayerNameResolver"/> 解析，可在「设置—图层」中改名）+ 颜色索引常量。
    ///
    /// 默认名与 LayerCatalogFactory 内置层表一致。
    /// <see cref="GetAll"/> 返回默认名与颜色，供层表工厂与首次建层使用（不调用解析器，避免循环依赖）。
    /// </summary>
    public static class HyRoadLayers
    {
        private const string _defAlignment = "05_hy_道路_平面线位";
        private const string _defProfile = "05_hy_道路_纵断面";
        private const string _defCorridor = "05_hy_道路_走廊";
        private const string _defMarking = "05_hy_道路_标线";
        private const string _defStation = "05_hy_道路_桩号";
        private const string _defGeometryPoint = "05_hy_道路_几何点";
        private const string _defOffset = "05_hy_道路_偏移线";
        private const string _defUserPick = "用户拾取";
        private const string _defRaw = "05_hy_道路_原线";
        private const string _defLivePreview = "05_hy_道路_预览";
        private const string _defIntersection = "05_hy_道路_交叉口";
        private const string _defCurbRamp = "05_hy_道路_缘石坡道";
        private const string _defTactile = "05_hy_道路_盲道";
        private const string _defCrosswalk = "05_hy_道路_人行横道";
        private const string _defStopLine = "05_hy_道路_停止线";
        private const string _defCsOutline = "05_hy_道路_横断面_轮廓";
        private const string _defCsCenter = "05_hy_道路_横断面_中心线";
        private const string _defCsPavement = "05_hy_道路_横断面_车行道";
        private const string _defCsSidewalk = "05_hy_道路_横断面_人行道";
        private const string _defCsKerb = "05_hy_道路_横断面_路牙";
        private const string _defCsGreen = "05_hy_道路_横断面_绿化带";
        private const string _defCsDim = "05_hy_道路_横断面_尺寸链";
        private const string _defCsAnno = "05_hy_道路_横断面_文字";
        private const string _defCsTitle = "05_hy_道路_横断面_图题";
        private const string _defCsTitleDeco = "05_hy_道路_横断面_图题_装饰";
        private const string _defCsOrientation = "05_hy_道路_横断面_方位";
        private const string _defPlanRed = "05_hy_道路_平面_红线";
        private const string _defPlanBand = "05_hy_道路_平面_板块分界";
        private const string _defPlanMarking = "05_hy_道路_平面_标线";

        public static string AlignmentLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadPlaneAlignment, _defAlignment);
        public const short AlignmentColor = 6;

        public static string ProfileLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadProfile, _defProfile);
        public const short ProfileColor = 2;

        public static string CorridorLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCorridor, _defCorridor);
        public const short CorridorColor = 8;

        public static string MarkingLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadMarking, _defMarking);
        public const short MarkingColor = 3;

        public static string StationLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadStation, _defStation);
        public const short StationColor = 7;

        public static string GeometryPointLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadGeometryPoint, _defGeometryPoint);
        public const short GeometryPointColor = 4;

        public static string OffsetLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadOffset, _defOffset);
        public const short OffsetColor = 30;

        public static string UserPickPreviewLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadUserPickPreview, _defUserPick);
        public const short UserPickPreviewLayerColor = 7;

        public static string RawPolylineLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadRawPolyline, _defRaw);
        public const short RawPolylineColor = 252;

        public static string LivePreviewLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadLivePreview, _defLivePreview);
        public const short LivePreviewColor = 2;

        public static string IntersectionLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadIntersection, _defIntersection);
        public const short IntersectionColor = 1;

        public static string CurbRampLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCurbRamp, _defCurbRamp);
        public const short CurbRampColor = 11;

        public static string TactilePavingLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadTactilePaving, _defTactile);
        public const short TactilePavingColor = 42;

        public static string CrosswalkLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrosswalk, _defCrosswalk);
        public const short CrosswalkColor = 7;

        public static string StopLineLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadStopLine, _defStopLine);
        public const short StopLineColor = 7;

        public static string CrossSectionOutlineLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionOutline, _defCsOutline);
        public const short CrossSectionOutlineColor = 7;

        public static string CrossSectionCenterlineLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionCenterline, _defCsCenter);
        public const short CrossSectionCenterlineColor = 1;

        public static string CrossSectionPavementLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionPavement, _defCsPavement);
        public const short CrossSectionPavementColor = 5;

        public static string CrossSectionSidewalkLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionSidewalk, _defCsSidewalk);
        public const short CrossSectionSidewalkColor = 52;

        public static string CrossSectionKerbLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionKerb, _defCsKerb);
        public const short CrossSectionKerbColor = 8;

        public static string CrossSectionGreenLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionGreen, _defCsGreen);
        public const short CrossSectionGreenColor = 92;

        public static string CrossSectionDimensionLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionDimension, _defCsDim);
        public const short CrossSectionDimensionColor = 4;

        public static string CrossSectionAnnotationLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionAnnotation, _defCsAnno);
        public const short CrossSectionAnnotationColor = 7;

        public static string CrossSectionTitleLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionTitle, _defCsTitle);
        public const short CrossSectionTitleColor = 3;

        public static string CrossSectionTitleDecorationLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionTitleDecoration, _defCsTitleDeco);
        public const short CrossSectionTitleDecorationColor = 3;

        public static string CrossSectionOrientationLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadCrossSectionOrientation, _defCsOrientation);
        public const short CrossSectionOrientationColor = 6;

        public static string PlanRedLineLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadPlanRedLine, _defPlanRed);
        public const short PlanRedLineColor = 1;

        public static string PlanBandDividerLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadPlanBandDivider, _defPlanBand);
        public const short PlanBandDividerColor = 30;

        public static string PlanMarkingLayer => UserLayerNameResolver.Get(LayerSemanticIds.RoadPlanMarking, _defPlanMarking);
        public const short PlanMarkingColor = 7;

        /// <summary>默认名 + 色表（供层表工厂；不调用 <see cref="UserLayerNameResolver"/>）。</summary>
        public static (string layerName, short colorIndex)[] GetAll()
        {
            return new[]
            {
                (_defAlignment, AlignmentColor),
                (_defProfile, ProfileColor),
                (_defCorridor, CorridorColor),
                (_defMarking, MarkingColor),
                (_defStation, StationColor),
                (_defGeometryPoint, GeometryPointColor),
                (_defOffset, OffsetColor),
                (_defIntersection, IntersectionColor),
                (_defCurbRamp, CurbRampColor),
                (_defTactile, TactilePavingColor),
                (_defCrosswalk, CrosswalkColor),
                (_defStopLine, StopLineColor),
                (_defCsOutline, CrossSectionOutlineColor),
                (_defCsCenter, CrossSectionCenterlineColor),
                (_defCsPavement, CrossSectionPavementColor),
                (_defCsSidewalk, CrossSectionSidewalkColor),
                (_defCsKerb, CrossSectionKerbColor),
                (_defCsGreen, CrossSectionGreenColor),
                (_defCsDim, CrossSectionDimensionColor),
                (_defCsAnno, CrossSectionAnnotationColor),
                (_defCsTitle, CrossSectionTitleColor),
                (_defCsTitleDeco, CrossSectionTitleDecorationColor),
                (_defCsOrientation, CrossSectionOrientationColor),
                (_defPlanRed, PlanRedLineColor),
                (_defPlanBand, PlanBandDividerColor),
                (_defPlanMarking, PlanMarkingColor),
                (_defUserPick, UserPickPreviewLayerColor),
                (_defRaw, RawPolylineColor),
                (_defLivePreview, LivePreviewColor),
            };
        }
    }
}
