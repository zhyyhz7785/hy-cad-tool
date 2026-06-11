using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using HyCADTool.Shell;
using System.Windows;
using System.Windows.Threading;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Features.Road.PlanAlignment.Domain;

using HyCADTool.Features.Road.PlanAlignment.Services;
namespace HyCADTool.Features.Road.PlanAlignment.Commands
{
    /// <summary>
    /// <c>hyRoadAlnUserPickRegister</c>（工作台「拾取」按钮入口）— 拾取任意图层上的
    /// Polyline / Polyline2d / Polyline3d，登记为一条 <see cref="Alignment"/>，**并按新工作流**
    /// 在「无 HY_ROAD 标记」的外部 Polyline 情形下删除原物、把几何转存到 <c>05_hy_道路_原线</c> 层。
    ///
    /// <para>新工作流（三层预览 + 一层存档）下的拾取分流：</para>
    /// <list type="number">
    ///   <item>
    ///     <b>命中 HY_ROAD Alignment 系列</b>（<see cref="HyRoadXdata.IsAlignmentKind"/>）且能按 Id 回查到
    ///     注册表：<b>reuse</b> — 只把该 Alignment 选中给工作台，不动 DWG 实体，不重画 Raw 与 DesignPreview
    ///     （用户点中的是"自己画过的线"，表示他要继续编辑）。
    ///   </item>
    ///   <item>
    ///     <b>其他</b>（外部 Polyline / 无 HY_ROAD Xdata / 其他 KIND）：
    ///     <list type="bullet">
    ///       <item>事务内 <c>RegisterFromPickedEntity</c> 读顶点，登记为新 Alignment；</item>
    ///       <item>事务内立即 <c>Erase</c> 原 Polyline（避免 Apply 定稿后出现"新旧两条"）；</item>
    ///       <item>事务外调 <see cref="RoadAlignmentRawPolylineService.DrawForAlignment"/> 画一条
    ///         <see cref="HyRoadXdata.KindAlignmentRawPick"/>（图层本色 252，跨会话留存）；</item>
    ///       <item>事务外调 <see cref="RoadAlignmentDesignPreviewService.Refresh"/> 画一批
    ///         <see cref="HyRoadXdata.KindAlignmentDesignPreview"/>（分段彩色 — 首次等同于原几何）；</item>
    ///       <item><see cref="RoadJsonExportService"/> 同步一次 <c>.roaddesign.json</c>。</item>
    ///     </list>
    ///   </item>
    /// </list>
    ///
    /// <para>登记完通过 <see cref="RoadAlignmentUserPickRegistrationSession.SetLastRegistered"/> 把 Id 回给
    /// 工作台 WPF 线程，面板捕获后自动选中该 Alignment。</para>
    /// </summary>
    public sealed class RoadAlignmentUserPickRegisterCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions("\n[道路] 拾取一条 Polyline（任意图层，含 Polyline2d / Polyline3d）登记为平面线位：");
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
            bool erasedOriginal = false;
            string pickedHandle = null;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    svc.RebindForDocument(doc.Name, tr, db);

                    var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                    var existingKind = HyRoadXdata.ReadKind(tr, ent);
                    bool isKnownAlignment = HyRoadXdata.IsAlignmentKind(existingKind);

                    if (isKnownAlignment)
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

                        // 新工作流：全新登记 & 原 Polyline 非 HY_ROAD 注册实体 → 同事务擦除原物，
                        // 避免 Apply 定稿后 05_hy_道路_平面线位 与外部图层并存两条"几乎一样的线"。
                        if (!reusedExisting && !isKnownAlignment)
                        {
                            try
                            {
                                var we = tr.GetObject(per.ObjectId, OpenMode.ForWrite);
                                if (we != null && !we.IsErased)
                                {
                                    we.Erase();
                                    erasedOriginal = true;
                                }
                            }
                            catch
                            {
                                // 擦不掉（锁定 / 块参照 / 只读）— 不致命，继续走后面的原线层转存。
                            }
                        }
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

            // v2 工作流：新登记只在 05_hy_道路_原线（启动即锁定）层写入 252 本色 RawPick 档案；
            // DesignPreview 自动彩色机制已下线，分段彩色由用户点「预览」按钮追加到 05_hy_道路_预览。
            // 服务内部 LockDocument + 独立事务 + LayerLockScope.Unlock —— 必须放在外层事务 Commit 之后。
            if (!reusedExisting)
            {
                try
                {
                    RoadAlignmentRawPolylineService.DrawForAlignment(doc, alignment);
                }
                catch
                {
                    // 绘制失败不影响注册结果，用户可在工作台切 Alignment 触发重绘。
                }
            }

            RoadAlignmentUserPickRegistrationSession.SetLastRegistered(alignment.Id);

            // 回到 WPF 线程打开/刷新路线工作台并选中本条线位（含「复用已有 HY_ROAD」拾取）。
            try
            {
                var disp = Application.Current?.Dispatcher;
                if (disp != null)
                {
                    var id = alignment.Id;
                    disp.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            var pm = ServiceLocator.Resolve<PanelManager>();
                            pm?.ShowAlignmentWorkbench(id, null);
                        }
                        catch
                        {
                            /* 无宿主 / 未注入时忽略 */
                        }
                    }), DispatcherPriority.Background);
                }
            }
            catch
            {
                /* Dispatcher 不可用 */
            }

            int piCount = alignment.Source?.PiElements?.Count ?? 0;
            double length = alignment.Centerline?.GetPlanarLength() ?? 0;

            if (reusedExisting)
            {
                ed.WriteMessage(
                    $"\n[道路] 已选中已登记线位 {alignment.Name}（PI={piCount}，长={length:F3} m，Handle={pickedHandle ?? "-"}）。");
            }
            else
            {
                string erasedNote = erasedOriginal
                    ? "原 Polyline 已删除并转存到 05_hy_道路_原线 层（图层本色 252）。"
                    : "注：原 Polyline 未能删除，请确认无锁定 / 无块参照。";
                ed.WriteMessage(
                    $"\n[道路] 已登记线位 {alignment.Name}（PI={piCount}，长={length:F3} m，源 Handle={pickedHandle ?? "-"}）。{erasedNote}"
                    + "\n[道路] 点「预览」可追加分段彩色到 05_hy_道路_预览；点「应用」定稿到 05_hy_道路_平面线位 并同步 .roaddesign.json。");
            }

            try
            {
                var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
                if (registry.TryGet(doc.Name, out var designOut))
                    exporter.SaveForDocument(designOut, doc.Name);
            }
            catch { /* ignore — JSON 落盘失败不致命 */ }
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
