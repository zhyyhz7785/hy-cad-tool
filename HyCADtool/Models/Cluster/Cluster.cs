using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Models.Cluster;
using HyCADTool.Tools;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.HelpClass
{
    /// <summary>
    /// 轮廓聚类处理类
    /// </summary>
    public class EnvelopeCluster
    {
        /// <summary>
        /// 获取聚类结果（供标注等模块调用）
        /// </summary>
        public List<ClusterResult> GetClusterResults()
        {
            return ClusterResults ?? new List<ClusterResult>();
        }
        public List<ClusterResult> ClusterResults { get; set; }
        /// <summary>
        /// 聚类轮廓命令入口
        /// </summary>
        [CommandMethod("CLUSTER_ENVELOPE")]
        public void RunClusterEnvelopeCommand()
        {
            // 获取当前文档和编辑器
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            Transaction tr = doc.TransactionManager.StartTransaction();
            try
            {
                // 步骤1: 获取点集合
                var inputPoints = GetPointCollection();
                if (inputPoints.Count == 0)
                {
                    ed.WriteMessage("\n没有选择点或未找到有效点，操作取消。");
                    return;
                }
                // 步骤2: 创建配置并设置图层
                var config = CreateClusterConfig(doc, ed);
                // 步骤3: 执行聚类并生成轮廓
                int clusterCount = ProcessClustersAndGenerateEnvelopes(inputPoints, config, db);
                // 步骤4: 输出结果信息
                ed.WriteMessage($"\n共生成 {clusterCount} 个聚类区域。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n执行聚类轮廓命令时出错: {ex.Message}");
            }
        }
        /// <summary>
        /// 创建聚类配置
        /// </summary>
        private ClusterConfig CreateClusterConfig(Document doc, Editor ed)
        {
            string clusterLayerName = "00_hy_基础_聚类轮廓";
            ObjectId clusterLayerId = EtGpt.CreateLayer(clusterLayerName, 123, doc.Database, ed);
            return new ClusterConfig
            {
                //EpsilonX = 6000.0,
                //EpsilonY = 400.0,
                EpsilonX = 200,
                EpsilonY = 6000,
                MinPoints = 2,
                ClusterLayerName = clusterLayerName,
                ClusterLayerId = clusterLayerId
            };
        }
        /// <summary>
        /// 处理聚类并生成轮廓多段线
        /// </summary>
        public int ProcessClustersAndGenerateEnvelopes(List<Point3d> points, ClusterConfig config, Database db)
        {
            int count = 0;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                // 执行DBSCAN聚类
                var clusters = PerformDBSCAN(points, config);
                ClusterResults = clusters;
                // 生成并插入轮廓多段线
                count = InsertEnvelopePolylinesFromClusters(clusters, config, tr, btr);
                tr.Commit();
            }
            return count;
        }
        /// <summary>
        /// 根据聚类结果生成轮廓多段线并添加到图形中
        /// </summary>
        public int InsertEnvelopePolylinesFromClusters(List<ClusterResult> clusters, ClusterConfig config, Transaction tr, BlockTableRecord btr)
        {
            int count = 0;
            var factory = NtsGeometryServices.Instance.CreateGeometryFactory();
            foreach (var cluster in clusters)
            {
                // 跳过空聚类
                if (cluster.Points.Count == 0) continue;
                // 使用NetTopologySuite计算包络矩形
                var coordinates = cluster.Points.Select(p => new Coordinate(p.X, p.Y)).ToArray();
                var geom = factory.CreateMultiPointFromCoords(coordinates);
                var env = geom.EnvelopeInternal;
                // 创建轮廓多段线
                var pline = CreateEnvelopePolyline(env);
                // 设置图层
                if (!config.ClusterLayerId.IsNull)
                    pline.LayerId = config.ClusterLayerId;
                // 将多段线添加到模型空间
                btr.AppendEntity(pline);
                tr.AddNewlyCreatedDBObject(pline, true);
                // 保存轮廓多段线到聚类结果中
                cluster.EnvelopePolyline = pline;
                // 新增：记录 MBR 范围
                cluster.EnvelopeExtents = new Extents3d(
                    new Point3d(env.MinX, env.MinY, 0),
                    new Point3d(env.MaxX, env.MaxY, 0));
                count++;
            }
            return count;
        }
        /// <summary>
        /// 从包络盒创建多段线
        /// </summary>
        private Polyline CreateEnvelopePolyline(Envelope envelope)
        {
            var pline = new Polyline();
            pline.AddVertexAt(0, new Point2d(envelope.MinX, envelope.MinY), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(envelope.MaxX, envelope.MinY), 0, 0, 0);
            pline.AddVertexAt(2, new Point2d(envelope.MaxX, envelope.MaxY), 0, 0, 0);
            pline.AddVertexAt(3, new Point2d(envelope.MinX, envelope.MaxY), 0, 0, 0);
            pline.Closed = true;
            return pline;
        }
        /// <summary>
        /// 执行DBSCAN聚类算法
        /// </summary>
        public List<ClusterResult> PerformDBSCAN(List<Point3d> points, ClusterConfig config)
        {
            int clusterId = 0;
            var visited = new HashSet<Point3d>();
            var pointClusterMap = new Dictionary<Point3d, int>();
            var results = new List<ClusterResult>();
            // 遍历所有点执行聚类
            foreach (var pt in points)
            {
                // 已访问的点跳过
                if (visited.Contains(pt)) continue;
                visited.Add(pt);
                // 获取邻居点
                var neighbors = GetNeighbors(pt, points, config.EpsilonX, config.EpsilonY);
                // 如果邻居点数量小于最小值，标记为噪声点
                if (neighbors.Count < config.MinPoints)
                {
                    pointClusterMap[pt] = -1; // 标记为噪声点
                    continue;
                }
                // 创建新聚类并扩展
                var cluster = new ClusterResult { ClusterId = clusterId };
                ExpandCluster(pt, neighbors, cluster, points, visited, pointClusterMap, config);
                results.Add(cluster);
                clusterId++;
            }
            return results;
        }
        /// <summary>
        /// 扩展聚类
        /// </summary>
        private void ExpandCluster(Point3d pt, List<Point3d> neighbors, ClusterResult cluster,
            List<Point3d> allPoints, HashSet<Point3d> visited,
            Dictionary<Point3d, int> clusterMap, ClusterConfig config)
        {
            // 将当前点添加到聚类中
            cluster.Points.Add(pt);
            clusterMap[pt] = cluster.ClusterId;
            // 遍历所有邻居点
            for (int i = 0; i < neighbors.Count; i++)
            {
                var np = neighbors[i];
                // 处理未访问的点
                if (!visited.Contains(np))
                {
                    visited.Add(np);
                    var newNeighbors = GetNeighbors(np, allPoints, config.EpsilonX, config.EpsilonY);
                    // 如果邻居点数量达到阈值，将新邻居添加到扩展列表中
                    if (newNeighbors.Count >= config.MinPoints)
                        neighbors.AddRange(newNeighbors.Except(neighbors));
                }
                // 如果点未被分配到聚类，将其添加到当前聚类
                if (!clusterMap.ContainsKey(np))
                {
                    cluster.Points.Add(np);
                    clusterMap[np] = cluster.ClusterId;
                }
            }
        }
        /// <summary>
        /// 获取在指定矩形范围内的邻居点
        /// </summary>
        private List<Point3d> GetNeighbors(Point3d center, List<Point3d> all, double epsX, double epsY)
        {
            return all.Where(p =>
                Math.Abs(p.X - center.X) <= epsX / 2 &&
                Math.Abs(p.Y - center.Y) <= epsY / 2
            ).ToList();
        }
        /// <summary>
        /// 获取用户选择的点集合
        /// </summary>
        public static List<Point3d> GetPointCollection()
        {
            // 获取当前文档和编辑器
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            // 创建选择过滤器，只选择点 (DXF代码 0 为对象类型)
            TypedValue[] filterList = new TypedValue[]
            {
                new TypedValue(0, "POINT") // 只选择 DBPoint 对象
            };
            SelectionFilter filter = new SelectionFilter(filterList);
            // 创建点集合
            List<Point3d> points = new List<Point3d>();
            try
            {
                // 提示用户选择点
                PromptSelectionResult selectionResult = ed.GetSelection(filter);
                // 检查选择结果
                if (selectionResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择任何点或操作取消");
                    return points; // 返回空集合
                }
                // 锁定文档并开始事务
                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction trans = db.TransactionManager.StartTransaction())
                    {
                        // 处理选择集
                        foreach (SelectedObject selObj in selectionResult.Value)
                        {
                            // 获取选择的实体
                            DBPoint dbPoint = trans.GetObject(selObj.ObjectId, OpenMode.ForRead) as DBPoint;
                            if (dbPoint != null)
                            {
                                // 添加点坐标到集合
                                points.Add(dbPoint.Position);
                            }
                        }
                        trans.Commit();
                    }
                }
                ed.WriteMessage($"\n成功选择了 {points.Count} 个点");
                return points;
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
                return points; // 返回空集合
            }
        }
    }
}