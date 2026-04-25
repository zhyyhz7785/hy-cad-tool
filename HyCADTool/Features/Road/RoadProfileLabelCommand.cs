using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.Shared.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// hyRoadProfLabel —— 把纵断面（设计线 FG / 地面线 EG）标注到 DWG 的命令（v1.1 实现）。
    ///
    /// 流程：
    /// <list type="number">
    ///   <item>拾取 HY_ROAD Alignment（用 Xdata KIND="Alignment" 校验）；</item>
    ///   <item>从 Domain 取该 Alignment 的设计 Profile（必须存在），可选取 EG Profile；</item>
    ///   <item>用 <see cref="ProfileFgDesigner.Build"/> 根据 PVI 顶点反算 FG 几何（含竖曲线段）；</item>
    ///   <item>提示原点（PromptPoint）+ 高程缩放比；</item>
    ///   <item>调 <see cref="ProfileLabelDrawService.Draw"/> 绘制网格 + FG + EG + PVI 标注。</item>
    /// </list>
    ///
    /// 降级策略（plan 要求）：
    /// - 没有 FG → 提示在 hyRoadP 里先建并退出；
    /// - 没有 EG → 跳过 EG，仍画 FG / 网格 / PVI；
    /// - PVI 仅 1 个 → ProfileFgDesigner 抛 ArgumentException，捕获并提示先补 PVI；
    /// - <see cref="ProfileLabelDrawService"/> 自身对 layer / Xdata 缺失做了静默回退（不抛异常）。
    /// </summary>
    public sealed class RoadProfileLabelCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var alignSvc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            // ===== 1) 拾取 HY_ROAD Alignment =====
            var peoAln = new PromptEntityOptions("\n[道路] 拾取要标注纵断面的平面线位（HY_ROAD Alignment）：");
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

            // ===== 2) 取 FG / EG =====
            Profile fg = alignment.Profiles.FirstOrDefault(p => p.IsDesignProfile);
            if (fg == null)
            {
                ed.WriteMessage("\n[道路] 该 Alignment 还没有设计纵断面（FG）。请先在 hyRoadP 中建好 PVI。");
                return;
            }
            if (fg.Vertices.Count < 2)
            {
                ed.WriteMessage($"\n[道路] FG（{fg.Name}）只有 {fg.Vertices.Count} 个 PVI，至少需要 2 个才能标注。");
                return;
            }
            Profile eg = alignment.Profiles.FirstOrDefault(p => !p.IsDesignProfile);

            // ===== 3) FG 几何 =====
            ProfileFgResult fgResult;
            try
            {
                fgResult = ProfileFgDesigner.Build(fg.Vertices);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] FG 几何构造失败：{ex.Message}");
                return;
            }

            // ===== 4) 高程缩放（默认 10× 标准纵断面） =====
            var pdoY = new PromptDoubleOptions("\n[道路] 高程方向缩放（图纸单位/m，标准 10）")
            {
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = 10.0,
                UseDefaultValue = true,
            };
            var pdrY = ed.GetDouble(pdoY);
            if (pdrY.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }
            double yScale = pdrY.Value;

            // ===== 5) 拾取原点 =====
            var ppoOrigin = new PromptPointOptions("\n[道路] 拾取纵断面图原点（左下角）：")
            {
                AllowNone = false,
            };
            var ppr = ed.GetPoint(ppoOrigin);
            if (ppr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }
            Point3d origin = ppr.Value;

            // ===== 6) 绘制 =====
            var drawSvc = new ProfileLabelDrawService();
            ProfileLabelDrawResult drawResult;
            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    drawResult = drawSvc.Draw(
                        tr, db, alignmentId,
                        fg, eg, fgResult, origin,
                        new ProfileLabelDrawOptions { YScale = yScale });
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 标注绘制失败：{ex.Message}");
                return;
            }

            ed.WriteMessage(
                $"\n[道路] 已标注纵断面（Alignment={alignment.Name}）："
                + $"FG={fg.Vertices.Count} PVI"
                + (eg != null ? $", EG={eg.Vertices.Count} 点" : ", 无 EG")
                + $", 高程缩放 {yScale:F1}×, "
                + drawResult);
        }
    }
}
