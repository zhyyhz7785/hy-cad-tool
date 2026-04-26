using System;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// 命令层共享的"拾取 Alignment + 弹 SaveFileDialog + 把 CSV 写到 UTF-8 BOM 文件"工作流。
    /// 供 <see cref="RoadAlignmentExportPiCommand"/> / <see cref="RoadAlignmentExportFrameCommand"/> 复用，
    /// 避免两个命令各写一份 AutoCAD 样板代码。
    /// </summary>
    internal static class AlignmentExportHelper
    {
        public delegate string CsvBuilder(Alignment alignment);

        public static void PickAndExport(
            string tag,
            string defaultFileNameSuffix,
            CsvBuilder build)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var peo = new PromptEntityOptions($"\n[道路] 拾取要导出 {tag} 的平面线位（HY_ROAD Alignment）：");
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
                    $"\n[道路] {tag} 仅支持按 PI 创建的 Alignment（当前 Source.PiElements 为空）。");
                return;
            }

            string defaultFileName = SanitizeFileName(alignment.Name ?? "Alignment") + defaultFileNameSuffix;
            var saveDialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "CSV 文件|*.csv",
                Title = $"导出 {tag}",
                FileName = defaultFileName,
                DefaultExt = "csv",
                AddExtension = true,
            };
            if (saveDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            try
            {
                string csv = build(alignment);
                File.WriteAllBytes(saveDialog.FileName, AlignmentReportService.ToUtf8BomBytes(csv));
                ed.WriteMessage($"\n[道路] {tag} 已导出：{saveDialog.FileName}");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] {tag} 导出失败：{ex.Message}");
            }
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Alignment";
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }
    }

    /// <summary>hyRoadAlnExportPi：导出交点表（PI 表）到 CSV。</summary>
    public sealed class RoadAlignmentExportPiCommand
    {
        public void Execute() => AlignmentExportHelper.PickAndExport(
            tag: "交点表(PI 表)",
            defaultFileNameSuffix: "_交点表.csv",
            build: a => AlignmentReportService.BuildPiTableCsv(a));
    }

    /// <summary>hyRoadAlnExportFrame：导出复测表 / 几何框架表到 CSV。</summary>
    public sealed class RoadAlignmentExportFrameCommand
    {
        public void Execute() => AlignmentExportHelper.PickAndExport(
            tag: "复测表(几何框架表)",
            defaultFileNameSuffix: "_复测表.csv",
            build: a => AlignmentReportService.BuildFrameTableCsv(a));
    }
}
