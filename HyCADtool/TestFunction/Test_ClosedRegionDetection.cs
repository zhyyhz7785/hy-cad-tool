using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Collections.Generic;
namespace HyCADTool
{
    public static partial class TestFunction
    {
        // CommandMethod 特性表明这是一个 AutoCAD 命令
        [CommandMethod("DetectClosedRegions")]
        public static void DetectClosedRegions()
        {
            // 获取当前活动文档及其编辑器
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 开启一个事务以与绘图数据库交互
            using (Transaction trans = doc.TransactionManager.StartTransaction())
            {
                // 获取块表和模型空间块表记录
                BlockTable bt = trans.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
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
                // 从收集到的线段构建图结构
                Dictionary<Point3d, List<Line>> graph = BuildGraph(lines);
                // 在图结构中识别封闭区域
                List<List<Point3d>> closedRegions = FindClosedRegions(graph);
                // 在编辑器中显示识别出的封闭区域
                DisplayClosedRegions(ed, closedRegions);
                // 提交事务
                trans.Commit();
            }
        }
        // 从线段列表构建图结构
        private static Dictionary<Point3d, List<Line>> BuildGraph(List<Line> lines)
        {
            // 创建一个字典来表示图结构。键是 Point3d（点），值是 List<Line>（从该点出发的线段列表）
            Dictionary<Point3d, List<Line>> graph = new Dictionary<Point3d, List<Line>>();
            // 遍历每条线段，构建起点和终点的连接关系
            foreach (Line line in lines)
            {
                // 获取线段的起点和终点
                Point3d start = line.StartPoint;
                Point3d end = line.EndPoint;
                // 如果图结构中还没有这个起点，则创建一个新的条目
                if (!graph.ContainsKey(start))
                {
                    graph[start] = new List<Line>();
                }
                // 将线段添加到起点对应的线段列表中
                graph[start].Add(line);
                // 如果图结构中还没有这个终点，则创建一个新的条目
                if (!graph.ContainsKey(end))
                {
                    graph[end] = new List<Line>();
                }
                // 将线段添加到终点对应的线段列表中
                graph[end].Add(line);
            }
            // 返回构建好的图结构
            return graph;
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
        // 在编辑器中显示识别出的封闭区域
        private static void DisplayClosedRegions(Editor ed, List<List<Point3d>> closedRegions)
        {
            // 遍历每个封闭区域，并在编辑器中输出其顶点信息
            foreach (var region in closedRegions)
            {
                if (region.Count > 1)
                {
                    for (int i = 0; i < region.Count - 1; i++)
                    {
                        ed.WriteMessage($"封闭区域顶点 {i}: {region[i]}\n");
                    }
                }
            }
        }
    }
}
