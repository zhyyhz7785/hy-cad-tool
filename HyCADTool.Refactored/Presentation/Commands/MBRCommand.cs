using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Services.GeometryAlgorithms;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// MBR 命令 - 最小边界矩形 (Minimum Bounding Rectangle)
    /// 功能：
    /// 1. 选择图元
    /// 2. 基于距离阈值进行聚类
    /// 3. 为每个聚类生成扩展的边界矩形
    /// 4. 创建在指定图层
    /// </summary>
    public class MBRCommand
    {
        private const string DEFAULT_LAYER_NAME = "00_hy_2公共_视口";
        private const short DEFAULT_LAYER_COLOR = 1; // 红色
        private const double DEFAULT_DISTANCE_THRESHOLD = 1500.0;
        private const double DEFAULT_EXPAND_X = 100.0;
        private const double DEFAULT_EXPAND_Y = 100.0;

        private readonly IClusteringService _clusteringService;

        /// <summary>
        /// 构造函数 - 通过依赖注入获取服务
        /// </summary>
        public MBRCommand()
        {
            _clusteringService = ServiceLocator.Resolve<IClusteringService>();
        }

        /// <summary>
        /// 执行 MBR 命令
        /// </summary>
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                // 获取用户输入参数
                var parameters = GetUserInput(ed);
                if (!parameters.HasValue)
                {
                    ed.WriteMessage("\n命令取消：无效的输入参数。");
                    return;
                }

                var (distanceThreshold, expandX, expandY) = parameters.Value;

                // 提示用户选择图元
                var selectionResult = SelectEntities(ed);
                if (selectionResult == null || selectionResult.Value.Count == 0)
                {
                    ed.WriteMessage("\n未选择任何图元。");
                    return;
                }

                // 收集图元边界
                List<(ObjectId Id, BoundingBox Bounds)> entityBounds;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    entityBounds = CollectEntityBounds(tr, selectionResult.Value);
                    tr.Commit();
                }

                if (entityBounds.Count == 0)
                {
                    ed.WriteMessage("\n没有有效边界的图元。");
                    return;
                }

                ed.WriteMessage($"\n已收集 {entityBounds.Count} 个图元的边界框，开始聚类...");

                // 使用 Domain 服务进行聚类
                var clusters = _clusteringService.ClusterByBoundsDistance(
                    entityBounds,
                    item => item.Bounds,
                    distanceThreshold);

                ed.WriteMessage($"\n聚类完成，生成了 {clusters.Count} 个区域。");

                // 图层已在 PluginInitializer 统一创建

                // 生成边界框
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    GenerateBoundaryBoxes(tr, db, clusters, expandX, expandY, ed);
                    tr.Commit();
                }

                ed.WriteMessage($"\n处理完成！");
                ed.WriteMessage($"\n  聚类数量：{clusters.Count}");
                ed.WriteMessage($"\n  目标图层：{DEFAULT_LAYER_NAME}");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n堆栈跟踪：{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 获取用户输入的参数
        /// </summary>
        private (double DistanceThreshold, double ExpandX, double ExpandY)? GetUserInput(Editor ed)
        {
            // 距离阈值
            var distOpt = new PromptDoubleOptions($"\n请输入聚类距离阈值 [默认={DEFAULT_DISTANCE_THRESHOLD}]：")
            {
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = DEFAULT_DISTANCE_THRESHOLD
            };
            var distRes = ed.GetDouble(distOpt);
            if (distRes.Status != PromptStatus.OK)
                return null;

            // X 方向放大
            var expandXOpt = new PromptDoubleOptions($"\n请输入X方向放大距离 [默认={DEFAULT_EXPAND_X}]：")
            {
                AllowNegative = false,
                DefaultValue = DEFAULT_EXPAND_X
            };
            var expandXRes = ed.GetDouble(expandXOpt);
            if (expandXRes.Status != PromptStatus.OK)
                return null;

            // Y 方向放大
            var expandYOpt = new PromptDoubleOptions($"\n请输入Y方向放大距离 [默认={DEFAULT_EXPAND_Y}]：")
            {
                AllowNegative = false,
                DefaultValue = DEFAULT_EXPAND_Y
            };
            var expandYRes = ed.GetDouble(expandYOpt);
            if (expandYRes.Status != PromptStatus.OK)
                return null;

            return (distRes.Value, expandXRes.Value, expandYRes.Value);
        }

        /// <summary>
        /// 提示用户选择图元
        /// </summary>
        private PromptSelectionResult SelectEntities(Editor ed)
        {
            var selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择图元进行区域聚类与边界框生成："
            };
            return ed.GetSelection(selOpts);
        }

        /// <summary>
        /// 收集有效图元的边界框
        /// </summary>
        private List<(ObjectId Id, BoundingBox Bounds)> CollectEntityBounds(
            Transaction tr, SelectionSet selection)
        {
            var entityBounds = new List<(ObjectId Id, BoundingBox Bounds)>();

            foreach (SelectedObject selObj in selection)
            {
                if (selObj == null) continue;

                var ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                // 获取AutoCAD边界框
                Extents3d? bounds = null;
                try
                {
                    bounds = ent.Bounds;
                }
                catch
                {
                    // 某些图元可能没有边界
                    continue;
                }

                if (!bounds.HasValue) continue;

                // 转换为 Domain BoundingBox
                var domainBounds = bounds.Value.ToBoundingBox();
                entityBounds.Add((selObj.ObjectId, domainBounds));
            }

            return entityBounds;
        }

        /// <summary>
        /// 为每个聚类生成边界框
        /// </summary>
        private void GenerateBoundaryBoxes(
            Transaction tr,
            Database db,
            List<List<(ObjectId Id, BoundingBox Bounds)>> clusters,
            double expandX,
            double expandY,
            Editor ed)
        {
            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            foreach (var cluster in clusters)
            {
                if (cluster.Count == 0) continue;

                // 合并聚类中所有边界框
                BoundingBox mergedBounds = cluster[0].Bounds;
                for (int i = 1; i < cluster.Count; i++)
                {
                    mergedBounds = mergedBounds.Union(cluster[i].Bounds);
                }

                // 扩展边界框
                BoundingBox expandedBounds = mergedBounds.Expand(expandX, expandY);

                // 创建矩形多段线
                Polyline rectangle = CreateRectangleFromBoundingBox(expandedBounds);
                rectangle.Layer = DEFAULT_LAYER_NAME;

                btr.AppendEntity(rectangle);
                tr.AddNewlyCreatedDBObject(rectangle, true);
                rectangle.Dispose();
            }

            ed.WriteMessage($"\n成功生成 {clusters.Count} 个区域的最小外接矩形，位于图层 '{DEFAULT_LAYER_NAME}'。");
        }

        /// <summary>
        /// 从 BoundingBox 创建矩形 Polyline
        /// </summary>
        private Polyline CreateRectangleFromBoundingBox(BoundingBox box)
        {
            var vertices = box.ToVertices();
            var pline = new Polyline(vertices.Length);

            for (int i = 0; i < vertices.Length; i++)
            {
                pline.AddVertexAt(i, new Point2d(vertices[i].X, vertices[i].Y), 0, 0, 0);
            }

            pline.Closed = true;
            return pline;
        }
    }
}

