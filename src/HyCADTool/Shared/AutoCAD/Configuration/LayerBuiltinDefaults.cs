using HyCADTool.Shell.Configuration.Global;

namespace HyCADTool.Shared.AutoCAD.Configuration
{
    /// <summary>
    /// v3 图层默认名（与 <see cref="LayerCatalogFactory"/> / hy-settings 层表一致），供
    /// <see cref="Services.UserLayerNameResolver"/> 第二参数及少数无法注入解析器的回退值使用。
    /// </summary>
    public static class LayerBuiltinDefaults
    {
        public const string ReinLine = "01-hy-1配筋-钢筋线";
        public const string ReinPoint = "01-hy-1配筋-钢筋点";
        public const string ReinLineExternal = "01-hy-1配筋-钢筋外";
        public const string CommonDimOuter = "00-hy-3标注-外";
        public const string CommonDimInsideHorizontal = "00-hy-3标注-内横";
        public const string CommonDimInsideVertical = "00-hy-3标注-内纵";
        public const string CommonMLeader = "00-hy-3标注-引线";
        public const string PublicAxisMain = "00-hy-6轴线-主";
        public const string PublicAxisText = "00-hy-6轴线-主-文";
        public const string PublicTableMain = "00-hy-5表格-主";
        public const string NoteGeneral = "00-hy-4说明-一般-文";
        public const string ElevationSymbol = "00-hy-7标高-符号";
        public const string ElevationNoText = "00-hy-7标高-无文";
        public const string ElevationWarning = "00-hy-7标高-警告";
        /// <summary>分组层名前缀 + 分组字母，如 <c>00-hy-7标高-A</c>。</summary>
        public const string ElevationGroupLayerPrefix = "00-hy-7标高-";
        public const string RaftOutline = "01-hy-4基础-筏板-轮廓";
        public const string RaftOutlineAdjust = "01-hy-4基础-筏板-辅";
        public const string RaftSlabXTop = "00_hy_筏板附加配筋x_上";
        public const string RaftSlabXBottom = "00_hy_筏板附加配筋x_下";
        public const string RaftSlabYTop = "00_hy_筏板附加配筋y_上";
        public const string RaftSlabYBottom = "00_hy_筏板附加配筋y_下";
        public const string RaftTextX = "00_hy_筏板附加配筋文字_x";
        public const string RaftTextY = "00_hy_筏板附加配筋文字_y";
        public const string RaftDimX = "00_hy_筏板附加配筋x_标注";
        public const string RaftDimY = "00_hy_筏板附加配筋Y_标注";
        public const string PublicViewport = "00-hy-2视口-主";
        public const string PileMain = "01-hy-2桩-主";
        public const string PileContour = "01-hy-2桩-地基轮廓";
        public const string Cushion = "01-hy-5垫层-主";
        public const string TitleBlock = "00-hy-1图框-主";
        public const string RaftSolid3D = "01-hy-4基础-筏板-体";
        public const string EquipSideSolid = "01-hy-4基础-设备-侧-体";
        public const string EquipTopSolid = "01-hy-4基础-设备-顶-体";
        public const string EquipBottomSolid = "01-hy-4基础-设备-底-体";
        public const string WallMainSolid = "01-hy-6墙-主-体";
        public const string WallRetainSolid = "01-hy-6墙-挡土-体";
        public const string WallConnectSolid = "01-hy-6墙-连接-体";
        public const string HyRebarTextH = "HY_H向钢筋";
        public const string HyRebarTextV = "HY_V向钢筋";
        public const string HyRebarManual = "HY_手动配筋";
        public const string ClusterMain = "03-hy-1主";
        public const string ClusterAxis = "03-hy-2轴";
        public const string ClusterRegion = "03-hy-3区域";
        public const string ClusterDim = "03-hy-4标";
        public const string ClusterAux = "03-hy-5辅";

        public const string RoadPlaneAlignment = "02-hy-1平面-线位";
        public const string RoadProfile = "02-hy-2纵断-主";
        public const string RoadCorridor = "02-hy-1平面-走廊";
        public const string RoadMarking = "02-hy-5标线-主";
        public const string RoadStation = "02-hy-6桩号-主";
        public const string RoadGeometryPoint = "02-hy-7几何-点";
        public const string RoadOffset = "02-hy-7几何-偏移";
        public const string RoadUserPickPreview = "02-hy-9预览-拾取-预";
        public const string RoadRawPolyline = "02-hy-9预览-原线-预";
        public const string RoadLivePreview = "02-hy-9预览-实时-预";
        public const string RoadIntersection = "02-hy-8交叉口-主";
        public const string RoadCurbRamp = "02-hy-7几何-缘石";
        public const string RoadTactilePaving = "02-hy-7几何-盲道";
        public const string RoadCrosswalk = "02-hy-5标线-人道";
        public const string RoadStopLine = "02-hy-5标线-停止";
        public const string RoadCrossSectionOutline = "02-hy-3横断-轮廓";
        public const string RoadCrossSectionCenterline = "02-hy-3横断-中心";
        public const string RoadCrossSectionPavement = "02-hy-3横断-车道";
        public const string RoadCrossSectionSidewalk = "02-hy-3横断-人道";
        public const string RoadCrossSectionKerb = "02-hy-3横断-路牙";
        public const string RoadCrossSectionGreen = "02-hy-3横断-绿化";
        public const string RoadCrossSectionDimension = "02-hy-3横断-尺寸-标";
        public const string RoadCrossSectionAnnotation = "02-hy-3横断-注释-文";
        public const string RoadCrossSectionTitle = "02-hy-3横断-图题";
        public const string RoadPlanRedLine = "02-hy-1平面-红线";
        public const string RoadPlanBandDivider = "02-hy-1平面-板块";
        public const string RoadPlanMarking = "02-hy-1平面-标线";

        public const string HyfeaResult = "01-hy-8FEA-结果";

        public const string LinetypeContinuous = "Continuous";
        public static string LinetypeHyCenter => HyLinetypeNames.Center;
    }
}
