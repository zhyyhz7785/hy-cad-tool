using HyCADTool.Features.AcadDimension.Tests;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.AcadDimension.Commands
{
    /// <summary>
    /// nddsT：跑 Phase 7 纯 Domain 金标，命令行打印每个 fixture 的 snapshot 与 baseline 差异。
    /// 不依赖 AutoCAD 选择 / 事务；可在任意时机调用，作为重构的快速回归通道。
    /// </summary>
    public sealed class NewDdsGoldenCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            if (ed == null) return;

            var runner = new NewDdsGoldenRunner();
            var snapshots = runner.RunAll();

            int total = snapshots.Count;
            int frozen = 0, mismatch = 0;
            ed.WriteMessage($"\n[NewDDS-Golden] 共 {total} 张 fixture：");
            foreach (var snap in snapshots)
            {
                ed.WriteMessage("\n  " + snap.ToShortLine());
                NewDdsGoldenBaseline.Map.TryGetValue(snap.Fixture, out var baseline);
                var diffs = NewDdsGoldenComparer.Compare(baseline, snap);
                if (baseline == null)
                {
                    ed.WriteMessage($"\n    BASELINE NOT SET（粘贴回 NewDdsGoldenBaseline.Map）");
                }
                else if (diffs.Count == 0)
                {
                    frozen++;
                    ed.WriteMessage("\n    OK 与基线一致");
                }
                else
                {
                    mismatch++;
                    foreach (var d in diffs)
                        ed.WriteMessage("\n    DIFF " + d);
                }
            }
            ed.WriteMessage($"\n[NewDDS-Golden] 冻结一致={frozen} 差异={mismatch} " +
                $"未设基线={total - frozen - mismatch}");
        }
    }
}
