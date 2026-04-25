using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Services.Cluster
{
    /// <summary>
    /// 聚类输入采集服务
    /// 替代旧 DimPointsAndAxis + DimPointsAndAxisConfig
    /// </summary>
    public class ClusterInputService
    {
        private static bool IsPublicAxisMainLayer(string layerName) =>
            string.Equals(layerName, UserLayerNameResolver.Get(LayerSemanticIds.PublicAxisMain, LayerBuiltinDefaults.PublicAxisMain), StringComparison.Ordinal)
            || string.Equals(layerName, "00_hy_3公共_轴线_总", StringComparison.Ordinal);

        /// <summary>
        /// 从 CAD 选择集中采集输入数据
        /// </summary>
        /// <param name="includeBPs">包含基础轮廓点</param>
        /// <param name="includeAAPs">包含轴线交点</param>
        /// <param name="includeBAPs">包含基础轴线交点</param>
        /// <param name="includeABs">包含螺栓点</param>
        /// <param name="includeSteelPlatePs">包含钢板点</param>
        /// <returns>输入数据，选择取消时返回 null</returns>
        public ClusterInputData GatherInput(
            bool includeBPs, bool includeAAPs, bool includeBAPs,
            bool includeABs, bool includeSteelPlatePs,
            double tolerance = 0.001)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<or"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Start, "POINT"),
                new TypedValue((int)DxfCode.Start, "CIRCLE"),
                new TypedValue((int)DxfCode.Start, "INSERT"),
                new TypedValue((int)DxfCode.Operator, "or>")
            };

            var filter = new SelectionFilter(filterList);
            var selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择轮廓、轴线、点、圆或块: "
            };

            var selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK) return null;

            var data = new ClusterInputData();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var basePolylines = new List<Polyline>();

                foreach (ObjectId objId in selRes.Value.GetObjectIds())
                {
                    var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    string layerName = ent.Layer ?? string.Empty;

                    switch (ent)
                    {
                        case Polyline pl:
                            if (layerName == "dcelInter")
                            {
                                basePolylines.Add(pl);
                            }
                            else if (layerName.StartsWith("00_Hy_螺栓_预埋板"))
                            {
                                var ext = pl.GeometricExtents;
                                var center = new Point3d(
                                    (ext.MinPoint.X + ext.MaxPoint.X) / 2,
                                    (ext.MinPoint.Y + ext.MaxPoint.Y) / 2, 0);
                                data.SteelPlatePs.Add(center);
                            }
                            break;

                        case Line line when IsPublicAxisMainLayer(layerName):
                            data.AxisLines.Add(line);
                            break;

                        case DBPoint pt when layerName.StartsWith("00_Hy_螺栓"):
                            data.ABs.Add(pt.Position);
                            break;

                        case Circle c when layerName.StartsWith("00_Hy_螺栓"):
                            data.ABs.Add(c.Center);
                            break;

                        case BlockReference br when layerName.StartsWith("00_Hy_螺栓"):
                            data.ABs.Add(br.Position);
                            break;
                    }
                }

                // 基础轮廓点 = 多段线顶点
                data.BPs = GetVertices(basePolylines, tolerance);

                // 轴线自交点
                data.AAPs = GetAxisSelfIntersections(data.AxisLines, tolerance);

                // 基础-轴线交点
                data.BAPs = GetPolylineAxisIntersections(basePolylines, data.AxisLines, tolerance);

                tr.Commit();
            }

            // 根据开关过滤点
            data.ComputeFilteredPoints(includeBPs, includeAAPs, includeBAPs, includeABs, includeSteelPlatePs, tolerance);

            return data;
        }

        private List<Point3d> GetVertices(List<Polyline> polylines, double tolerance)
        {
            var set = new HashSet<Point3d>(new Point2dEqualityComparer(tolerance));
            foreach (var pl in polylines)
            {
                for (int i = 0; i < pl.NumberOfVertices; i++)
                    set.Add(pl.GetPoint3dAt(i));
            }
            return set.ToList();
        }

        private List<Point3d> GetAxisSelfIntersections(List<Line> axisLines, double tolerance)
        {
            var points = new HashSet<Point3d>(new Point2dEqualityComparer(tolerance));
            for (int i = 0; i < axisLines.Count; i++)
            {
                for (int j = i + 1; j < axisLines.Count; j++)
                    points.UnionWith(GetIntersectionPoints(axisLines[i], axisLines[j]));
            }
            return points.ToList();
        }

        private List<Point3d> GetPolylineAxisIntersections(List<Polyline> polylines, List<Line> axisLines, double tolerance)
        {
            var points = new HashSet<Point3d>(new Point2dEqualityComparer(tolerance));
            var lines = ExplodePolylinesToLines(polylines);
            foreach (var baseLine in lines)
                foreach (var axis in axisLines)
                    points.UnionWith(GetIntersectionPoints(baseLine, axis));
            return points.ToList();
        }

        private List<Line> ExplodePolylinesToLines(List<Polyline> polylines)
        {
            var lines = new List<Line>();
            foreach (var pl in polylines)
            {
                var coll = new DBObjectCollection();
                pl.Explode(coll);
                foreach (DBObject obj in coll)
                    if (obj is Line line)
                        lines.Add(line);
            }
            return lines;
        }

        private List<Point3d> GetIntersectionPoints(Line l1, Line l2)
        {
            var pts = new Point3dCollection();
            try { l1.IntersectWith(l2, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero); }
            catch { /* 忽略不相交 */ }
            return pts.Cast<Point3d>().ToList();
        }

        /// <summary>2D 点相等比较器</summary>
        private class Point2dEqualityComparer : IEqualityComparer<Point3d>
        {
            private readonly double _tol;
            public Point2dEqualityComparer(double tol) => _tol = tol;
            public bool Equals(Point3d p1, Point3d p2) =>
                Math.Abs(p1.X - p2.X) <= _tol && Math.Abs(p1.Y - p2.Y) <= _tol;
            public int GetHashCode(Point3d p) => ((int)(p.X / _tol)) * 397 ^ (int)(p.Y / _tol);
        }
    }

    /// <summary>
    /// 聚类输入数据 DTO
    /// </summary>
    public class ClusterInputData
    {
        /// <summary>基础轮廓点</summary>
        public List<Point3d> BPs { get; set; } = new List<Point3d>();

        /// <summary>轴线交点</summary>
        public List<Point3d> AAPs { get; set; } = new List<Point3d>();

        /// <summary>基础轴线交点</summary>
        public List<Point3d> BAPs { get; set; } = new List<Point3d>();

        /// <summary>螺栓点</summary>
        public List<Point3d> ABs { get; set; } = new List<Point3d>();

        /// <summary>钢板点</summary>
        public List<Point3d> SteelPlatePs { get; set; } = new List<Point3d>();

        /// <summary>轴线集合</summary>
        public List<Line> AxisLines { get; set; } = new List<Line>();

        /// <summary>按开关过滤后的合并点集</summary>
        public List<Point3d> FilteredPoints { get; private set; } = new List<Point3d>();

        /// <summary>根据开关合并过滤点</summary>
        public void ComputeFilteredPoints(
            bool includeBPs, bool includeAAPs, bool includeBAPs,
            bool includeABs, bool includeSteelPlatePs,
            double tolerance = 0.001)
        {
            var pts = new List<Point3d>();
            if (includeBPs) pts.AddRange(BPs);
            if (includeAAPs) pts.AddRange(AAPs);
            if (includeBAPs) pts.AddRange(BAPs);
            if (includeABs) pts.AddRange(ABs);
            if (includeSteelPlatePs) pts.AddRange(SteelPlatePs);

            FilteredPoints = pts
                .Distinct(new HyCADTool.Shared.AutoCAD.Utilities.Point3dComparer(tolerance))
                .ToList();
        }
    }
}
