using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.AcadDimension.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.AcadDimension.Commands
{
    /// <summary>
    /// nddss：批量智能断面标注（NewDDS）。
    /// 范式与计划同 NewDdsCommand；逐条调 NewDdsService.Execute(ObjectId)，
    /// 每条独立事务边界——单条失败不影响其他条目（修 06 §7 #9 单事务粒度）。
    /// </summary>
    public sealed class NewDdsBatchCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });
            var opts = new PromptSelectionOptions { MessageForAdding = "\n[NewDDS] 选择多段线:" };

            var sel = ed.GetSelection(opts, filter);
            if (sel.Status != PromptStatus.OK || sel.Value.Count == 0)
            {
                ed.WriteMessage("\n[NewDDS] 未选择多段线。");
                return;
            }

            var service = new NewDdsService();
            int total = sel.Value.Count;
            int idx = 0;
            foreach (SelectedObject so in sel.Value)
            {
                idx++;
                service.Execute(so.ObjectId);
            }

            ed.WriteMessage($"\n[NewDDS] 批量完成 {total} 条。");
        }
    }
}
