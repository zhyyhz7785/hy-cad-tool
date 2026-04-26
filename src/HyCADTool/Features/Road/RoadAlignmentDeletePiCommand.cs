using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadAlnDeletePi</c> — 从已有平面线位的 PI 表中删除一个内部 PI。
    ///
    /// 交互流程：
    /// 1. 拾取 Alignment Polyline（共享 <see cref="RoadAlignmentPiPipeline.PickAlignment"/>）；
    /// 2. 打印 PI 表；
    /// 3. 提示输入 PI 序号 [1..count-2]（首尾禁止删）；
    /// 4. 额外提示 Y/N 确认（删 PI 会永久改变几何）；
    /// 5. 共享尾部 <see cref="RoadAlignmentPiPipeline.RebuildAndPersist"/>。
    /// </summary>
    public sealed class RoadAlignmentDeletePiCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            if (!RoadAlignmentPiPipeline.PickAlignment(doc, out var alignment, out var elements, out _))
                return;

            if (elements.Count <= 2)
            {
                ed.WriteMessage("\n[道路] PI 数量不足，至少需要 3 个才能删除内部 PI。");
                return;
            }

            RoadAlignmentPiPipeline.PrintPiTable(ed, elements);

            int idx = PromptDeleteIndex(ed, elements.Count);
            if (idx < 0) return;

            if (!ConfirmDelete(ed, idx)) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            var deleted = elements[idx];
            elements.RemoveAt(idx);

            string head = $"已删除 PI[{idx}] ({deleted.P.X:F3}, {deleted.P.Y:F3})，"
                        + $"R={deleted.Radius:F2}，重建平面线位。";
            RoadAlignmentPiPipeline.RebuildAndPersist(doc, alignment, elements, head);
        }

        private static int PromptDeleteIndex(Editor ed, int count)
        {
            if (count == 3)
            {
                // 只剩一个可删
                return 1;
            }
            var opt = new PromptIntegerOptions(
                $"\n[道路] 输入要删除的 PI 序号 [1..{count - 2}]（首尾不可删）：")
            {
                LowerLimit = 1,
                UpperLimit = count - 2,
                AllowNone = false,
                AllowNegative = false,
                AllowZero = false,
            };
            var res = ed.GetInteger(opt);
            if (res.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return -1; }
            return res.Value;
        }

        private static bool ConfirmDelete(Editor ed, int idx)
        {
            var opt = new PromptKeywordOptions($"\n[道路] 确认删除 PI[{idx}]？[是(Y)/否(N)] <否>：")
            {
                AllowNone = true,
            };
            opt.Keywords.Add("Yes", "Y", "是(Y)");
            opt.Keywords.Add("No", "N", "否(N)");
            opt.Keywords.Default = "No";
            var res = ed.GetKeywords(opt);
            if (res.Status != PromptStatus.OK && res.Status != PromptStatus.None) return false;
            if (res.Status == PromptStatus.None) return false;
            return string.Equals(res.StringResult, "Yes", System.StringComparison.Ordinal);
        }
    }
}
