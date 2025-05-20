using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Models;
using HyCADTool.Models.Cluster;
using HyCADTool.Tools;
using System.Collections.Generic;
using System.Linq;
using static HyCADTool.Tools.HyTool;

namespace HyCADTool.Drawing
{
    public static class DrawInCad
    {
        /*──────────── ① 总/分项 开关 ────────────*/
        public static bool EnableCadOutput = true;

        public static bool Draw_BPs = false;
        public static bool Draw_AAPs = false;
        public static bool Draw_BAPs = false;
        public static bool Draw_ABs = false;
        public static bool Draw_SteelPlPs = false;

        public static bool Draw_AxisCircle = false;
        public static bool Draw_AxisText = false;

        public static bool Draw_RegionFrame = false;
        public static bool Draw_RegionText = false;

        public static bool Draw_ClusterEnvelopePolyline = true;
        public static bool Draw_ClusterEnvelopeExpandedPolyline = false;
        public static bool Draw_ClusterHull = false;
        public static bool Draw_ClusterPts = false;

        public static bool Draw_DimX = true;
        public static bool Draw_DimY = true;

        /*──────────── ② 图层配置（元组） ─────────*/
        private static (string, short) BP = ("00_hy_BP", 3);
        private static (string, short) AAP = ("00_hy_AAP", 1);
        private static (string, short) BAP = ("00_hy_BAP", 4);
        private static (string, short) AB = ("00_hy_ABolt", 2);
        private static (string, short) SteelPl = ("00_hy_SteelPlate", 5);

        private static (string, short) AxisCir = ("00_hy_AxisCircle", 7);
        private static (string, short) AxisTxt = ("00_hy_AxisText", 7);

        private static (string, short) RegFrame = ("00_hy_Region", 9);
        private static (string, short) RegTxt = ("00_hy_RegionText", 9);

        private static (string, short) DimXLay = ("00_hy_Dim_X", 7);
        private static (string, short) DimYLay = ("00_hy_Dim_Y", 7);

        private static (string, short) ClEP = ("00_hy_ClusterEP", 8);
        private static (string, short) ClEEP = ("00_hy_ClusterEEP", 8);
        private static (string, short) ClHull = ("00_hy_ClusterHull", 6);
        private static (string, short) ClPts = ("00_hy_ClusterPts", 34);

        /*──────────── ③ 主写入入口 ─────────────*/
        public static void Draw(
            DimPointsAndAxis dpa,
            AxisDatas axes,
            IEnumerable<ClusterResult> clusters,
            IEnumerable<RotatedDimension> dims)
        {
            if (!EnableCadOutput) return;

            if (dpa != null) DrawPts(dpa);
            if (axes != null) DrawAxes(axes);
            if (clusters != null) DrawClusters(clusters);
            if (dims != null) DrawDims(dims);
        }

        /*──────────── 点集 ────────────*/
        private static void DrawPts(DimPointsAndAxis d)
        {
            if (Draw_BPs) WritePts(d.BPs, BP);
            if (Draw_AAPs) WritePts(d.A_APs, AAP);
            if (Draw_BAPs) WritePts(d.B_APs, BAP);
            if (Draw_ABs) WritePts(d.ABs, AB);
            if (Draw_SteelPlPs) WritePts(d.SteelPlatePs, SteelPl);
        }

        /*──────────── 轴线/区域 ─────────*/
        private static void DrawAxes(AxisDatas a)
        {
            if (Draw_AxisCircle && a.AxisCircles.Any())
            {
                EnsureLayer(AxisCir);
                Safe(() => a.AxisCircles.ToSpace());
            }
            if (Draw_AxisText && a.AxisTexts.Any())
            {
                EnsureLayer(AxisTxt);
                Safe(() =>
                {
                    foreach (var t in a.AxisTexts) t.SetLayer(AxisTxt.Item1);
                    a.AxisTexts.ToSpace();
                });
            }
            if (Draw_RegionFrame && a.RegionFrames.Any())
            {
                EnsureLayer(RegFrame);
                Safe(() =>
                {
                    a.RegionFrames.ForEach(pl => pl.SetLayer(RegFrame.Item1));
                    a.RegionFrames.ToSpace();
                });
            }
            if (Draw_RegionText && a.RegionTexts.Any())
            {
                EnsureLayer(RegTxt);
                Safe(() =>
                {
                    foreach (var t in a.RegionTexts)
                    {
                        t.SetLayer(RegTxt.Item1);
                        t.Color = Color.FromColorIndex(ColorMethod.ByAci, RegTxt.Item2);
                    }
                    a.RegionTexts.ToSpace();
                });
            }
        }

        /*──────────── 聚类 ─────────────*/
        private static void DrawClusters(IEnumerable<ClusterResult> clusters)
        {
            foreach (var cluster in clusters)
            {
                // 绘制最小外包矩形（Envelope）
                if (Draw_ClusterEnvelopePolyline && cluster.EnvelopePolyline != null)
                {
                    WriteEnt(cluster.EnvelopePolyline, ClEP);
                }

                // 绘制扩展后的外包矩形（Expanded Envelope）
                if (Draw_ClusterEnvelopeExpandedPolyline && cluster.EnvelopeExpandedPolyline != null)
                {
                    WriteEnt(cluster.EnvelopeExpandedPolyline, ClEEP);
                }

                // 绘制凸包
                if (Draw_ClusterHull && cluster.ConvexHullPolyline != null)
                {
                    WriteEnt(cluster.ConvexHullPolyline, ClHull);
                }

                // 绘制附加交点
                if (Draw_ClusterPts && cluster.AdditionalIntersections?.Any() == true)
                {
                    WritePts(cluster.AdditionalIntersections, ClPts);
                }
            }
        }


        /*──────────── 尺寸 ─────────────*/
        private static void DrawDims(IEnumerable<RotatedDimension> ds)
        {
            foreach (var d in ds)
            {
                bool isX = System.Math.Abs(d.Rotation) < 1e-6;
                if (isX && !Draw_DimX) continue;
                if (!isX && !Draw_DimY) continue;

                var lay = isX ? DimXLay : DimYLay;
                EnsureLayer(lay);
                Safe(() => { d.SetLayer(lay.Item1); d.ToSpace(); });
            }
        }

        /*──────────── 写入工具 ─────────*/
        private static void WritePts(IEnumerable<Point3d> pts, (string, short) layer)
        {
            if (!pts.Any()) return;
            EnsureLayer(layer);
            Safe(() => pts.ToSpace(layer.Item1, layer.Item2));
        }

        private static void WriteEnt(Entity ent, (string, short) layer, short? colorOverride = null)
        {
            EnsureLayer(layer);
            Safe(() =>
            {
                ent.SetLayer(layer.Item1);
                if (colorOverride.HasValue)
                    ent.Color = Color.FromColorIndex(ColorMethod.ByAci, colorOverride.Value);
                ent.ToSpace();
            });
        }

        private static void EnsureLayer((string name, short color) layer)
            => CreateLayer(layer.name, layer.color);

        private static void Safe(System.Action act)
        {
            if (EnableCadOutput && act != null) act();
        }
    }
}
