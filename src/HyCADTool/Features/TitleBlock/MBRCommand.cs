using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCAD.Geometry;
using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Cluster.Domain.Services;
using System;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.TitleBlock
{
    /// <summary>
    /// MBR 命令 - 最小边界矩形 (Minimum Bounding Rectangle)
    /// </summary>
    public class MBRCommand
    {
        private readonly IClusteringService _clusteringService;

        public MBRCommand()
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
                    ed.WriteMessage("\n未选择任何图元。");
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

                    var clusters = _clusteringService.ClusterByBoundsDistance(
                        entityBounds,
                        item => item.Bounds,
                        distanceThreshold);

                    MbrCommandCore.GenerateBoundaryBoxes(tr, db, clusters, expandX, expandY);
                    tr.Commit();

                    ed.WriteMessage($"\n完成：{entityBounds.Count} 个图元 → {clusters.Count} 个区域，图层 {MbrCommandCore.DefaultLayerName}");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }
    }
}
