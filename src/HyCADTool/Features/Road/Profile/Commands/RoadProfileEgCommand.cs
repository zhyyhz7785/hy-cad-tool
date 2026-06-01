using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using HyCADTool.Features.Road.PlanAlignment.Services;
using HyCADTool.Features.Road.PlanProfile.Services;
using HyCADTool.Features.Road.PlanProfile.Domain;
using HyCADTool.Features.Road.PlanProfile.Views;
namespace HyCADTool.Features.Road.PlanProfile.Commands
{
    /// <summary>
    /// hyRoadProfEG —— 现状地面线（EG = Existing Grade）拾取并采样命令（v1.1 实现）。
    ///
    /// 流程：
    /// <list type="number">
    ///   <item>拾取 HY_ROAD Alignment Polyline → 解析 AlignmentId / Centerline；</item>
    ///   <item>拾取一条 2D <c>Polyline</c> 或 3D <c>Polyline3d</c> 作为现状地面线（≥ 2 顶点）；</item>
    ///   <item>提示采样间隔（默认 5 m）；</item>
    ///   <item>调 <see cref="EgProfileSampler.Sample"/> 沿 Centerline 桩号采样 Z；</item>
    ///   <item>调 <see cref="RoadProfileService.CreateOrReplaceEgProfile"/> 写回 Domain；</item>
    ///   <item>调 <see cref="RoadJsonExportService.SaveForDocument"/> 同步落盘。</item>
    /// </list>
    ///
    /// 不弹任何 WPF 窗口（命令式纯命令行交互），与设计纵断面 hyRoadP 的窗口编辑解耦，
    /// 方便后续与 hyRoadProfLabel 配合"采样 → 标注"流水线。
    /// </summary>
    public sealed class RoadProfileEgCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var alignSvc = ServiceLocator.Resolve<RoadAlignmentService>();
            var profSvc = ServiceLocator.Resolve<RoadProfileService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            // ===== 1) 拾取 HY_ROAD Alignment =====
            var peoAln = new PromptEntityOptions("\n[道路] 拾取要采样地面线的平面线位（HY_ROAD Alignment）：");
            peoAln.SetRejectMessage("\n[道路] 只能选择已挂 HY_ROAD 的 Polyline。");
            peoAln.AddAllowedClass(typeof(Polyline), exactMatch: false);
            var perAln = ed.GetEntity(peoAln);
            if (perAln.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            Guid alignmentId;
            Alignment alignment;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                alignSvc.RebindForDocument(doc.Name, tr, db);

                var ent = tr.GetObject(perAln.ObjectId, OpenMode.ForRead);
                var kind = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal))
                {
                    ed.WriteMessage("\n[道路] 拾取的图元不是 HY_ROAD Alignment。请先用 hyRoadA / hyRoadAlnByPi 创建。");
                    return;
                }
                alignmentId = HyRoadXdata.ReadId(tr, ent);
                if (alignmentId == Guid.Empty)
                {
                    ed.WriteMessage("\n[道路] 拾取的图元缺少 HY_ROAD/ID。");
                    return;
                }
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ed.WriteMessage("\n[道路] 当前 DWG 没有道路数据，请先 hyRoadAlnByPi。");
                    return;
                }
                alignment = design.Alignments.FirstOrDefault(a => a.Id == alignmentId);
                if (alignment == null)
                {
                    ed.WriteMessage($"\n[道路] Domain 找不到 AlignmentId={alignmentId:N}。");
                    return;
                }
                tr.Commit();
            }

            // ===== 2) 拾取地面线（Polyline / Polyline3d） =====
            var peoGround = new PromptEntityOptions("\n[道路] 拾取地面线（2D Polyline 或 3D Polyline3d，≥ 2 顶点）：");
            peoGround.SetRejectMessage("\n[道路] 只能选择 Polyline 或 Polyline3d。");
            peoGround.AddAllowedClass(typeof(Polyline), exactMatch: false);
            peoGround.AddAllowedClass(typeof(Polyline3d), exactMatch: false);
            var perGround = ed.GetEntity(peoGround);
            if (perGround.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            Polyline3D groundLine;
            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(perGround.ObjectId, OpenMode.ForRead);
                    groundLine = ConvertToPolyline3D(tr, ent);
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 读取地面线失败：{ex.Message}");
                return;
            }

            if (groundLine == null || groundLine.VertexCount < 2)
            {
                ed.WriteMessage("\n[道路] 地面线顶点不足 2 个。");
                return;
            }

            // ===== 3) 采样间隔 =====
            var pdo = new PromptDoubleOptions("\n[道路] 采样间隔 (m)")
            {
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = 5.0,
                UseDefaultValue = true,
            };
            var pdr = ed.GetDouble(pdo);
            if (pdr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }
            double interval = pdr.Value;

            // ===== 4) 采样 =====
            EgProfileSampler.Result sampleResult;
            try
            {
                sampleResult = EgProfileSampler.Sample(
                    alignment.Centerline,
                    groundLine,
                    new EgProfileSampler.Options { IntervalM = interval, IncludeEnd = true });
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 采样失败：{ex.Message}");
                return;
            }

            if (sampleResult.Vertices.Count == 0)
            {
                ed.WriteMessage(
                    $"\n[道路] 采样未生成任何 PVI（{sampleResult.SkippedOutOfRange} 个桩号超出 MaxLateralOffset，"
                    + $"最大横向偏移 {sampleResult.MaxLateralOffsetSeen:F2} m）。"
                    + "\n[道路] 请确认地面线在 Alignment 横向覆盖范围内。");
                return;
            }

            // ===== 5) 写回 Domain =====
            Profile eg;
            try
            {
                eg = profSvc.CreateOrReplaceEgProfile(
                    doc.Name, alignmentId,
                    new List<ProfileVertex>(sampleResult.Vertices));
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 写回 EG 失败：{ex.Message}");
                return;
            }

            // ===== 6) 落盘 =====
            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design2))
            {
                savedTo = exporter.SaveForDocument(design2, doc.Name);
            }

            ed.WriteMessage(
                $"\n[道路] 已生成/替换 EG（{eg.Name}）："
                + $"PVI={sampleResult.Vertices.Count}, 间隔={interval:F2} m, "
                + $"最大横向偏移={sampleResult.MaxLateralOffsetSeen:F2} m"
                + (sampleResult.SkippedOutOfRange > 0 ? $", 跳过 {sampleResult.SkippedOutOfRange} 个超界点" : "")
                + "。");
            if (savedTo != null) ed.WriteMessage($"\n[道路] JSON 同步落盘：{savedTo}");
        }

        /// <summary>
        /// 把 AutoCAD 拾取到的 <c>Polyline</c> / <c>Polyline3d</c> 统一转成 Domain <see cref="Polyline3D"/>。
        ///
        /// - 2D <c>Polyline</c>：所有顶点 Z 取自 <see cref="Polyline.Elevation"/>（与 <see cref="RoadGeometryBridge.ToDomain(Polyline)"/> 一致）；
        /// - 3D <c>Polyline3d</c>：通过 vertex 子对象遍历获取 (X, Y, Z)，bulge=0（3D 多段线不支持 bulge）。
        /// </summary>
        private static Polyline3D ConvertToPolyline3D(Transaction tr, DBObject ent)
        {
            if (ent is Polyline pl2)
            {
                return RoadGeometryBridge.ToDomain(pl2);
            }

            if (ent is Polyline3d pl3)
            {
                var pts = new List<Point3d>();
                foreach (ObjectId vid in pl3)
                {
                    if (tr.GetObject(vid, OpenMode.ForRead) is PolylineVertex3d v)
                    {
                        pts.Add(v.Position);
                    }
                }
                return RoadGeometryBridge.ToDomain(pts, pl3.Closed);
            }

            return null;
        }
    }
}
