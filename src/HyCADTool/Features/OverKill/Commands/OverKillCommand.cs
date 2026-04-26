using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Features.OverKill.Domain;
using HyCADTool.Features.OverKill.Services;
using HyCADTool.Shared.Geometry;
using HyCADTool.Features.OverKill.Views;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.App.Bootstrap;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.OverKill.Commands
{
    /// <summary>
    /// OverKill 命令 - OVERKILL + FILLET 综合清理
    /// </summary>
    public class OverKillCommand
    {
        private readonly LineOverKillService _overKillService;
        private readonly MarkerLayerService _markerService;

        public OverKillCommand()
        {
            _overKillService = ServiceLocator.Resolve<LineOverKillService>();
            _markerService = ServiceLocator.Resolve<MarkerLayerService>();
        }

        /// <summary>
        /// HYOV 命令 - OVERKILL + FILLET 综合清理
        /// </summary>
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 保存当前图层
            ObjectId originalLayer = db.Clayer;

            try
            {
                // 1. 选择线段
                var selectionResult = GetLineSelection(ed);
                if (selectionResult == null || selectionResult.Value.Count == 0)
                {
                    ed.WriteMessage("\n未选择任何线段");
                    return;
                }

                int originalCount = selectionResult.Value.Count;
                var settings = HyovSettings.Instance;

                // 2. 收集线段数据
                List<(ObjectId Id, Line AcadLine, Line2D DomainLine)> lineData;
                using (var trans = db.TransactionManager.StartTransaction())
                {
                    lineData = CollectLines(selectionResult.Value.GetObjectIds(), trans);
                    trans.Abort();
                }

                // 3. 处理线段（直接调用核心服务）
                var processedLines = lineData.Select(x => x.DomainLine).ToList();

                if (settings.EnablePreClean)
                {
                    processedLines = _overKillService.MergeOverlappingLines(
                        processedLines,
                        settings.GetScaledGeometricTolerance(),
                        settings.GetScaledParallelMergeDistance());
                }

                if (settings.EnableBreakLines)
                {
                    processedLines = _overKillService.BreakAtIntersections(
                        processedLines,
                        settings.GetScaledGeometricTolerance(),
                        settings.GetScaledMinLineLength());
                }

                if (settings.EnableExtendEndpoints)
                {
                    processedLines = _overKillService.ExtendNearEndpoints(
                        processedLines,
                        settings.GetScaledGeometricTolerance(),
                        settings.GetScaledMaxExtendDistance());
                }

                if (settings.EnableExtendToLine)
                {
                    processedLines = _overKillService.ExtendEndpointToLine(
                        processedLines,
                        settings.GetScaledGeometricTolerance(),
                        settings.GetScaledMaxExtendDistance(),
                        settings.GetScaledMinLineLength());
                }

                processedLines = _overKillService.FilterShortSegments(
                    processedLines,
                    settings.GetScaledMinLineLength(),
                    false);

                processedLines = _overKillService.RemoveDuplicateLines(
                    processedLines,
                    settings.GetScaledGeometricTolerance());

                // 4. 查找独立端点（可选）
                List<(Point2D Point, Vector2D Direction)> independentEndpoints = null;
                if (settings.ShowIndependentEndpoints)
                {
                    independentEndpoints = _overKillService.FindIndependentEndpoints(
                        processedLines,
                        settings.GetScaledIndependentEndpointTolerance());
                }

                // 5. 输出结果
                ed.WriteMessage($"\n处理完成：{originalCount} → {processedLines.Count} 线段");

                if (settings.ShowIndependentEndpoints && independentEndpoints != null)
                {
                    if (independentEndpoints.Count > 0)
                    {
                        DrawIndependentEndpointMarkers(doc, db, independentEndpoints, settings.MarkerScale);
                        ed.WriteMessage($"\n已标记 {independentEndpoints.Count} 个独立端点");
                    }
                    else
                    {
                        ed.WriteMessage($"\n✅ 未找到独立端点（所有线段都已连接）");
                    }
                }

                // 6. 更新图纸
                using (doc.LockDocument())
                using (var trans = db.TransactionManager.StartTransaction())
                {
                    UpdateDrawing(db, trans, lineData, processedLines);
                    trans.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n堆栈：{ex.StackTrace}");
            }
            finally
            {
                // 恢复当前图层
                db.Clayer = originalLayer;
            }
        }

        /// <summary>
        /// HYOVSET 命令 - 打开 HYOV 参数设置窗口
        /// </summary>
        public static void ExecuteSettings()
        {
            try
            {
                var settingsWindow = new HyovSettingsWindow();
                AcApp.ShowModalWindow(settingsWindow);
            }
            catch (System.Exception ex)
            {
                var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n错误：{ex.Message}");
            }
        }

        #region 私有辅助方法

        /// <summary>
        /// 获取用户选择的线段
        /// </summary>
        private PromptSelectionResult GetLineSelection(Editor ed)
        {
            var selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择要处理的线段: "
            };

            var filter = new SelectionFilter(new[] {
                new TypedValue((int)DxfCode.Start, "LINE")
            });

            var result = ed.GetSelection(selOpts, filter);
            return result.Status == PromptStatus.OK ? result : null;
        }

        /// <summary>
        /// 收集线段数据（包括图层信息）
        /// </summary>
        private List<(ObjectId Id, Line AcadLine, Line2D DomainLine)> CollectLines(
            ObjectId[] selectedObjects,
            Transaction transaction)
        {
            var result = new List<(ObjectId, Line, Line2D)>();

            foreach (ObjectId objId in selectedObjects)
            {
                var line = transaction.GetObject(objId, OpenMode.ForRead) as Line;
                if (line != null)
                {
                    var domainLine = line.ToDomainLine2D();
                    result.Add((objId, line, domainLine));
                }
            }

            return result;
        }

        /// <summary>
        /// 更新图纸（删除旧线段，添加新线段，保留原图层信息）
        /// </summary>
        private void UpdateDrawing(
            Database db,
            Transaction trans,
            List<(ObjectId Id, Line AcadLine, Line2D DomainLine)> originalLineData,
            List<Line2D> cleanedLines)
        {
            var bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            var btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            // 提取原线段的图层信息（使用第一个线段的图层）
            string originalLayerName = "0"; // 默认图层
            if (originalLineData.Count > 0)
            {
                originalLayerName = originalLineData[0].AcadLine.Layer;
            }

            // 删除原有线段
            foreach (var (id, _, _) in originalLineData)
            {
                var ent = trans.GetObject(id, OpenMode.ForWrite) as Entity;
                ent?.Erase();
            }

            // 创建新线段（保留原图层）
            foreach (var domainLine in cleanedLines)
            {
                var acadLine = new Line(
                    new Point3d(domainLine.StartPoint.X, domainLine.StartPoint.Y, 0),
                    new Point3d(domainLine.EndPoint.X, domainLine.EndPoint.Y, 0));
                acadLine.Layer = originalLayerName; // 保留原图层
                btr.AppendEntity(acadLine);
                trans.AddNewlyCreatedDBObject(acadLine, true);
                acadLine.Dispose();
            }
        }

        /// <summary>
        /// 绘制独立端点标记（长方形，长向平行于直线）- 使用通用标记方法
        /// </summary>
        private void DrawIndependentEndpointMarkers(
            Autodesk.AutoCAD.ApplicationServices.Document doc,
            Database db,
            List<(Point2D Point, Vector2D Direction)> endpoints,
            double markerScale)
        {
            using (doc.LockDocument())
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                _markerService.EnsureMarkerLayer(db, trans, MarkerLayerService.LAYER_INDEPENDENT_ENDPOINTS);

                double longSide = 10.0 * markerScale;
                double shortSide = 3.0 * markerScale;

                foreach (var (point, direction) in endpoints)
                {
                    // 计算长方形的四个顶点
                    var perpendicular = new Vector2D(-direction.Y, direction.X);

                    var p1 = new Point3d(
                        point.X - direction.X * longSide / 2 - perpendicular.X * shortSide / 2,
                        point.Y - direction.Y * longSide / 2 - perpendicular.Y * shortSide / 2, 0);
                    var p2 = new Point3d(
                        point.X + direction.X * longSide / 2 - perpendicular.X * shortSide / 2,
                        point.Y + direction.Y * longSide / 2 - perpendicular.Y * shortSide / 2, 0);
                    var p3 = new Point3d(
                        point.X + direction.X * longSide / 2 + perpendicular.X * shortSide / 2,
                        point.Y + direction.Y * longSide / 2 + perpendicular.Y * shortSide / 2, 0);
                    var p4 = new Point3d(
                        point.X - direction.X * longSide / 2 + perpendicular.X * shortSide / 2,
                        point.Y - direction.Y * longSide / 2 + perpendicular.Y * shortSide / 2, 0);

                    // 使用通用标记方法绘制多段线矩形
                    var rectanglePoints = new[] { p1, p2, p3, p4 };
                    _markerService.DrawPolylineMarker(
                        db,
                        trans,
                        rectanglePoints,
                        MarkerLayerService.LAYER_INDEPENDENT_ENDPOINTS,
                        closed: true);
                }

                trans.Commit();
            }
        }

        #endregion
    }
}
