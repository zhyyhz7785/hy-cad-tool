using Autodesk.AutoCAD.DatabaseServices;
using HyCAD.Geometry;
using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Cluster.Domain.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.TitleBlock
{
    /// <summary>
    /// HYMBRN - HYMBR 聚类算法 A/B 性能对比（BFS vs 空间网格），结果一致时用 Grid 版出图。
    /// 对比完成后可删除本命令，将 HYMBR 切到 Grid 实现。
    /// </summary>
    public sealed class MBRNCommand
    {
        private readonly IClusteringService _clusteringService;

        public MBRNCommand()
        {
            _clusteringService = ServiceLocator.Resolve<IClusteringService>();
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                var parameters = MbrCommandCore.GetUserInput(ed);
                if (!parameters.HasValue)
                {
                    ed.WriteMessage("\n命令取消。");
                    return;
                }

                var (distanceThreshold, expandX, expandY) = parameters.Value;

                var selectionResult = MbrCommandCore.SelectEntities(ed);
                if (selectionResult == null || selectionResult.Value.Count == 0)
                {
                    ed.WriteMessage("\n未选择图元。");
                    return;
                }

                List<(ObjectId Id, BoundingBox Bounds)> entityBounds;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    entityBounds = MbrCommandCore.CollectEntityBounds(tr, selectionResult.Value);
                    if (entityBounds.Count == 0)
                    {
                        ed.WriteMessage("\n没有有效边界的图元。");
                        return;
                    }

                    var swBfs = Stopwatch.StartNew();
                    var clustersBfs = _clusteringService.ClusterByBoundsDistance(
                        entityBounds,
                        item => item.Bounds,
                        distanceThreshold);
                    swBfs.Stop();

                    var swGrid = Stopwatch.StartNew();
                    var clustersGrid = _clusteringService.ClusterByBoundsDistanceGrid(
                        entityBounds,
                        item => item.Bounds,
                        distanceThreshold);
                    swGrid.Stop();

                    bool equivalent = AreClusterPartitionsEquivalent(clustersBfs, clustersGrid);
                    var useGrid = equivalent && swGrid.Elapsed <= swBfs.Elapsed;
                    var outputClusters = useGrid ? clustersGrid : clustersBfs;

                    MbrCommandCore.GenerateBoundaryBoxes(tr, db, outputClusters, expandX, expandY);
                    tr.Commit();

                    ed.WriteMessage(
                        $"\n[HYMBRN] n={entityBounds.Count} | BFS {swBfs.ElapsedMilliseconds}ms ({clustersBfs.Count}组) | " +
                        $"Grid {swGrid.ElapsedMilliseconds}ms ({clustersGrid.Count}组) | 分区一致={equivalent} | 出图={(useGrid ? "Grid" : "BFS")}");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }

        private static bool AreClusterPartitionsEquivalent<T>(
            List<List<T>> a,
            List<List<T>> b)
        {
            if (a.Count != b.Count) return false;

            var mapA = BuildClusterIndexMap(a);
            var mapB = BuildClusterIndexMap(b);
            if (mapA.Count != mapB.Count) return false;

            foreach (var pair in mapA)
            {
                if (!mapB.TryGetValue(pair.Key, out int clusterB))
                    return false;
                if (pair.Value != clusterB)
                    return false;
            }

            return true;
        }

        private static Dictionary<T, int> BuildClusterIndexMap<T>(List<List<T>> clusters)
        {
            var map = new Dictionary<T, int>();
            for (int i = 0; i < clusters.Count; i++)
            {
                foreach (var item in clusters[i])
                    map[item] = i;
            }
            return map;
        }
    }
}
