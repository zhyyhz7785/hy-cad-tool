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
        private const double DEFAULT_TOLERANCE = 1e-6;
        private const double MIN_EXTENSION_DISTANCE = 10.0;  // 最小延伸距离
        private const double MAX_EXTENSION_DISTANCE = 500.0; // 最大延伸距离
        private const string WARNING_LAYER = "00_HY_警告_红色";
        private const string MARKER_LAYER = "00_HY_临时标记";  // 临时标记图层
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
        /// HYOV 命令 - 基础版：只做 OVERKILL 清理
        /// </summary>
        [CommandMethod("HYOV")]
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n=== HYOV 命令（基础 OVERKILL 清理） ===");
                
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

                // 3. FILLET 第一步：打断相交直线，删除短线段
                var domainLines = lineData.Select(x => x.DomainLine).ToList();
                var brokenLines = _overKillService.BreakAndCleanLines(domainLines, DEFAULT_TOLERANCE, minLength: 1.0);
                ed.WriteMessage($"\n第1步-打断并清理：{originalCount} → {brokenLines.Count} 线段");
                
                // 4. FILLET 第二步：延伸端点距离很近的线段
                var extendedLines = _overKillService.ExtendNearEndpoints(brokenLines, DEFAULT_TOLERANCE, maxDistance: 10.0);
                ed.WriteMessage($"\n第2步-端点延伸：{brokenLines.Count} → {extendedLines.Count} 线段");
                
                // 5. FILLET 第三步：端点延伸到线段并打断
                var extendedToLineLines = _overKillService.ExtendEndpointToLine(extendedLines, DEFAULT_TOLERANCE, maxDistance: 10.0);
                ed.WriteMessage($"\n第3步-端点到线：{extendedLines.Count} → {extendedToLineLines.Count} 线段");
                
                // 6. 最终清理：删除完全重复的线段
                var cleanedLines = _overKillService.RemoveDuplicateLines(extendedToLineLines, DEFAULT_TOLERANCE);
                
                // 7. 更新图纸
                UpdateLines(doc, db, lineData, cleanedLines);
                
                // 8. 输出结果
                ed.WriteMessage($"\n第4步-删除重复：{extendedToLineLines.Count} → {cleanedLines.Count} 线段");
                ed.WriteMessage($"\n最终结果：{originalCount} → {cleanedLines.Count} 线段");
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

    }
}