using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Shell.Configuration.Global;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Shared.AutoCAD.Configuration
{
    /// <summary>
    /// 内置默认图层表：非道路种子 + 全量 <see cref="HyRoadLayers.GetAll"/>（按语义去重；道路层与内置种子同名时以后者为准）。
    /// </summary>
    public static class LayerCatalogFactory
    {
        /// <summary>递增时与 <see cref="UserLayerSettingsMerger"/> 联动：旧 hy-settings 会刷新默认层名/线型。</summary>
        public const int CurrentCatalogVersion = 5;

        public static IReadOnlyList<LayerDefinitionItem> CreateDefaultItems()
        {
            var bySemantic = new Dictionary<string, LayerDefinitionItem>(StringComparer.Ordinal);

            void AddRow(string semanticId, string name, short aci,
                string linetype = "Continuous", int lineWeight = -1, bool plottable = true, bool locked = false)
            {
                if (string.IsNullOrWhiteSpace(semanticId) || string.IsNullOrWhiteSpace(name)) return;
                if (bySemantic.ContainsKey(semanticId)) return;
                bySemantic[semanticId] = new LayerDefinitionItem
                {
                    SemanticId = semanticId,
                    Name = name.Trim(),
                    AciColor = aci,
                    LinetypeName = linetype,
                    LineWeightRaw = lineWeight,
                    IsPlottable = plottable,
                    IsLocked = locked
                };
            }

            foreach (var row in GetNonRoadSeeds())
                AddRow(row.sid, row.name, row.color, row.linetype);

            foreach (var (layerName, colorIndex, lt) in HyRoadLayers.GetAll())
            {
                var sid = MapRoadNameToSemanticId(layerName);
                if (string.IsNullOrEmpty(sid)) continue;
                AddRow(sid, layerName, colorIndex, lt);
            }

            return bySemantic.Values
                .OrderBy(x => x.SemanticId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<(string sid, string name, short color, string linetype)> GetNonRoadSeeds()
        {
            var c = LayerBuiltinDefaults.LinetypeContinuous;
            var hyCenter = HyLinetypeNames.Center;
            yield return (LayerSemanticIds.ReinLine, LayerBuiltinDefaults.ReinLine, 1, c);
            yield return (LayerSemanticIds.ReinPoint, LayerBuiltinDefaults.ReinPoint, 2, c);
            yield return (LayerSemanticIds.ReinLineExternal, LayerBuiltinDefaults.ReinLineExternal, 3, c);
            yield return (LayerSemanticIds.CommonDimOuter, LayerBuiltinDefaults.CommonDimOuter, 3, c);
            yield return (LayerSemanticIds.CommonMLeader, LayerBuiltinDefaults.CommonMLeader, 3, c);
            yield return (LayerSemanticIds.RaftOutline, LayerBuiltinDefaults.RaftOutline, 3, c);
            yield return (LayerSemanticIds.RaftOutlineAdjust, LayerBuiltinDefaults.RaftOutlineAdjust, 1, c);
            yield return (LayerSemanticIds.RaftSlabXTop, LayerBuiltinDefaults.RaftSlabXTop, 1, c);
            yield return (LayerSemanticIds.RaftSlabXBottom, LayerBuiltinDefaults.RaftSlabXBottom, 1, c);
            yield return (LayerSemanticIds.RaftSlabYTop, LayerBuiltinDefaults.RaftSlabYTop, 3, c);
            yield return (LayerSemanticIds.RaftSlabYBottom, LayerBuiltinDefaults.RaftSlabYBottom, 3, c);
            yield return (LayerSemanticIds.RaftTextX, LayerBuiltinDefaults.RaftTextX, 7, c);
            yield return (LayerSemanticIds.RaftTextY, LayerBuiltinDefaults.RaftTextY, 7, c);
            yield return (LayerSemanticIds.RaftDimX, LayerBuiltinDefaults.RaftDimX, 7, c);
            yield return (LayerSemanticIds.RaftDimY, LayerBuiltinDefaults.RaftDimY, 7, c);
            yield return (LayerSemanticIds.PublicViewport, LayerBuiltinDefaults.PublicViewport, 8, c);
            yield return (LayerSemanticIds.PileMain, LayerBuiltinDefaults.PileMain, 2, c);
            yield return (LayerSemanticIds.PileContour, LayerBuiltinDefaults.PileContour, 7, c);
            yield return (LayerSemanticIds.Cushion, LayerBuiltinDefaults.Cushion, 8, c);
            yield return (LayerSemanticIds.TitleBlock, LayerBuiltinDefaults.TitleBlock, 7, c);
            yield return (LayerSemanticIds.HyRebarTextH, LayerBuiltinDefaults.HyRebarTextH, 7, c);
            yield return (LayerSemanticIds.HyRebarTextV, LayerBuiltinDefaults.HyRebarTextV, 7, c);
            yield return (LayerSemanticIds.HyRebarManual, LayerBuiltinDefaults.HyRebarManual, 6, c);
            yield return (LayerSemanticIds.ClusterBP, LayerBuiltinDefaults.ClusterMain, 3, c);
            yield return (LayerSemanticIds.ClusterAAP, LayerBuiltinDefaults.ClusterMain, 3, c);
            yield return (LayerSemanticIds.ClusterBAP, LayerBuiltinDefaults.ClusterMain, 3, c);
            yield return (LayerSemanticIds.ClusterABolt, LayerBuiltinDefaults.ClusterMain, 3, c);
            yield return (LayerSemanticIds.ClusterSteelPlate, LayerBuiltinDefaults.ClusterMain, 3, c);
            yield return (LayerSemanticIds.ClusterAxisCircle, LayerBuiltinDefaults.ClusterAxis, 7, hyCenter);
            yield return (LayerSemanticIds.ClusterAxisText, LayerBuiltinDefaults.ClusterAxis, 7, hyCenter);
            yield return (LayerSemanticIds.ClusterRegion, LayerBuiltinDefaults.ClusterRegion, 9, c);
            yield return (LayerSemanticIds.ClusterRegionText, LayerBuiltinDefaults.ClusterRegion, 9, c);
            yield return (LayerSemanticIds.ClusterDimX, LayerBuiltinDefaults.ClusterDim, 7, c);
            yield return (LayerSemanticIds.ClusterDimY, LayerBuiltinDefaults.ClusterDim, 7, c);
            yield return (LayerSemanticIds.ClusterEP, LayerBuiltinDefaults.ClusterAux, 8, c);
            yield return (LayerSemanticIds.ClusterEEP, LayerBuiltinDefaults.ClusterAux, 8, c);
            yield return (LayerSemanticIds.ClusterHull, LayerBuiltinDefaults.ClusterAux, 8, c);
            yield return (LayerSemanticIds.ClusterPts, LayerBuiltinDefaults.ClusterAux, 8, c);
            yield return (LayerSemanticIds.HyfeaResult, LayerBuiltinDefaults.HyfeaResult, 7, c);

            yield return (LayerSemanticIds.PublicAxisMain, LayerBuiltinDefaults.PublicAxisMain, 1, hyCenter);
            yield return (LayerSemanticIds.PublicAxisText, LayerBuiltinDefaults.PublicAxisText, 1, c);
            yield return (LayerSemanticIds.PublicTableMain, LayerBuiltinDefaults.PublicTableMain, 7, c);
            yield return (LayerSemanticIds.PublicNoteGeneral, LayerBuiltinDefaults.NoteGeneral, 7, c);
            yield return (LayerSemanticIds.CommonDimInsideHorizontal, LayerBuiltinDefaults.CommonDimInsideHorizontal, 1, c);
            yield return (LayerSemanticIds.CommonDimInsideVertical, LayerBuiltinDefaults.CommonDimInsideVertical, 2, c);
            yield return (LayerSemanticIds.ElevationSymbol, LayerBuiltinDefaults.ElevationSymbol, 3, c);
            yield return (LayerSemanticIds.ElevationNoText, LayerBuiltinDefaults.ElevationNoText, 200, c);
            yield return (LayerSemanticIds.ElevationWarning, LayerBuiltinDefaults.ElevationWarning, 1, c);
            yield return (LayerSemanticIds.RaftSolid3D, LayerBuiltinDefaults.RaftSolid3D, 8, c);
            yield return (LayerSemanticIds.EquipFoundationSideSolid, LayerBuiltinDefaults.EquipSideSolid, 1, c);
            yield return (LayerSemanticIds.EquipFoundationTopSolid, LayerBuiltinDefaults.EquipTopSolid, 3, c);
            yield return (LayerSemanticIds.EquipFoundationBottomSolid, LayerBuiltinDefaults.EquipBottomSolid, 5, c);
            yield return (LayerSemanticIds.WallMainSolid, LayerBuiltinDefaults.WallMainSolid, 8, c);
            yield return (LayerSemanticIds.WallRetainSolid, LayerBuiltinDefaults.WallRetainSolid, 5, c);
            yield return (LayerSemanticIds.WallConnectSolid, LayerBuiltinDefaults.WallConnectSolid, 4, c);
        }

        public static string MapRoadNameToSemanticId(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return null;
            switch (layerName.Trim())
            {
                case LayerBuiltinDefaults.RoadPlaneAlignment:
                case "05_hy_道路_平面线位":
                    return LayerSemanticIds.RoadPlaneAlignment;
                case LayerBuiltinDefaults.RoadProfile:
                case "05_hy_道路_纵断面":
                    return LayerSemanticIds.RoadProfile;
                case LayerBuiltinDefaults.RoadCorridor:
                case "05_hy_道路_走廊":
                    return LayerSemanticIds.RoadCorridor;
                case LayerBuiltinDefaults.RoadMarking:
                case "05_hy_道路_标线":
                    return LayerSemanticIds.RoadMarking;
                case LayerBuiltinDefaults.RoadStation:
                case "05_hy_道路_桩号":
                    return LayerSemanticIds.RoadStation;
                case LayerBuiltinDefaults.RoadGeometryPoint:
                case "05_hy_道路_几何点":
                    return LayerSemanticIds.RoadGeometryPoint;
                case LayerBuiltinDefaults.RoadOffset:
                case "05_hy_道路_偏移线":
                    return LayerSemanticIds.RoadOffset;
                case LayerBuiltinDefaults.RoadUserPickPreview:
                case "用户拾取":
                    return LayerSemanticIds.RoadUserPickPreview;
                case LayerBuiltinDefaults.RoadRawPolyline:
                case "05_hy_道路_原线":
                    return LayerSemanticIds.RoadRawPolyline;
                case LayerBuiltinDefaults.RoadLivePreview:
                case "05_hy_道路_预览":
                    return LayerSemanticIds.RoadLivePreview;
                case LayerBuiltinDefaults.RoadIntersection:
                case "05_hy_道路_交叉口":
                    return LayerSemanticIds.RoadIntersection;
                case LayerBuiltinDefaults.RoadCurbRamp:
                case "05_hy_道路_缘石坡道":
                    return LayerSemanticIds.RoadCurbRamp;
                case LayerBuiltinDefaults.RoadTactilePaving:
                case "05_hy_道路_盲道":
                    return LayerSemanticIds.RoadTactilePaving;
                case LayerBuiltinDefaults.RoadCrosswalk:
                case "05_hy_道路_人行横道":
                    return LayerSemanticIds.RoadCrosswalk;
                case LayerBuiltinDefaults.RoadStopLine:
                case "05_hy_道路_停止线":
                    return LayerSemanticIds.RoadStopLine;
                case LayerBuiltinDefaults.RoadCrossSectionOutline:
                case "05_hy_道路_横断面_轮廓":
                    return LayerSemanticIds.RoadCrossSectionOutline;
                case LayerBuiltinDefaults.RoadCrossSectionCenterline:
                case "05_hy_道路_横断面_中心线":
                    return LayerSemanticIds.RoadCrossSectionCenterline;
                case LayerBuiltinDefaults.RoadCrossSectionPavement:
                case "05_hy_道路_横断面_车行道":
                    return LayerSemanticIds.RoadCrossSectionPavement;
                case LayerBuiltinDefaults.RoadCrossSectionSidewalk:
                case "05_hy_道路_横断面_人行道":
                    return LayerSemanticIds.RoadCrossSectionSidewalk;
                case LayerBuiltinDefaults.RoadCrossSectionKerb:
                case "05_hy_道路_横断面_路牙":
                    return LayerSemanticIds.RoadCrossSectionKerb;
                case LayerBuiltinDefaults.RoadCrossSectionGreen:
                case "05_hy_道路_横断面_绿化带":
                    return LayerSemanticIds.RoadCrossSectionGreen;
                case LayerBuiltinDefaults.RoadCrossSectionDimension:
                case "05_hy_道路_横断面_尺寸链":
                    return LayerSemanticIds.RoadCrossSectionDimension;
                case "05_hy_道路_横断面_图题_装饰":
                    return LayerSemanticIds.RoadCrossSectionTitle;
                case "05_hy_道路_横断面_方位":
                    return LayerSemanticIds.RoadCrossSectionAnnotation;
                case "02-hy-3横断-图题":
                case "05_hy_道路_横断面_图题":
                    return LayerSemanticIds.RoadCrossSectionTitle;
                case "02-hy-3横断-注释-文":
                case "05_hy_道路_横断面_文字":
                    return LayerSemanticIds.RoadCrossSectionAnnotation;
                case LayerBuiltinDefaults.RoadPlanRedLine:
                case "05_hy_道路_平面_红线":
                    return LayerSemanticIds.RoadPlanRedLine;
                case LayerBuiltinDefaults.RoadPlanBandDivider:
                case "05_hy_道路_平面_板块分界":
                    return LayerSemanticIds.RoadPlanBandDivider;
                case LayerBuiltinDefaults.RoadPlanMarking:
                case "05_hy_道路_平面_标线":
                    return LayerSemanticIds.RoadPlanMarking;
                default: return null;
            }
        }
    }
}
