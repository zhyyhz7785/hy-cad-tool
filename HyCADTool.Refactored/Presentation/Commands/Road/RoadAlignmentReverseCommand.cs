using System;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Services.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadAlnReverse</c> — 反转已有 <see cref="Domain.Models.Road.Alignment"/> 的前进方向。
    ///
    /// 交互流程：
    /// 1) 拾取 Alignment Polyline（共享 <see cref="RoadAlignmentPiPipeline.PickAlignment"/>）。
    /// 2) Y/N 确认（反转会改变桩号方向并清空桩号方程，需要用户明确授权）。
    /// 3) PI 表反转：
    ///    · 序列整体倒排；
    ///    · 每个 PI 的 <see cref="Domain.Services.Road.PiElement.SpiralIn"/> 与 <see cref="Domain.Services.Road.PiElement.SpiralOut"/> 互换
    ///      （沿反方向前进时"入侧"变"出侧"）；
    ///    · Tag 保持原值（用户可自行改名）。
    /// 4) 桩号方程"几何镜像"：<c>BeforeRaw' = totalRaw − BeforeRaw</c>，<c>AheadStation</c> 保留，
    ///    镜像后落在端点外的方程自动剔除；连续两次反转复原（可逆）。
    /// 5) 共享尾部 <see cref="RoadAlignmentPiPipeline.RebuildAndPersist"/>：
    ///    Build → RebuildCenterline → Source → JSON → Validate。
    ///
    /// <b>工程警示</b>：原 DWG 上的桩号标注 / 几何点标注仍是旧方向的，需要用户再跑一次
    /// <c>hyRoadAlnStation</c> / <c>hyRoadAlnGeomPt</c>（均幂等）才能更新。
    /// </summary>
    public sealed class RoadAlignmentReverseCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            if (!RoadAlignmentPiPipeline.PickAlignment(doc, out var alignment, out var elements))
                return;

            if (!ConfirmReverse(ed, alignment)) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            var reversed = AlignmentReverser.ReversePiElements(elements);

            // 方程镜像：需要在"原几何"上取 totalRaw（反转后 totalRaw 不变，因为物理长度一样）
            double totalRaw = alignment.Centerline?.GetPlanarLength() ?? 0.0;
            int oldCount = alignment.StationEquations?.Count ?? 0;
            int newCount = 0;
            int droppedCount = 0;
            if (totalRaw > 0 && oldCount > 0)
            {
                var mapped = AlignmentReverser.ReverseStationEquations(alignment.StationEquations, totalRaw);
                newCount = mapped.Count;
                droppedCount = oldCount - newCount;
                alignment.StationEquations.Clear();
                foreach (var eq in mapped) alignment.StationEquations.Add(eq);
            }

            string eqMsg = oldCount == 0
                ? "无桩号方程"
                : $"桩号方程已镜像：{oldCount} → {newCount}"
                  + (droppedCount > 0 ? $"（{droppedCount} 条端点外方程被剔除）" : string.Empty);
            string head = $"已反转平面线位方向（{elements.Count} PI，{eqMsg}）。"
                        + "\n[道路]   · 桩号 / 几何点标注请重新运行 hyRoadAlnStation / hyRoadAlnGeomPt 更新。";
            RoadAlignmentPiPipeline.RebuildAndPersist(doc, alignment, reversed, head);
        }

        private static bool ConfirmReverse(Editor ed, Domain.Models.Road.Alignment alignment)
        {
            int eqCount = alignment.StationEquations?.Count ?? 0;
            string eqWarn = eqCount > 0 ? $"（{eqCount} 条桩号方程将按几何镜像）" : string.Empty;
            var opt = new PromptKeywordOptions(
                $"\n[道路] 确认反转 Alignment[{alignment.Name ?? "-"}] 方向？{eqWarn} [是(Y)/否(N)] <否>：")
            {
                AllowNone = true,
            };
            opt.Keywords.Add("Yes", "Y", "是(Y)");
            opt.Keywords.Add("No", "N", "否(N)");
            opt.Keywords.Default = "No";
            var res = ed.GetKeywords(opt);
            if (res.Status != PromptStatus.OK && res.Status != PromptStatus.None) return false;
            if (res.Status == PromptStatus.None) return false;
            return string.Equals(res.StringResult, "Yes", StringComparison.Ordinal);
        }
    }
}
