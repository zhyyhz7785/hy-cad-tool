using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using HyCADTool.Presentation.ViewModels.Road;
using HyCADTool.Presentation.Views.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// P1.B：纵断面设计入口（v1，"FG 设计线 + 4 项规范"）。
    ///
    /// 流程：
    /// <list type="number">
    ///   <item>拾取 HY_ROAD Alignment Polyline → 解析 AlignmentId；</item>
    ///   <item><see cref="RoadProfileService.GetOrCreateDesignProfile"/> 取/建设计 Profile（DesignSpeed 默认 60）；</item>
    ///   <item>打开 <see cref="ProfileEditorWindow"/>（带 PVI 表 / 4 项规范 / 纵断面 Canvas）；</item>
    ///   <item>用户【确定】→ <see cref="RoadProfileService.ReplaceVertices"/>（事件总线发 ProfileChanged）→
    ///         <see cref="RoadJsonExportService.SaveForDocument"/> 同步落盘。</item>
    /// </list>
    ///
    /// 暂不做：DWG 上画"高程网格 / 标尺"实体（v1.1 由 hyRoadProfLabel 单独承担）。
    /// </summary>
    public sealed class RoadProfileCommand
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
            var peo = new PromptEntityOptions("\n[道路] 拾取要编辑纵断面的平面线位（HY_ROAD Alignment）：");
            peo.SetRejectMessage("\n[道路] 只能选择已挂 HY_ROAD 的 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
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

                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
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

            // ===== 2) 取/建 Profile =====
            Profile profile;
            try
            {
                var (p, created) = profSvc.GetOrCreateDesignProfile(doc.Name, alignmentId);
                profile = p;
                if (created)
                {
                    ed.WriteMessage($"\n[道路] 已为 {alignment.Name} 新建设计纵断面 {profile.Name}（默认 v={profile.DesignSpeed} km/h）。");
                }
                else
                {
                    ed.WriteMessage($"\n[道路] 进入既有设计纵断面 {profile.Name}（PVI {profile.Vertices.Count} 个 / v={profile.DesignSpeed} km/h）。");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 取/建 Profile 失败：{ex.Message}");
                return;
            }

            // ===== 3) 打开窗口收集 PVI =====
            ProfileEditorViewModel vm;
            try
            {
                vm = new ProfileEditorViewModel(alignment, profile);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 打开纵断面编辑器失败：{ex.Message}");
                return;
            }

            var window = new ProfileEditorWindow(vm);
            AcApp.ShowModalWindow(window);

            if (vm.ConfirmedVertices == null || vm.ConfirmedVertices.Count == 0)
            {
                ed.WriteMessage("\n[道路] 已取消，未修改纵断面。");
                return;
            }

            // ===== 4) 写回 Domain（带事件） + 同步速度 =====
            bool replaced = profSvc.ReplaceVertices(doc.Name, alignmentId, profile.Id, vm.ConfirmedVertices);
            if (!replaced)
            {
                ed.WriteMessage("\n[道路] 写回失败：Profile 未在 Domain 中找到。");
                return;
            }
            // DesignSpeed 在 VM 里编辑过；ReplaceVertices 不动它，单独写回
            if (profile.DesignSpeed != vm.DesignSpeed)
            {
                profile.DesignSpeed = vm.DesignSpeed;
                profile.LastModifiedUtc = DateTime.UtcNow;
            }

            // ===== 5) 落盘 =====
            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design2))
            {
                savedTo = exporter.SaveForDocument(design2, doc.Name);
            }

            ed.WriteMessage(
                $"\n[道路] 纵断面已保存：PVI={vm.ConfirmedVertices.Count}, v={vm.DesignSpeed} km/h, "
                + $"全部规范通过={vm.AllChecksPassed}。");
            if (savedTo != null) ed.WriteMessage($"\n[道路] JSON 同步落盘：{savedTo}");
        }
    }
}
