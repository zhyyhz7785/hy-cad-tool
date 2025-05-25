using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Tools
{
    public static partial class Tools
    {
        public static void RemoveDuplicateEdgesAndCreatePolylines()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            // 提示用户选择多边形
            PromptSelectionResult selRes = ed.GetSelection();
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("选择失败或用户取消了操作。\n");
                return;
            }
            SelectionSet selSet = selRes.Value;
            List<LineSegment3d> edges = new List<LineSegment3d>();
            // 遍历选定的多边形，获取所有边
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selObj in selSet)
                {
                    if (selObj != null)
                    {
                        Polyline pline = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
                        if (pline != null)
                        {
                            int numVertices = pline.NumberOfVertices;
                            for (int i = 0; i < numVertices; i++)
                            {
                                Point3d startPoint = pline.GetPoint3dAt(i);
                                Point3d endPoint = pline.GetPoint3dAt((i + 1) % numVertices);
                                edges.Add(new LineSegment3d(startPoint, endPoint));
                            }
                        }
                    }
                }
                // 使用哈希集合来检测和删除重复的边
                HashSet<Edge> edgeSet = new HashSet<Edge>();
                foreach (var edge in edges)
                {
                    Edge e = new Edge(edge);
                    if (edgeSet.Contains(e))
                    {
                        edgeSet.Remove(e); // 删除重复的边
                    }
                    else
                    {
                        edgeSet.Add(e);
                    }
                }
                // 剩余的边
                List<LineSegment3d> remainingEdges = edgeSet.Select(e => e.LineSegment).ToList();
                // 创建封闭的 Polyline
                List<Polyline> polylines = CreateClosedPolylines(remainingEdges);
                // 将 Polyline 添加到图形数据库
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                foreach (var polyline in polylines)
                {
                    btr.AppendEntity(polyline);
                    tr.AddNewlyCreatedDBObject(polyline, true);
                }
                // 提交事务
                tr.Commit();
            }
            ed.WriteMessage("已删除重复的边并创建封闭的 Polyline。\n");
        }
        private static List<Polyline> CreateClosedPolylines(List<LineSegment3d> edges)
        {
            List<Polyline> polylines = new List<Polyline>();
            Dictionary<Point3d, List<Point3d>> adjacencyList = new Dictionary<Point3d, List<Point3d>>();
            // 创建邻接表
            foreach (var edge in edges)
            {
                if (!adjacencyList.ContainsKey(edge.StartPoint))
                {
                    adjacencyList[edge.StartPoint] = new List<Point3d>();
                }
                adjacencyList[edge.StartPoint].Add(edge.EndPoint);
                if (!adjacencyList.ContainsKey(edge.EndPoint))
                {
                    adjacencyList[edge.EndPoint] = new List<Point3d>();
                }
                adjacencyList[edge.EndPoint].Add(edge.StartPoint);
            }
            // DFS 创建封闭的 Polyline
            HashSet<Point3d> visited = new HashSet<Point3d>();
            foreach (var vertex in adjacencyList.Keys)
            {
                if (!visited.Contains(vertex))
                {
                    List<Point3d> path = new List<Point3d>();
                    Stack<Point3d> stack = new Stack<Point3d>();
                    stack.Push(vertex);
                    while (stack.Count > 0)
                    {
                        Point3d current = stack.Pop();
                        if (!visited.Contains(current))
                        {
                            visited.Add(current);
                            path.Add(current);
                            foreach (var neighbor in adjacencyList[current])
                            {
                                if (!visited.Contains(neighbor))
                                {
                                    stack.Push(neighbor);
                                }
                            }
                        }
                    }
                    if (path.Count > 0)
                    {
                        Polyline polyline = new Polyline();
                        for (int i = 0; i < path.Count; i++)
                        {
                            polyline.AddVertexAt(i, new Point2d(path[i].X, path[i].Y), 0, 0, 0);
                        }
                        polyline.Closed = true;
                        polylines.Add(polyline);
                    }
                }
            }
            return polylines;
        }
        private class Edge
        {
            public LineSegment3d LineSegment { get; }
            public Point3d StartPoint => LineSegment.StartPoint;
            public Point3d EndPoint => LineSegment.EndPoint;
            public Edge(LineSegment3d lineSegment)
            {
                LineSegment = lineSegment;
            }
            public override bool Equals(object obj)
            {
                if (obj is Edge edge)
                {
                    return (StartPoint.IsEqualTo(edge.StartPoint) && EndPoint.IsEqualTo(edge.EndPoint)) ||
                           (StartPoint.IsEqualTo(edge.EndPoint) && EndPoint.IsEqualTo(edge.StartPoint));
                }
                return false;
            }
            public override int GetHashCode()
            {
                return StartPoint.GetHashCode() ^ EndPoint.GetHashCode();
            }
        }
    }
}
