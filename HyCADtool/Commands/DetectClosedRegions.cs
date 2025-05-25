using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using static HyCADTool.Tools.Tools;
[assembly: CommandClass(typeof(HyCADTool.Commands.HyCommand))]
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("DetectClosedRegions")]
        public static void DetectClosedRegions()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (Transaction trans = doc.TransactionManager.StartTransaction())
            {
                // 获取块表和模型空间块表记录
                BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                List<Line> lines = new List<Line>();
                // 遍历模型空间中的所有实体，并收集线段
                foreach (ObjectId objId in btr)
                {
                    Entity entity = trans.GetObject(objId, OpenMode.ForRead) as Entity;
                    if (entity is Line)
                    {
                        lines.Add(entity as Line);
                    }
                }
                // 构建图结构，考虑所有线段的起点、终点和交点
                Dictionary<Point3d, List<Line>> graph = BuildGraph(lines);
                // 在图结构中识别封闭区域
                //List<List<Point3d>> closedRegions = FindClosedRegions(graph);
                // 在编辑器中显示识别出的封闭区域
                // DisplayClosedRegions(db, closedRegions);
                // 提交事务
                trans.Commit();
            }
        }
        // 创建图层的方法
        private static void CreateLayer(Database db, string layerName, short colorIndex)
        {
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                LayerTable layerTable = trans.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
                if (!layerTable.Has(layerName))
                {
                    LayerTableRecord layer = new LayerTableRecord();
                    layer.Name = layerName;
                    layer.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
                    layerTable.UpgradeOpen();
                    layerTable.Add(layer);
                    trans.AddNewlyCreatedDBObject(layer, true);
                }
                trans.Commit();
            }
        }
        // 获取所有线段的交点
        private static List<Point3d> GetAllIntersectionPoints(List<Line> lines)
        {
            List<Point3d> intersections = new List<Point3d>();
            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {
                    Point3d? intersection = GetIntersectionPoint(lines[i], lines[j]);
                    if (intersection.HasValue && !intersections.Contains(intersection.Value))
                    {
                        intersections.Add(intersection.Value);
                    }
                }
            }
            return intersections;
        }
        // 从线段列表构建图结构
        private static Dictionary<Point3d, List<Line>> BuildGraph(List<Line> lines)
        {
            Dictionary<Point3d, List<Line>> graph = new Dictionary<Point3d, List<Line>>();
            List<Point3d> allPoints = new List<Point3d>();
            // 遍历每条线段的起点和终点，加入到所有点的列表中
            foreach (Line line in lines)
            {
                allPoints.Add(line.StartPoint);
                allPoints.Add(line.EndPoint);
            }
            // 获取所有线段之间的交点
            List<Point3d> intersections = GetAllIntersectionPoints(lines);
            allPoints.AddRange(intersections);
            allPoints.ToSpace();
            // 从所有点构建图结构
            foreach (Line line in lines)
            {
                Point3d start = line.StartPoint;
                Point3d end = line.EndPoint;
                // 获取线段上所有交点并排序
                List<Point3d> pointsOnLine = new List<Point3d> { start, end };
                foreach (Point3d pt in allPoints)
                {
                    if (pt.IsEqualTo(start) || pt.IsEqualTo(end))
                        continue;
                    if (IsPointOnLine(pt, line))
                    {
                        pointsOnLine.Add(pt);
                    }
                }
                pointsOnLine.Sort((a, b) => a.DistanceTo(start).CompareTo(b.DistanceTo(start)));
                // 构建图的连接关系
                for (int k = 0; k < pointsOnLine.Count - 1; k++)
                {
                    Point3d pt1 = pointsOnLine[k];
                    Point3d pt2 = pointsOnLine[k + 1];
                    if (!graph.ContainsKey(pt1))
                    {
                        graph[pt1] = new List<Line>();
                    }
                    if (!graph.ContainsKey(pt2))
                    {
                        graph[pt2] = new List<Line>();
                    }
                    Line segment = new Line(pt1, pt2);
                    graph[pt1].Add(segment);
                    graph[pt2].Add(segment);
                }
            }
            return graph;
        }
        // 深度优先搜索算法查找封闭区域
        private static bool DFS(Point3d current, Point3d start, Dictionary<Point3d, List<Line>> graph, HashSet<Point3d> visited, List<Point3d> region)
        {
            // 将当前点标记为已访问，并添加到区域列表中
            visited.Add(current);
            region.Add(current);
            // 遍历当前节点的每条连接线段，递归查找封闭路径
            foreach (Line line in graph[current])
            {
                // 确定下一个节点，如果当前点是线段的起点，则下一个节点是终点，反之亦然
                Point3d next = line.StartPoint == current ? line.EndPoint : line.StartPoint;
                // 如果下一个节点未被访问，继续递归搜索
                if (!visited.Contains(next))
                {
                    if (DFS(next, start, graph, visited, region))
                    {
                        return true;
                    }
                }
                // 如果下一个节点是起始节点并且当前区域包含超过两个点，则找到封闭区域
                else if (next == start && region.Count > 2)
                {
                    region.Add(start);
                    return true;
                }
            }
            // 如果没有找到封闭区域，将当前点从已访问集合和区域列表中移除
            visited.Remove(current);
            region.Remove(current);
            return false;
        }
        // 在图结构中查找封闭区域
        private static List<List<Point3d>> FindClosedRegions(Dictionary<Point3d, List<Line>> graph)
        {
            List<List<Point3d>> closedRegions = new List<List<Point3d>>();
            HashSet<Point3d> visited = new HashSet<Point3d>();
            // 遍历图中的每个节点，使用深度优先搜索查找封闭区域
            foreach (Point3d node in graph.Keys)
            {
                if (!visited.Contains(node))
                {
                    List<Point3d> region = new List<Point3d>();
                    if (DFS(node, node, graph, visited, region))
                    {
                        closedRegions.Add(region);
                    }
                }
            }
            return closedRegions;
        }
        // 在编辑器中显示识别出的封闭区域
        private static void DisplayClosedRegions(Database db, List<List<Point3d>> closedRegions)
        {
            const string layerName = "ClosedRegions";
            const short colorIndex = 1; // 1表示红色，可以根据需要修改
            // 创建新的图层
            CreateLayer(db, layerName, colorIndex);
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                foreach (var region in closedRegions)
                {
                    if (region.Count > 1)
                    {
                        Polyline polyline = new Polyline();
                        polyline.SetDatabaseDefaults();
                        polyline.Layer = layerName;
                        for (int i = 0; i < region.Count; i++)
                        {
                            polyline.AddVertexAt(i, new Point2d(region[i].X, region[i].Y), 0, 0, 0);
                        }
                        polyline.Closed = true;
                        btr.AppendEntity(polyline);
                        trans.AddNewlyCreatedDBObject(polyline, true);
                    }
                }
                trans.Commit();
            }
        }
        // 计算两条线段的交点
        private static Point3d? GetIntersectionPoint(Line line1, Line line2)
        {
            Point3dCollection points = new Point3dCollection();
            line1.IntersectWith(line2, Intersect.OnBothOperands, points, IntPtr.Zero, IntPtr.Zero);
            if (points.Count > 0)
            {
                return points[0];
            }
            return null;
        }
        // 检查点是否在线段上
        private static bool IsPointOnLine(Point3d pt, Line line)
        {
            LineSegment3d segment = new LineSegment3d(line.StartPoint, line.EndPoint);
            return segment.IsOn(pt);
        }
    }
}
