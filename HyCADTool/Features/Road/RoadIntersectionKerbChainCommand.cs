using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shared.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadIntersectionKerbChain</c> v1.1 —— 切换交叉口"路缘外边线直段链"的开 / 关。
    ///
    /// <para><b>用途</b></para>
    /// 仅绘制 <see cref="Autodesk.AutoCAD.DatabaseServices.Arc"/> 的 CornerArc 不能覆盖整条 Leg 的路缘外边线，
    /// 需要在每条 Leg 的两侧补一段"从 ApproachPoint 延伸到相邻 CornerArc 切点"的直线
    /// （由 <see cref="KerbChainDesigner"/> 计算，N 条 Leg 产生至多 2N 段 Line）。
    /// 本命令作为 <b>切换</b>：
    /// <list type="bullet">
    /// <item>当 <c>HasKerbChain = false</c> → 开启：置 true + 重建画出；</item>
    /// <item>当 <c>HasKerbChain = true</c>  → 关闭：置 false + 清除 Kerb 线（保留 CornerArc）。</item>
    /// </list>
    ///
    /// <para><b>交互</b></para>
    /// <list type="number">
    /// <item>拾取目标交叉口的任意实体（Arc / CurbRamp / TactilePaving / Kerb Line 均可）；</item>
    /// <item>自动反查归属 <see cref="Domain.Models.Road.Intersection"/>；</item>
    /// <item>切换 <c>HasKerbChain</c> → <see cref="RoadIntersectionService.RebuildIntersection"/>（自动联动 Kerb 画 / 擦）；</item>
    /// <item>JSON 落盘（开关状态入 <c>Intersection.HasKerbChain</c>）。</item>
    /// </list>
    ///
    /// <para><b>与 hyRoadIntersectionEdit 的协作</b></para>
    /// 开启 Kerb 链后，<c>hyRoadIntersectionEdit</c> 改 R / 改 HalfWidth 时
    /// <c>RebuildIntersection</c> 会自动重算 Kerb 段 —— 无需用户再次调用本命令。
    /// </summary>
    public sealed class RoadIntersectionKerbChainCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions(
                "\n[道路] 拾取目标交叉口的任意实体（转角弧 / 坡道 / 盲道 / 路缘线）：")
            {
                AllowNone = false,
            };
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

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

                bool previous = target.HasKerbChain;
                target.HasKerbChain = !previous;
                target.LastModifiedUtc = DateTime.UtcNow;

                // 重建会自动按新的 HasKerbChain 画 / 擦 Kerb 线
                ixSvc.RebuildIntersection(tr, db, target, HyRoadLayers.IntersectionLayer);

                // 若有无障碍设施，保持同步（Legs / Corner 未变，但 Rebuild 删除可能误伤 —— 保守重建）
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

                int segCount = target.HasKerbChain
                    ? KerbChainDesigner.ComputeKerbSegments(target).Count
                    : 0;
                ed.WriteMessage(target.HasKerbChain
                    ? $"\n[道路] 已开启路缘外边线链：Leg={target.Legs.Count}，Kerb 段={segCount}。"
                    : $"\n[道路] 已关闭路缘外边线链（保留 {target.CornerArcs.Count} 条 CornerArc）。");
            }
        }
    }
}
