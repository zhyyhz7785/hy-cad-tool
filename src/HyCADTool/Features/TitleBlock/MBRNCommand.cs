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
    /// HYMBRN - 收集/聚类/出图分段计时；BFS / Grid / Sweep 三路对比，一致时采用最快者出图。
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
                long msCollect;
                long msBfs;
                long msGrid;
                long msSweep;
                long msDraw;
                int countBfs;
                int countGrid;
                int countSweep;
                bool equivalent;
                string winnerName;
                List<List<(ObjectId Id, BoundingBox Bounds)>> outputClusters;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var swCollect = Stopwatch.StartNew();
                    entityBounds = MbrCommandCore.CollectEntityBounds(tr, selectionResult.Value);
                    swCollect.Stop();
                    msCollect = swCollect.ElapsedMilliseconds;

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
                    msBfs = swBfs.ElapsedMilliseconds;
                    countBfs = clustersBfs.Count;

                    var swGrid = Stopwatch.StartNew();
                    var clustersGrid = _clusteringService.ClusterByBoundsDistanceGrid(
                        entityBounds,
                        item => item.Bounds,
                        distanceThreshold);
                    swGrid.Stop();
                    msGrid = swGrid.ElapsedMilliseconds;
                    countGrid = clustersGrid.Count;

                    var swSweep = Stopwatch.StartNew();
                    var clustersSweep = _clusteringService.ClusterByBoundsDistanceSweep(
                        entityBounds,
                        item => item.Bounds,
                        distanceThreshold);
                    swSweep.Stop();
                    msSweep = swSweep.ElapsedMilliseconds;
                    countSweep = clustersSweep.Count;

                    equivalent =
                        AreClusterPartitionsEquivalent(clustersBfs, clustersGrid) &&
                        AreClusterPartitionsEquivalent(clustersBfs, clustersSweep) &&
                        AreClusterPartitionsEquivalent(clustersGrid, clustersSweep);

                    outputClusters = clustersBfs;
                    winnerName = "BFS";

                    if (equivalent)
                    {
                        if (msSweep <= msGrid && msSweep <= msBfs)
                        {
                            outputClusters = clustersSweep;
                            winnerName = "Sweep";
                        }
                        else if (msGrid <= msBfs)
                        {
                            outputClusters = clustersGrid;
                            winnerName = "Grid";
                        }
                    }
                    else
                    {
                        ed.WriteMessage("\n警告：三路聚类分区不一致，出图回退 BFS。");
                    }

                    var swDraw = Stopwatch.StartNew();
                    MbrCommandCore.GenerateBoundaryBoxes(tr, db, outputClusters, expandX, expandY);
                    swDraw.Stop();
                    msDraw = swDraw.ElapsedMilliseconds;

                    tr.Commit();
                }

                ed.WriteMessage(
                    $"\n[HYMBRN] n={entityBounds.Count} | 收集={msCollect}ms | " +
                    $"BFS={msBfs}ms({countBfs}) | Grid={msGrid}ms({countGrid}) | Sweep={msSweep}ms({countSweep}) | " +
                    $"一致={equivalent} | 出图={msDraw}ms | 采用={winnerName}");
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
