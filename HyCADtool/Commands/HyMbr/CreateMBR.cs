using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        private const string TargetLayerName = "00_hy_2公共_视口";
        private const short TargetLayerColor = 1; // 颜色索引 1（红色）
        private const int ParallelThreshold = 100; // 大于100图元时启用并行处理
        [CommandMethod("HYMBR")]
        public static void CreateClusteredMBRs()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            // 获取聚类和放大参数
            (double distanceThreshold, double expandX, double expandY) = GetUserInput(ed);
            if (double.IsNaN(distanceThreshold))
            {
                ed.WriteMessage("\n命令取消：无效的输入参数。");
                return;
            }
            // 提示用户选择图元
            PromptSelectionResult selResult = SelectEntities(ed);
            if (selResult.Status != PromptStatus.OK || selResult.Value == null || selResult.Value.Count == 0)
            {
                ed.WriteMessage("\n未选择任何图元。");
                return;
            }
            // 收集有效图元的边界
            List<(ObjectId Id, Extents3d Bounds)> entityBounds = CollectEntityBounds(db, selResult.Value);
            if (entityBounds.Count == 0)
            {
                ed.WriteMessage("\n没有有效边界的图元。");
                return;
            }
            // 基于边界距离进行聚类
            List<List<(ObjectId Id, Extents3d Bounds)>> clusters = ClusterByBoundsDistance(entityBounds, distanceThreshold, ed);
            // 创建或获取目标图层
            ObjectId layerId = Tools.Et.CreateLayer(TargetLayerName, TargetLayerColor, db, ed);
            // 为每个聚类生成边界框
            GenerateBoundaryBoxes(db, clusters, expandX, expandY, layerId, ed);
        }
        // 获取用户输入的距离阈值和放大距离
        private static (double DistanceThreshold, double ExpandX, double ExpandY) GetUserInput(Editor ed)
        {
            // 距离阈值
            PromptDoubleOptions distOpt = new PromptDoubleOptions("\n请输入聚类距离阈值 [默认=1500]：")
            {
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = 1500.0
            };
            PromptDoubleResult distRes = ed.GetDouble(distOpt);
            if (distRes.Status != PromptStatus.OK)
                return (double.NaN, 0, 0);
            // X 方向放大
            PromptDoubleOptions expandXOpt = new PromptDoubleOptions("\n请输入X方向放大距离 [默认=100]：")
            {
                AllowNegative = false,
                DefaultValue = 100
            };
            PromptDoubleResult expandXRes = ed.GetDouble(expandXOpt);
            if (expandXRes.Status != PromptStatus.OK)
                return (double.NaN, 0, 0);
            // Y 方向放大
            PromptDoubleOptions expandYOpt = new PromptDoubleOptions("\n请输入Y方向放大距离 [默认=100]：")
            {
                AllowNegative = false,
                DefaultValue = 100
            };
            PromptDoubleResult expandYRes = ed.GetDouble(expandYOpt);
            if (expandYRes.Status != PromptStatus.OK)
                return (double.NaN, 0, 0);
            return (distRes.Value, expandXRes.Value, expandYRes.Value);
        }
        // 提示用户选择图元
        private static PromptSelectionResult SelectEntities(Editor ed)
        {
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择图元进行区域聚类与边界框生成："
            };
            return ed.GetSelection(selOpts);
        }
        // 收集有效图元的边界
        private static List<(ObjectId Id, Extents3d Bounds)> CollectEntityBounds(Database db, SelectionSet selection)
        {
            List<(ObjectId Id, Extents3d Bounds)> entityBounds = new List<(ObjectId Id, Extents3d Bounds)>();
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selObj in selection)
                {
                    if (selObj == null) continue;
                    Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.Bounds == null) continue;
                    entityBounds.Add((selObj.ObjectId, ent.Bounds.Value));
                }
                tr.Commit();
            }
            return entityBounds;
        }
        // 基于边界距离的聚类（BFS，支持并行距离计算）
        private static List<List<(ObjectId Id, Extents3d Bounds)>> ClusterByBoundsDistance(
            List<(ObjectId Id, Extents3d Bounds)> entities, double maxDist, Editor ed)
        {
            int n = entities.Count;
            bool[] visited = new bool[n];
            List<List<(ObjectId Id, Extents3d Bounds)>> clusters = new List<List<(ObjectId Id, Extents3d Bounds)>>();
            // 为大数据集预计算距离
            double[,] distances = null;
            if (n > ParallelThreshold)
            {
                distances = new double[n, n];
                Parallel.For(0, n, i =>
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        double dist = CalculateMinBoundsDistance(entities[i].Bounds, entities[j].Bounds);
                        distances[i, j] = dist;
                        distances[j, i] = dist;
                    }
                });
            }
            for (int i = 0; i < n; i++)
            {
                if (visited[i]) continue;
                var cluster = new List<(ObjectId Id, Extents3d Bounds)>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(i);
                visited[i] = true;
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    cluster.Add(entities[current]);
                    for (int j = 0; j < n; j++)
                    {
                        if (visited[j]) continue;
                        double distance = distances != null ? distances[current, j] :
                            CalculateMinBoundsDistance(entities[current].Bounds, entities[j].Bounds);
                        if (distance <= maxDist)
                        {
                            queue.Enqueue(j);
                            visited[j] = true;
                        }
                    }
                }
                if (cluster.Count > 0)
                    clusters.Add(cluster);
            }
            ed.WriteMessage($"\n聚类完成，生成了 {clusters.Count} 个区域。");
            return clusters;
        }
        // 为每个聚类生成边界框
        private static void GenerateBoundaryBoxes(Database db, List<List<(ObjectId Id, Extents3d Bounds)>> clusters,
            double expandX, double expandY, ObjectId layerId, Editor ed)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                foreach (var cluster in clusters)
                {
                    if (cluster.Count == 0) continue;
                    // 合并聚类边界
                    Extents3d mergedBounds = cluster[0].Bounds;
                    for (int i = 1; i < cluster.Count; i++)
                    {
                        mergedBounds = UnionExtents(mergedBounds, cluster[i].Bounds);
                    }
                    // 放大边界框
                    Extents3d expandedBounds = ExpandBounds(mergedBounds, expandX, expandY);
                    // 创建边界框
                    Polyline boundingBox = CreateRectangleFromExtents(expandedBounds);
                    boundingBox.SetLayer(TargetLayerName); // 设置到目标图层
                    btr.AppendEntity(boundingBox);
                    tr.AddNewlyCreatedDBObject(boundingBox, true);
                }
                tr.Commit();
            }
            ed.WriteMessage($"\n成功生成 {clusters.Count} 个区域的最小外接矩形，位于图层 '{TargetLayerName}'。");
        }
        // 基于形心放大边界框
        private static Extents3d ExpandBounds(Extents3d bounds, double expandX, double expandY)
        {
            // 计算形心
            Point3d centroid = new Point3d(
                (bounds.MinPoint.X + bounds.MaxPoint.X) / 2,
                (bounds.MinPoint.Y + bounds.MaxPoint.Y) / 2,
                bounds.MinPoint.Z);
            // 放大边界
            double minX = bounds.MinPoint.X - expandX;
            double maxX = bounds.MaxPoint.X + expandX;
            double minY = bounds.MinPoint.Y - expandY;
            double maxY = bounds.MaxPoint.Y + expandY;
            return new Extents3d(
                new Point3d(minX, minY, bounds.MinPoint.Z),
                new Point3d(maxX, maxY, bounds.MaxPoint.Z));
        }
        // 计算两个边界框的最近距离（2D XY 平面）
        private static double CalculateMinBoundsDistance(Extents3d bounds1, Extents3d bounds2)
        {
            Point2d min1 = new Point2d(bounds1.MinPoint.X, bounds1.MinPoint.Y);
            Point2d max1 = new Point2d(bounds1.MaxPoint.X, bounds1.MaxPoint.Y);
            Point2d min2 = new Point2d(bounds2.MinPoint.X, bounds2.MinPoint.Y);
            Point2d max2 = new Point2d(bounds2.MaxPoint.X, bounds2.MaxPoint.Y);
            double dx = Math.Max(0, Math.Max(min1.X - max2.X, min2.X - max1.X));
            double dy = Math.Max(0, Math.Max(min1.Y - max2.Y, min2.Y - max1.Y));
            return Math.Sqrt(dx * dx + dy * dy);
        }
        // 合并两个边界框
        private static Extents3d UnionExtents(Extents3d ext1, Extents3d ext2)
        {
            return new Extents3d(
                new Point3d(
                    Math.Min(ext1.MinPoint.X, ext2.MinPoint.X),
                    Math.Min(ext1.MinPoint.Y, ext2.MinPoint.Y),
                    Math.Min(ext1.MinPoint.Z, ext2.MinPoint.Z)),
                new Point3d(
                    Math.Max(ext1.MaxPoint.X, ext2.MaxPoint.X),
                    Math.Max(ext1.MaxPoint.Y, ext2.MaxPoint.Y),
                    Math.Max(ext1.MaxPoint.Z, ext2.MaxPoint.Z)));
        }
        // 从 Extents3d 创建矩形 Polyline（XY 平面）
        private static Polyline CreateRectangleFromExtents(Extents3d ext)
        {
            Point2d min = new Point2d(ext.MinPoint.X, ext.MinPoint.Y);
            Point2d max = new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y);
            Polyline pline = new Polyline(4);
            pline.AddVertexAt(0, new Point2d(min.X, min.Y), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(max.X, min.Y), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(max.X, max.Y), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(min.X, max.Y), 0, 0, 0);
            pline.Closed = true;
            return pline;
        }
    }
}