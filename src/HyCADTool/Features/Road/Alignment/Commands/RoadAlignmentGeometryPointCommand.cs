using System.Linq;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Features.Road.PlanAlignment.Domain;

using HyCADTool.Features.Road.PlanAlignment.Services;
namespace HyCADTool.Features.Road.PlanAlignment.Commands
{
    /// <summary>
    /// hyRoadAlnGeomPt：为当前 DWG 里所有 Alignment 生成几何点标注
    /// （BP / EP / BC / EC / TS / SC / CS / ST）。
    ///
    /// 交互：
    /// - 无参数调用，一次处理 Registry 里当前 doc 的全部 Alignment；
    /// - 幂等：<see cref="RoadAlignmentService.DrawGeometryPointLabels"/> 内部先按
    ///   AlignmentId 清掉同类标注，再重建；
    /// - Alignment 若不是按 PI 创建（<c>Source.PiElements</c> 为空）则跳过并提示。
    ///
    /// 落图走 <see cref="Infrastructure.AutoCAD.Xdata.HyRoadLayers.GeometryPointLayer"/>；
    /// 每实体挂 HY_ROAD XData（KIND=GeometryPointLabel，ID=AlignmentId）。
    ///
    /// 不修改 Domain / 不发布事件 / 不写 JSON。
    /// </summary>
    public sealed class RoadAlignmentGeometryPointCommand
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
                svc.RebindForDocument(doc.Name, tr, db);

                if (!registry.TryGet(doc.Name, out var design) || design.Alignments.Count == 0)
                {
                    ed.WriteMessage(
                        "\n[道路] 当前图纸未登记任何平面线位，无法生成几何点标注。"
                        + "\n       请先运行 hyRoadAlnByPi / hyRoadA 创建一条线位。");
                    tr.Abort();
                    return;
                }

                var options = RoadGeometryPointLabelOptions.Default;

                int totalPoints = 0;
                int touched = 0;
                int skipped = 0;
                var snapshot = design.Alignments.ToArray();
                foreach (var a in snapshot)
                {
                    if (a.Source == null || a.Source.PiElements == null || a.Source.PiElements.Count < 2)
                    {
                        ed.WriteMessage(
                            $"\n[道路] 跳过 {a.Name}（Id={a.Id:N}）：非 PI 法创建（没有 PI 表快照）。");
                        skipped++;
                        continue;
                    }
                    int n = svc.DrawGeometryPointLabels(doc.Name, tr, db, a.Id, options);
                    if (n == 0)
                    {
                        ed.WriteMessage($"\n[道路] {a.Name}：未生成几何点（可能已被 Build 判为全直线）。");
                        continue;
                    }
                    totalPoints += n;
                    touched++;
                    ed.WriteMessage($"\n[道路] {a.Name}：几何点 {n} 个。");
                }

                tr.Commit();

                if (touched == 0)
                {
                    ed.WriteMessage("\n[道路] 没有可标注几何点的 Alignment。");
                }
                else
                {
                    ed.WriteMessage(
                        $"\n[道路] 几何点标注完成：共 {touched} 条 Alignment，点 {totalPoints} 个；"
                        + $"已写入图层 {HyCADTool.Shared.AutoCAD.Xdata.HyRoadLayers.GeometryPointLayer}。"
                        + (skipped > 0 ? $"（{skipped} 条因非 PI 法被跳过）" : string.Empty));
                }
            }
        }
    }
}
