using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.User;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Configuration
{
    /// <summary>
    /// 内置默认图层表：原 <c>PluginInitializer.GetRequiredLayers</c> 中非道路行 + 全量 <see cref="HyRoadLayers.GetAll"/>（按语义去重；道路层与内置种子同名时以后者为准）。
    /// </summary>
    public static class LayerCatalogFactory
    {
        public const int CurrentCatalogVersion = 1;

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

            foreach (var (sid, n, c) in GetNonRoadSeeds())
                AddRow(sid, n, c);

            foreach (var (layerName, colorIndex) in HyRoadLayers.GetAll())
            {
                var sid = MapRoadNameToSemanticId(layerName);
                if (string.IsNullOrEmpty(sid)) continue;
                AddRow(sid, layerName, colorIndex);
            }

            return bySemantic.Values
                .OrderBy(x => x.SemanticId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<(string semanticId, string name, short color)> GetNonRoadSeeds()
        {
            yield return (LayerSemanticIds.ReinLine, "01_hy_1钢筋_线钢筋", 1);
            yield return (LayerSemanticIds.ReinPoint, "01_hy_1钢筋_点钢筋", 5);
            yield return (LayerSemanticIds.ReinLineExternal, "01_hy_1钢筋_线钢筋_外部", 1);
            yield return (LayerSemanticIds.CommonDimOuter, "00_hy_3公共_标注1_外", 3);
            yield return (LayerSemanticIds.CommonMLeader, "00_hy_3公共_标注3_引线", 92);
            yield return (LayerSemanticIds.RaftOutline, "00_hy_配筋轮廓", 1);
            yield return (LayerSemanticIds.RaftOutlineAdjust, "00_hy_调整配筋轮廓", 3);
            yield return (LayerSemanticIds.RaftSlabXTop, "00_hy_筏板附加配筋x_上", 1);
            yield return (LayerSemanticIds.RaftSlabXBottom, "00_hy_筏板附加配筋x_下", 1);
            yield return (LayerSemanticIds.RaftSlabYTop, "00_hy_筏板附加配筋y_上", 3);
            yield return (LayerSemanticIds.RaftSlabYBottom, "00_hy_筏板附加配筋y_下", 3);
            yield return (LayerSemanticIds.RaftTextX, "00_hy_筏板附加配筋文字_x", 7);
            yield return (LayerSemanticIds.RaftTextY, "00_hy_筏板附加配筋文字_y", 7);
            yield return (LayerSemanticIds.RaftDimX, "00_hy_筏板附加配筋x_标注", 1);
            yield return (LayerSemanticIds.RaftDimY, "00_hy_筏板附加配筋Y_标注", 3);
            yield return (LayerSemanticIds.PublicViewport, "00_hy_2公共_视口", 1);
            yield return (LayerSemanticIds.PileMain, "02_hy_1桩_主", 3);
            yield return (LayerSemanticIds.PileContour, "02_hy_3桩_地基内轮廓", 8);
            yield return (LayerSemanticIds.Cushion, "00_hy_垫层", 7);
            yield return (LayerSemanticIds.TitleBlock, "00_hy_图框", 7);
            yield return (LayerSemanticIds.HyRebarTextH, "HY_H向钢筋", 7);
            yield return (LayerSemanticIds.HyRebarTextV, "HY_V向钢筋", 2);
            yield return (LayerSemanticIds.HyRebarManual, "HY_手动配筋", 1);
            yield return (LayerSemanticIds.ClusterBP, "00_hy_BP", 3);
            yield return (LayerSemanticIds.ClusterAAP, "00_hy_AAP", 1);
            yield return (LayerSemanticIds.ClusterBAP, "00_hy_BAP", 4);
            yield return (LayerSemanticIds.ClusterABolt, "00_hy_ABolt", 2);
            yield return (LayerSemanticIds.ClusterSteelPlate, "00_hy_SteelPlate", 5);
            yield return (LayerSemanticIds.ClusterAxisCircle, "00_hy_AxisCircle", 7);
            yield return (LayerSemanticIds.ClusterAxisText, "00_hy_AxisText", 7);
            yield return (LayerSemanticIds.ClusterRegion, "00_hy_Region", 9);
            yield return (LayerSemanticIds.ClusterRegionText, "00_hy_RegionText", 9);
            yield return (LayerSemanticIds.ClusterDimX, "00_hy_Dim_X", 7);
            yield return (LayerSemanticIds.ClusterDimY, "00_hy_Dim_Y", 7);
            yield return (LayerSemanticIds.ClusterEP, "00_hy_ClusterEP", 8);
            yield return (LayerSemanticIds.ClusterEEP, "00_hy_ClusterEEP", 8);
            yield return (LayerSemanticIds.ClusterHull, "00_hy_ClusterHull", 6);
            yield return (LayerSemanticIds.ClusterPts, "00_hy_ClusterPts", 34);
        }

        public static string MapRoadNameToSemanticId(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return null;
            switch (layerName.Trim())
            {
                case "05_hy_道路_平面线位": return LayerSemanticIds.RoadPlaneAlignment;
                case "05_hy_道路_纵断面": return LayerSemanticIds.RoadProfile;
                case "05_hy_道路_走廊": return LayerSemanticIds.RoadCorridor;
                case "05_hy_道路_标线": return LayerSemanticIds.RoadMarking;
                case "05_hy_道路_桩号": return LayerSemanticIds.RoadStation;
                case "05_hy_道路_几何点": return LayerSemanticIds.RoadGeometryPoint;
                case "05_hy_道路_偏移线": return LayerSemanticIds.RoadOffset;
                case "用户拾取": return LayerSemanticIds.RoadUserPickPreview;
                case "05_hy_道路_原线": return LayerSemanticIds.RoadRawPolyline;
                case "05_hy_道路_预览": return LayerSemanticIds.RoadLivePreview;
                case "05_hy_道路_交叉口": return LayerSemanticIds.RoadIntersection;
                case "05_hy_道路_缘石坡道": return LayerSemanticIds.RoadCurbRamp;
                case "05_hy_道路_盲道": return LayerSemanticIds.RoadTactilePaving;
                case "05_hy_道路_人行横道": return LayerSemanticIds.RoadCrosswalk;
                case "05_hy_道路_停止线": return LayerSemanticIds.RoadStopLine;
                case "05_hy_道路_横断面_轮廓": return LayerSemanticIds.RoadCrossSectionOutline;
                case "05_hy_道路_横断面_中心线": return LayerSemanticIds.RoadCrossSectionCenterline;
                case "05_hy_道路_横断面_车行道": return LayerSemanticIds.RoadCrossSectionPavement;
                case "05_hy_道路_横断面_人行道": return LayerSemanticIds.RoadCrossSectionSidewalk;
                case "05_hy_道路_横断面_路牙": return LayerSemanticIds.RoadCrossSectionKerb;
                case "05_hy_道路_横断面_绿化带": return LayerSemanticIds.RoadCrossSectionGreen;
                case "05_hy_道路_横断面_尺寸链": return LayerSemanticIds.RoadCrossSectionDimension;
                case "05_hy_道路_横断面_文字": return LayerSemanticIds.RoadCrossSectionAnnotation;
                case "05_hy_道路_横断面_图题": return LayerSemanticIds.RoadCrossSectionTitle;
                case "05_hy_道路_横断面_图题_装饰": return LayerSemanticIds.RoadCrossSectionTitleDecoration;
                case "05_hy_道路_横断面_方位": return LayerSemanticIds.RoadCrossSectionOrientation;
                case "05_hy_道路_平面_红线": return LayerSemanticIds.RoadPlanRedLine;
                case "05_hy_道路_平面_板块分界": return LayerSemanticIds.RoadPlanBandDivider;
                case "05_hy_道路_平面_标线": return LayerSemanticIds.RoadPlanMarking;
                default: return null;
            }
        }
    }
}
