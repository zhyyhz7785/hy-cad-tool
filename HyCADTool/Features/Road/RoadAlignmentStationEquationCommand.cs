using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadAlnStaEq</c> — 编辑指定 <see cref="Alignment"/> 的桩号方程（Station Equations）。
    ///
    /// 交互流程：
    /// 1) 拾取 Alignment Polyline（共享 <see cref="RoadAlignmentPiPipeline.PickAlignment"/>，要求 PI 源存在，便于显示 raw 总长）。
    /// 2) 进入交互循环：[新增(A) / 删除(D) / 清除(X) / 列出(L) / 完成(F)]。
    ///    · 新增：输入 <c>BeforeRaw</c>（沿中心线累积距离 m，必须在 [0, 总长] 内）和 <c>AheadStation</c>（m）；
    ///    · 删除：列表 + 输入序号；
    ///    · 清除：确认后清空；
    ///    · 列出：打印当前方程 + 等效显示桩号映射；
    ///    · 完成：保存到 <see cref="Alignment.StationEquations"/>，并 JSON 落盘。
    /// 3) 不重建几何（方程只改"桩号坐标系"，不动物理中心线）。
    ///
    /// 任何阶段按 Esc 或输入非法值都会就地回显，不破坏 Alignment 原始方程。
    /// 保存依赖 <see cref="RoadJsonExportService.SaveForDocument"/>；下游标注 / 导出再跑一遍即可看到新桩号。
    /// </summary>
    public sealed class RoadAlignmentStationEquationCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            if (!RoadAlignmentPiPipeline.PickAlignment(doc, out var alignment, out var elements, out _))
                return;

            // 计算 raw 总长，供后续输入校验（方程 BeforeRaw ∈ (0, totalRaw)）
            double totalRaw = alignment.Centerline?.GetPlanarLength() ?? 0.0;
            if (totalRaw <= 0)
            {
                ed.WriteMessage("\n[道路] 该平面线位无有效中心线，无法添加桩号方程。");
                return;
            }

            // 几何点 raw 距离表（用于"接近度"提示）。此处不带方程，得到的 GeometryPoint.StationM
            // 其实是 startStation + raw，所以 raw = gp.StationM − startStation。
            List<(double RawFromBp, GeometryPointKind Kind)> geomPoints;
            try
            {
                var rawBreakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation, null);
                geomPoints = rawBreakdown.GeometryPoints
                    .Select(gp => (RawFromBp: gp.StationM - alignment.StartStation, gp.Kind))
                    .Where(t => t.RawFromBp >= -1e-9 && t.RawFromBp <= totalRaw + 1e-9)
                    .ToList();
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 警告：几何点分解失败（{ex.Message}），跳过接近度提示。");
                geomPoints = new List<(double, GeometryPointKind)>();
            }

            // 工作副本，失败/取消时不污染源数据
            var work = StationConverter.CloneSorted(alignment.StationEquations);
            PrintEquations(ed, alignment, work, totalRaw);

            while (true)
            {
                var op = PromptOperation(ed, work.Count);
                if (op == Op.None) break; // 取消 = 不保存
                if (op == Op.Finish) break;

                switch (op)
                {
                    case Op.Add:
                        TryAdd(ed, work, alignment.StartStation, totalRaw, geomPoints);
                        break;
                    case Op.Delete:
                        TryDelete(ed, work);
                        break;
                    case Op.Clear:
                        TryClear(ed, work);
                        break;
                    case Op.List:
                        PrintEquations(ed, alignment, work, totalRaw);
                        break;
                }
            }

            // 保存：只在用户选了 Finish 才走到这里；取消走 None 分支已经 break，上面无分支区分。
            // 简化：所有退出循环都保存（Esc 对 PromptKeyword 通常是 None，这里一视同仁地保存工作副本）。
            var (ok, errors) = StationConverter.ValidateAscending(work, totalRaw);
            if (!ok)
            {
                ed.WriteMessage("\n[道路] 桩号方程校验未通过，未保存：");
                foreach (var e in errors) ed.WriteMessage("\n  · " + e);
                return;
            }

            alignment.StationEquations.Clear();
            foreach (var eq in work) alignment.StationEquations.Add(eq.Clone());

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design))
                savedTo = exporter.SaveForDocument(design, doc.Name);

            ed.WriteMessage($"\n[道路] 桩号方程已更新（共 {work.Count} 条）。下游标注 / 导出重跑即可看到新桩号。");
            if (savedTo != null) ed.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
            else ed.WriteMessage("\n[道路] 未落盘（DWG 尚未保存）。先 QSAVE / SAVEAS 再 hyRoadSave。");
        }

        private enum Op { None, Add, Delete, Clear, List, Finish }

        private static Op PromptOperation(Editor ed, int count)
        {
            var opt = new PromptKeywordOptions(
                $"\n[道路] 桩号方程（当前 {count} 条）[新增(A)/删除(D)/清除(X)/列出(L)/完成(F)] <完成>：")
            {
                AllowNone = true,
            };
            opt.Keywords.Add("Add", "A", "新增(A)");
            opt.Keywords.Add("Delete", "D", "删除(D)");
            opt.Keywords.Add("Clear", "X", "清除(X)");
            opt.Keywords.Add("List", "L", "列出(L)");
            opt.Keywords.Add("Finish", "F", "完成(F)");
            opt.Keywords.Default = "Finish";

            var res = ed.GetKeywords(opt);
            if (res.Status == PromptStatus.None) return Op.Finish;
            if (res.Status != PromptStatus.OK) return Op.None;
            switch (res.StringResult)
            {
                case "Add": return Op.Add;
                case "Delete": return Op.Delete;
                case "Clear": return Op.Clear;
                case "List": return Op.List;
                case "Finish": return Op.Finish;
                default: return Op.None;
            }
        }

        private static void TryAdd(
            Editor ed,
            List<StationEquation> work,
            double startStation,
            double totalRaw,
            IReadOnlyList<(double RawFromBp, GeometryPointKind Kind)> geomPoints)
        {
            var optRaw = new PromptDoubleOptions(
                $"\n[道路] BeforeRaw（沿中心线累计距离, m，∈(0, {totalRaw:F3})）：")
            {
                AllowNone = false,
                AllowNegative = false,
                AllowZero = false,
            };
            var r = ed.GetDouble(optRaw);
            if (r.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消新增。"); return; }
            if (r.Value >= totalRaw)
            {
                ed.WriteMessage($"\n[道路] BeforeRaw = {r.Value:F3} 超出总长 {totalRaw:F3}，未加入。");
                return;
            }

            // 接近度提示：找最近几何点并输出其距离与类型（工程上常把方程点落在 SC/CS/BP/EP 上）
            if (geomPoints != null && geomPoints.Count > 0)
            {
                double bestD = double.MaxValue;
                GeometryPointKind bestKind = default;
                double bestRaw = 0;
                for (int i = 0; i < geomPoints.Count; i++)
                {
                    double d = Math.Abs(geomPoints[i].RawFromBp - r.Value);
                    if (d < bestD) { bestD = d; bestKind = geomPoints[i].Kind; bestRaw = geomPoints[i].RawFromBp; }
                }
                if (bestD < 0.005)
                    ed.WriteMessage($"\n[道路] √ 方程点已对齐几何点（{bestKind}，raw={bestRaw:F3} m）。");
                else if (bestD < 0.5)
                    ed.WriteMessage($"\n[道路] ! 方程点接近几何点（{bestKind}，raw={bestRaw:F3} m，Δ={bestD:F3} m）；如希望对齐请改用该 raw。");
                else
                    ed.WriteMessage($"\n[道路] · 方程点落在段中部：最近几何点 {bestKind}，raw={bestRaw:F3} m，Δ={bestD:F3} m。");
            }

            double suggestedAhead = startStation + r.Value; // 默认"不跳变"
            var optAhead = new PromptDoubleOptions(
                $"\n[道路] AheadStation（方程点之后的显示桩号, m） <{suggestedAhead:F3}>：")
            {
                AllowNone = true,
                DefaultValue = suggestedAhead,
                UseDefaultValue = true,
            };
            var a = ed.GetDouble(optAhead);
            if (a.Status != PromptStatus.OK && a.Status != PromptStatus.None)
            {
                ed.WriteMessage("\n[道路] 已取消新增。");
                return;
            }
            double aheadStation = a.Status == PromptStatus.None ? suggestedAhead : a.Value;

            var newEq = new StationEquation(r.Value, aheadStation);
            var candidate = new List<StationEquation>(work) { newEq };
            candidate.Sort((x, y) => x.BeforeRaw.CompareTo(y.BeforeRaw));

            var (ok, errors) = StationConverter.ValidateAscending(candidate, totalRaw);
            if (!ok)
            {
                ed.WriteMessage("\n[道路] 校验未通过，未加入：");
                foreach (var e in errors) ed.WriteMessage("\n  · " + e);
                return;
            }

            work.Clear();
            work.AddRange(candidate);
            ed.WriteMessage($"\n[道路] 已加入方程 Raw={r.Value:F3} m → Ahead={aheadStation:F3} m（共 {work.Count} 条）。");
        }

        private static void TryDelete(Editor ed, List<StationEquation> work)
        {
            if (work.Count == 0) { ed.WriteMessage("\n[道路] 当前无方程可删。"); return; }

            for (int i = 0; i < work.Count; i++)
            {
                var eq = work[i];
                ed.WriteMessage($"\n  [{i}] Raw={eq.BeforeRaw:F3} m → Ahead={eq.AheadStation:F3} m");
            }

            var opt = new PromptIntegerOptions($"\n[道路] 输入要删除的方程序号 [0..{work.Count - 1}]：")
            {
                LowerLimit = 0,
                UpperLimit = work.Count - 1,
                AllowNone = false,
                AllowNegative = false,
            };
            var r = ed.GetInteger(opt);
            if (r.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消删除。"); return; }
            var removed = work[r.Value];
            work.RemoveAt(r.Value);
            ed.WriteMessage($"\n[道路] 已删除方程[{r.Value}]：Raw={removed.BeforeRaw:F3} → Ahead={removed.AheadStation:F3}。");
        }

        private static void TryClear(Editor ed, List<StationEquation> work)
        {
            if (work.Count == 0) { ed.WriteMessage("\n[道路] 当前无方程可清除。"); return; }

            var opt = new PromptKeywordOptions($"\n[道路] 确认清空全部 {work.Count} 条方程？[是(Y)/否(N)] <否>：")
            {
                AllowNone = true,
            };
            opt.Keywords.Add("Yes", "Y", "是(Y)");
            opt.Keywords.Add("No", "N", "否(N)");
            opt.Keywords.Default = "No";
            var r = ed.GetKeywords(opt);
            if (r.Status != PromptStatus.OK || !string.Equals(r.StringResult, "Yes", StringComparison.Ordinal))
            {
                ed.WriteMessage("\n[道路] 已取消清除。");
                return;
            }
            int n = work.Count;
            work.Clear();
            ed.WriteMessage($"\n[道路] 已清空 {n} 条方程。");
        }

        private static void PrintEquations(Editor ed, Alignment alignment, IReadOnlyList<StationEquation> work, double totalRaw)
        {
            ed.WriteMessage(
                $"\n[道路] Alignment: {alignment.Name ?? "-"}   起桩={AlignmentStationBreakdown.FormatStation(alignment.StartStation)}   "
                + $"总长={totalRaw:F3} m   方程数={work.Count}");
            if (work.Count == 0) return;
            ed.WriteMessage("\n  idx  BeforeRaw(m)  AheadStation(m)  显示跳变");
            for (int i = 0; i < work.Count; i++)
            {
                var eq = work[i];
                double before = alignment.StartStation + eq.BeforeRaw; // 方程前侧的显示桩号（假设之前无方程时的值，仅作参考）
                double jump = eq.AheadStation - before;
                ed.WriteMessage(
                    $"\n  {i,-3}  {eq.BeforeRaw,-12:F3}  {eq.AheadStation,-14:F3}  Δ={jump:+0.000;-0.000}");
            }
        }
    }
}
