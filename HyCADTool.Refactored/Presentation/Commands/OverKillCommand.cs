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
    /// OverKill 命令 - 线段去重合并
    /// 功能：
    /// 1. 合并重叠的线段
    /// 2. 延伸独立端点到最近的线段
    /// 3. 对无法延伸的端点标记警告
    /// </summary>
    public class OverKillCommand
    {
        private const double DEFAULT_TOLERANCE = 1e-6;
        private const double DEFAULT_EXTENSION_DISTANCE = 10.0;
        private const string WARNING_LAYER = "00_HY_警告_红色";
        private const short WARNING_COLOR = 1; // 红色

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
        /// 执行OverKill命令
        /// </summary>
        [CommandMethod("HYOV")]
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
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

                if (lineData.Count == 0)
                {
                    ed.WriteMessage("\n没有找到有效的线段。");
                    return;
                }

                int originalCount = lineData.Count;
                ed.WriteMessage($"\n已选择 {originalCount} 条线段，开始处理...");

                // 3. 使用Domain服务处理线段
                var domainLines = lineData.Select(x => x.DomainLine).ToList();
                
                // 3.1 合并重叠线段
                var mergedLines = _overKillService.MergeOverlappingLines(domainLines, DEFAULT_TOLERANCE);
                ed.WriteMessage($"\n合并重叠线段：{originalCount} → {mergedLines.Count}");

                // 3.2 查找独立端点
                var independentEndpoints = _overKillService.FindIndependentEndpoints(
                    mergedLines, DEFAULT_TOLERANCE);
                
                if (independentEndpoints.Count > 0)
                {
                    ed.WriteMessage($"\n找到 {independentEndpoints.Count} 个独立端点，尝试延伸...");
                }

                // 3.3 延伸独立端点
                var finalLines = new List<Line2D>(mergedLines);
                var warningPoints = new List<(Point2D Point, Vector2D Direction)>();

                foreach (var (line, isStartPoint) in independentEndpoints)
                {
                    var lineIndex = finalLines.IndexOf(line);
                    if (lineIndex < 0) continue;

                    var otherLines = finalLines.Where((l, i) => i != lineIndex).ToList();
                    
                    var (extendedLine, found, intersection) = _overKillService.ExtendToIntersection(
                        line, isStartPoint, otherLines, DEFAULT_EXTENSION_DISTANCE, DEFAULT_TOLERANCE);

                    if (found)
                    {
                        finalLines[lineIndex] = extendedLine;
                    }
                    else
                    {
                        // 记录需要标记警告的点
                        Point2D warningPoint = isStartPoint ? line.StartPoint : line.EndPoint;
                        Vector2D direction = line.Direction.Normalize();
                        if (isStartPoint)
                            direction = direction * -1;
                        warningPoints.Add((warningPoint, direction));
                    }
                }

                // 4. 更新图纸
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var btr = GetModelSpace(tr, db);
                    
                    // 4.1 删除原有线段
                    foreach (var (id, _, _) in lineData)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        ent?.Erase();
                    }

                    // 4.2 创建新线段
                    foreach (var domainLine in finalLines)
                    {
                        var acadLine = domainLine.ToAcadLine();
                        btr.AppendEntity(acadLine);
                        tr.AddNewlyCreatedDBObject(acadLine, true);
                        acadLine.Dispose();
                    }

                    // 4.3 创建警告标记
                    if (warningPoints.Count > 0)
                    {
                        // 确保警告图层存在
                        EnsureWarningLayerExists(tr, db);

                        foreach (var (point, direction) in warningPoints)
                        {
                            CreateWarningRectangle(tr, btr, point, direction);
                        }

                        ed.WriteMessage($"\n无法延伸的端点数量：{warningPoints.Count}，已标记警告矩形。");
                    }

                    tr.Commit();
                }

                ed.WriteMessage($"\n处理完成！");
                ed.WriteMessage($"\n  原始线段：{originalCount}");
                ed.WriteMessage($"\n  最终线段：{finalLines.Count}");
                ed.WriteMessage($"\n  合并数量：{originalCount - finalLines.Count}");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n堆栈跟踪：{ex.StackTrace}");
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
        /// 确保警告图层存在
        /// </summary>
        private void EnsureWarningLayerExists(Transaction tr, Database db)
        {
            var lt = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;
            
            if (!lt.Has(WARNING_LAYER))
            {
                var ltr = new LayerTableRecord
                {
                    Name = WARNING_LAYER,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                        Autodesk.AutoCAD.Colors.ColorMethod.ByAci, WARNING_COLOR)
                };
                
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }

        /// <summary>
        /// 创建警告矩形标记
        /// </summary>
        private void CreateWarningRectangle(
            Transaction tr, 
            BlockTableRecord btr,
            Point2D center,
            Vector2D direction)
        {
            const double length = 20 * DEFAULT_EXTENSION_DISTANCE;
            const double width = length / 2;

            // 转换为AutoCAD类型
            var center3d = center.ToAcadPoint3d();
            var direction3d = direction.ToAcadVector3d();

            // 计算垂直方向
            var perpendicular = direction3d.RotateBy(Math.PI / 2, Vector3d.ZAxis);

            // 计算矩形四个顶点
            var p1 = center3d + perpendicular * (width / 2) - direction3d * (length / 2);
            var p2 = center3d + perpendicular * (width / 2) + direction3d * (length / 2);
            var p3 = center3d - perpendicular * (width / 2) + direction3d * (length / 2);
            var p4 = center3d - perpendicular * (width / 2) - direction3d * (length / 2);

            // 创建多段线
            var rect = new Polyline();
            rect.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
            rect.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
            rect.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
            rect.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
            rect.Closed = true;
            rect.Layer = WARNING_LAYER;

            btr.AppendEntity(rect);
            tr.AddNewlyCreatedDBObject(rect, true);
        }
    }
}