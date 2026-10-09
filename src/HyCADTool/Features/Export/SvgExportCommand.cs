using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using HyCADTool.Features.Export.Services.Svg;
using HyCADTool.Features.Export.Views;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Export
{
    /// <summary>
    /// hysvg / N42：弹出打印式对话框 → 按纸张/窗口写出 SVG。
    /// </summary>
    public class SvgExportCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;
            var ed = doc.Editor;

            var window = new SvgExportWindow();
            bool? ok;
            try { ok = AcApp.ShowModalWindow(window); }
            catch (Exception ex)
            {
                ed.WriteMessage("\nhysvg 打开对话框失败: " + ex.Message);
                return;
            }

            if (ok != true)
            {
                ed.WriteMessage("\nhysvg 已取消。");
                return;
            }

            var settings = window.ResultSettings;
            string path;
            try { path = ResolveOutputPath(doc.Name, doc.Database.Filename); }
            catch { path = Path.Combine(Path.GetTempPath(), "hysvg-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".svg"); }

            var sw = Stopwatch.StartNew();
            if (!SvgExportRunner.TryExport(doc, settings, path, out var stats, out string error))
            {
                ed.WriteMessage("\nhysvg 导出失败: " + (error ?? ""));
                return;
            }
            sw.Stop();
            stats.ElapsedMs = sw.ElapsedMilliseconds;

            ed.WriteMessage($"\nhysvg 完成：源实体 {stats.SourceEntityCount}，图元 {stats.PrimitiveCount}，Explode {stats.ExplodedCount}，{stats.ElapsedMs} ms");
            ed.WriteMessage("\n  文件: " + path);
            if (stats.Skipped.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("\n  跳过:");
                foreach (var kv in stats.Skipped)
                    sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
                ed.WriteMessage(sb.ToString());
            }
        }

        private static string ResolveOutputPath(string docName, string dbFilename)
        {
            string candidate = null;
            if (!string.IsNullOrWhiteSpace(dbFilename) && Path.IsPathRooted(dbFilename)
                && dbFilename.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                candidate = dbFilename;
            else if (!string.IsNullOrWhiteSpace(docName) && Path.IsPathRooted(docName))
                candidate = docName;

            if (candidate != null && Directory.Exists(Path.GetDirectoryName(candidate)))
                return Path.ChangeExtension(candidate, ".svg");

            return Path.Combine(Path.GetTempPath(), "hysvg-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".svg");
        }
    }
}
