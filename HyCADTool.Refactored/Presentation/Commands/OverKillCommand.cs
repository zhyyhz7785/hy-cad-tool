using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Application.DTOs.OverKill;
using HyCADTool.Refactored.Application.UseCases.OverKill;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Repositories;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.OverKillCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// OverKill 命令
    /// 处理重叠线段的合并、端点延伸、警告标记
    /// </summary>
    public class OverKillCommand
    {
        [CommandMethod("HYOV")]
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;
            
            try
            {
                // 步骤 1: 用户选择线段
                var selectionResult = GetLineSelection(ed);
                if (selectionResult == null)
                {
                    ed.WriteMessage("\n未选择任何线段，命令取消。");
                    return;
                }
                
                // 步骤 2: 从 DI 容器获取服务
                var useCase = ServiceLocator.Resolve<OverKillUseCase>();
                var repository = ServiceLocator.Resolve<ILineRepository>();
                
                // 步骤 3: 获取选中的线段
                var selectedLines = repository.GetSelectedLines(selectionResult.Value);
                
                if (selectedLines.Count == 0)
                {
                    ed.WriteMessage("\n未找到有效的线段。");
                    return;
                }
                
                ed.WriteMessage($"\n选中 {selectedLines.Count} 条线段，开始处理...");
                
                // 步骤 4: 执行用例
                var request = new OverKillRequest
                {
                    Lines = selectedLines.Select(l => l.Line).ToList(),
                    Tolerance = 1e-6,
                    ExtensionDistance = 10.0
                };
                
                var result = useCase.Execute(request);
                
                if (!result.Success)
                {
                    ed.WriteMessage($"\n{result.Message}");
                    return;
                }
                
                // 步骤 5: 更新图纸
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // 替换线段
                        repository.ReplaceLines(
                            tr,
                            selectedLines.Select(l => l.OriginalId).ToList(),
                            result.ProcessedLines);
                        
                        // 创建警告标记
                        if (result.WarningMarkers.Count > 0)
                        {
                            repository.CreateWarningMarkers(tr, result.WarningMarkers);
                        }
                        
                        tr.Commit();
                        ed.WriteMessage($"\n{result.Message}");
                        
                        if (result.WarningMarkers.Count > 0)
                        {
                            ed.WriteMessage($"\n警告：{result.WarningMarkers.Count} 个端点无法连接，已标记。");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n更新图纸失败: {ex.Message}");
                        tr.Abort();
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n命令执行失败: {ex.Message}");
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
    }
}

