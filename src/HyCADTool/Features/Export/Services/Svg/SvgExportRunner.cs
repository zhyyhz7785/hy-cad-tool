using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace HyCADTool.Features.Export.Services.Svg
{
    internal static class SvgExportRunner
    {
        public static bool TryExport(
            Document doc,
            SvgPlotSettings settings,
            string outputPath,
            out SvgExportStats stats,
            out string error)
        {
            stats = new SvgExportStats();
            error = null;
            if (doc == null)
            {
                error = "没有活动文档。";
                return false;
            }
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                error = "输出路径为空。";
                return false;
            }

            var db = doc.Database;
            var ed = doc.Editor;
            List<ObjectId> selectionIds = null;
            if (settings.Area == SvgPlotArea.Selection)
            {
                selectionIds = new List<ObjectId>();
                CollectSelection(ed, selectionIds);
                if (selectionIds.Count == 0)
                {
                    error = "选择集为空。请先选择实体，或改用图形范围。";
                    return false;
                }
            }

            string svg;
            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    if (!TryResolveArea(ed, tr, db, settings, selectionIds, out var ids, out var window, out error))
                        return false;

                    var source = new List<ObjectId>(ids);
                    stats.SourceEntityCount = 0;
                    var filtered = SvgEntityFlattener.FilterIntersecting(tr, source, window);
                    var flattener = new SvgEntityFlattener(tr, db);
                    var primitives = flattener.Flatten(filtered, stats);

                    var content = window;
                    if (settings.IgnoreBlank && SvgEntityFlattener.TryContentBounds(primitives, out var box))
                    {
                        var clipped = SvgPlotRect.Intersect(window, box);
                        content = clipped.IsValid ? clipped : box;
                    }

                    var layout = SvgPlotLayout.Compute(settings, content);
                    svg = SvgDocumentWriter.Write(primitives, settings, layout);
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            try
            {
                string dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(outputPath, svg, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                error = "写文件失败: " + ex.Message;
                return false;
            }

            stats.OutputPath = outputPath;
            return true;
        }

        private static bool TryResolveArea(
            Editor ed,
            Transaction tr,
            Database db,
            SvgPlotSettings settings,
            List<ObjectId> selectionIds,
            out List<ObjectId> ids,
            out SvgPlotRect window,
            out string error)
        {
            ids = new List<ObjectId>();
            window = default;
            error = null;

            switch (settings.Area)
            {
                case SvgPlotArea.Selection:
                    if (selectionIds != null)
                        ids.AddRange(selectionIds);
                    if (ids.Count == 0)
                    {
                        error = "选择集为空。请先选择实体，或改用图形范围。";
                        return false;
                    }
                    if (!SvgEntityFlattener.TryUnionExtents(tr, ids, out window))
                        window = SvgPlotRect.FromPoints(0, 0, 100, 100);
                    return true;

                case SvgPlotArea.Window:
                    if (!settings.HasWindow)
                    {
                        error = "尚未指定打印窗口。请先点「窗口 <」。";
                        return false;
                    }
                    window = settings.GetWindowRect();
                    if (!window.IsValid)
                    {
                        error = "打印窗口无效。";
                        return false;
                    }
                    CollectCurrentSpace(tr, db, ids);
                    return true;

                case SvgPlotArea.Display:
                    if (!TryCurrentView(ed, out window))
                    {
                        error = "无法读取当前显示范围。";
                        return false;
                    }
                    CollectCurrentSpace(tr, db, ids);
                    return true;

                default:
                    CollectCurrentSpace(tr, db, ids);
                    if (!SvgEntityFlattener.TryUnionExtents(tr, ids, out window))
                        window = SvgPlotRect.FromPoints(0, 0, 100, 100);
                    return true;
            }
        }

        private static void CollectCurrentSpace(Transaction tr, Database db, List<ObjectId> ids)
        {
            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
            foreach (ObjectId id in btr)
                ids.Add(id);
        }

        private static void CollectSelection(Editor ed, List<ObjectId> ids)
        {
            PromptSelectionResult psr = null;
            try { psr = ed.SelectImplied(); }
            catch { }

            if (psr == null || psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
            {
                try
                {
                    var pso = new PromptSelectionOptions
                    {
                        MessageForAdding = "\n选择要导出的实体"
                    };
                    psr = ed.GetSelection(pso);
                }
                catch
                {
                    return;
                }
            }

            if (psr == null || psr.Status != PromptStatus.OK || psr.Value == null)
                return;
            foreach (SelectedObject so in psr.Value)
            {
                if (so != null && !so.ObjectId.IsNull)
                    ids.Add(so.ObjectId);
            }
        }

        private static bool TryCurrentView(Editor ed, out SvgPlotRect window)
        {
            window = default;
            try
            {
                using (var view = ed.GetCurrentView())
                {
                    if (view == null || view.Width < 1e-9 || view.Height < 1e-9)
                        return false;
                    double cx = view.CenterPoint.X;
                    double cy = view.CenterPoint.Y;
                    double hx = view.Width * 0.5;
                    double hy = view.Height * 0.5;
                    window = new SvgPlotRect
                    {
                        MinX = cx - hx,
                        MaxX = cx + hx,
                        MinY = cy - hy,
                        MaxY = cy + hy
                    };
                    return window.IsValid;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
