using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.App.Bootstrap;
using HyCADTool.Presentation.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadAName</c>：与 <c>hyRoadA</c> 相同拾取多段线登记线位，但每条在导入后提示用户输入显示名称并写回
    /// <see cref="Alignment.Name"/>，适合「新建路线 + 命名」工作流。
    /// </summary>
    public sealed class RoadAlignmentNameCommand
    {
        private sealed class Outcome
        {
            public Alignment Alignment;
            public int RawBulgeNonZeroCount;
            public bool RawHasSegmentArcs;
            public bool RawHasSegmentLines;
            public bool WasNew;
            public readonly List<string> Violations = new List<string>();
            public readonly List<string> Advisories = new List<string>();
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var pso = new PromptSelectionOptions
            {
                MessageForAdding = "\n[道路] 选择多段线作为平面线位中心线（可多选）：",
                AllowDuplicates = false,
            };
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") });
            var psr = ed.GetSelection(pso, filter);
            if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            var outcomes = new List<Outcome>();
            int skipped = 0;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                svc.RebindForDocument(doc.Name, tr, db);

                foreach (SelectedObject so in psr.Value)
                {
                    if (so == null) continue;
                    DBObject picked;
                    try { picked = tr.GetObject(so.ObjectId, OpenMode.ForRead); }
                    catch { skipped++; continue; }

                    if (!(picked is Polyline poly)) { skipped++; continue; }

                    var o = new Outcome();
                    for (int i = 0; i < poly.NumberOfVertices; i++)
                    {
                        var segType = poly.GetSegmentType(i);
                        if (segType == SegmentType.Arc) o.RawHasSegmentArcs = true;
                        else if (segType == SegmentType.Line) o.RawHasSegmentLines = true;
                        if (Math.Abs(poly.GetBulgeAt(i)) > 1e-12) o.RawBulgeNonZeroCount++;
                    }

                    try
                    {
                        poly.UpgradeOpen();
                        o.Alignment = svc.ImportFromPolyline(doc.Name, tr, db, poly, out o.WasNew);
                    }
                    catch (Exception ex)
                    {
                        ed.WriteMessage($"\n[道路] 导入 Handle={so.ObjectId.Handle} 失败：{ex.Message}");
                        skipped++;
                        continue;
                    }

                    if (o.Alignment == null) { skipped++; continue; }

                    if (o.WasNew)
                    {
                        var defaults = SettingsPanelViewModel.Current?.CreateAlignmentDefaults();
                        if (defaults != null) o.Alignment.StartStation = defaults.DefaultStartStation;
                    }

                    // 命名：每条线位提示一次
                    var soName = new PromptStringOptions($"\n[道路] 线位显示名称 <{o.Alignment.Name}>：")
                    {
                        AllowSpaces = true,
                    };
                    var rName = ed.GetString(soName);
                    if (rName.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(rName.StringResult))
                        o.Alignment.Name = rName.StringResult.Trim();

                    outcomes.Add(o);
                }

                tr.Commit();
            }

            if (outcomes.Count == 0)
            {
                ed.WriteMessage($"\n[道路] 未成功导入任何线位（跳过 {skipped}）。");
                return;
            }

            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design))
                savedTo = exporter.SaveForDocument(design, doc.Name);

            foreach (var o in outcomes)
            {
                ed.WriteMessage(
                    $"\n[道路] [{(o.WasNew ? "+" : "~")}] {o.Alignment.Name}：Id={o.Alignment.Id:N}，"
                    + $"顶点 {o.Alignment.Centerline.VertexCount} 个。");

                var validation = o.Alignment.Validate();
                if (!validation.Ok) foreach (var e in validation.Errors) o.Violations.Add(e);

                if (o.RawBulgeNonZeroCount == 0 && !o.RawHasSegmentArcs && !o.RawHasSegmentLines
                    && o.Alignment.Centerline.VertexCount >= 2)
                {
                    o.Violations.Add("未检测到任何弧段/直线段。若屏幕上是曲线，可能是 PEDIT → S 样条拟合。");
                }

                if (o.Alignment.Centerline.HasArcs
                    && (o.Alignment.Source == null
                        || o.Alignment.Source.PiElements == null
                        || o.Alignment.Source.PiElements.Count < 2))
                {
                    o.Advisories.Add("含弧段且无 PI 表：请用 hyRoadAlnByPi 以支持 PI 编辑。");
                }
            }

            if (savedTo != null) ed.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
            else ed.WriteMessage("\n[道路] 未落盘：请先保存 DWG。");

            foreach (var o in outcomes)
                foreach (var a in o.Advisories)
                    ed.WriteMessage($"\n[道路] 提示（{o.Alignment.Name}）：{a}");
            foreach (var o in outcomes)
                if (o.Violations.Count > 0)
                {
                    ed.WriteMessage($"\n[道路] ⚠ {o.Alignment.Name}：");
                    foreach (var v in o.Violations) ed.WriteMessage("\n  · " + v);
                }
        }
    }
}
