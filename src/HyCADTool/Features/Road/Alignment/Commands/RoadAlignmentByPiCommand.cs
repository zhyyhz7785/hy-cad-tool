using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Interactive;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using HyCADTool.Shell;
using HyCADTool.Shell.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Features.Road.PlanAlignment.Domain;

using HyCADTool.Features.Road.PlanAlignment.Services;
namespace HyCADTool.Features.Road.PlanAlignment.Commands
{
    /// <summary>
    /// P1：导线法（PI 点法）创建平面线位。对应设计院最常用工作流：
    /// 坐标表 → 逐点输入 PI → 指定每 PI 半径 / 缓和曲线 → 预览 → 一键出 Alignment。
    ///
    /// 三通道输入（数据驱动优先）：
    /// 1. 点取（P）：起点用普通点取，后续 PI 用 <see cref="HyCADTool.Shared.AutoCAD.Interactive.AlignmentPiPickJig"/> 连续点取（实时折线预览，支持撤销 Z）；每个内部 PI 再追问 R / Ls_in / Ls_out；
    /// 2. 导入 CSV（F）：读取 <c>.csv</c>；列顺序 <c>x,y[,R,Ls_in,Ls_out,tag]</c>；
    /// 3. 剪贴板（C）：Excel 复制 → <c>System.Windows.Clipboard</c>；解析同 CSV。
    ///
    /// 干跑预览（Step D）：
    /// - 不写 DWG 之前先在命令行打印总览 + 每 PI 诊断；
    /// - 再追问 <c>[接受(A) / 重选半径(R) / 取消(X)]</c>。<c>R</c> 可一次性覆盖所有 R=0 的 PI，
    ///   解决"CSV 里半径列留空"的常见场景。
    ///
    /// 交付链路（与 hyRoadA 一致）：
    /// AlignmentPiDesigner → Polyline3D → RoadGeometryBridge.ToAutoCadPolyline → ModelSpace →
    /// <see cref="RoadAlignmentService.ImportFromPolyline"/> 写 Xdata + 发事件 + 登记 Domain；
    /// 最后把 PI 表快照（<see cref="AlignmentSource"/>）写入 <see cref="Alignment.Source"/>，供后续
    /// <c>hyRoadAlnEditPi</c> 反推参数使用；命令尾 <see cref="Alignment.Validate"/> 自检。
    /// </summary>
    public sealed class RoadAlignmentByPiCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            // ── Step 1. 选择输入通道
            var channel = PromptInputChannel(ed);
            if (channel == InputChannel.Cancel) return;

            // ── Step 2. 收集 PI 表
            List<PiElement> elements;
            string sourceTag;
            switch (channel)
            {
                case InputChannel.Pick:
                    elements = CollectByPicking(ed, out sourceTag);
                    break;
                case InputChannel.Csv:
                    elements = CollectFromCsv(ed, out sourceTag);
                    break;
                case InputChannel.Clipboard:
                    elements = CollectFromClipboard(ed, out sourceTag);
                    break;
                default:
                    return;
            }
            if (elements == null || elements.Count < 2)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return;
            }

            // ── Step 3. 干跑预览 + 确认（允许一次性批量改 R 再预览）
            if (!RunPreviewLoop(ed, ref elements, out var finalResult)) return;

            // ── Step 4. 落图 + 写 Xdata + 写 JSON + 自检
            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            Alignment alignment;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                svc.RebindForDocument(doc.Name, tr, db);

                var acadPoly = RoadGeometryBridge.ToAutoCadPolyline(finalResult.Polyline);
                AssignLayerIfExists(tr, db, acadPoly, HyRoadLayers.AlignmentLayer);

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(
                    bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                ms.AppendEntity(acadPoly);
                tr.AddNewlyCreatedDBObject(acadPoly, true);

                alignment = svc.ImportFromPolyline(doc.Name, tr, db, acadPoly, out _);

                // 持久化 PI 表到 Domain，供 hyRoadAlnEditPi 反推参数
                alignment.Source = BuildAlignmentSource(elements);

                // 起桩号取 hy-settings.json 默认（hyRoadAlnDefaults 可调）
                var defaults = SettingsPanelViewModel.Current?.CreateAlignmentDefaults();
                if (defaults != null) alignment.StartStation = defaults.DefaultStartStation;

                tr.Commit();
            }

            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design))
            {
                savedTo = exporter.SaveForDocument(design, doc.Name);
            }

            ed.WriteMessage(
                $"\n[道路] 导线法生成 {alignment.Name}：PI {elements.Count} 个，"
                + $"圆角 {finalResult.CurvedPiCount} 段，缓和 {finalResult.SpiraledPiCount} 段，"
                + $"折线 {finalResult.StraightPiCount} 段，跳过 {finalResult.SkippedCount} 段，"
                + $"顶点 {alignment.Centerline.VertexCount} 个，"
                + $"平面长度 {alignment.Centerline.GetPlanarLength():F3} m。"
                + $"（输入通道：{sourceTag}）");

            foreach (var w in finalResult.Warnings)
            {
                ed.WriteMessage("\n[道路] ⚠ " + w);
            }

            var validation = alignment.Validate();
            if (!validation.Ok)
            {
                ed.WriteMessage("\n[道路] ⚠ 自检未通过：");
                foreach (var e in validation.Errors) ed.WriteMessage("\n  · " + e);
            }

            if (savedTo != null)
                ed.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
            else
                ed.WriteMessage(
                    "\n[道路] 未落盘（DWG 尚未保存）。先 QSAVE / SAVEAS，再跑 hyRoadSave 即可。");

            // ── Step 5. 自动打开路线工作台并预选新线位（若任何异常不可向外抛）
            TryOpenWorkbench(alignment.Id);
        }

        /// <summary>尽力而为地打开"路线工作台"并预选新生成的 Alignment。任何异常都吞掉，不影响命令主流程。</summary>
        private static void TryOpenWorkbench(Guid alignmentId)
        {
            try
            {
                var panels = ServiceLocator.Resolve<PanelManager>();
                panels?.ShowAlignmentWorkbench(alignmentId, piIndex: null);
            }
            catch
            {
                // 静默：面板未注册 / WPF 未初始化等场景都不该让命令失败
            }
        }

        // =============================================================================
        // Step 1：输入通道选择
        // =============================================================================

        private enum InputChannel { Pick, Csv, Clipboard, Cancel }

        private static InputChannel PromptInputChannel(Editor ed)
        {
            var opts = new PromptKeywordOptions(
                "\n[道路] 选择 PI 输入方式 [点取(P)/导入CSV(F)/剪贴板(C)] <P>：")
            {
                AllowNone = true,
            };
            opts.Keywords.Add("P");
            opts.Keywords.Add("F");
            opts.Keywords.Add("C");
            opts.Keywords.Default = "P";

            var res = ed.GetKeywords(opts);
            if (res.Status == PromptStatus.None) return InputChannel.Pick;
            if (res.Status != PromptStatus.OK) return InputChannel.Cancel;
            switch (res.StringResult)
            {
                case "P": return InputChannel.Pick;
                case "F": return InputChannel.Csv;
                case "C": return InputChannel.Clipboard;
                default: return InputChannel.Cancel;
            }
        }

        // =============================================================================
        // Step 2a：图上点取
        // =============================================================================

        /// <summary>
        /// 交互点取：首尾 PI 仅需坐标；第二点至以后用 Jig 预览折线；中间 PI 在坐标确定后于命令行追问 R / Ls_in / Ls_out。
        /// 设计意图：把"一边画一边想参数"固化到每个点的节奏内，省去事后补表。
        /// </summary>
        private static List<PiElement> CollectByPicking(Editor ed, out string sourceTag)
        {
            sourceTag = "点取";
            var pts = new List<Point2D>();

            var firstOpts = new PromptPointOptions("\n[道路] 指定 PI 起点：") { AllowNone = false };
            var firstRes = ed.GetPoint(firstOpts);
            if (firstRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return null;
            }
            pts.Add(new Point2D(firstRes.Value.X, firstRes.Value.Y));

            var jig = new AlignmentPiPickJig(firstRes.Value);
            var jigRes = jig.Run(ed);
            if (jigRes == PromptStatus.Cancel)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return null;
            }

            for (int i = 1; i < jig.Points.Count; i++)
            {
                var p = jig.Points[i];
                pts.Add(new Point2D(p.X, p.Y));
            }

            if (pts.Count < 2)
            {
                ed.WriteMessage($"\n[道路] 需要至少 2 个 PI 点（当前 {pts.Count}），已取消。");
                return null;
            }

            // 中间 PI 追问参数；首尾默认 0。
            // 默认值复用上一次输入（radius/lsIn/lsOut），减少重复敲键。
            // 首次默认值从 hy-settings.json 的 AlignmentDefaults 读取（hyRoadAlnDefaults 可调）。
            var elements = new List<PiElement>(pts.Count);
            var defaults = SettingsPanelViewModel.Current?.CreateAlignmentDefaults()
                          ?? new global::HyCADTool.Domain.ValueObjects.Road.AlignmentDefaults();
            double lastR = defaults.DefaultRadius > 0 ? defaults.DefaultRadius : 30.0;
            double lastLsIn = defaults.DefaultSpiralIn;
            double lastLsOut = defaults.DefaultSpiralOut;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i == 0 || i == pts.Count - 1)
                {
                    elements.Add(new PiElement(pts[i]));
                    continue;
                }
                double r = PromptPositiveOrZeroDistance(ed, $"\n[道路] PI[{i}] 圆曲线 R（0=折线）", lastR, out bool cancelR);
                if (cancelR) return null;
                double lsIn = PromptPositiveOrZeroDistance(ed, $"\n[道路] PI[{i}] 入侧缓和曲线 Ls_in（0=无）", lastLsIn, out bool cancelLi);
                if (cancelLi) return null;
                double lsOut = PromptPositiveOrZeroDistance(ed, $"\n[道路] PI[{i}] 出侧缓和曲线 Ls_out（0=无）", lastLsOut, out bool cancelLo);
                if (cancelLo) return null;

                elements.Add(new PiElement(pts[i], r, lsIn, lsOut));
                lastR = r;
                lastLsIn = lsIn;
                lastLsOut = lsOut;
            }

            return elements;
        }

        private static double PromptPositiveOrZeroDistance(Editor ed, string prompt, double defaultVal, out bool cancelled)
        {
            cancelled = false;
            var opt = new PromptDistanceOptions(prompt + $"（默认 {defaultVal:F2}）：")
            {
                DefaultValue = defaultVal,
                UseDefaultValue = true,
                AllowNegative = false,
                AllowZero = true,
                AllowNone = true,
            };
            var res = ed.GetDistance(opt);
            if (res.Status == PromptStatus.None) return defaultVal;
            if (res.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                cancelled = true;
                return 0;
            }
            return res.Value;
        }

        // =============================================================================
        // Step 2b：CSV
        // =============================================================================

        private static List<PiElement> CollectFromCsv(Editor ed, out string sourceTag)
        {
            sourceTag = "CSV";
            var fileOpt = new PromptOpenFileOptions("\n[道路] 选择 PI 坐标表（CSV）")
            {
                Filter = "CSV files (*.csv)|*.csv|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            };
            var res = ed.GetFileNameForOpen(fileOpt);
            if (res.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return null;
            }

            string text;
            try
            {
                text = File.ReadAllText(res.StringResult, DetectEncoding(res.StringResult));
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 读取 CSV 失败：{ex.Message}");
                return null;
            }

            sourceTag = "CSV(" + Path.GetFileName(res.StringResult) + ")";
            return ParseAndReport(ed, text);
        }

        private static Encoding DetectEncoding(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                if (fs.Length >= 3)
                {
                    var bom = new byte[3];
                    fs.Read(bom, 0, 3);
                    if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF) return new UTF8Encoding(true);
                }
            }
            return Encoding.UTF8;
        }

        // =============================================================================
        // Step 2c：剪贴板
        // =============================================================================

        private static List<PiElement> CollectFromClipboard(Editor ed, out string sourceTag)
        {
            sourceTag = "剪贴板";
            string text = ReadClipboardText(ed);
            if (string.IsNullOrWhiteSpace(text))
            {
                ed.WriteMessage("\n[道路] 剪贴板为空或不含文本。");
                return null;
            }
            return ParseAndReport(ed, text);
        }

        /// <summary>
        /// 在 STA 线程读剪贴板（AutoCAD 当前线程不一定是 STA，WPF Clipboard 必须 STA）。
        /// </summary>
        private static string ReadClipboardText(Editor ed)
        {
            string result = null;
            Exception err = null;
            var t = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                        result = System.Windows.Clipboard.GetText();
                }
                catch (Exception ex) { err = ex; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(TimeSpan.FromSeconds(3));
            if (err != null)
            {
                ed.WriteMessage($"\n[道路] 读取剪贴板失败：{err.Message}");
                return null;
            }
            return result;
        }

        // =============================================================================
        // 文本解析 + 错误回显（CSV / 剪贴板共用）
        // =============================================================================

        private static List<PiElement> ParseAndReport(Editor ed, string text)
        {
            if (!PiElementTextParser.TryParse(text, out var elements, out var errors))
            {
                ed.WriteMessage("\n[道路] 文本解析失败：");
                foreach (var e in errors) ed.WriteMessage("\n  · " + e);
                return null;
            }
            if (errors.Count > 0)
            {
                ed.WriteMessage($"\n[道路] 解析警告（仍继续）：");
                foreach (var e in errors) ed.WriteMessage("\n  · " + e);
            }
            ed.WriteMessage($"\n[道路] 解析成功：共 {elements.Count} 个 PI。");
            return elements;
        }

        // =============================================================================
        // Step 3：干跑预览 + 批量改 R + 接受 / 取消
        // =============================================================================

        /// <summary>
        /// 循环：跑 Designer → 命令行打印总览 + 诊断 → 追问 A/R/X。
        /// 返回 false 表示用户取消或预览失败。
        /// </summary>
        private static bool RunPreviewLoop(Editor ed, ref List<PiElement> elements, out PiDesignResult result)
        {
            result = default;
            while (true)
            {
                try
                {
                    result = AlignmentPiDesigner.Build(elements, new PiDesignOptions());
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n[道路] 几何生成失败：{ex.Message}");
                    return false;
                }

                PrintPreview(ed, elements, result);

                var kw = new PromptKeywordOptions(
                    "\n[道路] 操作 [接受(A)/重选半径(R)/取消(X)] <A>：")
                {
                    AllowNone = true,
                };
                kw.Keywords.Add("A");
                kw.Keywords.Add("R");
                kw.Keywords.Add("X");
                kw.Keywords.Default = "A";

                var res = ed.GetKeywords(kw);
                if (res.Status == PromptStatus.None || (res.Status == PromptStatus.OK && res.StringResult == "A"))
                    return true;
                if (res.Status != PromptStatus.OK || res.StringResult == "X")
                {
                    ed.WriteMessage("\n[道路] 已取消。");
                    return false;
                }

                // "R"：批量改 R
                var opt = new PromptDistanceOptions("\n[道路] 统一设置所有 R=0 的 PI 半径为：")
                {
                    AllowNegative = false,
                    AllowZero = true,
                    UseDefaultValue = true,
                    DefaultValue = 30.0,
                    AllowNone = true,
                };
                var dres = ed.GetDistance(opt);
                if (dres.Status == PromptStatus.None)
                {
                    elements = ApplyFallbackRadius(elements, opt.DefaultValue);
                    continue;
                }
                if (dres.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n[道路] 已取消。");
                    return false;
                }
                elements = ApplyFallbackRadius(elements, dres.Value);
            }
        }

        private static List<PiElement> ApplyFallbackRadius(List<PiElement> elements, double r)
        {
            var next = new List<PiElement>(elements.Count);
            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                bool isInternal = i > 0 && i < elements.Count - 1;
                if (isInternal && e.Radius <= 0)
                    next.Add(new PiElement(e.P, r, e.SpiralIn, e.SpiralOut, e.Tag));
                else
                    next.Add(e);
            }
            return next;
        }

        private static void PrintPreview(Editor ed, IReadOnlyList<PiElement> elements, PiDesignResult result)
        {
            ed.WriteMessage(
                $"\n[道路/预览] PI={elements.Count}, 圆角={result.CurvedPiCount}, 缓和={result.SpiraledPiCount}, "
                + $"折线={result.StraightPiCount}, 跳过={result.SkippedCount}, "
                + $"平面长度={result.Polyline.GetPlanarLength():F3} m。");

            int rows = Math.Min(result.PerPi.Count, 20);
            if (rows > 0)
            {
                ed.WriteMessage("\n[道路/预览] 诊断（最多 20 条）：");
                ed.WriteMessage("\n  i   X          Y          turn°    R       Ls     T       状态");
                for (int k = 0; k < rows; k++)
                {
                    var d = result.PerPi[k];
                    double turnDeg = d.TurnRad * 180.0 / Math.PI;
                    double lsSum = d.SpiralIn + d.SpiralOut;
                    string status = StatusText(d.Status, d.Note);
                    ed.WriteMessage(
                        $"\n  {d.Index,-3} {d.P.X,-10:F3} {d.P.Y,-10:F3} {turnDeg,-8:F2} {d.Radius,-7:F2} {lsSum,-6:F2} {d.TangentLen,-7:F2} {status}");
                }
                if (result.PerPi.Count > rows)
                    ed.WriteMessage($"\n  …（省略 {result.PerPi.Count - rows} 条）");
            }

            foreach (var w in result.Warnings) ed.WriteMessage("\n  ⚠ " + w);
        }

        private static string StatusText(PiDiagnosticStatus status, string note)
        {
            switch (status)
            {
                case PiDiagnosticStatus.Curved: return "圆曲线";
                case PiDiagnosticStatus.Spiraled: return "直-缓-圆-缓-直";
                case PiDiagnosticStatus.Skipped: return "跳过：" + note;
                case PiDiagnosticStatus.Straight: return "折线" + (string.IsNullOrEmpty(note) ? "" : "(" + note + ")");
                default: return "?";
            }
        }

        // =============================================================================
        // Step 4：落图辅助
        // =============================================================================

        private static AlignmentSource BuildAlignmentSource(IReadOnlyList<PiElement> elements)
        {
            var s = new AlignmentSource
            {
                Kind = AlignmentSourceKind.PiTable,
            };
            foreach (var e in elements)
            {
                s.PiElements.Add(new AlignmentPiInput
                {
                    P = e.P,
                    Radius = e.Radius,
                    SpiralIn = e.SpiralIn,
                    SpiralOut = e.SpiralOut,
                    Tag = e.Tag,
                });
            }
            return s;
        }

        private static void AssignLayerIfExists(Transaction tr, Database db, Polyline poly, string layerName)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName))
            {
                poly.Layer = layerName;
            }
        }
    }
}
