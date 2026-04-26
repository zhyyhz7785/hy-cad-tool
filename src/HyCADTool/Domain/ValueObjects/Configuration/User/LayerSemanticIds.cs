namespace HyCADTool.Domain.ValueObjects.Configuration.User
{
    /// <summary>
    /// 图层语义 ID（写入层表 JSON，与 <see cref="LayerCatalogFactory"/> 第一列一致）。
    /// 恢复重建：值采用与成员名相同的 Pascal 字符串；若需与历史 hy-settings 完全一致，请从备份替换本文件。
    /// </summary>
    public static class LayerSemanticIds
    {
        public const string ReinLine = nameof(ReinLine);
        public const string ReinPoint = nameof(ReinPoint);
        public const string ReinLineExternal = nameof(ReinLineExternal);
        public const string CommonDimOuter = nameof(CommonDimOuter);
        public const string CommonDimInsideHorizontal = nameof(CommonDimInsideHorizontal);
        public const string CommonDimInsideVertical = nameof(CommonDimInsideVertical);
        public const string CommonMLeader = nameof(CommonMLeader);
        public const string PublicAxisMain = nameof(PublicAxisMain);
        public const string PublicAxisText = nameof(PublicAxisText);
        public const string PublicTableMain = nameof(PublicTableMain);
        public const string PublicNoteGeneral = nameof(PublicNoteGeneral);
        public const string ElevationSymbol = nameof(ElevationSymbol);
        public const string ElevationNoText = nameof(ElevationNoText);
        public const string ElevationWarning = nameof(ElevationWarning);
        public const string RaftOutline = nameof(RaftOutline);
        public const string RaftOutlineAdjust = nameof(RaftOutlineAdjust);
        public const string RaftSlabXTop = nameof(RaftSlabXTop);
        public const string RaftSlabXBottom = nameof(RaftSlabXBottom);
        public const string RaftSlabYTop = nameof(RaftSlabYTop);
        public const string RaftSlabYBottom = nameof(RaftSlabYBottom);
        public const string RaftTextX = nameof(RaftTextX);
        public const string RaftTextY = nameof(RaftTextY);
        public const string RaftDimX = nameof(RaftDimX);
        public const string RaftDimY = nameof(RaftDimY);
        public const string PublicViewport = nameof(PublicViewport);
        public const string PileMain = nameof(PileMain);
        public const string PileContour = nameof(PileContour);
        public const string Cushion = nameof(Cushion);
        public const string TitleBlock = nameof(TitleBlock);
        public const string RaftSolid3D = nameof(RaftSolid3D);
        public const string EquipFoundationSideSolid = nameof(EquipFoundationSideSolid);
        public const string EquipFoundationTopSolid = nameof(EquipFoundationTopSolid);
        public const string EquipFoundationBottomSolid = nameof(EquipFoundationBottomSolid);
        public const string WallMainSolid = nameof(WallMainSolid);
        public const string WallRetainSolid = nameof(WallRetainSolid);
        public const string WallConnectSolid = nameof(WallConnectSolid);
        public const string HyRebarTextH = nameof(HyRebarTextH);
        public const string HyRebarTextV = nameof(HyRebarTextV);
        public const string HyRebarManual = nameof(HyRebarManual);
        public const string ClusterBP = nameof(ClusterBP);
        public const string ClusterAAP = nameof(ClusterAAP);
        public const string ClusterBAP = nameof(ClusterBAP);
        public const string ClusterABolt = nameof(ClusterABolt);
        public const string ClusterSteelPlate = nameof(ClusterSteelPlate);
        public const string ClusterAxisCircle = nameof(ClusterAxisCircle);
        public const string ClusterAxisText = nameof(ClusterAxisText);
        public const string ClusterRegion = nameof(ClusterRegion);
        public const string ClusterRegionText = nameof(ClusterRegionText);
        public const string ClusterDimX = nameof(ClusterDimX);
        public const string ClusterDimY = nameof(ClusterDimY);
        public const string ClusterEP = nameof(ClusterEP);
        public const string ClusterEEP = nameof(ClusterEEP);
        public const string ClusterHull = nameof(ClusterHull);
        public const string ClusterPts = nameof(ClusterPts);

        public const string RoadPlaneAlignment = nameof(RoadPlaneAlignment);
        public const string RoadProfile = nameof(RoadProfile);
        public const string RoadCorridor = nameof(RoadCorridor);
        public const string RoadMarking = nameof(RoadMarking);
        public const string RoadStation = nameof(RoadStation);
        public const string RoadGeometryPoint = nameof(RoadGeometryPoint);
        public const string RoadOffset = nameof(RoadOffset);
        public const string RoadUserPickPreview = nameof(RoadUserPickPreview);
        public const string RoadRawPolyline = nameof(RoadRawPolyline);
        public const string RoadLivePreview = nameof(RoadLivePreview);
        public const string RoadIntersection = nameof(RoadIntersection);
        public const string RoadCurbRamp = nameof(RoadCurbRamp);
        public const string RoadTactilePaving = nameof(RoadTactilePaving);
        public const string RoadCrosswalk = nameof(RoadCrosswalk);
        public const string RoadStopLine = nameof(RoadStopLine);
        public const string RoadCrossSectionOutline = nameof(RoadCrossSectionOutline);
        public const string RoadCrossSectionCenterline = nameof(RoadCrossSectionCenterline);
        public const string RoadCrossSectionPavement = nameof(RoadCrossSectionPavement);
        public const string RoadCrossSectionSidewalk = nameof(RoadCrossSectionSidewalk);
        public const string RoadCrossSectionKerb = nameof(RoadCrossSectionKerb);
        public const string RoadCrossSectionGreen = nameof(RoadCrossSectionGreen);
        public const string RoadCrossSectionDimension = nameof(RoadCrossSectionDimension);
        public const string RoadCrossSectionAnnotation = nameof(RoadCrossSectionAnnotation);
        public const string RoadCrossSectionTitle = nameof(RoadCrossSectionTitle);
        public const string RoadCrossSectionTitleDecoration = nameof(RoadCrossSectionTitleDecoration);
        public const string RoadCrossSectionOrientation = nameof(RoadCrossSectionOrientation);
        public const string RoadPlanRedLine = nameof(RoadPlanRedLine);
        public const string RoadPlanBandDivider = nameof(RoadPlanBandDivider);
        public const string RoadPlanMarking = nameof(RoadPlanMarking);
    }
}
