using System.Linq;
using Autodesk.AutoCAD.EditorInput;

namespace HyCADTool.Features.Tables.TableApp
{
    /// <summary>
    /// AC7 摘要命令行输出（固定模板）。
    /// </summary>
    internal static class TableSummaryWriter
    {
        public static void WriteSummary(Editor ed, TableSummary summary)
        {
            ed.WriteMessage("\n[HyTable] ── 表格摘要 ──");
            ed.WriteMessage("\n  标题: " + (summary.DisplayTitle ?? string.Empty));
            ed.WriteMessage("\n  ID:   " + summary.TableId.ToString("D"));
            ed.WriteMessage(
                $"\n  规模: {summary.RowCount} 行 × {summary.ColCount} 列 | " +
                $"可见格 {summary.VisibleCellCount} | 合并区 {summary.MergeRegionCount}");
            ed.WriteMessage("\n  字段: " + FormatFieldKeys(summary));
            ed.WriteMessage(
                $"\n  公式: {summary.FormulaCellCount} | 绑定: {summary.BoundCellCount}");

            if (summary.InvariantsValid)
                ed.WriteMessage("\n  不变量: 通过");
            else
                ed.WriteMessage("\n  不变量: 失败 — " + (summary.FirstInvariantMessage ?? "未知"));

            if (!string.IsNullOrEmpty(summary.CarrierHandle))
                ed.WriteMessage("\n  载体: Polyline Handle=" + summary.CarrierHandle);
        }

        public static void WriteRerenderPrompt(Editor ed)
        {
            ed.WriteMessage("\n[HyTable] 再渲染? [是(R)/否(N)] <N>: ");
        }

        private static string FormatFieldKeys(TableSummary summary)
        {
            if (summary.FieldKeyCount == 0)
                return "0 个";

            var listed = summary.FieldKeys ?? System.Array.Empty<string>();
            if (summary.FieldKeyCount <= listed.Count)
                return summary.FieldKeyCount + " 个 (" + string.Join(", ", listed) + ")";

            var extra = summary.FieldKeyCount - listed.Count;
            return summary.FieldKeyCount + " 个 (" + string.Join(", ", listed) + ", … +" + extra + ")";
        }
    }
}
