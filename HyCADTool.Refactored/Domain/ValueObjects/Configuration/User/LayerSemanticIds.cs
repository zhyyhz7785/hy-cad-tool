namespace HyCADTool.Refactored.Domain.ValueObjects.Configuration.User
{
    /// <summary>
    /// 图层语义 ID（稳定、不随用户改名而变）。命令与解析器用此键从 <see cref="UserLayerSettings"/> 取实际图层名。
    /// </summary>
    public static class LayerSemanticIds
    {
        // 钢筋 / 公共 / 筏板 / 视口 / 桩 / 垫层 / 图框 / HY 文字 / 聚类
        public const string ReinLine = "Builtin.Rein.Line";
        public const string ReinPoint = "Builtin.Rein.Point";
        public const string ReinLineExternal = "Builtin.Rein.LineExternal";
        public const string CommonDimOuter = "Builtin.Common.DimOuter";
        public const string CommonDimInsideHorizontal = "Builtin.Common.DimInsideHorizontal";
        public const string CommonDimInsideVertical = "Builtin.Common.DimInsideVertical";
        public const string CommonMLeader = "Builtin.Common.MLeader";
        public const string PublicAxisMain = "Builtin.Public.AxisMain";
        public const string PublicAxisText = "Builtin.Public.AxisText";
        public const string PublicTableMain = "Builtin.Public.TableMain";
        public const string PublicNoteGeneral = "Builtin.Public.NoteGeneral";
        public const string ElevationSymbol = "Builtin.Elevation.Symbol";
        public const string ElevationNoText = "Builtin.Elevation.NoText";
        public const string ElevationWarning = "Builtin.Elevation.Warning";
        public const string RaftOutline = "Builtin.Raft.Outline";
        public const string RaftOutlineAdjust = "Builtin.Raft.OutlineAdjust";
        public const string RaftSlabXTop = "Builtin.Raft.SlabXTop";
        public const string RaftSlabXBottom = "Builtin.Raft.SlabXBottom";
        public const string RaftSlabYTop = "Builtin.Raft.SlabYTop";
        public const string RaftSlabYBottom = "Builtin.Raft.SlabYBottom";
        public const string RaftTextX = "Builtin.Raft.TextX";
        public const string RaftTextY = "Builtin.Raft.TextY";
        public const string RaftDimX = "Builtin.Raft.DimX";
        public const string RaftDimY = "Builtin.Raft.DimY";
        public const string PublicViewport = "Builtin.Public.Viewport";
        public const string PileMain = "Builtin.Pile.Main";
        public const string PileContour = "Builtin.Pile.Contour";
        public const string Cushion = "Builtin.Cushion";
        public const string TitleBlock = "Builtin.TitleBlock";
        public const string HyRebarTextH = "Builtin.Hy.RebarTextH";
        public const string HyRebarTextV = "Builtin.Hy.RebarTextV";
        public const string HyRebarManual = "Builtin.Hy.RebarManual";
        public const string ClusterBP = "Builtin.Cluster.BP";
        public const string ClusterAAP = "Builtin.Cluster.AAP";
        public const string ClusterBAP = "Builtin.Cluster.BAP";
        public const string ClusterABolt = "Builtin.Cluster.ABolt";
        public const string ClusterSteelPlate = "Builtin.Cluster.SteelPlate";
        public const string ClusterAxisCircle = "Builtin.Cluster.AxisCircle";
        public const string ClusterAxisText = "Builtin.Cluster.AxisText";
        public const string ClusterRegion = "Builtin.Cluster.Region";
        public const string ClusterRegionText = "Builtin.Cluster.RegionText";
        public const string ClusterDimX = "Builtin.Cluster.DimX";
        public const string ClusterDimY = "Builtin.Cluster.DimY";
        public const string ClusterEP = "Builtin.Cluster.EP";
        public const string ClusterEEP = "Builtin.Cluster.EEP";
        public const string ClusterHull = "Builtin.Cluster.Hull";
        public const string ClusterPts = "Builtin.Cluster.Pts";

        // 道路（与 HyRoadLayers 默认名一一对应）
        public const string RoadPlaneAlignment = "Road.PlaneAlignment";
        public const string RoadProfile = "Road.Profile";
        public const string RoadCorridor = "Road.Corridor";
        public const string RoadMarking = "Road.Marking";
        public const string RoadStation = "Road.Station";
        public const string RoadGeometryPoint = "Road.GeometryPoint";
        public const string RoadOffset = "Road.Offset";
        public const string RoadUserPickPreview = "Road.UserPickPreview";
        public const string RoadRawPolyline = "Road.RawPolyline";
        public const string RoadLivePreview = "Road.LivePreview";
        public const string RoadIntersection = "Road.Intersection";
        public const string RoadCurbRamp = "Road.CurbRamp";
        public const string RoadTactilePaving = "Road.TactilePaving";
        public const string RoadCrosswalk = "Road.Crosswalk";
        public const string RoadStopLine = "Road.StopLine";
        public const string RoadCrossSectionOutline = "Road.CrossSection.Outline";
        public const string RoadCrossSectionCenterline = "Road.CrossSection.Centerline";
        public const string RoadCrossSectionPavement = "Road.CrossSection.Pavement";
        public const string RoadCrossSectionSidewalk = "Road.CrossSection.Sidewalk";
        public const string RoadCrossSectionKerb = "Road.CrossSection.Kerb";
        public const string RoadCrossSectionGreen = "Road.CrossSection.Green";
        public const string RoadCrossSectionDimension = "Road.CrossSection.Dimension";
        public const string RoadCrossSectionAnnotation = "Road.CrossSection.Annotation";
        public const string RoadCrossSectionTitle = "Road.CrossSection.Title";
        public const string RoadCrossSectionTitleDecoration = "Road.CrossSection.TitleDecoration";
        public const string RoadCrossSectionOrientation = "Road.CrossSection.Orientation";
        public const string RoadPlanRedLine = "Road.Plan.RedLine";
        public const string RoadPlanBandDivider = "Road.Plan.BandDivider";
        public const string RoadPlanMarking = "Road.Plan.Marking";

        /// <summary>三维筏板体（原 00_hy_筏板3D）。</summary>
        public const string RaftSolid3D = "Structure.Raft.Solid";
        public const string EquipFoundationSideSolid = "Structure.EquipFoundation.SideSolid";
        public const string EquipFoundationTopSolid = "Structure.EquipFoundation.TopSolid";
        public const string EquipFoundationBottomSolid = "Structure.EquipFoundation.BottomSolid";
        public const string WallMainSolid = "Structure.Wall.Solid";
        public const string WallRetainSolid = "Structure.Wall.RetainSolid";
        public const string WallConnectSolid = "Structure.Wall.ConnectSolid";
    }
}
