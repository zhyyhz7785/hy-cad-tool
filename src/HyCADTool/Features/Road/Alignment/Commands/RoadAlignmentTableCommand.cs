using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Road.PlanAlignment.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Features.Road.PlanAlignment.Views;

using HyCADTool.Features.Road.PlanAlignment.Services;
namespace HyCADTool.Features.Road.PlanAlignment.Commands
{
    /// <summary>
    /// hyRoadAlnTable：Alignment Sub-Entity 全表命令。
    ///
    /// 工作流：
    /// 1. 拾取 DWG 里一条已挂 HY_ROAD Alignment 的 Polyline，从 <see cref="RoadDesignRegistry"/> 拿到 <see cref="Alignment"/>；
    /// 2. 调用 <see cref="AlignmentStationBreakdown.Build"/> 从 <see cref="AlignmentSource.PiElements"/> 构造分段表
    ///    （段 / 几何点），桩号按 <see cref="Alignment.StartStation"/> 累加；
    /// 3. 以 <see cref="AlignmentTableWindow"/> + <see cref="AlignmentTableViewModel"/> 展示两 Tab（段表 / 几何点表）；
    /// 4. 用户在段表里选中一行时，本命令订阅
    ///    <see cref="AlignmentTableViewModel.SegmentSelectionChanged"/> 通过
    ///    <see cref="RoadAlignmentPreviewService"/> 以高亮色瞬态绘出该段，窗口关闭时自动 Clear。
    ///
    /// 只读：不写 Domain、不写 DWG、不发布事件、不落 JSON。
    /// </summary>
    public sealed class RoadAlignmentTableCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions("\n[道路] 拾取要查看分段表的平面线位（HY_ROAD Alignment）：");
            peo.SetRejectMessage("\n[道路] 只能选择已挂 HY_ROAD 的 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            Alignment alignment;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                svc.RebindForDocument(doc.Name, tr, db);

                var ent = tr.GetObject(per.ObjectId, OpenMode.ForRead);
                var kind = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(kind, "Alignment", StringComparison.Ordinal))
                {
                    ed.WriteMessage("\n[道路] 拾取的图元没有 HY_ROAD Alignment 标识。");
                    return;
                }
                var id = HyRoadXdata.ReadId(tr, ent);
                if (id == Guid.Empty)
                {
                    ed.WriteMessage("\n[道路] 拾取的图元缺少 HY_ROAD/ID。");
                    return;
                }
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ed.WriteMessage("\n[道路] 当前 DWG 没有道路设计数据。");
                    return;
                }
                alignment = design.Alignments.FirstOrDefault(a => a.Id == id);
                if (alignment == null)
                {
                    ed.WriteMessage($"\n[道路] Domain 里找不到 AlignmentId={id:N} 的数据。");
                    return;
                }

                tr.Commit();
            }

            if (alignment.Source == null || alignment.Source.PiElements == null || alignment.Source.PiElements.Count < 2)
            {
                ed.WriteMessage(
                    "\n[道路] 该平面线位不是按 PI 创建（或老 JSON 无 PI 表快照）。"
                    + "\n[道路] 请先用 hyRoadAlnByPi 重建，以便生成分段表。");
                return;
            }

            var elements = alignment.Source.PiElements
                .Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag))
                .ToList();

            AlignmentBreakdown breakdown;
            try
            {
                breakdown = AlignmentStationBreakdown.Build(
                    elements,
                    alignment.StartStation,
                    new PiDesignOptions(),
                    alignment.StationEquations);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 分段分解失败：{ex.Message}");
                return;
            }

            ed.WriteMessage(
                $"\n[道路] {alignment.Name}：段数 {breakdown.Segments.Count}、几何点 {breakdown.GeometryPoints.Count}、"
                + $"总长 {breakdown.TotalLengthM:F3} m，起桩 "
                + $"{AlignmentStationBreakdown.FormatStation(breakdown.StartStationM)}，"
                + $"止桩 {AlignmentStationBreakdown.FormatStation(breakdown.EndStationM)}。");

            var vm = new AlignmentTableViewModel(alignment, breakdown);
            var window = new AlignmentTableWindow(vm);

            // 使用 Preview 服务做"段高亮"：用红色瞬态 Drawable，窗口关闭 using 自动 Clear。
            using (var highlight = new RoadAlignmentPreviewService { ColorIndex = 1 /* 红 */ })
            {
                vm.SegmentSelectionChanged += (_, row) =>
                {
                    if (row == null)
                    {
                        highlight.Clear();
                        return;
                    }

                    var highlightPoly = BuildSegmentHighlight(row.Source);
                    highlight.Update(highlightPoly);
                };

                AcApp.ShowModalWindow(window);
            }
        }

        /// <summary>
        /// 为一段 <see cref="SegmentRecord"/> 构造用于瞬态高亮的 <see cref="Polyline3D"/>。
        /// - 直线段：两顶点直线；
        /// - 圆曲线段：两顶点 + bulge = tan(Δψ/4)；
        /// - 缓和段：两顶点直线（弦近似；真实缓和曲线形态已由原始 Alignment Polyline 呈现，
        ///   这里高亮只起"视觉定位"作用，精度不影响功能）。
        /// </summary>
        private static Polyline3D BuildSegmentHighlight(SegmentRecord r)
        {
            var verts = new[]
            {
                new Point3D(r.StartPoint.X, r.StartPoint.Y, 0),
                new Point3D(r.EndPoint.X, r.EndPoint.Y, 0),
            };

            double bulge = 0;
            if (r.Kind == SegmentKind.Arc)
            {
                double dPsi = NormalizePi(r.EndBearingRad - r.StartBearingRad);
                bulge = Math.Tan(dPsi / 4.0);
            }

            return new Polyline3D(verts, isClosed: false, bulges: new[] { bulge, 0.0 });
        }

        private static double NormalizePi(double rad)
        {
            while (rad > Math.PI) rad -= 2 * Math.PI;
            while (rad <= -Math.PI) rad += 2 * Math.PI;
            return rad;
        }
    }
}
