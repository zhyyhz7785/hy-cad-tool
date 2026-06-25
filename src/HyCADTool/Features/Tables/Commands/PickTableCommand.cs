using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.Tables.TableApp;

namespace HyCADTool.Features.Tables.Commands
{
    /// <summary>
    /// AC7：拾取 HyTable 实体，输出摘要并可选再渲染（N35）。
    /// </summary>
    public sealed class PickTableCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            var ed = doc.Editor;
            var pickService = new TablePickService(doc.Database);
            var result = pickService.TryPickSummary(ed);
            if (result == null)
                return;

            TableSummaryWriter.WriteSummary(ed, result.Summary);

            if (!result.Summary.InvariantsValid)
            {
                ed.WriteMessage("\n[HyTable] 不变量未通过，跳过再渲染。");
                return;
            }

            TableSummaryWriter.WriteRerenderPrompt(ed);
            var pko = new PromptKeywordOptions(string.Empty)
            {
                AllowNone = true,
            };
            pko.Keywords.Add("再渲染");
            pko.Keywords.Add("R");
            pko.Keywords.Add("否");
            pko.Keywords.Add("N");
            pko.Keywords.Default = "否";

            var pkr = ed.GetKeywords(pko);
            if (pkr.Status != PromptStatus.OK)
                return;

            if (!IsRerenderKeyword(pkr.StringResult))
                return;

            var ppr = ed.GetPoint("\n指定新表左上角: ");
            if (ppr.Status != PromptStatus.OK)
                return;

            var handle = pickService.RerenderAt(result.Grid, ppr.Value);
            ed.WriteMessage(
                $"\n[HyTable] 已再渲染 {handle.EntityCount} 个实体 @ ({ppr.Value.X:F1}, {ppr.Value.Y:F1})；" +
                $"新 TableId={handle.TableId:D}");
        }

        private static bool IsRerenderKeyword(string keyword)
        {
            return string.Equals(keyword, "再渲染", System.StringComparison.Ordinal)
                || string.Equals(keyword, "R", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
