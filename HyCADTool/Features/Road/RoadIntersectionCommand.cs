using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shared.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadIntersection</c> — 从 ≥ 2 条已存在的 <see cref="Alignment"/> 自动生成平面交叉口 CornerArc。
    ///
    /// <para><b>交互</b></para>
    /// <list type="number">
    /// <item>循环拾取 HY_ROAD Alignment Polyline（至少 2 条，回车结束）；</item>
    /// <item>点取交叉口大致中心点 P（用于决定每条 Alignment 取起端还是末端）；</item>
    /// <item>输入转角半径 R（默认 <see cref="Intersection.DefaultCornerRadiusValue"/> = 20 m）；</item>
    /// <item>输入半宽 W（默认 <see cref="IntersectionLeg.DefaultHalfWidth"/> = 7.5 m）；</item>
    /// <item>输入设计速度 V（默认 30 km/h，用于 <see cref="IntersectionCodeChecker"/> 查表校核）。</item>
    /// </list>
    ///
    /// <para><b>内部流程</b></para>
    /// <list type="number">
    /// <item>调 <see cref="IntersectionDesigner.ComputeFromAlignments"/> 构造 <see cref="Intersection"/>（含 CCW 排序 Legs + CornerArcs）；</item>
    /// <item>调 <see cref="IntersectionCodeChecker.CheckCornerRadius"/> 逐弧校核；</item>
    /// <item>加入 <c>RoadDesign.Intersections</c> + 调 <see cref="RoadIntersectionService.RebuildIntersection"/> 幂等画到 DWG；</item>
    /// <item>每条 CornerArc 上挂 HY_ROAD Xdata（KIND=<see cref="RoadIntersectionService.IntersectionKind"/>，ID=Intersection.Id）；</item>
    /// <item>调 <c>RoadJsonExportService.SaveForDocument</c> 落盘 <c>.roaddesign.json</c>。</item>
    /// </list>
    ///
    /// <para><b>已知局限（v1.0 占位）</b></para>
    /// <list type="bullet">
    /// <item>单轮次所有 CornerArc 使用同一半径；"逐 corner 独立半径" 留给 <c>hyRoadIntersectionEdit</c>（v1.1）；</item>
    /// <item>进口展宽 / 缘石坡道 / 盲道 在 v1.0 不做，分别由 <c>hyRoadCurbRamp</c> / <c>hyRoadTactilePaving</c>（I1-C）与
    /// P2 Template 阶段的 ApproachWiden 专项补齐；</item>
    /// <item>CornerArc 之间的"路缘外边线连接直段"目前不画（只画转角弧），视觉上需用户配合 <c>hyRoadAlnOffset</c> 辅助。</item>
    /// </list>
    /// </summary>
    public sealed class RoadIntersectionCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var pickedAlignmentIds = PickAlignments(ed);
            if (pickedAlignmentIds == null || pickedAlignmentIds.Count < 2)
            {
                ed.WriteMessage("\n[道路] 已取消（需要至少选 2 条 Alignment）。");
                return;
            }

            var optCenter = new PromptPointOptions("\n[道路] 指定交叉口大致中心点：") { AllowNone = false };
            var rCenter = ed.GetPoint(optCenter);
            if (rCenter.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }
            var aroundPoint = new Point2D(rCenter.Value.X, rCenter.Value.Y);

            var optR = new PromptDoubleOptions(
                $"\n[道路] 转角半径 R (默认 {Intersection.DefaultCornerRadiusValue:F1} m)：")
            {
                DefaultValue = Intersection.DefaultCornerRadiusValue,
                UseDefaultValue = true,
                AllowNone = true,
                AllowNegative = false,
                AllowZero = false,
            };
            var rR = ed.GetDouble(optR);
            double radius = rR.Status == PromptStatus.OK ? rR.Value : Intersection.DefaultCornerRadiusValue;

            var optW = new PromptDoubleOptions(
                $"\n[道路] 半宽 W (默认 {IntersectionLeg.DefaultHalfWidth:F1} m)：")
            {
                DefaultValue = IntersectionLeg.DefaultHalfWidth,
                UseDefaultValue = true,
                AllowNone = true,
                AllowNegative = false,
                AllowZero = false,
            };
            var rW = ed.GetDouble(optW);
            double halfWidth = rW.Status == PromptStatus.OK ? rW.Value : IntersectionLeg.DefaultHalfWidth;

            var optV = new PromptDoubleOptions("\n[道路] 设计速度 V (km/h，默认 30)：")
            {
                DefaultValue = 30,
                UseDefaultValue = true,
                AllowNone = true,
                AllowNegative = false,
                AllowZero = false,
            };
            var rV = ed.GetDouble(optV);
            double designSpeed = rV.Status == PromptStatus.OK ? rV.Value : 30;

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var ixSvc = ServiceLocator.Resolve<RoadIntersectionService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            Intersection built;
            int drawnCount;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                svc.RebindForDocument(doc.Name, tr, db);
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ed.WriteMessage("\n[道路] 当前 DWG 没有道路设计数据。");
                    return;
                }

                var alignments = ResolveAlignments(tr, pickedAlignmentIds, design);
                if (alignments.Count < 2)
                {
                    ed.WriteMessage(
                        $"\n[道路] 有效 Alignment 不足（{alignments.Count}）—— 拾取对象未挂 HY_ROAD 或 Domain 中不存在。");
                    return;
                }

                try
                {
                    built = IntersectionDesigner.ComputeFromAlignments(
                        alignments, aroundPoint, radius, halfWidth, name: null, designSpeed: designSpeed);
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n[道路] 交叉口几何计算失败：{ex.Message}");
                    return;
                }

                design.Intersections.Add(built);
                var ids = ixSvc.RebuildIntersection(tr, db, built, HyRoadLayers.IntersectionLayer);
                drawnCount = ids.Count;

                tr.Commit();
            }

            try
            {
                if (registry.TryGet(doc.Name, out var designForSave))
                    exporter.SaveForDocument(designForSave, doc.Name);
            }
            catch (Exception ex) { ed.WriteMessage($"\n[道路][警告] .roaddesign.json 写盘失败：{ex.Message}"); }

            int warnCount = 0;
            foreach (var arc in built.CornerArcs)
            {
                var r = IntersectionCodeChecker.CheckCornerRadius(arc.Radius, built.DesignSpeed);
                if (!r.Pass) { ed.WriteMessage($"\n[道路][警告] {r.Message}"); warnCount++; }
            }

            ed.WriteMessage(
                $"\n[道路] 交叉口已创建：{built.Legs.Count} 臂 / {built.CornerArcs.Count} 弧 / 绘制 {drawnCount} 条。"
                + $" R={radius:F2}，W={halfWidth:F2}，V={designSpeed:F0} km/h。"
                + (warnCount > 0 ? $" 校核警告 {warnCount} 条。" : " 规范校核全部通过。"));
        }

        /// <summary>交互循环拾取 HY_ROAD Alignment Polyline，回车结束。</summary>
        private static List<ObjectId> PickAlignments(Editor ed)
        {
            var result = new List<ObjectId>();
            while (true)
            {
                var peo = new PromptEntityOptions(
                    result.Count == 0
                        ? "\n[道路] 拾取参与交叉口的平面线位（HY_ROAD Alignment Polyline），回车结束："
                        : $"\n[道路] 已选 {result.Count} 条；继续拾取或回车结束：");
                peo.SetRejectMessage("\n[道路] 只能选择 Polyline。");
                peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
                peo.AllowNone = true;

                var per = ed.GetEntity(peo);
                if (per.Status == PromptStatus.None) break;
                if (per.Status != PromptStatus.OK) return null;

                if (!result.Contains(per.ObjectId)) result.Add(per.ObjectId);
                else ed.WriteMessage("\n[道路] 已拾取过此 Alignment，跳过。");
            }
            return result;
        }

        /// <summary>把 DWG 的 Polyline ObjectId 翻译回 Domain <see cref="Alignment"/>（通过 HY_ROAD Xdata ID）。</summary>
        private static List<Alignment> ResolveAlignments(
            Transaction tr, IReadOnlyList<ObjectId> ids, RoadDesign design)
        {
            var list = new List<Alignment>();
            var seen = new HashSet<Guid>();
            foreach (var id in ids)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;
                var kind = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal)) continue;
                var guid = HyRoadXdata.ReadId(tr, ent);
                if (guid == Guid.Empty || !seen.Add(guid)) continue;
                var aln = design.Alignments.FirstOrDefault(a => a.Id == guid);
                if (aln != null && aln.Centerline != null && aln.Centerline.VertexCount >= 2)
                    list.Add(aln);
            }
            return list;
        }
    }
}
