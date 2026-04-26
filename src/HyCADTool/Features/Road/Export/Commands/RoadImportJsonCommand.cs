using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// P1：从与 DWG 同名的 <c>.roaddesign.json</c> 加载 RoadDesign 到 Domain Registry，
    /// 并在当前 DWG 中反向绘制缺失的 Alignment 中心线（已有同 Id 的多段线会保留）。
    ///
    /// 设计：
    /// - 若 JSON 不存在，保留当前内存状态并提示；
    /// - 加载后发布 <see cref="Domain.Events.Road.RoadDesignReloadedEvent"/>（内部逻辑由 <c>RoadDesignRegistry.Replace</c> 或 exporter 触发）；
    /// - 通过 <see cref="RoadAlignmentService.RedrawCenterlines"/> 新建 Polyline，挂 Xdata，写入标准图层。
    ///
    /// 作为 v2 阶段 Blender → AutoCAD 单向同步的基础：
    /// - <c>RoadDesignFileWatcher</c> 检测到 .roaddesign.json 外部改动后，可调用同一命令完成"JSON → DWG"。
    /// </summary>
    public sealed class RoadImportJsonCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var svc = ServiceLocator.Resolve<RoadAlignmentService>();

            string path = RoadJsonExportService.GetDefaultJsonPath(doc.Name);
            if (string.IsNullOrEmpty(path))
            {
                ed.WriteMessage("\n[道路] 当前 DWG 未保存，无法定位 .roaddesign.json。");
                return;
            }

            var design = exporter.Load(path);
            if (design == null)
            {
                ed.WriteMessage($"\n[道路] 未找到 {path}，保持当前内存状态不变。");
                return;
            }

            registry.Replace(doc.Name, design);
            ed.WriteMessage(
                $"\n[道路] 已加载 {path}（Id={design.Id:N}，Alignments={design.Alignments.Count}）。");

            int created;
            int updated;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                (created, updated) = svc.RedrawCenterlines(doc.Name, tr, db);
                tr.Commit();
            }

            if (created == 0 && updated == 0)
            {
                ed.WriteMessage("\n[道路] JSON 中没有可绘制的平面线位（Centerline 顶点不足）。");
            }
            else
            {
                ed.WriteMessage(
                    $"\n[道路] DWG 已对齐 JSON：新建 {created} 条、就地更新 {updated} 条平面线位。");
                if (updated > 0)
                {
                    ed.WriteMessage("\n[道路] 被更新的多段线保留了原 ObjectId 与图层，仅几何（顶点 + 弧段）被刷新。");
                }
            }
        }
    }
}
