using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCADTool.Features.Tables.Infrastructure.AutoCad;

namespace HyCADTool.Features.Tables.Commands
{
    /// <summary>
    /// AC4：点选 HyTable 实体读回 TableGrid 并校验（N34）。
    /// </summary>
    public sealed class ReadTableBackCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            var ed = doc.Editor;
            var peo = new PromptEntityOptions("\n选择 HyTable 表格实体（外框/网格线/文字/组）: ")
            {
                AllowNone = false,
            };

            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
                return;

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var picked = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                if (!AcadTableStore.TryResolveTableGrid(tr, doc.Database, picked, out var grid, out var error))
                {
                    ed.WriteMessage("\n[HyTable] 读回失败：" + (error ?? "未知错误"));
                    return;
                }

                var topology = grid.Structure.Topology;
                var report = GridInvariants.Validate(grid);

                ed.WriteMessage("\n[HyTable] 读回成功");
                ed.WriteMessage(
                    $"\n  行×列: {topology.RowCount}×{topology.ColCount}");
                ed.WriteMessage(
                    $"\n  合并区域: {grid.Structure.Merges.Count}");
                ed.WriteMessage(
                    $"\n  FieldKey 数: {grid.Structure.FieldIndex.Count}");
                ed.WriteMessage(
                    $"\n  GridInvariants: {(report.IsValid ? "通过" : "失败")}");

                if (!report.IsValid)
                {
                    foreach (var violation in report.Violations.Take(3))
                        ed.WriteMessage("\n    - " + violation.Message);
                }

                if (grid.Structure.FieldIndex.TryGetValue("name", out var nameAddr))
                {
                    var nameValue = FormatCellValue(GridEditor.GetValue(grid, nameAddr));
                    ed.WriteMessage("\n  name = " + nameValue);
                }

                ed.WriteMessage("\n  TableId: " + grid.Id.ToString("D"));

                tr.Commit();
            }
        }

        private static string FormatCellValue(CellValue value)
        {
            if (value.Runs != null && value.Runs.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var run in value.Runs)
                    sb.Append(run.Text);
                return sb.ToString();
            }

            return value.Text ?? string.Empty;
        }
    }
}
