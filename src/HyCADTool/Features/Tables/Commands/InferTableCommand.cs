using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Tables.Inference;
using HyCADTool.Features.Tables.Infrastructure.AutoCad;
using HyCADTool.Features.Tables.TableApp;

namespace HyCADTool.Features.Tables.Commands
{
    /// <summary>
    /// AC9：框选线+文字，反推 TableGrid draft（N36）。
    /// </summary>
    public sealed class InferTableCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            var ed = doc.Editor;
            ed.WriteMessage("\n[HyTable] 框选表格线框与文字（Line/Polyline/DBText/MText）...");

            var service = new TableInferService(doc.Database);
            var result = service.InferFromSelection(ed);
            if (result == null)
                return;

            TableInferService.WriteResult(ed, result);
            if (result.Status != InferStatus.Success || result.Grid == null)
                return;

            TryWriteGoldenDiff(ed, doc.Database, result);

            if (!TableInferService.PromptConfirmRender(ed))
            {
                ed.WriteMessage("\n[HyTable] 已取消，未写入 DWG。");
                return;
            }

            var ppr = ed.GetPoint("\n指定新表左上角: ");
            if (ppr.Status != PromptStatus.OK)
                return;

            var handle = service.ConfirmAndRender(result.Grid, ppr.Value);
            ed.WriteMessage(
                $"\n[HyTable] 已渲染 {handle.EntityCount} 个实体 @ ({ppr.Value.X:F1}, {ppr.Value.Y:F1})；" +
                $"TableId={handle.TableId:D}");
        }

        private static void TryWriteGoldenDiff(
            Editor ed,
            Autodesk.AutoCAD.DatabaseServices.Database database,
            TableInferResult result)
        {
            var peo = new PromptEntityOptions("\n[HyTable] 可选：选择 HY_TABLE 载体对比 JSON（Esc 跳过）: ")
            {
                AllowNone = true,
            };

            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
                return;

            using (var tr = database.TransactionManager.StartTransaction())
            {
                var picked = tr.GetObject(per.ObjectId, Autodesk.AutoCAD.DatabaseServices.OpenMode.ForRead);
                if (!AcadTableStore.TryResolveTableGrid(tr, database, picked, out var golden, out var error))
                {
                    ed.WriteMessage("\n[HyTable] 读回 golden 失败：" + (error ?? "未知错误"));
                    return;
                }

                var diff = TableInferDiff.Compare(result.Grid!, golden);
                TableInferService.WriteDiff(ed, diff);
                tr.Commit();
            }
        }
    }
}
