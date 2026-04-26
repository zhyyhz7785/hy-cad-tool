using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using HyCADTool.Features.Road.PlanAlignment.Services;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCADTool.Features.Road.PlanAlignment.Commands;
using HyCADTool.Features.Road.CrossSection.Commands;
namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadAutoIntersection</c>：扫描当前图内全部路线中心线，两两求平面真交点并聚类，再按用户选择批量生成 <see cref="Intersection"/>。
    /// </summary>
    public sealed class RoadAutoIntersectionCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var ixSvc = ServiceLocator.Resolve<RoadIntersectionService>();
            var alnSvc = ServiceLocator.Resolve<RoadAlignmentService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            if (!registry.TryGet(doc.Name, out var design) || design == null || design.Alignments.Count < 2)
            {
                ed.WriteMessage("\n[道路] 至少需要 2 条路线才可自动检测交叉口。");
                return;
            }

            var det = ServiceLocator.Resolve<IntersectionDetector>();
            var clusters = det.Detect(design.Alignments, 0.5);
            if (clusters.Count == 0)
            {
                ed.WriteMessage("\n[道路] 未发现路线中心线之间的平面交点。");
                return;
            }

            ed.WriteMessage($"\n[道路] 发现 {clusters.Count} 个交叉口候选：");
            for (int i = 0; i < clusters.Count; i++)
            {
                var c = clusters[i];
                var names = c.AlignmentIds
                    .Select(id => design.Alignments.FirstOrDefault(a => a.Id == id)?.Name ?? id.ToString("N"))
                    .ToArray();
                ed.WriteMessage(
                    $"\n  {i + 1}. 中心≈({c.Center.X:F2},{c.Center.Y:F2})  参与：{string.Join("，", names)}");
            }

            var so = new PromptStringOptions(
                "\n[道路] 输入要生成的序号（1 起，多个用逗号；A=全部；回车取消）：")
            {
                AllowSpaces = true,
            };
            var rs = ed.GetString(so);
            if (rs.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(rs.StringResult))
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            var raw = rs.StringResult.Trim();
            List<int> pick;
            if (string.Equals(raw, "A", StringComparison.OrdinalIgnoreCase)
                || string.Equals(raw, "全部", StringComparison.Ordinal))
            {
                pick = Enumerable.Range(1, clusters.Count).ToList();
            }
            else
            {
                pick = new List<int>();
                foreach (var part in raw.Split(new[] { ',', '，', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                        && v >= 1 && v <= clusters.Count)
                    {
                        if (!pick.Contains(v)) pick.Add(v);
                    }
                }
            }

            if (pick.Count == 0)
            {
                ed.WriteMessage("\n[道路] 无有效序号。");
                return;
            }

            var optR = new PromptDoubleOptions(
                $"\n[道路] 转角半径 R (默认 {Intersection.DefaultCornerRadiusValue:F1} m)：")
            {
                DefaultValue = Intersection.DefaultCornerRadiusValue,
                UseDefaultValue = true, AllowNone = true, AllowNegative = false, AllowZero = false,
            };
            var rR = ed.GetDouble(optR);
            double radius = rR.Status == PromptStatus.OK ? rR.Value : Intersection.DefaultCornerRadiusValue;

            var optW = new PromptDoubleOptions(
                $"\n[道路] 半宽 W (默认 {IntersectionLeg.DefaultHalfWidth:F1} m)：")
            {
                DefaultValue = IntersectionLeg.DefaultHalfWidth,
                UseDefaultValue = true, AllowNone = true, AllowNegative = false, AllowZero = false,
            };
            var rW = ed.GetDouble(optW);
            double halfWidth = rW.Status == PromptStatus.OK ? rW.Value : IntersectionLeg.DefaultHalfWidth;

            var optV = new PromptDoubleOptions("\n[道路] 设计速度 V (km/h，默认 30)：")
            {
                DefaultValue = 30, UseDefaultValue = true, AllowNone = true, AllowNegative = false, AllowZero = false,
            };
            var rV = ed.GetDouble(optV);
            double designSpeed = rV.Status == PromptStatus.OK ? rV.Value : 30;

            int created = 0, failed = 0;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                alnSvc.RebindForDocument(doc.Name, tr, db);

                foreach (var idx in pick.OrderBy(x => x))
                {
                    var cl = clusters[idx - 1];
                    var aligns = cl.AlignmentIds
                        .Select(id => design.Alignments.FirstOrDefault(a => a.Id == id))
                        .Where(a => a != null)
                        .Cast<Alignment>()
                        .ToList();
                    if (aligns.Count < 2)
                    {
                        failed++;
                        continue;
                    }

                    try
                    {
                        var built = IntersectionDesigner.ComputeFromAlignments(
                            aligns,
                            cl.Center,
                            radius,
                            halfWidth,
                            name: $"自动交叉口 {idx}",
                            designSpeed: designSpeed);
                        design.Intersections.Add(built);
                        ixSvc.RebuildIntersection(tr, db, built, HyRoadLayers.IntersectionLayer);
                        created++;
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\n[道路][警告] 候选 {idx} 生成失败：{ex.Message}");
                        failed++;
                    }
                }

                tr.Commit();
            }

            try
            {
                if (registry.TryGet(doc.Name, out var dSave))
                {
                    var p = exporter.SaveForDocument(dSave, doc.Name);
                    if (p != null) ed.WriteMessage($"\n[道路] JSON：{p}");
                }
            }
            catch (Exception ex) { ed.WriteMessage($"\n[道路][警告] 落盘失败：{ex.Message}"); }

            ed.WriteMessage($"\n[道路] 自动交叉口：成功 {created}，失败 {failed}。");
        }
    }
}
