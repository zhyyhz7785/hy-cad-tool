using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Cluster;
using HyCADTool.Domain.ValueObjects.Configuration.User;
using HyCADTool.Shared.Geometry;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shared.AutoCAD.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Services.Cluster
{
    /// <summary>
    /// 聚类绘图输出服务
    /// 替代旧 DrawInCad：按面板开关将实体写入 CAD
    /// </summary>
    public class ClusterDrawService
    {
        #region 图层配置

        private static (string Name, short Color) L(string semanticId, string fallback, short color) =>
            (UserLayerNameResolver.Get(semanticId, fallback), color);

        private static (string Name, short Color) LayerBP => L(LayerSemanticIds.ClusterBP, LayerBuiltinDefaults.ClusterMain, 3);
        private static (string Name, short Color) LayerAAP => L(LayerSemanticIds.ClusterAAP, LayerBuiltinDefaults.ClusterMain, 1);
        private static (string Name, short Color) LayerBAP => L(LayerSemanticIds.ClusterBAP, LayerBuiltinDefaults.ClusterMain, 4);
        private static (string Name, short Color) LayerAB => L(LayerSemanticIds.ClusterABolt, LayerBuiltinDefaults.ClusterMain, 2);
        private static (string Name, short Color) LayerSteelPl => L(LayerSemanticIds.ClusterSteelPlate, LayerBuiltinDefaults.ClusterMain, 5);

        private static (string Name, short Color) LayerAxisCir => L(LayerSemanticIds.ClusterAxisCircle, LayerBuiltinDefaults.ClusterAxis, 7);
        private static (string Name, short Color) LayerAxisTxt => L(LayerSemanticIds.ClusterAxisText, LayerBuiltinDefaults.ClusterAxis, 7);
        private static (string Name, short Color) LayerRegFrame => L(LayerSemanticIds.ClusterRegion, LayerBuiltinDefaults.ClusterRegion, 9);
        private static (string Name, short Color) LayerRegTxt => L(LayerSemanticIds.ClusterRegionText, LayerBuiltinDefaults.ClusterRegion, 9);

        private static (string Name, short Color) LayerDimX => L(LayerSemanticIds.ClusterDimX, LayerBuiltinDefaults.ClusterDim, 7);
        private static (string Name, short Color) LayerDimY => L(LayerSemanticIds.ClusterDimY, LayerBuiltinDefaults.ClusterDim, 7);

        private static (string Name, short Color) LayerClEP => L(LayerSemanticIds.ClusterEP, LayerBuiltinDefaults.ClusterAux, 8);
        private static (string Name, short Color) LayerClEEP => L(LayerSemanticIds.ClusterEEP, LayerBuiltinDefaults.ClusterAux, 8);
        private static (string Name, short Color) LayerClHull => L(LayerSemanticIds.ClusterHull, LayerBuiltinDefaults.ClusterAux, 6);
        private static (string Name, short Color) LayerClPts => L(LayerSemanticIds.ClusterPts, LayerBuiltinDefaults.ClusterAux, 34);

        private static IEnumerable<(string Name, short Color)> AllDrawLayers()
        {
            yield return LayerBP;
            yield return LayerAAP;
            yield return LayerBAP;
            yield return LayerAB;
            yield return LayerSteelPl;
            yield return LayerAxisCir;
            yield return LayerAxisTxt;
            yield return LayerRegFrame;
            yield return LayerRegTxt;
            yield return LayerDimX;
            yield return LayerDimY;
            yield return LayerClEP;
            yield return LayerClEEP;
            yield return LayerClHull;
            yield return LayerClPts;
        }

        #endregion

        /// <summary>
        /// 主绘制入口
        /// </summary>
        /// <param name="input">输入数据</param>
        /// <param name="axes">轴线分析结果</param>
        /// <param name="clusters">聚类结果</param>
        /// <param name="dims">标注集合</param>
        /// <param name="switches">绘图开关</param>
        public void Draw(
            ClusterInputData input,
            AxisAnalysisResult axes,
            IEnumerable<ClusterResult> clusters,
            IEnumerable<RotatedDimension> dims,
            DrawSwitches switches)
        {
            EnsureLayers();
            if (input != null) DrawPoints(input, switches);
            if (axes != null) DrawAxes(axes, switches);
            if (clusters != null) DrawClusters(clusters, switches);
            if (dims != null) DrawDims(dims, switches);
        }

        /// <summary>
        /// 确保所有聚类图层存在（防止新图纸中图层未创建）
        /// </summary>
        private void EnsureLayers()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                bool upgraded = false;

                foreach (var (name, color) in AllDrawLayers())
                {
                    if (lt.Has(name)) continue;
                    if (!upgraded) { lt.UpgradeOpen(); upgraded = true; }

                    var rec = new LayerTableRecord
                    {
                        Name = name,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, color)
                    };
                    lt.Add(rec);
                    tr.AddNewlyCreatedDBObject(rec, true);
                }

                tr.Commit();
            }
        }

        #region 点集绘制

        private void DrawPoints(ClusterInputData d, DrawSwitches sw)
        {
            if (sw.DrawBPs) WritePoints(d.BPs, LayerBP);
            if (sw.DrawAAPs) WritePoints(d.AAPs, LayerAAP);
            if (sw.DrawBAPs) WritePoints(d.BAPs, LayerBAP);
            if (sw.DrawABs) WritePoints(d.ABs, LayerAB);
            if (sw.DrawSteelPlatePs) WritePoints(d.SteelPlatePs, LayerSteelPl);
        }

        #endregion

        #region 轴线/区域绘制

        private void DrawAxes(AxisAnalysisResult a, DrawSwitches sw)
        {
            // 图层已在 PluginInitializer 统一创建
            if (sw.DrawAxisCircle && a.AxisCircles.Any())
            {
                foreach (var c in a.AxisCircles) { c.Layer = LayerAxisCir.Name; }
                a.AxisCircles.Cast<Entity>().ToList().ToSpace();
            }
            if (sw.DrawAxisText && a.AxisTexts.Any())
            {
                foreach (var t in a.AxisTexts) { t.Layer = LayerAxisTxt.Name; }
                a.AxisTexts.Cast<Entity>().ToList().ToSpace();
            }
            if (sw.DrawRegionFrame && a.RegionFrames.Any())
            {
                foreach (var pl in a.RegionFrames) { pl.Layer = LayerRegFrame.Name; }
                a.RegionFrames.Cast<Entity>().ToList().ToSpace();
            }
            if (sw.DrawRegionText && a.RegionTexts.Any())
            {
                foreach (var t in a.RegionTexts)
                {
                    t.Layer = LayerRegTxt.Name;
                    t.Color = Color.FromColorIndex(ColorMethod.ByAci, LayerRegTxt.Color);
                }
                a.RegionTexts.Cast<Entity>().ToList().ToSpace();
            }
        }

        #endregion

        #region 聚类图形绘制

        private void DrawClusters(IEnumerable<ClusterResult> clusters, DrawSwitches sw)
        {
            foreach (var cluster in clusters)
            {
                if (sw.DrawClusterEnvelopePolyline)
                {
                    var pline = ClusterFactoryService.CreateEnvelopePolyline(cluster);
                    if (pline != null) WriteEntity(pline, LayerClEP);
                }

                if (sw.DrawClusterExpandedEnvelope)
                {
                    var pline = ClusterFactoryService.CreateExpandedEnvelopePolyline(cluster);
                    if (pline != null) WriteEntity(pline, LayerClEEP);
                }

                if (sw.DrawClusterHull)
                {
                    var pline = ClusterFactoryService.CreateConvexHullPolyline(cluster);
                    if (pline != null) WriteEntity(pline, LayerClHull);
                }

                if (sw.DrawClusterPts && cluster.AdditionalIntersections?.Any() == true)
                {
                    // 将 Domain Point2D 转换为 AutoCAD Point3d
                    var pts = cluster.AdditionalIntersections
                        .Select(p => new Point3d(p.X, p.Y, 0));
                    WritePoints(pts, LayerClPts);
                }
            }
        }

        #endregion

        #region 标注绘制

        private void DrawDims(IEnumerable<RotatedDimension> dims, DrawSwitches sw)
        {
            foreach (var d in dims)
            {
                bool isX = Math.Abs(d.Rotation) < 1e-6;
                if (isX && !sw.DrawDimX) continue;
                if (!isX && !sw.DrawDimY) continue;

                var lay = isX ? LayerDimX : LayerDimY;
                d.Layer = lay.Name;
                d.ToSpace();
            }
        }

        #endregion

        #region 写入工具

        private void WritePoints(IEnumerable<Point3d> pts, (string Name, short Color) layer)
        {
            if (pts == null || !pts.Any()) return;
            // 图层已在 PluginInitializer 统一创建

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                foreach (var point in pts)
                {
                    var dbPoint = new DBPoint(point)
                    {
                        Layer = layer.Name,
                        Color = Color.FromColorIndex(ColorMethod.ByAci, layer.Color)
                    };
                    btr.AppendEntity(dbPoint);
                    tr.AddNewlyCreatedDBObject(dbPoint, true);
                }

                tr.Commit();
            }
        }

        private void WriteEntity(Entity ent, (string Name, short Color) layer)
        {
            // 图层已在 PluginInitializer 统一创建
            ent.Layer = layer.Name;
            ent.ToSpace();
        }

        // EnsureLayer 已移除 —— 图层在 PluginInitializer 统一创建

        #endregion
    }

    /// <summary>
    /// 绘图开关 DTO，从 ViewModel 提取
    /// </summary>
    public class DrawSwitches
    {
        // 点类
        public bool DrawBPs { get; set; }
        public bool DrawAAPs { get; set; }
        public bool DrawBAPs { get; set; }
        public bool DrawABs { get; set; }
        public bool DrawSteelPlatePs { get; set; }

        // 聚类图形
        public bool DrawClusterX { get; set; }
        public bool DrawClusterY { get; set; }
        public bool DrawClusterEnvelopePolyline { get; set; }
        public bool DrawClusterExpandedEnvelope { get; set; }
        public bool DrawClusterHull { get; set; }
        public bool DrawClusterPts { get; set; }

        // 标注
        public bool DrawDimX { get; set; }
        public bool DrawDimY { get; set; }

        // 辅助
        public bool DrawAxisCircle { get; set; }
        public bool DrawAxisText { get; set; }
        public bool DrawRegionFrame { get; set; }
        public bool DrawRegionText { get; set; }
    }
}
