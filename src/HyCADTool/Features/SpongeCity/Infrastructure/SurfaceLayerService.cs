using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.SpongeCity.Domain.Models;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.SpongeCity.Infrastructure
{
    /// <summary>
    /// AutoCAD 侧的下垫面图层工具：
    /// - 自动建层 / 取色；
    /// - 扫描特定图层闭合 Polyline 的面积；
    /// - 计算 Polyline 形心（优先 Region.Centroid，失败退回外接框中心）。
    /// </summary>
    public static class SurfaceLayerService
    {
        /// <summary>确保图层存在；不存在则按 ACI 颜色创建（事务内已 ForRead 后 UpgradeOpen）。</summary>
        public static void EnsureLayer(Transaction tr, Database db, string layerName, short aciColor)
        {
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has(layerName)) return;
            layerTable.UpgradeOpen();
            var ltr = new LayerTableRecord
            {
                Name = layerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, aciColor),
            };
            layerTable.Add(ltr);
            tr.AddNewlyCreatedDBObject(ltr, true);
        }

        /// <summary>扫描整个 ModelSpace，收集指定图层上的闭合 Polyline {Id, 形心, 面积}。</summary>
        public static List<PolylineHit> CollectClosedPolylines(Transaction tr, Database db, string layerName)
        {
            var result = new List<PolylineHit>();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                if (!string.Equals(ent.Layer, layerName, StringComparison.Ordinal)) continue;

                if (ent is Polyline pl && pl.Closed)
                {
                    double area = Math.Abs(pl.Area);
                    var centroid = ComputeCentroid(pl);
                    result.Add(new PolylineHit { Id = id, Area = area, Centroid = centroid });
                }
                else if (ent is Polyline2d p2d && p2d.Closed)
                {
                    double area = Math.Abs(p2d.Area);
                    var box = SafeGeomExtents(p2d);
                    var centroid = new Point3d(
                        (box.MinPoint.X + box.MaxPoint.X) / 2,
                        (box.MinPoint.Y + box.MaxPoint.Y) / 2,
                        0);
                    result.Add(new PolylineHit { Id = id, Area = area, Centroid = centroid });
                }
            }
            return result;
        }

        /// <summary>
        /// 计算 Polyline 形心：用 Shoelace 公式按段对 Polyline 顶点求面积加权重心；
        /// 失败（凸性/段数异常）时退回 Region.GeometricExtents 中心，再退回外接框中心。
        /// </summary>
        public static Point3d ComputeCentroid(Polyline pl)
        {
            // 1) 多边形顶点直接 Shoelace 形心（仅取顶点链，圆弧段近似为弦）
            try
            {
                int n = pl.NumberOfVertices;
                if (n >= 3)
                {
                    double a = 0, cx = 0, cy = 0;
                    for (int i = 0; i < n; i++)
                    {
                        var p0 = pl.GetPoint2dAt(i);
                        var p1 = pl.GetPoint2dAt((i + 1) % n);
                        double cross = p0.X * p1.Y - p1.X * p0.Y;
                        a += cross;
                        cx += (p0.X + p1.X) * cross;
                        cy += (p0.Y + p1.Y) * cross;
                    }
                    a *= 0.5;
                    if (Math.Abs(a) > 1e-9)
                    {
                        cx /= (6.0 * a);
                        cy /= (6.0 * a);
                        return new Point3d(cx, cy, 0);
                    }
                }
            }
            catch { }

            // 2) Region 外接框中心兜底
            try
            {
                var curves = new DBObjectCollection();
                var clone = (Polyline)pl.Clone();
                curves.Add(clone);
                using (var regions = Region.CreateFromCurves(curves))
                {
                    if (regions != null && regions.Count > 0 && regions[0] is Region rg)
                    {
                        var ext = rg.GeometricExtents;
                        return new Point3d(
                            (ext.MinPoint.X + ext.MaxPoint.X) / 2,
                            (ext.MinPoint.Y + ext.MaxPoint.Y) / 2,
                            0);
                    }
                }
            }
            catch { }

            // 3) Polyline 外接框中心兜底
            var box = SafeGeomExtents(pl);
            return new Point3d(
                (box.MinPoint.X + box.MaxPoint.X) / 2,
                (box.MinPoint.Y + box.MaxPoint.Y) / 2,
                0);
        }

        private static Extents3d SafeGeomExtents(Entity ent)
        {
            try { return ent.GeometricExtents; }
            catch
            {
                return new Extents3d(Point3d.Origin, Point3d.Origin);
            }
        }

        public class PolylineHit
        {
            public ObjectId Id;
            public Point3d Centroid;
            public double Area;
        }
    }
}
