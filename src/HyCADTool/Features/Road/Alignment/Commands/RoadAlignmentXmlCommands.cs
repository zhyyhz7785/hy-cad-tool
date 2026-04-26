using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Features.Road.PlanAlignment.Services;

namespace HyCADTool.Features.Road.PlanAlignment.Commands
{
    /// <summary>
    /// <c>hyRoadAlnExportXml</c> — 将拾取的平面线位导出为 LandXML 1.2。
    ///
    /// 流程：
    /// 1) 拾取 HY_ROAD Alignment Polyline；
    /// 2) 定位 Domain 对象，校验 <c>Source.PiElements</c> 非空（按 PI 创建的 Alignment）；
    /// 3) 弹 SaveFileDialog（默认扩展 .xml）；
    /// 4) 调 <see cref="LandXmlExportService.SaveToFile"/> 落盘。
    ///
    /// <b>覆盖要点</b>：
    /// <list type="bullet">
    ///   <item>CoordGeom：Line / Curve / Spiral 参数完整（半径、方位、转向、半径起 / 终）；</item>
    ///   <item>StaEquation：一条一个，字段 staAhead / staBack / staInternal；</item>
    ///   <item>坐标顺序：LandXML 惯例 "北 东"（Y X）；</item>
    ///   <item>方位：十进制度（North=0, CW）；内部 rad/CCW 自动换算。</item>
    /// </list>
    /// </summary>
    public sealed class RoadAlignmentExportXmlCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            // Rebind 一次（避免 SAVEAS 后 key 漂移）
            try
            {
                using (doc.LockDocument())
                using (var tr0 = db.TransactionManager.StartTransaction())
                {
                    svc.RebindForDocument(doc.Name, tr0, db);
                    tr0.Commit();
                }
            }
            catch { /* ignore */ }

            if (!registry.TryGet(doc.Name, out var design) || design.Alignments.Count == 0)
            {
                ed.WriteMessage("\n[道路] 当前 DWG 未登记任何 Alignment。");
                return;
            }

            // 范围选择：A=全部 / P=拾取（多选）
            var pko = new PromptKeywordOptions("\n[道路] 选择导出范围 [全部(A)/拾取(P)] <全部>：");
            pko.Keywords.Add("A");
            pko.Keywords.Add("P");
            pko.Keywords.Default = "A";
            pko.AllowNone = true;
            var pkr = ed.GetKeywords(pko);
            if (pkr.Status != PromptStatus.OK && pkr.Status != PromptStatus.None)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }
            string scope = string.IsNullOrEmpty(pkr.StringResult) ? "A" : pkr.StringResult;

            List<Alignment> alignments;
            if (string.Equals(scope, "A", StringComparison.OrdinalIgnoreCase))
            {
                alignments = design.Alignments
                    .Where(a => a?.Centerline != null && a.Centerline.VertexCount >= 2)
                    .ToList();
            }
            else
            {
                alignments = PickAlignments(doc, ed, db, svc, registry);
                if (alignments == null) return;
            }

            // 仅保留 PI 法 / 或具备 Centerline 兜底的（兜底分支会用 Centerline 折线）
            alignments = alignments
                .Where(a => a != null && a.Centerline != null && a.Centerline.VertexCount >= 2)
                .ToList();
            if (alignments.Count == 0)
            {
                ed.WriteMessage("\n[道路] 选中范围内无可导出的 Alignment。");
                return;
            }

            string defaultFileName;
            try
            {
                defaultFileName = SanitizeFileName(Path.GetFileNameWithoutExtension(doc.Name)
                    ?? alignments[0].Name ?? "Alignment");
            }
            catch { defaultFileName = "Alignment"; }
            defaultFileName += $"_alignments_x{alignments.Count}.xml";

            var saveDialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "LandXML 1.2|*.xml",
                Title = $"导出 LandXML 1.2（{alignments.Count} 条线位）",
                FileName = defaultFileName,
                DefaultExt = "xml",
                AddExtension = true,
            };
            if (saveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            try
            {
                string projectName = Path.GetFileNameWithoutExtension(doc.Name) ?? alignments[0].Name;
                LandXmlExportService.SaveAllToFile(alignments, saveDialog.FileName, options: null, projectName: projectName);
                int piTotal = alignments.Sum(a => a.Source?.PiElements?.Count ?? 0);
                int eqTotal = alignments.Sum(a => a.StationEquations?.Count ?? 0);
                ed.WriteMessage(
                    $"\n[道路] LandXML 1.2 已导出：{saveDialog.FileName}"
                    + $"（线位 {alignments.Count}，PI 合计 {piTotal}，方程合计 {eqTotal}）。");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] LandXML 导出失败：{ex.Message}");
            }
        }

        /// <summary>多选拾取 HY_ROAD Alignment Polyline。已挂 KIND=Alignment 才纳入；按 ID 去重。</summary>
        private static List<Alignment> PickAlignments(
            Autodesk.AutoCAD.ApplicationServices.Document doc,
            Editor ed, Database db,
            RoadAlignmentService svc, RoadDesignRegistry registry)
        {
            var pso = new PromptSelectionOptions
            {
                MessageForAdding = "\n[道路] 选择要导出的 HY_ROAD Alignment Polyline（可多选，回车结束）：",
                AllowDuplicates = false,
            };
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") });
            var psr = ed.GetSelection(pso, filter);
            if (psr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return null;
            }

            var seen = new HashSet<Guid>();
            var picked = new List<Alignment>();
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                svc.RebindForDocument(doc.Name, tr, db);
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ed.WriteMessage("\n[道路] 当前 DWG 未登记任何 Alignment。");
                    return null;
                }
                foreach (SelectedObject so in psr.Value)
                {
                    if (so == null) continue;
                    var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead);
                    var kind = HyRoadXdata.ReadKind(tr, ent);
                    if (!string.Equals(kind, "Alignment", StringComparison.Ordinal)) continue;
                    var id = HyRoadXdata.ReadId(tr, ent);
                    if (id == Guid.Empty || !seen.Add(id)) continue;
                    var aln = design.Alignments.FirstOrDefault(a => a.Id == id);
                    if (aln != null) picked.Add(aln);
                }
                tr.Commit();
            }

            if (picked.Count == 0)
                ed.WriteMessage("\n[道路] 拾取项中无 HY_ROAD Alignment。");
            return picked;
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Alignment";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }
    }

    /// <summary>
    /// <c>hyRoadAlnImportXml</c> — 从 LandXML 1.2 导入平面线位。
    ///
    /// 流程：
    /// 1) 弹 OpenFileDialog 选 LandXML 文件；
    /// 2) <see cref="LandXmlImportService.LoadFromFile"/> 产出一个 Domain <see cref="Alignment"/>
    ///    （含 Centerline + Elements + StationEquations，<b>不含</b> PI 源）；
    /// 3) 把 Alignment 登记到 <see cref="RoadDesignRegistry"/>，并在 ModelSpace 画出一条 Polyline，
    ///    挂 HY_ROAD XData（KIND=Alignment, ID=alignment.Id）+ 分配道路图层；
    /// 4) 保存 JSON（若 DWG 有路径）。
    ///
    /// <b>导入后行为</b>：后续命令可对它跑 <c>hyRoadAlnStation</c> / <c>hyRoadAlnGeomPt</c> /
    /// <c>hyRoadAlnStaEq</c> / <c>hyRoadAlnOffset</c> / <c>hyRoadAlnExportXml</c>；
    /// 因为没有 PI，<c>hyRoadAlnEditPi</c> / <c>hyRoadAlnInsertPi</c> / <c>hyRoadAlnReverse</c> /
    /// <c>hyRoadAlnExportPi</c> 暂不可用。
    /// </summary>
    public sealed class RoadAlignmentImportXmlCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var openDialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "LandXML 1.2|*.xml|所有文件|*.*",
                Title = "导入 LandXML 1.2",
                CheckFileExists = true,
            };
            if (openDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            IReadOnlyList<Alignment> importedList;
            IReadOnlyList<string> parseErrors;
            try
            {
                importedList = LandXmlImportService.LoadAllFromFile(openDialog.FileName, out parseErrors);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] LandXML 解析失败：{ex.Message}");
                return;
            }

            if (parseErrors != null)
            {
                foreach (var msg in parseErrors)
                    ed.WriteMessage($"\n[道路] (跳过) {msg}");
            }

            var validList = importedList
                .Where(a => a != null && a.Centerline != null && a.Centerline.VertexCount >= 2)
                .ToList();
            if (validList.Count == 0)
            {
                ed.WriteMessage("\n[道路] LandXML 中未得到任何合法 Centerline（顶点 < 2）。");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            int totalEq = 0, totalEl = 0, totalVertex = 0;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                svc.RebindForDocument(doc.Name, tr, db);
                var design = registry.GetOrCreate(doc.Name);

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                foreach (var imported in validList)
                {
                    design.Alignments.Add(imported);
                    var acadPoly = RoadGeometryBridge.ToAutoCadPolyline(imported.Centerline);
                    if (LayerExistsStatic(tr, db, HyRoadLayers.AlignmentLayer))
                        acadPoly.Layer = HyRoadLayers.AlignmentLayer;

                    ms.AppendEntity(acadPoly);
                    tr.AddNewlyCreatedDBObject(acadPoly, true);

                    HyRoadXdata.Write(tr, db, acadPoly, imported.Id, "Alignment", SchemaVersion.Current);

                    totalEq += imported.StationEquations?.Count ?? 0;
                    totalEl += imported.Elements?.Count ?? 0;
                    totalVertex += imported.Centerline.VertexCount;
                }

                tr.Commit();
            }

            string savedTo = null;
            if (registry.TryGet(doc.Name, out var designOut))
                savedTo = exporter.SaveForDocument(designOut, doc.Name);

            ed.WriteMessage(
                $"\n[道路] 已导入 LandXML：{validList.Count} 条线位"
                + $"（顶点 {totalVertex}，元素 {totalEl}，方程 {totalEq}"
                + (parseErrors != null && parseErrors.Count > 0 ? $"，失败 {parseErrors.Count}" : "")
                + "）。");
            if (savedTo != null) ed.WriteMessage($"\n[道路] JSON 已同步：{savedTo}");
        }

        private static bool LayerExistsStatic(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }
    }
}
