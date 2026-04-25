using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Presentation.Commands.Base;
using System.Threading.Tasks;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.DimensionForReinforcement
{
    /// <summary>
    /// 配筋尺寸标注命令（旧 dds — 单个多段线）
    /// 选择一条多段线，自动生成内外尺寸标注
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

                Polyline poly;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    poly = tr.GetObject(result.ObjectId, OpenMode.ForRead) as Polyline;
                    tr.Commit();
                }

                if (poly == null)
                {
                    ed.WriteMessage("\n选择的不是多段线。");
                    return;
                }

                var service = new DimensionForReinforcementService();
                service.GenerateDimension(poly);
                ed.WriteMessage("\n标注完成。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }
    }

    /// <summary>
    /// 配筋尺寸标注批量命令（旧 ddss — 多个多段线）
    /// 选择多条多段线，逐一生成内外尺寸标注
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
                int count = 0;

                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selObj in selResult.Value)
                    {
                        var poly = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
                        if (poly != null)
                        {
                            service.GenerateDimension(poly);
                            count++;
                        }
                    }
                    tr.Commit();
                }

                ed.WriteMessage($"\n已完成 {count} 条多段线标注。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
            }
        }
    }
}
