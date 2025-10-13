using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
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
            
            // TODO: Application层删除后需要重新实现此命令
            // 原实现依赖于已删除的 OverKillUseCase、OverKillRequest、OverKillResult
            ed.WriteMessage("\n此命令暂时不可用，正在重构中...");
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