using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// P1：为当前 DWG 里已登记的每条 Alignment 生成桩号标注（主桩 20m + 副桩 5m，默认）。
    ///
    /// 交互：
    /// - 无参数调用，一次处理 Registry 里当前 doc 的全部 Alignment；
    /// - 幂等：内部先按 AlignmentId 清掉旧标注实体，再重新生成，重复跑不会累积图元；
    /// - 未登记任何 Alignment 时给出清晰提示，引导用户先跑 <c>hyRoadA</c>。
    ///
    /// 落图：
    /// - 短刻度线（<see cref="Line"/>） + 主桩桩号文字（<see cref="DBText"/>），统一落到
    ///   <see cref="Infrastructure.AutoCAD.Xdata.HyRoadLayers.StationLayer"/> 图层；
    /// - 每个实体挂 HY_ROAD Xdata（KIND=StationLabel，ID=AlignmentId），为 v1.1 的"改 Alignment → 自动刷新标注"打底。
    ///
    /// 性能：桩号标注纯粹是 DWG 层视觉元素，不修改 Domain，因此不触发事件总线，也不写 JSON。
    /// </summary>
    public sealed class RoadAlignmentStationCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 与 hyRoadA 一致：SAVEAS 后先 rebind，避免老 key 下的 alignment 被忽略
                svc.RebindForDocument(doc.Name, tr, db);

                if (!registry.TryGet(doc.Name, out var design) || design.Alignments.Count == 0)
                {
                    ed.WriteMessage(
                        "\n[道路] 当前图纸未登记任何平面线位，无法生成桩号。"
                        + "\n       请先运行 hyRoadA 拾取一条多段线作为 Alignment。");
                    tr.Abort();
                    return;
                }

                var options = RoadStationLabelOptions.Default;

                int totalMain = 0;
                int totalSub = 0;
                int touched = 0;
                // 拷一份 Alignment 列表，避免 DrawStationLabels 内部读 Registry 过程中 Mutation。
                var snapshot = design.Alignments.ToArray();
                foreach (var a in snapshot)
                {
                    if (a.Centerline == null || a.Centerline.VertexCount < 2)
                    {
                        ed.WriteMessage(
                            $"\n[道路] 跳过 {a.Name}（Id={a.Id:N}）：中心线顶点不足 2 个。");
                        continue;
                    }
                    var (mainCount, subCount) = svc.DrawStationLabels(doc.Name, tr, db, a.Id, options);
                    totalMain += mainCount;
                    totalSub += subCount;
                    touched++;
                    ed.WriteMessage(
                        $"\n[道路] {a.Name}：主桩 {mainCount}（每 {options.MainInterval} m）、副桩 {subCount}（每 {options.SubInterval} m），"
                        + $"平面长度 {a.Centerline.GetPlanarLength():F3} m。");
                }

                tr.Commit();

                if (touched == 0)
                {
                    ed.WriteMessage("\n[道路] 没有可标注的 Alignment。");
                }
                else
                {
                    ed.WriteMessage(
                        $"\n[道路] 桩号标注完成：共 {touched} 条 Alignment，主桩 {totalMain}、副桩 {totalSub}；"
                        + $"已写入图层 {Infrastructure.AutoCAD.Xdata.HyRoadLayers.StationLayer}。");
                }
            }
        }
    }
}
