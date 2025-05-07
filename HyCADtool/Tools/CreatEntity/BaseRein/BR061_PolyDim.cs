using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
[assembly: CommandClass(typeof(HyCADTool.BaseRein))]
namespace HyCADTool
{
    public static partial class BaseRein
    {
        // 求交点1 (配筋区域的y向，polyline的第二根edge  和x向轴线的交点)   
        public static Dictionary<Polyline, Dictionary<IntersectionType, Point3d>> GetPolylineIntersectionsLeftRight(
        List<Line> lines, List<Polyline> polylines, IntersectionsDirection direction, double AxisExtendDistance)
        {
            var intersections = new Dictionary<Polyline, Dictionary<IntersectionType, Point3d>>();
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (var polyline in polylines)
                {
                    Dictionary<IntersectionType, Point3d> points;
                    if (direction == IntersectionsDirection.LeftRight)
                    {
                        points = GetPolylineIntersectionPointsLeftRight(polyline, lines, AxisExtendDistance);
                    }
                    else
                    {
                        points = GetPolylineIntersectionPointsUpDown(polyline, lines, AxisExtendDistance);
                    }
                    intersections[polyline] = points;
                }
                tr.Commit();
            }
            return intersections;
        }
        // 求交点2  求x向轴线，和右侧y向Polyline交点 集合
        private static Dictionary<IntersectionType, Point3d> GetPolylineIntersectionPointsLeftRight(
            Polyline polyline, List<Line> lines, double AxisExtendDistance)
        {
            var points = new Dictionary<IntersectionType, Point3d>();
            if (polyline.NumberOfVertices > 1)
            {
                LineSegment3d targetEdge = polyline.GetLineSegmentAt(1); // 选择第二条边（端点为右下1，右上2）
                Line targetEdgeLine = new Line(targetEdge.StartPoint, targetEdge.EndPoint);
                // 将选择的边的起点和终点加入字典
                points[IntersectionType.StartPoint] = targetEdge.StartPoint;
                points[IntersectionType.EndPoint] = targetEdge.EndPoint;
                // 创建延长线
                Line targetEdgeExtendedPositive = CreateExtendedLine(targetEdge.EndPoint, AxisExtendDistance, new Vector3d(0, 1, 0));
                Line targetEdgeExtendedNegative = CreateExtendedLine(targetEdge.StartPoint, AxisExtendDistance, new Vector3d(0, -1, 0));
                // 找到交点
                FindIntersectionsLeftRight(lines, targetEdgeLine, targetEdgeExtendedPositive, targetEdgeExtendedNegative, points);
            }
            return points;
        }
        private static void FindIntersectionsLeftRight(
            List<Line> lines, Line targetEdgeLine, Line targetEdgeExtendedPositive, Line targetEdgeExtendedNegative, Dictionary<IntersectionType, Point3d> points)
        {
            List<Point3d> targetEdgeIntersections = new List<Point3d>();
            foreach (var line in lines)
            {
                // 与选择的边求交点
                Point3dCollection intersectionPoints = new Point3dCollection();
                line.IntersectWith(targetEdgeLine, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
                if (intersectionPoints.Count > 0)
                {
                    targetEdgeIntersections.AddRange(intersectionPoints.Cast<Point3d>());
                }
                // 与正延长线求交点
                intersectionPoints = new Point3dCollection();
                line.IntersectWith(targetEdgeExtendedPositive, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
                if (intersectionPoints.Count > 0)
                {
                    var positiveIntersection = intersectionPoints.Cast<Point3d>().OrderBy(pt => pt.Y).First(); // 取 y 坐标最小的点
                    if (!points.TryGetValue(IntersectionType.PositiveExtensionIntersection, out var currentPositiveIntersection) || positiveIntersection.Y < currentPositiveIntersection.Y)
                    {
                        points[IntersectionType.PositiveExtensionIntersection] = positiveIntersection;
                    }
                }
                // 与负延长线求交点
                intersectionPoints = new Point3dCollection();
                line.IntersectWith(targetEdgeExtendedNegative, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
                if (intersectionPoints.Count > 0)
                {
                    var negativeIntersection = intersectionPoints.Cast<Point3d>().OrderByDescending(pt => pt.Y).First(); // 取 y 坐标最大的点
                    if (!points.TryGetValue(IntersectionType.NegativeExtensionIntersection, out var currentNegativeIntersection) || negativeIntersection.Y > currentNegativeIntersection.Y)
                    {
                        points[IntersectionType.NegativeExtensionIntersection] = negativeIntersection;
                    }
                }
            }
            // 按 x 坐标排序并记录第一个和最后一个交点
            if (targetEdgeIntersections.Count > 0)
            {
                targetEdgeIntersections = targetEdgeIntersections.OrderBy(pt => pt.Y).ToList();
                points[IntersectionType.InsideLowerIntersection] = targetEdgeIntersections.First();
                points[IntersectionType.InsideUpperIntersection] = targetEdgeIntersections.Last();
            }
        }
        // 调整尺寸1   总方法
        public static Dictionary<Polyline, Dictionary<IntersectionType, Point3d>> AdjustPolylinesLeftRight(
                  this Dictionary<Polyline, Dictionary<IntersectionType, Point3d>> originalDic, double interval)
        {
            // 创建一个新的字典，用于存储调整后的多段线及其相应的点
            var adjustedDic = new Dictionary<Polyline, Dictionary<IntersectionType, Point3d>>();
            // 遍历原始字典中的每一对多段线和点集
            foreach (var kvp in originalDic)
            {
                var polyline = kvp.Key;
                var points = kvp.Value;
                var newPoints = new Dictionary<IntersectionType, Point3d>();
                var newPolyLine = new Polyline();
                bool hasLowerIntersection = points.TryGetValue(IntersectionType.InsideLowerIntersection, out var lowerIntersectionPoint);
                bool hasUpperIntersection = points.TryGetValue(IntersectionType.InsideUpperIntersection, out var upperIntersectionPoint);
                bool hasNegativeExtension = points.TryGetValue(IntersectionType.NegativeExtensionIntersection, out var negativeExtensionPoint);
                bool hasStartPoint = points.TryGetValue(IntersectionType.StartPoint, out var startPoint);
                bool hasEndPoint = points.TryGetValue(IntersectionType.EndPoint, out var endPoint);
                bool hasPositiveExtension = points.TryGetValue(IntersectionType.PositiveExtensionIntersection, out var positiveExtensionPoint);
                if (hasLowerIntersection || hasUpperIntersection)
                {
                    // 如果其中一个交点有值，按原来的逻辑处理
                    startPoint = points[IntersectionType.StartPoint];
                    endPoint = points[IntersectionType.EndPoint];
                    if (hasLowerIntersection)
                    {
                        // 计算调整后的点
                        newPoints = AdjustPointLeftRight(polyline, points, lowerIntersectionPoint, interval);
                        newPolyLine = AdjustPolyLeftRight(polyline, lowerIntersectionPoint, interval);
                        newPolyLine.LayerId = ReinforcementPolylineLayerId;
                    }
                    else if (hasUpperIntersection)
                    {
                        // 计算调整后的点
                        newPoints = AdjustPointLeftRight(polyline, points, upperIntersectionPoint, interval);
                        newPolyLine = AdjustPolyLeftRight(polyline, upperIntersectionPoint, interval);
                        newPolyLine.LayerId = ReinforcementPolylineLayerId;
                    }
                }
                else
                {
                    // 如果都没有值，使用新的方法处理(直线内没有交点，就调整轮廓外部和轴线的关系)
                    newPoints = AdjustPointNoIntersectionLeftRight(polyline, points, interval, negativeExtensionPoint, startPoint, endPoint, positiveExtensionPoint);
                    newPolyLine = AdjustPolyNoIntersection(polyline, interval, negativeExtensionPoint, startPoint, endPoint, positiveExtensionPoint);
                    newPolyLine.LayerId = ReinforcementPolylineLayerId;
                }
                adjustedDic[newPolyLine] = newPoints;
            }
            // 设置当前图层为 "00_hy_调整配筋轮廓"
            //EtGpt.SetCurrentLayer("00_hy_调整配筋轮廓");
            // 将调整后的多段线放置到模型空间中
            adjustedDic.Keys.ToSpace();
            // 返回调整后的字典
            return adjustedDic;
        }
        // 调整尺寸2   调整配筋区域
        private static Polyline AdjustPolyLeftRight(Polyline polyline, Point3d point, double interval)
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
        // 调整尺寸3   调整交点，准备标注
        private static Dictionary<IntersectionType, Point3d> AdjustPointLeftRight
            (Polyline polyline, Dictionary<IntersectionType, Point3d> points, Point3d point, double interval)
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
            points[IntersectionType.StartPoint] = new Point3d(p1.X, point.Y - dDown, p1.Z);
            points[IntersectionType.EndPoint] = new Point3d(p2.X, point.Y + dUp, p2.Z); ;
            return points;
        }
        // 调整尺寸4   无交点情况1 用于调整多段线的形状
        private static Polyline AdjustPolyNoIntersection(
            Polyline polyline, double interval, Point3d a, Point3d b, Point3d c, Point3d d)
        {
            // 获取Polyline的点
            Point3d p0 = polyline.GetPoint3dAt(0);
            Point3d p1 = polyline.GetPoint3dAt(1);
            Point3d p2 = polyline.GetPoint3dAt(2);
            Point3d p3 = polyline.GetPoint3dAt(3);
            // 计算上下偏移量
            // 计算上下偏移量
            var dDown = b.Y - a.Y;
            var dUp = d.Y - c.Y;
            dDown = Math.Floor(dDown / interval) * interval;
            dUp = Math.Floor(dUp / interval) * interval;
            // 调整点的位置
            p0 = new Point3d(p0.X, a.Y + dDown, p0.Z);
            p1 = new Point3d(p1.X, a.Y + dDown, p1.Z);
            p2 = new Point3d(p2.X, d.Y - dUp, p2.Z);
            p3 = new Point3d(p3.X, d.Y - dUp, p3.Z);
            // 创建新的Polyline
            Polyline newPolyline = new Polyline();
            newPolyline.AddVertexAt(0, new Point2d(p0.X, p0.Y), 0, 0, 0);
            newPolyline.AddVertexAt(1, new Point2d(p1.X, p1.Y), 0, 0, 0);
            newPolyline.AddVertexAt(2, new Point2d(p2.X, p2.Y), 0, 0, 0);
            newPolyline.AddVertexAt(3, new Point2d(p3.X, p3.Y), 0, 0, 0);
            newPolyline.Closed = true;
            return newPolyline;
        }
        // 调整尺寸5   无交点情况2 用于调整点的位置
        private static Dictionary<IntersectionType, Point3d> AdjustPointNoIntersectionLeftRight(
       Polyline polyline, Dictionary<IntersectionType, Point3d> points, double interval, Point3d a, Point3d b, Point3d c, Point3d d)
        {
            // 获取Polyline的点
            Point3d p0 = polyline.GetPoint3dAt(0);
            Point3d p1 = polyline.GetPoint3dAt(1);
            Point3d p2 = polyline.GetPoint3dAt(2);
            Point3d p3 = polyline.GetPoint3dAt(3);
            // 计算上下偏移量
            var dDown = b.Y - a.Y;
            var dUp = d.Y - c.Y;
            dDown = Math.Floor(dDown / interval) * interval;
            dUp = Math.Floor(dUp / interval) * interval;
            points[IntersectionType.StartPoint] = new Point3d(b.X, a.Y + dDown, 0);
            points[IntersectionType.EndPoint] = new Point3d(c.X, d.Y - dUp, 0);
            return points;
        }
    }
}
