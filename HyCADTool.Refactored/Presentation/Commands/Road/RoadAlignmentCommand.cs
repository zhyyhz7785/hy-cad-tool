using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// P1：拾取 AutoCAD 多段线 → 抽取顶点 → 创建 / 同步 Domain Alignment → 写 Xdata → 发布事件。
    ///
    /// 交互：
    /// - 在命令行提示"选择一条多段线作为平面线位中心线"；
    /// - 仅接受 2D <c>Polyline</c>（<c>Polyline3d</c> / <c>Line</c> 在 P1 先不支持，P3 再并入）；
    /// - ESC / 右键取消时优雅退出，不抛异常；
    /// - 同一对象重复拾取 → 更新现有 Alignment 的 Centerline（依靠 Xdata 中的 GUID 识别）。
    ///
    /// 架构：
    /// - 命令本身保持"薄壳"：只负责交互与 Transaction 生命周期；
    /// - 几何转换 + Xdata 由 <see cref="RoadAlignmentService.ImportFromPolyline"/> 完成；
    /// - JSON 持久化 v1.1 起改为命令收尾同步调用 <see cref="RoadJsonExportService.SaveForDocument"/>，无异步 Timer。
    /// </summary>
    public sealed class RoadAlignmentCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions("\n[道路] 选择一条多段线作为平面线位中心线：");
            peo.SetRejectMessage("\n只能选择二维多段线（Polyline）。");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: true);
            peo.AllowNone = false;

            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            Alignment alignment;
            int rawBulgeNonZeroCount = 0;
            bool rawHasSegmentArcs = false;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var picked = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                if (!(picked is Polyline poly))
                {
                    // AddAllowedClass(exactMatch:true) 理论上拦住了这条路径，但保留兜底（SDK 历史版本有差异）
                    ed.WriteMessage(
                        $"\n[道路] 所选实体类型为 {picked?.GetType().Name ?? "null"}，当前仅支持 LWPOLYLINE（AcDbPolyline）。"
                        + "\n       若是 2dPolyline（heavyweight） / 3dPolyline / Spline / Line+Arc，请先 PEDIT 或 DXF 转为 LWPOLYLINE。");
                    tr.Abort();
                    return;
                }

                // 读原始 bulge 诊断信息（直接问 AutoCAD，反映"DWG 端真实存了什么"）。
                // 视觉像弧但 bulge==0 的典型场景：PEDIT → S（Spline）拟合 —— 顶点不变、曲线靠样条控制点计算，
                // bulge 一律为 0，Polyline3D.ShouldSerializeBulges 就会省略 JSON 字段。
                for (int i = 0; i < poly.NumberOfVertices; i++)
                {
                    var segType = poly.GetSegmentType(i);
                    if (segType == SegmentType.Arc) rawHasSegmentArcs = true;
                    if (Math.Abs(poly.GetBulgeAt(i)) > 1e-12) rawBulgeNonZeroCount++;
                }

                // Xdata 写入需要 ForWrite
                poly.UpgradeOpen();

                // 先 rebind：如果 SAVEAS 把 doc.Name 换成了新路径，Registry 里的 design 还挂在老 key，
                // 不 rebind 就会让 ImportFromPolyline 的 GetOrCreate 在新 key 下建空壳，Xdata GUID 找不到匹配 Alignment，
                // 最终这条 polyline 会被当作"新 Alignment"加进空壳 design，老 key 下的那条就变成 JSON 孤儿。
                svc.RebindForDocument(doc.Name, tr, db);

                alignment = svc.ImportFromPolyline(doc.Name, tr, db, poly);
                tr.Commit();
            }

            // 命令收尾同步落盘（v1.1 取代原防抖持久化服务）。
            // 在事务外执行，避免 I/O 延迟拉长事务生命周期；此时 doc.Name 与命令上下文一致，不存在异步时序问题。
            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design))
            {
                savedTo = exporter.SaveForDocument(design, doc.Name);
            }

            ed.WriteMessage(
                $"\n[道路] 平面线位已登记 {alignment.Name}："
                + $"Id={alignment.Id:N}，顶点 {alignment.Centerline.VertexCount} 个，"
                + $"含弧段 {rawBulgeNonZeroCount} 段，"
                + $"平面长度 {alignment.Centerline.GetPlanarLength():F3} m。");

            // 诊断：视觉疑似弧但 bulge 全 0 时主动提示常见原因。
            if (rawBulgeNonZeroCount == 0 && !rawHasSegmentArcs)
            {
                ed.WriteMessage(
                    "\n[道路] ⚠ 未检测到任何弧段（所有 bulge=0）。"
                    + "\n       若 AutoCAD 里看到的是曲线，常见两种原因："
                    + "\n       1. PEDIT → S（Spline）样条拟合：顶点 bulge=0，曲线靠样条控制点计算。"
                    + "\n          修复：PEDIT → D（Decurve）还原折线 → PEDIT → F（Fit）圆弧拟合 → 重跑 hyRoadA。"
                    + "\n       2. PLINE 时未按 A 切入 Arc 子模式，只是用直线近似弧。"
                    + "\n          修复：重画时按 A 切圆弧模式，或 ARC + PEDIT → J 合并成 LWPOLYLINE。");
            }

            if (savedTo != null)
                ed.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
            else
                ed.WriteMessage("\n[道路] 未落盘（DWG 尚未保存或 RoadDesign 为空）。先保存 DWG 后再次运行 hyRoadSave 即可。");
        }
    }
}
