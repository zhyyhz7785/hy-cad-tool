using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Shell.Commands.Base;
using System.Threading.Tasks;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.DimensionForReinforcement
{
    /// <summary>
    /// 配筋尺寸标注命令（旧 dds — 单个多段线）
    /// </summary>
    public class DimensionForReinforcementCommand : ICommand
    {
        public string Name => "DimensionForReinforcement";

        public Task<bool> ExecuteAsync()
        {
            Execute();
            return Task.FromResult(true);
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var opts = new PromptEntityOptions("\n选择多段线:");
                opts.SetRejectMessage("\n请选择多段线。");
                opts.AddAllowedClass(typeof(Polyline), true);
                var result = ed.GetEntity(opts);
                if (result.Status != PromptStatus.OK) return;

                var service = new DimensionForReinforcementService();
                if (!service.GenerateDimension(result.ObjectId))
                    ed.WriteMessage("\n标注未完成。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }
    }

    /// <summary>
    /// 配筋尺寸标注批量命令（旧 ddss — 多个多段线）
    /// </summary>
    public class DimensionForReinforcementBatchCommand : ICommand
    {
        public string Name => "DimensionForReinforcementBatch";

        public Task<bool> ExecuteAsync()
        {
            Execute();
            return Task.FromResult(true);
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
                });

                var opts = new PromptSelectionOptions { MessageForAdding = "\n选择多段线:" };
                var selResult = ed.GetSelection(opts, filter);
                if (selResult.Status != PromptStatus.OK || selResult.Value.Count == 0)
                {
                    ed.WriteMessage("\n未选择多段线。");
                    return;
                }

                var service = new DimensionForReinforcementService();
                int success = 0;
                int failed = 0;

                foreach (SelectedObject selObj in selResult.Value)
                {
                    try
                    {
                        if (service.GenerateDimension(selObj.ObjectId, writeSummary: false))
                            success++;
                        else
                            failed++;
                    }
                    catch (System.Exception ex)
                    {
                        failed++;
                        ed.WriteMessage($"\n  跳过：{ex.Message}");
                    }
                }

                ed.WriteMessage($"\n批量标注完成：成功 {success}，失败/跳过 {failed}。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }
    }
}
