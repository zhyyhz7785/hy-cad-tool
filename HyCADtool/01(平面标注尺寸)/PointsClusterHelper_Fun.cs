using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Annotation;
using HyCADTool.Config;
using HyCADTool.Models;
using HyCADTool.Models.Cluster;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.HelpClass
{
    public partial class PointClusterHelper
    {    
        #region 外包框生成
      
        #endregion
        #region 绘制到AutoCAD
        public void DrawExpandedEnvelopes()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                // 创建图层
                ObjectId layerId = EtGpt.CreateLayer("00_hy_Points_聚类轮廓", 126, db, ed);
                foreach (var rect in ExpandedRects)
                {
                    var pline = CreateRectPolyline(rect);
                    pline.LayerId = layerId;
                    btr.AppendEntity(pline);
                    tr.AddNewlyCreatedDBObject(pline, true);
                }
                tr.Commit();
            }
        }
        public void DrawClusterConvexHulls()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (DocumentLock docLock = doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                // 创建图层
                ObjectId layerId = EtGpt.CreateLayer("00_hy_Points_聚类凸包", 127, db, ed);
                foreach (var cluster in ClusterResults)
                {
                    if (cluster.Points == null || cluster.Points.Count < 3) continue;
                    var hull = CalculateConvexHull(cluster.Points);
                    if (hull.Count < 3) continue;
                    var pline = new Polyline();
                    for (int i = 0; i < hull.Count; i++)
                    {
                        pline.AddVertexAt(i, new Point2d(hull[i].X, hull[i].Y), 0, 0, 0);
                    }
                    pline.Closed = true;
                    pline.LayerId = layerId;
                    btr.AppendEntity(pline);
                    tr.AddNewlyCreatedDBObject(pline, true);
                }
                tr.Commit();
            }
        }
        private List<Point3d> CalculateConvexHull(List<Point3d> points)
        {
            var sorted = points.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            List<Point3d> lower = new List<Point3d>();
            foreach (var p in sorted)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }
            List<Point3d> upper = new List<Point3d>();
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                var p = sorted[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }
            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }
        private double Cross(Point3d o, Point3d a, Point3d b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }
        private static Polyline CreateRectPolyline(Extents3d rect)
        {
            var pline = new Polyline();
            pline.AddVertexAt(0, new Point2d(rect.MinPoint.X, rect.MinPoint.Y), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(rect.MaxPoint.X, rect.MinPoint.Y), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(rect.MaxPoint.X, rect.MaxPoint.Y), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(rect.MinPoint.X, rect.MaxPoint.Y), 0, 0, 0);
            pline.Closed = true;
            return pline;
        }
        #endregion
        #region 辅助命令方法        
        public static List<Point3d> SelectPointsOrCircles()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            List<Point3d> points = new List<Point3d>();
            TypedValue[] filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<or"),
                new TypedValue((int)DxfCode.Start, "POINT"),
                new TypedValue((int)DxfCode.Start, "CIRCLE"),
                new TypedValue((int)DxfCode.Start, "INSERT"),
                new TypedValue((int)DxfCode.Operator, "or>")
            };
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择圆、点或带螺栓数据的块: "
            };
            SelectionFilter filter = new SelectionFilter(filterList);
            PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK) return points;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in selRes.Value.GetObjectIds())
                {
                    var ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                    if (ent is DBPoint pt)
                    {
                        points.Add(pt.Position);
                    }
                    else if (ent is Circle circle)
                    {
                        points.Add(circle.Center);
                    }
                    else if (ent is BlockReference br)
                    {
                        points.Add(br.Position);
                    }
                }
                tr.Commit();
            }
            return points;
        }
        [CommandMethod("HYCPD_ClusterPointsAndDraw")]
        public static void ClusterPointsAndDraw()
        {
            var points = SelectPointsOrCircles();
            if (points.Count == 0)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n未找到有效点，操作取消。");
                return;
            }
            var helper = Create(points);
            helper.DrawExpandedEnvelopes();
            helper.DrawClusterConvexHulls();
        }
        #endregion
        public static void AddIntersectionPointsAndCreateDimensions(List<ClusterResult> clusters, List<Line> axisLines, ObjectId layerX, ObjectId layerY)
        {
            if (clusters == null || axisLines == null || axisLines.Count == 0) return;
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var allDims = new List<RotatedDimension>();
            foreach (var cluster in clusters)
            {
                if (cluster.EnvelopeExpandedPolyline == null || cluster.Points == null || cluster.Points.Count == 0)
                    continue;
                var pts = new List<Point3d>();
                for (int i = 0; i < cluster.EnvelopeExpandedPolyline.NumberOfVertices; i++)
                {
                    pts.Add(cluster.EnvelopeExpandedPolyline.GetPoint3dAt(i));
                }
                if (pts.Count < 4) continue;
                Line bottom = new Line(pts[0], pts[1]);
                Line left = new Line(pts[0], pts[3]);
                Point3d center = new Point3d(
                    cluster.Points.Average(p => p.X),
                    cluster.Points.Average(p => p.Y),
                    0);
                Point3d? leftX = IntersectClosest(bottom, center, axisLines);
                Point3d? rightX = IntersectClosest(bottom, center, axisLines);
                if (leftX.HasValue) cluster.Points.Add(leftX.Value);
                if (rightX.HasValue) cluster.Points.Add(rightX.Value);
                Point3d? bottomY = IntersectClosest(left, center, axisLines);
                Point3d? topY = IntersectClosest(left, center, axisLines);
                if (bottomY.HasValue) cluster.Points.Add(bottomY.Value);
                if (topY.HasValue) cluster.Points.Add(topY.Value);
                allDims.AddRange(EtGpt.CreateDimensionsForClusterBothSides(cluster.Points, layerX, layerY, new ClusterDimOptions()));
            }
            allDims.ToSpace(db);
        }
        private static Point3d? IntersectClosest(Line probe, Point3d center, List<Line> axisLines)
        {
            var all = new List<Point3d>();
            foreach (Line axis in axisLines)
            {
                var pts = new Point3dCollection();
                probe.IntersectWith(axis, Intersect.ExtendBoth, pts, IntPtr.Zero, IntPtr.Zero);
                all.AddRange(pts.Cast<Point3d>());
            }
            return all.Count == 0 ? (Point3d?)null : all.OrderBy(p => p.DistanceTo(center)).First();
        }
        public static (List<Point3d> Points, List<Line> Lines) SelectPointsOrCirclesAndLines()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            List<Point3d> points = new List<Point3d>();
            List<Line> lines = new List<Line>();
            TypedValue[] filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<or"),
                new TypedValue((int)DxfCode.Start, "POINT"),
                new TypedValue((int)DxfCode.Start, "CIRCLE"),
                new TypedValue((int)DxfCode.Start, "INSERT"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Operator, "or>")
            };
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择圆、点、块或直线: "
            };
            SelectionFilter filter = new SelectionFilter(filterList);
            PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK) return (points, lines);
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in selRes.Value.GetObjectIds())
                {
                    Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                    if (ent is DBPoint pt)
                    {
                        points.Add(pt.Position);
                    }
                    else if (ent is Circle circle)
                    {
                        points.Add(circle.Center);
                    }
                    else if (ent is BlockReference br)
                    {
                        points.Add(br.Position);
                    }
                    else if (ent is Line line)
                    {
                        lines.Add(line);
                    }
                }
                tr.Commit();
            }
            return (points, lines);
        }
        [Autodesk.AutoCAD.Runtime.CommandMethod("HY_GenerateBoltDimensions")]
        public static void GenerateBoltDimensions()
        {
            var (points, lines) = SelectPointsOrCirclesAndLines();
            if (points.Count == 0 || lines.Count == 0) return;
            var envelopeCluster = new Cluster();
            var config = new ClusterConfig
            {
                EpsilonX = 1500,
                EpsilonY = 1500,
                MinPoints = 1
            };
            var clusters = envelopeCluster.PerformDBSCAN(points, config);
            foreach (var cluster in clusters)
            {
                if (cluster.Points == null || cluster.Points.Count == 0) continue;
                double minX = cluster.Points.Min(p => p.X);
                double maxX = cluster.Points.Max(p => p.X);
                double minY = cluster.Points.Min(p => p.Y);
                double maxY = cluster.Points.Max(p => p.Y);
                cluster.EnvelopeExpandedPolyline = CreateRectPolyline(
                    new Extents3d(
                        new Point3d(minX - GlobalMargin, minY - GlobalMargin, 0),
                        new Point3d(maxX + GlobalMargin, maxY + GlobalMargin, 0)
                    ));
            }
            ObjectId layerX = EtGpt.CreateLayer("00_hy_基础_螺栓_x标注", 142);
            ObjectId layerY = EtGpt.CreateLayer("00_hy_基础_螺栓_y标注", 132);
            AddIntersectionPointsAndCreateDimensions(clusters, lines, layerX, layerY);
        }
        [CommandMethod("HY_AnnotateClustersTest")]
        public static void AnnotateClustersTest()
        {
            var tolerance = 0.001;
            var (points, lines) = SelectPointsOrCirclesAndLines();
            if (points.Count == 0 || lines.Count == 0)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n未选择足够的点和轴线！");
                return;
            }
            // 拆分 X/Y 向轴线
            List<Line> xAxes = new List<Line>();
            List<Line> yAxes = new List<Line>();
            foreach (var axis in lines)
            {
                if (Math.Abs(axis.StartPoint.Y - axis.EndPoint.Y) < tolerance)
                    xAxes.Add(axis);
                else if (Math.Abs(axis.StartPoint.X - axis.EndPoint.X) < tolerance)
                    yAxes.Add(axis);
            }
            // 聚类处理
            Cluster clusterProcessor = new Cluster();
            ClusterConfig config = new ClusterConfig
            {
                EpsilonX = GlobalEpsilonX,
                EpsilonY = GlobalEpsilonY,
                MinPoints = GlobalMinPoints
            };
            List<ClusterResult> clusters = clusterProcessor.PerformDBSCAN(points, config);
            // 补全每个 Cluster 的 EnvelopeExtents（用于标注）
            foreach (var cluster in clusters)
            {
                if (cluster.Points == null || cluster.Points.Count == 0) continue;
                double minX = cluster.Points.Min(p => p.X);
                double maxX = cluster.Points.Max(p => p.X);
                double minY = cluster.Points.Min(p => p.Y);
                double maxY = cluster.Points.Max(p => p.Y);
                cluster.EnvelopeExtents = new Extents3d(
                    new Point3d(minX, minY, 0),
                    new Point3d(maxX, maxY, 0)
                );
                cluster.EnvelopeExpandedPolyline = CreateRectPolyline(
                     new Extents3d(
                         new Point3d(minX - GlobalMargin, minY - GlobalMargin, 0),
                         new Point3d(maxX + GlobalMargin, maxY + GlobalMargin, 0)
                     ));
                cluster.EnvelopeExpandedPolyline.ToSpace();
            }
            // 提示输入比例系数
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            PromptDoubleOptions opts = new PromptDoubleOptions("\n请输入标注比例（例如 1:50 输入 0.02）");
            opts.DefaultValue = GlobalScale;
            opts.AllowZero = false;
            opts.AllowNegative = false;
            PromptDoubleResult res = ed.GetDouble(opts);
            if (res.Status != PromptStatus.OK) return;
            double scale = res.Value;
            // 调用标注主流程
            AnnotationFramework framework = new AnnotationFramework();
            framework.Run(clusters, xAxes, yAxes, scale);
        }
    }
}
