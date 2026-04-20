using System;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadAlnCommit</c> — 将路线工作台当前选中的「草稿平面线位」
    /// （<see cref="Domain.Models.Road.AlignmentSourceKind.UserPicked"/>）正式提交：
    ///
    /// 1) 在 <see cref="Infrastructure.AutoCAD.Xdata.HyRoadLayers.AlignmentLayer"/> 新建 HY_ROAD Polyline；
    /// 2) 擦除同 AlignmentId 的 AlignmentPreview 预览实体；
    /// 3) 升级 <see cref="Domain.Models.Road.AlignmentSource.Kind"/> 为 PiTable；
    /// 4) JSON 同步落盘。
    ///
    /// AlignmentId 通过 <see cref="RoadAlignmentCommitSession"/> 从工作台 WPF 线程传递（参考
    /// <see cref="RoadAlignmentUserPickPreviewSession"/> 的一次性旗标模式），避免跨线程调用 ViewModel。
    /// </summary>
    public sealed class RoadAlignmentCommitCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            Guid? id = RoadAlignmentCommitSession.ConsumePendingCommit();
            if (!id.HasValue || id.Value == Guid.Empty)
            {
                ed.WriteMessage("\n[道路] 未收到工作台提交请求。请在路线工作台点「提交为平面线位」。");
                return;
            }

            var commitSvc = ServiceLocator.Resolve<RoadAlignmentCommitService>();
            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            // Commit 前先 Rebind（避免 SAVEAS 后 key 漂移）
            try
            {
                var db = doc.Database;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    svc.RebindForDocument(doc.Name, tr, db);
                    tr.Commit();
                }
            }
            catch { /* 忽略 */ }

            RoadAlignmentCommitService.CommitResult r;
            try
            {
                r = commitSvc.Commit(doc, id.Value);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 提交失败：{ex.Message}");
                return;
            }

            if (!r.Success)
            {
                ed.WriteMessage($"\n[道路] 提交失败：{r.Message}");
                return;
            }

            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design))
                savedTo = exporter.SaveForDocument(design, doc.Name);

            ed.WriteMessage($"\n[道路] {r.Message}");
            if (savedTo != null)
                ed.WriteMessage($"\n[道路] JSON 已同步：{savedTo}");
            else
                ed.WriteMessage("\n[道路] 未落盘（DWG 尚未保存），稍后 QSAVE 或 hyRoadSave 即可。");
        }
    }

    /// <summary>
    /// 路线工作台「提交为平面线位」经 <see cref="Commands.CommandDispatcher.Send"/>
    /// 投递到 AutoCAD 命令线程时，携带当前选中的 AlignmentId。
    /// </summary>
    internal static class RoadAlignmentCommitSession
    {
        private static Guid? _pendingCommit;

        internal static void RequestCommit(Guid alignmentId)
        {
            _pendingCommit = alignmentId;
        }

        internal static Guid? ConsumePendingCommit()
        {
            var v = _pendingCommit;
            _pendingCommit = null;
            return v;
        }
    }
}
