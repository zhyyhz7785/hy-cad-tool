using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadAlnUserPickRegister</c> — 拾取任意图层上的 Polyline / Polyline2d / Polyline3d（不校验 HY_ROAD），
    /// 登记为一条 <see cref="AlignmentSourceKind.UserPicked"/> 来源的 <see cref="Alignment"/>。
    ///
    /// 与 <c>hyRoadAlnByPi</c>（按 PI 点序列交互生成）互补：本命令适合"已有中心线"的现场资料。
    ///
    /// 注意：
    /// - 本命令 <b>不写</b>原始 Polyline 的 HY_ROAD Xdata、<b>不迁移</b>其图层；
    /// - 登记后通过 <see cref="RoadAlignmentUserPickRegistrationSession.SetLastRegistered"/>
    ///   把 AlignmentId 回给工作台 WPF 线程，供面板自动选中 + 高亮预览；
    /// - 后续「提交为平面线位」<c>hyRoadAlnCommit</c> 才会在 <c>05_hy_道路_平面线位</c>
    ///   新建 HY_ROAD Polyline、擦除预览、刷 JSON。
    /// </summary>
    public sealed class RoadAlignmentUserPickRegisterCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions("\n[道路] 拾取一条 Polyline（任意图层，含 Polyline2d / Polyline3d）登记为平面线位草稿：");
            peo.SetRejectMessage("\n[道路] 只能选择 Polyline 类实体。");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
            peo.AddAllowedClass(typeof(Polyline2d), exactMatch: false);
            peo.AddAllowedClass(typeof(Polyline3d), exactMatch: false);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            var register = ServiceLocator.Resolve<RoadAlignmentUserPickRegisterService>();
            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            Alignment alignment = null;
            bool reusedExisting = false;
            string pickedHandle = null;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    svc.RebindForDocument(doc.Name, tr, db);

                    // 若拾取的 Polyline 已挂 HY_ROAD Alignment Xdata，复用原 Alignment（不新登记草稿）。
                    var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    var existingKind = HyRoadXdata.ReadKind(tr, ent);
                    if (string.Equals(existingKind, HyRoadXdata.KindAlignment, StringComparison.Ordinal))
                    {
                        var existingId = HyRoadXdata.ReadId(tr, ent);
                        if (existingId != Guid.Empty
                            && registry.TryGet(doc.Name, out var design))
                        {
                            alignment = design.Alignments.FirstOrDefault(a => a.Id == existingId);
                            if (alignment != null) reusedExisting = true;
                        }
                    }

                    if (alignment == null)
                    {
                        alignment = register.RegisterFromPickedEntity(doc.Name, tr, db, per.ObjectId);
                    }

                    if (alignment != null)
                    {
                        pickedHandle = per.ObjectId.IsValid ? per.ObjectId.Handle.ToString() : null;
                    }
                    tr.Commit();
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n[道路] 登记失败：{ex.Message}");
                    return;
                }
            }

            if (alignment == null)
            {
                ed.WriteMessage("\n[道路] 该图元顶点不足，未登记。");
                return;
            }

            RoadAlignmentUserPickRegistrationSession.SetLastRegistered(alignment.Id);
            int piCount = alignment.Source?.PiElements?.Count ?? 0;
            double length = alignment.Centerline?.GetPlanarLength() ?? 0;

            if (reusedExisting)
            {
                ed.WriteMessage(
                    $"\n[道路] 已选中已登记线位 {alignment.Name}（PI={piCount}，长={length:F3} m，Handle={pickedHandle ?? "-"}）。");
            }
            else
            {
                ed.WriteMessage(
                    $"\n[道路] 已登记草稿线位 {alignment.Name}（PI={piCount}，长={length:F3} m，源 Handle={pickedHandle ?? "-"}）。"
                    + "\n[道路] 提示：在路线工作台点「绘出预览」可在「用户拾取」层按段着色查看；"
                    + "\n[道路] 确认后点「提交为平面线位」把草稿写入 05_hy_道路_平面线位 层并挂 HY_ROAD Xdata。");
            }

            // 把 JSON 同步一次，避免关 AutoCAD 时草稿丢失
            try
            {
                var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
                if (registry.TryGet(doc.Name, out var designOut))
                    exporter.SaveForDocument(designOut, doc.Name);
            }
            catch { /* ignore */ }
        }
    }

    /// <summary>
    /// 新登记的 <see cref="Alignment.Id"/> 从 AutoCAD 命令线程回传到路线工作台 WPF 线程的通道。
    /// 与 <see cref="RoadAlignmentUserPickPreviewSession"/> 同属"一次性旗标"模式（读后清零），
    /// 只在本插件进程内使用，避免跨线程直接调用 ViewModel。
    /// </summary>
    internal static class RoadAlignmentUserPickRegistrationSession
    {
        private static Guid? _lastRegistered;

        internal static void SetLastRegistered(Guid alignmentId)
        {
            _lastRegistered = alignmentId;
        }

        internal static Guid? ConsumeLastRegistered()
        {
            var v = _lastRegistered;
            _lastRegistered = null;
            return v;
        }
    }
}
