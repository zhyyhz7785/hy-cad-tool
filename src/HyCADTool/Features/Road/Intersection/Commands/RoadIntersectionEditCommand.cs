using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using HyCADTool.Features.Road.PlanAlignment.Services;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCADTool.Features.Road.PlanAlignment.Commands;
using HyCADTool.Features.Road.CrossSection.Commands;
namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadIntersectionEdit</c> — 对已存在的交叉口做 <b>局部编辑</b>。
    ///
    /// <para><b>支持的子操作</b></para>
    /// <list type="bullet">
    /// <item><b>R (Radius)</b> —— 修改 <b>单个 CornerArc</b> 的半径（用户先拾取那条 Arc 实体）；</item>
    /// <item><b>W (halfWidth)</b> —— 修改 <b>单条 Leg</b> 的半宽（用户拾取的 Arc 反查其相邻两 Leg，再二选一）；</item>
    /// <item><b>V (designSpeed)</b> —— 修改交叉口设计速度（仅影响下次校核警告，不改几何）；</item>
    /// <item><b>X</b> —— 退出。</item>
    /// </list>
    ///
    /// <para><b>交互流程</b></para>
    /// <list type="number">
    /// <item>拾取目标交叉口的 <b>CornerArc Arc</b>（也支持拾取 CurbRamp / TactilePaving 再按 ID 反查交叉口；但修改 R 需要 Arc 本身）；</item>
    /// <item>选择子操作关键字；</item>
    /// <item>输入新值（带默认当前值）；</item>
    /// <item>Designer 局部重算 → <see cref="RoadIntersectionService.RebuildIntersection"/> +
    /// （如已有 CurbRamp / TactilePaving）<see cref="RoadAccessibilityService.RebuildAccessibility"/>；</item>
    /// <item>JSON 落盘。</item>
    /// </list>
    ///
    /// <para><b>与 hyRoadIntersection 的区别</b></para>
    /// - <c>hyRoadIntersection</c>：从 Alignment 全量构造新交叉口（破坏性）；
    /// - <c>hyRoadIntersectionEdit</c>：保留 Legs 排序 / 其他 CornerArc，仅修改目标量（非破坏性）。
    /// </summary>
    public sealed class RoadIntersectionEditCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions("\n[道路] 拾取目标交叉口的转角弧（或坡道 / 盲道）：")
            {
                AllowNone = false,
            };
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            var opt = new PromptKeywordOptions(
                "\n[道路] 编辑项 [半径(R)/半宽(W)/设计速度(V)/退出(X)]：")
            {
                AllowNone = true,
            };
            opt.Keywords.Add("R");
            opt.Keywords.Add("W");
            opt.Keywords.Add("V");
            opt.Keywords.Add("X");
            opt.Keywords.Default = "R";
            var rKw = ed.GetKeywords(opt);
            if (rKw.Status != PromptStatus.OK) return;
            string action = rKw.StringResult ?? "R";
            if (string.Equals(action, "X", StringComparison.OrdinalIgnoreCase)) return;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var alnSvc = ServiceLocator.Resolve<RoadAlignmentService>();
            var ixSvc = ServiceLocator.Resolve<RoadIntersectionService>();
            var accSvc = ServiceLocator.Resolve<RoadAccessibilityService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                alnSvc.RebindForDocument(doc.Name, tr, db);
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ed.WriteMessage("\n[道路] 当前 DWG 没有道路设计数据。");
                    return;
                }

                var target = RoadCurbRampCommand.ResolveIntersection(tr, per.ObjectId, design);
                if (target == null)
                {
                    ed.WriteMessage("\n[道路] 未找到所属交叉口。");
                    return;
                }

                bool mutated = false;
                string summary = string.Empty;

                switch (action.ToUpperInvariant())
                {
                    case "R":
                        mutated = EditRadius(ed, tr, per.ObjectId, target, out summary);
                        break;
                    case "W":
                        mutated = EditHalfWidth(ed, tr, per.ObjectId, target, out summary);
                        break;
                    case "V":
                        mutated = EditDesignSpeed(ed, target, out summary);
                        break;
                }

                if (!mutated)
                {
                    ed.WriteMessage("\n[道路] 未应用任何修改。");
                    return;
                }

                // 几何变动 → 重画交叉口 + 无障碍设施；纯速度变动 → 只需持久化 + 再校核。
                ixSvc.RebuildIntersection(tr, db, target, HyRoadLayers.IntersectionLayer);
                if (target.CurbRamps.Count > 0 || target.TactilePavings.Count > 0)
                {
                    accSvc.RebuildAccessibility(
                        tr, db, target, HyRoadLayers.CurbRampLayer, HyRoadLayers.TactilePavingLayer);
                }

                tr.Commit();

                try
                {
                    if (registry.TryGet(doc.Name, out var designForSave))
                        exporter.SaveForDocument(designForSave, doc.Name);
                }
                catch (Exception ex) { ed.WriteMessage($"\n[道路][警告] .roaddesign.json 写盘失败：{ex.Message}"); }

                int warn = 0;
                foreach (var arc in target.CornerArcs)
                {
                    var r = IntersectionCodeChecker.CheckCornerRadius(arc.Radius, target.DesignSpeed);
                    if (!r.Pass) { ed.WriteMessage($"\n[道路][警告] {r.Message}"); warn++; }
                }

                ed.WriteMessage($"\n[道路] {summary}"
                    + (warn > 0 ? $" 规范警告 {warn} 条。" : " 规范校核全部通过。"));
            }
        }

        // ============================== 子操作 ==============================

        private static bool EditRadius(
            Editor ed, Transaction tr, ObjectId pickedId, Intersection target, out string summary)
        {
            summary = null;
            var arcEnt = tr.GetObject(pickedId, OpenMode.ForRead) as Arc;
            if (arcEnt == null)
            {
                ed.WriteMessage("\n[道路] 修改半径需要拾取转角弧（Arc）实体。");
                return false;
            }

            int idx = FindNearestCornerArcIndex(target, arcEnt);
            if (idx < 0)
            {
                ed.WriteMessage("\n[道路] 无法把拾取的 Arc 对应到 Intersection.CornerArcs。");
                return false;
            }

            double currentR = target.CornerArcs[idx].Radius;
            var opt = new PromptDoubleOptions($"\n[道路] CornerArc#{idx} 新半径 R (当前 {currentR:F2} m)：")
            {
                DefaultValue = currentR,
                UseDefaultValue = true,
                AllowNone = true,
                AllowNegative = false,
                AllowZero = false,
            };
            var r = ed.GetDouble(opt);
            if (r.Status != PromptStatus.OK) return false;
            if (Math.Abs(r.Value - currentR) < 1e-6) return false;

            if (!IntersectionDesigner.TryRebuildCornerArc(target, idx, r.Value))
            {
                ed.WriteMessage("\n[道路] 几何不可解（两臂共线 / 反向），半径未更新。");
                return false;
            }

            summary = $"CornerArc#{idx} 半径已改为 R={r.Value:F2}（原 {currentR:F2}）。";
            return true;
        }

        private static bool EditHalfWidth(
            Editor ed, Transaction tr, ObjectId pickedId, Intersection target, out string summary)
        {
            summary = null;
            var arcEnt = tr.GetObject(pickedId, OpenMode.ForRead) as Arc;
            if (arcEnt == null)
            {
                ed.WriteMessage("\n[道路] 修改半宽需要拾取该 Leg 关联的转角弧（Arc）实体。");
                return false;
            }

            int idx = FindNearestCornerArcIndex(target, arcEnt);
            if (idx < 0)
            {
                ed.WriteMessage("\n[道路] 无法把拾取的 Arc 对应到 Intersection.CornerArcs。");
                return false;
            }

            var arc = target.CornerArcs[idx];
            int[] candidates = { arc.LegIndexA, arc.LegIndexB };

            var opt = new PromptKeywordOptions(
                $"\n[道路] 选择 Leg [A(Leg#{arc.LegIndexA})/B(Leg#{arc.LegIndexB})]：")
            {
                AllowNone = true,
            };
            opt.Keywords.Add("A");
            opt.Keywords.Add("B");
            opt.Keywords.Default = "A";
            var kw = ed.GetKeywords(opt);
            if (kw.Status != PromptStatus.OK) return false;
            int legIndex = string.Equals(kw.StringResult, "B", StringComparison.OrdinalIgnoreCase)
                ? candidates[1] : candidates[0];

            var leg = target.Legs[legIndex];
            var opt2 = new PromptDoubleOptions($"\n[道路] Leg#{legIndex}（{leg.Tag ?? "-"}）新半宽 W (当前 {leg.HalfWidth:F2} m)：")
            {
                DefaultValue = leg.HalfWidth,
                UseDefaultValue = true,
                AllowNone = true,
                AllowNegative = false,
                AllowZero = false,
            };
            var r = ed.GetDouble(opt2);
            if (r.Status != PromptStatus.OK) return false;
            if (Math.Abs(r.Value - leg.HalfWidth) < 1e-6) return false;

            var touched = IntersectionDesigner.UpdateLegHalfWidth(target, legIndex, r.Value);
            summary = $"Leg#{legIndex} 半宽已改为 W={r.Value:F2}（原 {leg.HalfWidth:F2}），相邻 CornerArc 重算 {touched.Count} 条：{string.Join(",", touched)}。";
            return true;
        }

        private static bool EditDesignSpeed(Editor ed, Intersection target, out string summary)
        {
            summary = null;
            var opt = new PromptDoubleOptions($"\n[道路] 新设计速度 V (km/h，当前 {target.DesignSpeed:F0})：")
            {
                DefaultValue = target.DesignSpeed,
                UseDefaultValue = true,
                AllowNone = true,
                AllowNegative = false,
                AllowZero = false,
            };
            var r = ed.GetDouble(opt);
            if (r.Status != PromptStatus.OK) return false;
            if (Math.Abs(r.Value - target.DesignSpeed) < 1e-6) return false;

            double old = target.DesignSpeed;
            target.DesignSpeed = r.Value;
            target.LastModifiedUtc = DateTime.UtcNow;
            summary = $"设计速度已改为 V={r.Value:F0} km/h（原 {old:F0}），几何未变。";
            return true;
        }

        /// <summary>
        /// 拿拾取到的 AutoCAD <see cref="Arc"/>，按 (Center, Radius) 匹配到 <see cref="Intersection.CornerArcs"/> 的索引。
        /// 容差 0.01 m（1 cm）。
        /// </summary>
        internal static int FindNearestCornerArcIndex(Intersection intersection, Arc arc)
        {
            if (intersection == null || arc == null) return -1;
            var arcCenter = new Point2D(arc.Center.X, arc.Center.Y);
            double bestDist = double.MaxValue;
            int bestIdx = -1;
            for (int i = 0; i < intersection.CornerArcs.Count; i++)
            {
                var ca = intersection.CornerArcs[i];
                if (Math.Abs(ca.Radius - arc.Radius) > 0.01) continue;
                double d = ca.Center.DistanceTo(arcCenter);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIdx = i;
                }
            }
            return bestDist < 0.01 ? bestIdx : -1;
        }
    }
}
