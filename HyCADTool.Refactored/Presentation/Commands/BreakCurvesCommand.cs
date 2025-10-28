using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.HyApplication.Services;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.BreakCurvesCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// BreakCurves 命令 - 在交点处打断曲线（重构版）
    /// 
    /// 功能：
    /// 1. 选择曲线（LINE, ARC, CIRCLE, LWPOLYLINE, POLYLINE, SPLINE）
    /// 2. 在交点处分割曲线
    /// 3. 可选：使用 /MARK 参数标记打断点
    /// 
    /// 架构：
    /// - Presentation（本类）：用户交互、输出（~100行）
    /// - Application（Workflow）：业务流程编排、时间分析
    /// - Domain + Infrastructure：算法和AutoCAD操作
    /// </summary>
    public class BreakCurvesCommand
    {
        private readonly BreakCurvesWorkflow _workflow;
        private readonly MarkerLayerService _markerService;

        public BreakCurvesCommand()
        {
            // 构造时直接解析服务（原始方式）
            _workflow = ServiceLocator.Resolve<BreakCurvesWorkflow>();
            _markerService = ServiceLocator.Resolve<MarkerLayerService>();
        }

        [CommandMethod("HYBC")]
        public void Execute()
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            
            try
            {

                // 检查命令行参数：是否有 /MARK
                bool markIntersections = CheckMarkParameter(ed);

                // 1. 选择曲线
                var selectedObjects = SelectCurves(ed);
                if (selectedObjects == null || selectedObjects.Length == 0)
                {
                    ed.WriteMessage("\n未选择任何曲线。");
                    return;
                }

                int inputCount = selectedObjects.Length;

                // 2. 执行工作流（业务逻辑在 Application 层）
                BreakCurvesResult result;
                using (Transaction trans = doc.Database.TransactionManager.StartTransaction())
                {
                    result = _workflow.Execute(selectedObjects, trans);
                    trans.Abort(); // 工作流只计算，不提交
                }

                // 3. 输出结果（在更新图纸之前，避免上下文失效）
#if DEBUG
                OutputDetailedTimings(ed, inputCount, result);
#else
                ed.WriteMessage($"\n处理完成：{inputCount} → {result.NewCurves.Count} 曲线，{result.TotalTime} 毫秒");
#endif

                // 4. 绘制标记（如果需要）
                if (markIntersections && result.IntersectionPoints.Count > 0)
                {
                    DrawIntersectionMarkers(doc.Database, result.IntersectionPoints);
                    ed.WriteMessage($"\n已标记 {result.IntersectionPoints.Count} 个打断点");
                }

                // 5. 更新图纸
                UpdateDrawing(doc.Database, result);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
#if DEBUG
                ed.WriteMessage($"\n堆栈：{ex.StackTrace}");
#endif
            }
        }

        /// <summary>
        /// 检查命令行参数是否有 /MARK
        /// 暂时禁用标记功能，避免命令状态异常
        /// </summary>
        private bool CheckMarkParameter(Editor ed)
        {
            // 暂时直接返回 false，不标记
            // TODO: 后续通过命令行参数解析 /MARK
            return false;
        }

        /// <summary>
        /// 选择曲线
        /// </summary>
        private ObjectId[] SelectCurves(Editor ed)
        {
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择要处理的曲线："
            };

            SelectionFilter filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LINE,ARC,CIRCLE,LWPOLYLINE,POLYLINE,SPLINE")
            });

            PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK)
                return null;

            return selRes.Value.GetObjectIds();
        }

        /// <summary>
        /// 绘制交点标记（圆圈，半径 50mm）
        /// </summary>
        private void DrawIntersectionMarkers(Database db, System.Collections.Generic.List<Autodesk.AutoCAD.Geometry.Point3d> points)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            using (doc.LockDocument())
            using (var trans = db.TransactionManager.StartTransaction())
            {
                _markerService.EnsureMarkerLayer(db, trans, MarkerLayerService.LAYER_BREAK_POINTS);

                foreach (var point in points)
                {
                    _markerService.DrawCircleMarker(db, trans, point, 50.0, MarkerLayerService.LAYER_BREAK_POINTS);
                }

                trans.Commit();
            }
        }

        /// <summary>
        /// 更新图纸
        /// </summary>
        private void UpdateDrawing(Database db, BreakCurvesResult result)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            using (doc.LockDocument())
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                // 添加新曲线段
                foreach (Curve newCurve in result.NewCurves)
                {
                    btr.AppendEntity(newCurve);
                    trans.AddNewlyCreatedDBObject(newCurve, true);
                }

                // 删除原始对象
                foreach (ObjectId id in result.ObjectsToDelete)
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForWrite) as Entity;
                    ent?.Erase();
                }

                trans.Commit();
            }
        }

        /// <summary>
        /// 输出详细时间分析（DEBUG 模式）
        /// </summary>
        private void OutputDetailedTimings(Editor ed, int inputCount, BreakCurvesResult result)
        {
            ed.WriteMessage($"\n处理完成：{inputCount} → {result.NewCurves.Count} 曲线");
            ed.WriteMessage("\n=== 详细时间分析 ===");
            foreach (var timing in result.PhaseTimings)
            {
                ed.WriteMessage($"\n  [{timing.Key}] {timing.Value} 毫秒");
            }
            ed.WriteMessage($"\n总时间：{result.TotalTime} 毫秒");
        }
    }
}
