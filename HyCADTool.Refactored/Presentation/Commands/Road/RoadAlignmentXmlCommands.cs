using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
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

            var peo = new PromptEntityOptions("\n[道路] 拾取要导出 LandXML 的平面线位（HY_ROAD Alignment）：");
            peo.SetRejectMessage("\n[道路] 只能选择已挂 HY_ROAD 的 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

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
                if (id == Guid.Empty) { ed.WriteMessage("\n[道路] 图元缺少 HY_ROAD/ID。"); return; }
                if (!registry.TryGet(doc.Name, out var design))
                {
                    ed.WriteMessage("\n[道路] 当前 DWG 没有道路设计数据。");
                    return;
                }
                alignment = design.Alignments.FirstOrDefault(a => a.Id == id);
                if (alignment == null) { ed.WriteMessage($"\n[道路] Domain 里找不到 AlignmentId={id:N}。"); return; }
                tr.Commit();
            }

            if (alignment.Source == null || alignment.Source.PiElements == null || alignment.Source.PiElements.Count < 2)
            {
                ed.WriteMessage("\n[道路] LandXML 导出仅支持按 PI 创建的 Alignment（当前 Source.PiElements 为空）。");
                return;
            }

            string defaultFileName = SanitizeFileName(alignment.Name ?? "Alignment") + "_alignment.xml";
            var saveDialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "LandXML 1.2|*.xml",
                Title = "导出 LandXML 1.2",
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
                LandXmlExportService.SaveToFile(alignment, saveDialog.FileName);
                int segs = alignment.Source?.PiElements?.Count > 0
                    ? alignment.Source.PiElements.Count
                    : 0;
                ed.WriteMessage($"\n[道路] LandXML 1.2 已导出：{saveDialog.FileName}（PI={segs}，方程={alignment.StationEquations?.Count ?? 0}）。");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] LandXML 导出失败：{ex.Message}");
            }
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

            Alignment imported;
            try
            {
                imported = LandXmlImportService.LoadFromFile(openDialog.FileName);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] LandXML 解析失败：{ex.Message}");
                return;
            }

            if (imported.Centerline == null || imported.Centerline.VertexCount < 2)
            {
                ed.WriteMessage("\n[道路] LandXML 中未得到合法 Centerline（顶点 < 2）。");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                svc.RebindForDocument(doc.Name, tr, db);
                var design = registry.GetOrCreate(doc.Name);
                design.Alignments.Add(imported);

                var acadPoly = RoadGeometryBridge.ToAutoCadPolyline(imported.Centerline);
                if (LayerExistsStatic(tr, db, HyRoadLayers.AlignmentLayer))
                    acadPoly.Layer = HyRoadLayers.AlignmentLayer;

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                ms.AppendEntity(acadPoly);
                tr.AddNewlyCreatedDBObject(acadPoly, true);

                HyRoadXdata.Write(tr, db, acadPoly, imported.Id, "Alignment", SchemaVersion.Current);
                tr.Commit();
            }

            string savedTo = null;
            if (registry.TryGet(doc.Name, out var designOut))
                savedTo = exporter.SaveForDocument(designOut, doc.Name);

            int eqCount = imported.StationEquations?.Count ?? 0;
            int elCount = imported.Elements?.Count ?? 0;
            ed.WriteMessage(
                $"\n[道路] 已导入 LandXML：{imported.Name ?? "-"}，顶点 {imported.Centerline.VertexCount}，"
                + $"元素 {elCount}，方程 {eqCount}。");
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
