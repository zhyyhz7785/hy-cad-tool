using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Markup;
using System.Windows.Shapes;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
namespace HyCADTool.Command
{
    public static partial class DrawBaseReinforcement
    {
        public enum IntersectionType
        {
            NegativeExtensionIntersection, // 负延交点
            StartPoint,                // 起点
            InsideLowerIntersection,   // 内下交点
            InsideUpperIntersection,   // 内上交点
            EndPoint,                  // 端点
            PositiveExtensionIntersection, // 正延交点        
        }
        //输入取整间隔
        public static double Interval { get; set; } = 1000;
        public static void Poly4Dim()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            Reinforcement.Scale = 100;
            var lines = new List<Line>();
            var polylines = new List<Polyline>();
            var ids = EtGpt.SelectWithFilter(AcTv.Or(AcTv.Line, AcTv.Polyline).Getfilter());
            ClassifyLineAndPolyline(ids, out lines, out polylines);
            var dic = GetPolylineIntersections(lines, polylines, 10000);
            DeletePolylinesInCad(dic);
            dic= dic.AdjustPolylines(Interval);
            EtGpt.CreateMultipleLayers(
                ("00_hy_筏板附加配筋x", 1), // 红色
                ("00_hy_筏板附加配筋y", 1), // 红色
                ("00_hy_筏板附加配筋文字", 7), // 白色
                ("00_hy_筏板附加配筋标注", 7), // 白色
                ("00_hy_调整配筋轮廓", 3), // 绿色                
                ("00_hy_筏板附加配筋x_标注", 3),// 绿色                
                ("00_hy_筏板附加配筋Y_标注", 3) // 绿色                
            );
            EtGpt.CreateOrUpdateTextStyle($"Reinforcement_Text_{Scale}", "hztxt.shx", "tssdeng.shx", Scale);
            EtGpt.CreateDimStyle($"Reinforcement_Dim_{Scale}", $"Reinforcement_Text_{Scale}", Scale);
            EtGpt.CreateOrUpdateMLeaderStyle($"Reinforcement_Mle_{Scale}", $"Reinforcement_Text_{Scale}", Scale);
            EtGpt.SetCurrentLayer("00_hy_筏板附加配筋x_标注");
            AnnotatePolylineIntersections(dic, 100,6);
        }
        #region 放大配筋面积
        public static void DeletePolylinesInCad(Dictionary<Polyline, Dictionary<IntersectionType, Point3d>> dic)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var kvp in dic)
                    {
                        //Polyline polyline = kvp.Key;
                        //polyline.UpgradeOpen();
                        //polyline.Erase();
                        var polyline = tr.GetObject(kvp.Key.ObjectId, OpenMode.ForWrite) as Polyline;
                        if (polyline != null)
                        {
                            polyline.Erase();
                        }
                    }
                    tr.Commit();
                }
            }
        }
        public static Dictionary<Polyline, Dictionary<IntersectionType, Point3d>> AdjustPolylines(this
        Dictionary<Polyline, Dictionary<IntersectionType, Point3d>> originalDic, double interval)
        {
            var adjustedDic = new Dictionary<Polyline, Dictionary<IntersectionType, Point3d>>();
            foreach (var kvp in originalDic)
            {
                var polyline = kvp.Key;
                var points = kvp.Value;
                var newPoints = new Dictionary<IntersectionType, Point3d>();
                var newPolyLine = new Polyline();
                var adjustedPoints = new Dictionary<IntersectionType, Point3d>(points);
                // 获取第二条边上的点和两个端点
                if (points.TryGetValue(IntersectionType.InsideLowerIntersection, out var point))
                {
                    var startPoint = points[IntersectionType.StartPoint];
                    var endPoint = points[IntersectionType.EndPoint];
                    // 计算调整后的点
                    newPoints = AdjustPoint(polyline, points, point, interval);
                    newPolyLine = AdjustPoly(polyline, point, interval);
                }
                adjustedDic[newPolyLine] = newPoints;
            }
            EtGpt.SetCurrentLayer("00_hy_调整配筋轮廓");
            adjustedDic.Keys.ToSpace();
            return adjustedDic;
        }
        private static Polyline AdjustPoly(Polyline polyline, Point3d point, double interval)
        {
            // 获取Polyline的点
            Point3d p0 = polyline.GetPoint3dAt(0);
            Point3d p1 = polyline.GetPoint3dAt(1);
            Point3d p2 = polyline.GetPoint3dAt(2);
            Point3d p3 = polyline.GetPoint3dAt(3);
            // 计算上下偏移量
            var dDown = point.Y - p1.Y;
            var dUp = p2.Y - point.Y;
            dDown = Math.Ceiling(dDown / interval) * interval;
            dUp = Math.Ceiling(dUp / interval) * interval;
            // 调整点的位置
            p0 = new Point3d(p0.X, point.Y - dDown, p0.Z);
            p1 = new Point3d(p1.X, point.Y - dDown, p1.Z);
            p2 = new Point3d(p2.X, point.Y + dUp, p2.Z);
            p3 = new Point3d(p3.X, point.Y + dUp, p3.Z);
            // 创建新的Polyline
            Polyline newPolyline = new Polyline();
            newPolyline.AddVertexAt(0, new Point2d(p0.X, p0.Y), 0, 0, 0);
            newPolyline.AddVertexAt(1, new Point2d(p1.X, p1.Y), 0, 0, 0);
            newPolyline.AddVertexAt(2, new Point2d(p2.X, p2.Y), 0, 0, 0);
            newPolyline.AddVertexAt(3, new Point2d(p3.X, p3.Y), 0, 0, 0);
            newPolyline.Closed = true;
            return newPolyline;
        }
        private static Dictionary<IntersectionType, Point3d> AdjustPoint(Polyline polyline, Dictionary<IntersectionType, Point3d> points,  Point3d point, double interval)
        {
            // 获取Polyline的点
            Point3d p0 = polyline.GetPoint3dAt(0);
            Point3d p1 = polyline.GetPoint3dAt(1);
            Point3d p2 = polyline.GetPoint3dAt(2);
            Point3d p3 = polyline.GetPoint3dAt(3);
            // 计算上下偏移量
            var dDown = point.Y - p1.Y;
            var dUp = p2.Y - point.Y;
            dDown = Math.Ceiling(dDown / interval) * interval;
            dUp = Math.Ceiling(dUp / interval) * interval;
            points[IntersectionType.StartPoint]=new Point3d(p1.X, point.Y - dDown, p1.Z);
            points[IntersectionType.EndPoint]= new Point3d(p2.X, point.Y + dUp, p2.Z); ;
            return points;
        } 
        #endregion
        public static void ClassifyLineAndPolyline(ObjectId[] ids, out List<Line> lines, out List<Polyline> polylines)
        {
            lines = new List<Line>();
            polylines = new List<Polyline>();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in ids)
                {
                    Entity entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (entity != null)
                    {
                        if (entity is Line)
                        {
                            lines.Add(entity as Line);
                        }
                        else if (entity is Polyline)
                        {
                            polylines.Add(entity as Polyline);
                        }
                    }
                }
                tr.Commit();
            }
        }
        public static void AnnotatePolylineIntersections(Dictionary<Polyline, Dictionary<IntersectionType, Point3d>> intersections, double scale, double dimDistance)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    // 创建或设置当前尺寸样式
                    string dimStyleName = $"Reinforcement_Dim_{scale}";
                    EtGpt.CreateDimStyle(dimStyleName, $"Reinforcement_Text_{scale}", scale);
                    EtGpt.SetCurrentLayer("00_hy_筏板附加配筋x_标注");
                    DimStyleTable dst = tr.GetObject(db.DimStyleTableId, OpenMode.ForRead) as DimStyleTable;
                    DimStyleTableRecord dimStyle = tr.GetObject(dst[dimStyleName], OpenMode.ForRead) as DimStyleTableRecord;
                    foreach (var kvp in intersections)
                    {
                        Polyline polyline = kvp.Key;
                        Dictionary<IntersectionType, Point3d> points = kvp.Value;
                        // 删除延长线交点
                        points.Remove(IntersectionType.PositiveExtensionIntersection);
                        points.Remove(IntersectionType.NegativeExtensionIntersection);
                        // 获取交点列表并去除空点和重复点
                        List<Point3d> intersectionPoints = points.Values.Distinct().Where(pt => pt != null).ToList();
                        // 按 y 坐标排序
                        intersectionPoints.Sort((p1, p2) => p1.Y.CompareTo(p2.Y));
                        // 标注点之间的距离
                        for (int i = 0; i < intersectionPoints.Count - 1; i++)
                        {
                            Point3d startPt = intersectionPoints[i];
                            Point3d endPt = intersectionPoints[i + 1];
                            // 创建尺寸标注
                            RotatedDimension dim = EtGpt.GetDimByTwoPoints(startPt, endPt, dimDistance * scale, EtGpt.DimensionFor.ForRight, false);
                            dim.DimensionStyle = dimStyle.ObjectId;
                            btr.AppendEntity(dim);
                            tr.AddNewlyCreatedDBObject(dim, true);
                        }
                    }
                    tr.Commit();
                }
            }
            ed.WriteMessage("\n交点距离标注已完成。\n");
        }
        #region 求交点 (配筋面积的y向，polyline的第二根edge  和轴线的交点)
        public static Dictionary<Polyline, Dictionary<IntersectionType, Point3d>> GetPolylineIntersections(
           List<Line> lines, List<Polyline> polylines, double AxisExtendDistance)
        {
            var intersections = new Dictionary<Polyline, Dictionary<IntersectionType, Point3d>>();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (var polyline in polylines)
                {
                    var points = GetPolylineIntersectionPoints(polyline, lines, AxisExtendDistance);
                    intersections[polyline] = points;
                }
                tr.Commit();
            }
            return intersections;
        }
        // 获取多段线的交点
        private static Dictionary<IntersectionType, Point3d> GetPolylineIntersectionPoints(Polyline polyline, List<Line> lines, double AxisExtendDistance)
        {
            var points = new Dictionary<IntersectionType, Point3d>();
            if (polyline.NumberOfVertices > 1)
            {
                // 获取多段线的第二条边
                LineSegment3d secondEdge = polyline.GetLineSegmentAt(1);
                Line secondEdgeLine = new Line(secondEdge.StartPoint, secondEdge.EndPoint);
                // 将第二条边的起点和终点加入字典
                points[IntersectionType.StartPoint] = secondEdge.StartPoint;
                points[IntersectionType.EndPoint] = secondEdge.EndPoint;
                // 创建Y方向的正负延长线
                Line secondEdgeExtendedPositive = CreateExtendedLine(secondEdge.StartPoint, AxisExtendDistance);
                Line secondEdgeExtendedNegative = CreateExtendedLine(secondEdge.StartPoint, -AxisExtendDistance);
                // 找到交点
                FindIntersections(lines, secondEdgeLine, secondEdgeExtendedPositive, secondEdgeExtendedNegative, points);
            }
            return points;
        }
        // 创建延长线
        private static Line CreateExtendedLine(Point3d startPoint, double extendDistance)
        {
            return new Line(startPoint, startPoint + new Vector3d(0, extendDistance, 0));
        }
        // 找到交点
        private static void FindIntersections(List<Line> lines, Line secondEdgeLine, Line secondEdgeExtendedPositive, Line secondEdgeExtendedNegative, Dictionary<IntersectionType, Point3d> points)
        {
            Point3d? firstPositiveIntersection = null;
            Point3d? firstNegativeIntersection = null;
            List<Point3d> secondEdgeIntersections = new List<Point3d>();
            foreach (var line in lines)
            {
                // 与第二条边求交点
                Point3dCollection intersectionPoints = new Point3dCollection();
                line.IntersectWith(secondEdgeLine, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
                if (intersectionPoints.Count > 0)
                {
                    secondEdgeIntersections.AddRange(intersectionPoints.Cast<Point3d>());
                }
                // 与Y方向正向延长线求交点
                intersectionPoints = new Point3dCollection();
                line.IntersectWith(secondEdgeExtendedPositive, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
                if (intersectionPoints.Count > 0)
                {
                    firstPositiveIntersection = intersectionPoints[0]; // 取第一个交点
                    points[IntersectionType.PositiveExtensionIntersection] = firstPositiveIntersection.Value;
                }
                // 与Y方向负向延长线求交点
                intersectionPoints = new Point3dCollection();
                line.IntersectWith(secondEdgeExtendedNegative, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
                if (intersectionPoints.Count > 0)
                {
                    firstNegativeIntersection = intersectionPoints[0]; // 取第一个交点
                    points[IntersectionType.NegativeExtensionIntersection] = firstNegativeIntersection.Value;
                }
            }
            // 按 Y 方向排序并记录第一个和最后一个交点
            if (secondEdgeIntersections.Count > 0)
            {
                secondEdgeIntersections = secondEdgeIntersections.OrderBy(pt => pt.Y).ToList();
                points[IntersectionType.InsideLowerIntersection] = secondEdgeIntersections.First();
                points[IntersectionType.InsideUpperIntersection] = secondEdgeIntersections.Last();
            }
        }
        // 更新交点
        private static void UpdateIntersectionPoints(Point3dCollection intersectionPoints, ref Point3d? firstIntersection, Point3d startPoint, IntersectionType type, Dictionary<IntersectionType, Point3d> points)
        {
            if (intersectionPoints.Count > 0)
            {
                var intersection = intersectionPoints[0];
                if (firstIntersection == null || intersection.DistanceTo(startPoint) < firstIntersection.Value.DistanceTo(startPoint))
                {
                    firstIntersection = intersection;
                    points[type] = firstIntersection.Value;
                }
            }
        }
        #endregion
    }
}
