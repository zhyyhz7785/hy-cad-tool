using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.OverKillCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// OverKill 命令 - 基础 OVERKILL 清理（极简版）
    /// 
    /// 当前功能（阶段1）：
    /// 1. 删除完全重复的线段
    /// 2. 合并部分重叠的共线线段
    /// 3. 合并端点接触的共线线段
    /// 
    /// 不做（符合 AutoCAD OVERKILL 定义）：
    /// - 在交点处分割（那是 BREAK 命令）
    /// - FILLET 自动连接（阶段2功能，暂时禁用）
    /// </summary>
    public class OverKillCommand
    {
        private const string MARKER_LAYER = "00_HY_临时标记";  // 临时标记图层

        private readonly LineOverKillService _overKillService;
        private readonly ILayerService _layerService;

        /// <summary>
        /// 构造函数 - 通过依赖注入获取服务
        /// </summary>
        public OverKillCommand()
        {
            _overKillService = ServiceLocator.Resolve<LineOverKillService>();
            _layerService = ServiceLocator.Resolve<ILayerService>();
        }

        /// <summary>
        /// HYOV 命令 - OVERKILL + FILLET 综合清理
        /// 
        /// 测试命令：C11（由 Recall.cs 动态调用，支持热重启）
        /// </summary>
        [CommandMethod("HYOV")]
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                ed.WriteMessage("\n=== HYOV 命令（OVERKILL + FILLET） ===");
                ed.WriteMessage("\n提示：如果修改代码后命令未更新，请使用 C11 命令或重启 AutoCAD");
                
                // 1. 获取用户选择的线段
                var selectionResult = GetLineSelection(ed);
                if (selectionResult == null || selectionResult.Value.Count == 0)
                {
                    ed.WriteMessage("\n未选择任何线段，命令取消。");
                    return;
                }

                // 2. 收集线段数据
                List<(ObjectId Id, Line AcadLine, Line2D DomainLine)> lineData;
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    lineData = CollectLines(tr, selectionResult);
                    tr.Commit();
                }

                int originalCount = lineData.Count;
                ed.WriteMessage($"\n已选择 {originalCount} 条线段");

                // 获取当前设置并应用单位换算
                var settings = HyovSettings.Instance;
                double tolerance = settings.GetScaledGeometricTolerance();
                double parallelDistance = settings.GetScaledParallelMergeDistance();
                double minLineLength = settings.GetScaledMinLineLength();
                double maxExtendDistance = settings.GetScaledMaxExtendDistance();
                double independentTolerance = settings.GetScaledIndependentEndpointTolerance();


                var domainLines = lineData.Select(x => x.DomainLine).ToList();
                List<Line2D> processedLines = domainLines;

                var stepwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // 3. OVERKILL 预清理：合并重叠和共线线段（可选）
                if (settings.EnablePreClean)
                {
                    stepwatch.Restart();
                    processedLines = _overKillService.MergeOverlappingLines(processedLines, tolerance, parallelDistance);
                    ed.WriteMessage($"\n第0步-预清理：{originalCount} → {processedLines.Count} 线段 ({stepwatch.ElapsedMilliseconds}ms)");
                }
                
                // 4. FILLET 第一步：打断相交线段（不过滤）
                if (settings.EnableBreakLines)
                {
                    var beforeCount = processedLines.Count;
                    stepwatch.Restart();
                    processedLines = _overKillService.BreakAtIntersections(processedLines, tolerance, minLineLength);
                    ed.WriteMessage($"\n第1步-打断相交：{beforeCount} → {processedLines.Count} 线段 ({stepwatch.ElapsedMilliseconds}ms)");
                }
                
                // 5. FILLET 第二步：端点延伸（不过滤）
                if (settings.EnableExtendEndpoints)
                {
                    var beforeCount = processedLines.Count;
                    stepwatch.Restart();
                    processedLines = _overKillService.ExtendNearEndpoints(processedLines, tolerance, maxExtendDistance);
                    ed.WriteMessage($"\n第2步-端点延伸：{beforeCount} → {processedLines.Count} 线段 ({stepwatch.ElapsedMilliseconds}ms)");
                }
                
                // 6. FILLET 第三步：端点到线延伸（不过滤）
                if (settings.EnableExtendToLine)
                {
                    var beforeCount = processedLines.Count;
                    stepwatch.Restart();
                    processedLines = _overKillService.ExtendEndpointToLine(processedLines, tolerance, maxExtendDistance, minLineLength);
                    ed.WriteMessage($"\n第3步-端点到线：{beforeCount} → {processedLines.Count} 线段 ({stepwatch.ElapsedMilliseconds}ms)");
                }
                
                // 7. 统一过滤短线段（一次性）
                var beforeFilter = processedLines.Count;
                stepwatch.Restart();
                processedLines = _overKillService.FilterShortSegments(processedLines, minLineLength, false);
                ed.WriteMessage($"\n第4步-过滤短线段：{beforeFilter} → {processedLines.Count} 线段 ({stepwatch.ElapsedMilliseconds}ms)");
                
                // 8. 删除完全重复的线段
                stepwatch.Restart();
                var cleanedLines = _overKillService.RemoveDuplicateLines(processedLines, tolerance);
                ed.WriteMessage($"\n第5步-删除重复：{processedLines.Count} → {cleanedLines.Count} 线段 ({stepwatch.ElapsedMilliseconds}ms)");
                
                // 9. 查找并标记独立端点
                if (settings.ShowIndependentEndpoints)
                {
                    var independentEndpoints = _overKillService.FindIndependentEndpoints(cleanedLines, independentTolerance);
                    if (independentEndpoints.Count > 0)
                    {
                        DrawIndependentEndpointMarkers(doc, db, independentEndpoints, settings.MarkerScale);
                        ed.WriteMessage($"\n找到 {independentEndpoints.Count} 个独立端点，已标记");
                    }
                    else
                    {
                        ed.WriteMessage($"\n✅ 未找到独立端点（所有线段都已连接）");
                    }
                }
                
                // 9. 更新图纸
                UpdateLines(doc, db, lineData, cleanedLines);
                
                // 10. 输出结果
                stopwatch.Stop();
                ed.WriteMessage($"\n最终结果：{originalCount} → {cleanedLines.Count} 线段");
                ed.WriteMessage($"\n处理时间：{stopwatch.ElapsedMilliseconds} 毫秒");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n堆栈：{ex.StackTrace}");
            }
        }


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
        /// 收集选中的线段数据
        /// </summary>
        private List<(ObjectId Id, Line AcadLine, Line2D DomainLine)> CollectLines(
            Transaction tr, PromptSelectionResult selectionResult)
        {
            var result = new List<(ObjectId, Line, Line2D)>();

            foreach (SelectedObject selObj in selectionResult.Value)
            {
                var line = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Line;
                if (line != null)
                {
                    var domainLine = line.ToDomainLine2D();
                    result.Add((selObj.ObjectId, line, domainLine));
                }
            }

            return result;
        }

        /// <summary>
        /// 获取模型空间
        /// </summary>
        private BlockTableRecord GetModelSpace(Transaction tr, Database db)
        {
            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            return tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
        }


        /// <summary>
        /// 更新图纸线段
        /// </summary>
        private void UpdateLines(
            Autodesk.AutoCAD.ApplicationServices.Document doc,
            Database db,
            List<(ObjectId Id, Line AcadLine, Line2D DomainLine)> originalData,
            List<Line2D> newLines)
        {
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = GetModelSpace(tr, db);
                
                // 删除原有线段
                foreach (var (id, _, _) in originalData)
                {
                    var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    ent?.Erase();
                }

                // 创建新线段
                foreach (var domainLine in newLines)
                {
                    var acadLine = domainLine.ToAcadLine();
                    btr.AppendEntity(acadLine);
                    tr.AddNewlyCreatedDBObject(acadLine, true);
                    acadLine.Dispose();
                }

                tr.Commit();
            }
        }

        /// <summary>
        /// 绘制独立端点标记（长方形，长向平行于直线）
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
                // 确保标记图层存在
                EnsureMarkerLayerExists(db, trans);
                
                BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                // 标记尺寸：长方形，长边 = 10，短边 = 3，放大倍数由参数控制
                double longSide = 10.0 * markerScale;
                double shortSide = 3.0 * markerScale;
                
                foreach (var (point, direction) in endpoints)
                {
                    // 计算长方形的四个顶点
                    // 长边平行于直线方向，短边垂直于直线
                    Vector2D perpendicular = new Vector2D(-direction.Y, direction.X); // 垂直向量
                    
                    Point2D p1 = new Point2D(
                        point.X - direction.X * longSide / 2 - perpendicular.X * shortSide / 2,
                        point.Y - direction.Y * longSide / 2 - perpendicular.Y * shortSide / 2
                    );
                    Point2D p2 = new Point2D(
                        point.X + direction.X * longSide / 2 - perpendicular.X * shortSide / 2,
                        point.Y + direction.Y * longSide / 2 - perpendicular.Y * shortSide / 2
                    );
                    Point2D p3 = new Point2D(
                        point.X + direction.X * longSide / 2 + perpendicular.X * shortSide / 2,
                        point.Y + direction.Y * longSide / 2 + perpendicular.Y * shortSide / 2
                    );
                    Point2D p4 = new Point2D(
                        point.X - direction.X * longSide / 2 + perpendicular.X * shortSide / 2,
                        point.Y - direction.Y * longSide / 2 + perpendicular.Y * shortSide / 2
                    );
                    
                    // 创建多段线矩形
                    Polyline rectangle = new Polyline(4);
                    rectangle.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
                    rectangle.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
                    rectangle.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
                    rectangle.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
                    rectangle.Closed = true;
                    rectangle.Layer = MARKER_LAYER;
                    rectangle.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1); // 红色
                    
                    btr.AppendEntity(rectangle);
                    trans.AddNewlyCreatedDBObject(rectangle, true);
                }

                trans.Commit();
            }
        }

        /// <summary>
        /// 确保标记图层存在
        /// </summary>
        private void EnsureMarkerLayerExists(Database db, Transaction trans)
        {
            LayerTable lt = trans.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            
            if (!lt.Has(MARKER_LAYER))
            {
                lt.UpgradeOpen();
                LayerTableRecord ltr = new LayerTableRecord();
                ltr.Name = MARKER_LAYER;
                ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1); // 红色
                lt.Add(ltr);
                trans.AddNewlyCreatedDBObject(ltr, true);
                lt.DowngradeOpen();
            }
        }

        /// <summary>
        /// HYOVSET 命令 - 打开 HYOV 参数设置窗口
        /// 
        /// 测试命令：C12（由 Recall.cs 动态调用，支持热重启）
        /// </summary>
        [CommandMethod("HYOVSET")]
        public void ExecuteSettings()
        {
            try
            {
                // 创建并显示设置窗口
                var settingsWindow = new HyovSettingsWindow();
                var result = AcApp.ShowModalWindow(settingsWindow);
                
                if (result == true)
                {
                    var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
                    ed.WriteMessage("\n✅ HYOV 参数已更新");
                    
                    // 显示当前设置
                    var settings = HyovSettings.Instance;
                    ed.WriteMessage("\n========== 当前设置 ==========");
                    ed.WriteMessage($"\n绘图单位: {(settings.Unit == DrawingUnit.Millimeter ? "毫米(mm)" : "米(m)")}");
                    ed.WriteMessage($"\n几何容差: {settings.GeometricTolerance:G} → 实际值: {settings.GetScaledGeometricTolerance():G}");
                    ed.WriteMessage($"\n平行线合并距离: {settings.ParallelMergeDistance:F3} → 实际值: {settings.GetScaledParallelMergeDistance():F6}");
                    ed.WriteMessage($"\n最小线段长度: {settings.MinLineLength:F3} → 实际值: {settings.GetScaledMinLineLength():F6}");
                    ed.WriteMessage($"\n端点延伸最大距离: {settings.MaxExtendDistance:F3} → 实际值: {settings.GetScaledMaxExtendDistance():F6}");
                    ed.WriteMessage($"\n标记缩放倍数: {settings.MarkerScale:F1}");
                    ed.WriteMessage($"\n独立端点检测容差: {settings.IndependentEndpointTolerance:F3} → 实际值: {settings.GetScaledIndependentEndpointTolerance():F6}");
                }
            }
            catch (System.Exception ex)
            {
                var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }

    }
}