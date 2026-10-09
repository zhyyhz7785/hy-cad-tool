using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using FontDescriptor = Autodesk.AutoCAD.GraphicsInterface.FontDescriptor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.DesignSpec.Services
{
    /// <summary>
    /// 真 MText 测字宽 / 折行 N / 行距，并叠 HyMd 块框。不改 InsertFromHyMdDump。
    /// </summary>
    public static class HymdCadCompareService
    {
        public const string DefaultFixtureDir = @"E:\Code\HyMdTauri2\samples\cad-compare";
        private const double ColumnWidthMm = 165.0;
        private const double TextHeightMm = 3.5;
        private const double LineSpacingFactor = 1.2;
        private const double H1Scale = 1.6;

        private static readonly string[] Glyphs = { "A", "i", "W", "0", "中", "一", "、", " " };

        private sealed class FontRecipe
        {
            public string Id;
            public string FontFileName;
            public string BigFontFileName;
            public string StyleName;
            public double XScale;
        }

        private static readonly FontRecipe[] Recipes =
        {
            new FontRecipe { Id = "yahei", FontFileName = "Microsoft YaHei", BigFontFileName = "", StyleName = "0-hy-compare-T-yahei", XScale = 1.0 },
            new FontRecipe { Id = "simsun", FontFileName = "SimSun", BigFontFileName = "", StyleName = "0-hy-compare-T-simsun", XScale = 1.0 },
            new FontRecipe { Id = "tssd", FontFileName = "tssdeng.shx", BigFontFileName = "tssdchn.shx", StyleName = "0-hy-compare-S-tssd", XScale = 0.7 },
        };

        public static int FindWrapN(Func<int, int> measure, int max = 400)
        {
            int lo = 0;
            int hi = Math.Max(1, max);
            int first = measure(1);
            if (first > 1) return 0;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                int lines = measure(mid);
                if (lines <= 1) lo = mid;
                else hi = mid - 1;
            }
            return lo;
        }

        public static string MixedSample(int n)
        {
            if (n <= 0) return string.Empty;
            var sb = new StringBuilder(n);
            for (int i = 0; i < n; i++)
                sb.Append(i % 2 == 0 ? '中' : 'A');
            return sb.ToString();
        }

        public static string JoinMarkdown(CadCompareBundleDto hymd, CadCompareBundleDto cad)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 纸面预览 vs AutoCAD 对照表");
            sb.AppendLine();
            sb.AppendLine("数字来自夹具 JSON。未跑的一侧留空。证据：已写入文件的为已验证，缺文件为待验证。");
            sb.AppendLine();
            sb.AppendLine("| caseId | recipe hymd | recipe cad | asciiN hymd | asciiN cad | cjkN hymd | cjkN cad | oneLine hymd | oneLine cad | twoLine hymd | twoLine cad |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|");

            var hymdReports = hymd?.Reports ?? new List<CadCompareReportDto>();
            var cadReports = cad?.Reports ?? new List<CadCompareReportDto>();

            foreach (var h in hymdReports)
            {
                var pair = cadReports.FirstOrDefault(c => RecipesMatch(h.Recipe, c.Recipe, h.FontResolved));
                sb.Append("| ").Append(Esc(h.CaseId))
                    .Append(" | ").Append(Esc(h.Recipe))
                    .Append(" | ").Append(pair == null ? "" : Esc(pair.Recipe))
                    .Append(" | ").Append(h.Wrap?.AsciiN.ToString(CultureInfo.InvariantCulture) ?? "")
                    .Append(" | ").Append(pair?.Wrap?.AsciiN.ToString(CultureInfo.InvariantCulture) ?? "")
                    .Append(" | ").Append(h.Wrap?.CjkN.ToString(CultureInfo.InvariantCulture) ?? "")
                    .Append(" | ").Append(pair?.Wrap?.CjkN.ToString(CultureInfo.InvariantCulture) ?? "")
                    .Append(" | ").Append(Fmt(h.Vertical?.OneLineMm))
                    .Append(" | ").Append(Fmt(pair?.Vertical?.OneLineMm))
                    .Append(" | ").Append(Fmt(h.Vertical?.TwoLineMm))
                    .Append(" | ").Append(Fmt(pair?.Vertical?.TwoLineMm))
                    .AppendLine(" |");
            }

            foreach (var c in cadReports)
            {
                bool hasHymd = hymdReports.Any(h => RecipesMatch(h.Recipe, c.Recipe, h.FontResolved));
                if (hasHymd) continue;
                sb.Append("| ").Append(Esc(c.CaseId))
                    .Append(" | | ").Append(Esc(c.Recipe))
                    .Append(" | | ").Append(c.Wrap?.AsciiN.ToString(CultureInfo.InvariantCulture) ?? "")
                    .Append(" | | ").Append(c.Wrap?.CjkN.ToString(CultureInfo.InvariantCulture) ?? "")
                    .Append(" | | ").Append(Fmt(c.Vertical?.OneLineMm))
                    .Append(" | | ").Append(Fmt(c.Vertical?.TwoLineMm))
                    .AppendLine(" |");
            }

            return sb.ToString();
        }

        private static bool RecipesMatch(string hymdRecipe, string cadRecipe, string fontResolved)
        {
            if (string.Equals(hymdRecipe, "preview-yahei", StringComparison.OrdinalIgnoreCase)
                && string.Equals(cadRecipe, "yahei", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(hymdRecipe, "preview-simsun", StringComparison.OrdinalIgnoreCase)
                && string.Equals(cadRecipe, "simsun", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(hymdRecipe, "preview-tssd", StringComparison.OrdinalIgnoreCase)
                && string.Equals(cadRecipe, "tssd", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(hymdRecipe, "dumpHost", StringComparison.OrdinalIgnoreCase))
            {
                string f = (fontResolved ?? "").ToLowerInvariant();
                if (f.IndexOf("tssd", StringComparison.Ordinal) >= 0)
                    return string.Equals(cadRecipe, "tssd", StringComparison.OrdinalIgnoreCase);
                if (f.IndexOf("simsun", StringComparison.Ordinal) >= 0 || f.IndexOf("宋体") >= 0)
                    return string.Equals(cadRecipe, "simsun", StringComparison.OrdinalIgnoreCase);
                return string.Equals(cadRecipe, "yahei", StringComparison.OrdinalIgnoreCase);
            }
            return string.Equals(hymdRecipe, cadRecipe, StringComparison.OrdinalIgnoreCase);
        }

        private static string Fmt(double? v) =>
            v == null ? "" : v.Value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string Esc(string s) => (s ?? "").Replace("|", "/");

        public static void Run(string fixtureDir, Point3d origin, CadCompareBundleDto hymdOverlay)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) throw new InvalidOperationException("无活动文档");
            var db = doc.Database;
            var ed = doc.Editor;

            var reports = new List<CadCompareReportDto>();
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                double colX = origin.X;
                foreach (var recipe in Recipes)
                {
                    ObjectId styleId;
                    try
                    {
                        styleId = EnsureStyle(db, tr, recipe);
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n[HYMDCOMPARE] 跳过 {recipe.Id}：{ex.Message}");
                        continue;
                    }

                    var report = MeasureRecipe(tr, btr, styleId, recipe, new Point3d(colX, origin.Y, origin.Z));
                    reports.Add(report);
                    colX += ColumnWidthMm + 40.0;
                }

                if (hymdOverlay?.Reports != null)
                {
                    var overlayOrigin = new Point3d(origin.X, origin.Y - 120.0, origin.Z);
                    DrawOverlay(tr, btr, hymdOverlay, overlayOrigin);
                }

                tr.Commit();
            }

            var bundle = new CadCompareBundleDto
            {
                SchemaVersion = 1,
                GeneratedAt = DateTime.Now.ToString("o"),
                SourcePath = fixtureDir,
                Reports = reports
            };
            Directory.CreateDirectory(fixtureDir);
            string cadJson = Path.Combine(fixtureDir, "cad-mtext.json");
            File.WriteAllText(cadJson, JsonConvert.SerializeObject(bundle, Formatting.Indented), new UTF8Encoding(false));

            var hymd = hymdOverlay ?? TryLoadHymd(fixtureDir);
            string table = JoinMarkdown(hymd, bundle);
            File.WriteAllText(Path.Combine(fixtureDir, "compare-table.md"), table, new UTF8Encoding(false));
            ed.WriteMessage($"\n[HYMDCOMPARE] 已写 {cadJson}");
        }

        public static CadCompareBundleDto TryLoadHymd(string fixtureDir)
        {
            foreach (var name in new[] { "hymd-compare.json", "hymd-dump.json", "hymd-preview.json" })
            {
                string path = Path.Combine(fixtureDir, name);
                if (!File.Exists(path)) continue;
                try
                {
                    return JsonConvert.DeserializeObject<CadCompareBundleDto>(File.ReadAllText(path, Encoding.UTF8));
                }
                catch
                {
                    /* 下一文件 */
                }
            }
            return null;
        }

        private static CadCompareReportDto MeasureRecipe(
            Transaction tr,
            BlockTableRecord btr,
            ObjectId styleId,
            FontRecipe recipe,
            Point3d origin)
        {
            var label = CreateMText(styleId, origin.X, origin.Y, 0, ColumnWidthMm * 2, recipe.Id + "  " + recipe.FontFileName);
            label.TextHeight = TextHeightMm * 1.2;
            btr.AppendEntity(label);
            tr.AddNewlyCreatedDBObject(label, true);

            var probe = CreateMText(styleId, origin.X, origin.Y - 10.0, 0, ColumnWidthMm, "中");
            btr.AppendEntity(probe);
            tr.AddNewlyCreatedDBObject(probe, true);

            var glyphs = new List<CadCompareGlyphDto>();
            double gx = origin.X;
            foreach (var ch in Glyphs)
            {
                probe.Width = 0;
                string sample = ch == " " ? new string(' ', 10) : string.Concat(Enumerable.Repeat(ch, 10));
                probe.Contents = sample;
                Touch(probe);
                double adv = SafeActualWidth(probe) / 10.0;
                glyphs.Add(new CadCompareGlyphDto { Ch = ch, AdvanceMm = Round3(adv) });
                var shown = CreateMText(styleId, gx, origin.Y - 18.0, 0, 20, ch == " " ? "␣" : ch);
                btr.AppendEntity(shown);
                tr.AddNewlyCreatedDBObject(shown, true);
                gx += 18.0;
            }

            probe.Width = 0;
            probe.Contents = new string('0', 20);
            Touch(probe);
            glyphs.Add(new CadCompareGlyphDto { Ch = "0×20", AdvanceMm = Round3(SafeActualWidth(probe) / 20.0) });
            probe.Contents = new string('中', 20);
            Touch(probe);
            glyphs.Add(new CadCompareGlyphDto { Ch = "中×20", AdvanceMm = Round3(SafeActualWidth(probe) / 20.0) });

            probe.Width = ColumnWidthMm;
            probe.Location = new Point3d(origin.X, origin.Y - 30.0, origin.Z);
            Touch(probe);
            probe.Contents = "中";
            Touch(probe);
            double oneH = Math.Max(0.01, SafeActualHeight(probe));

            int LineCount(string s)
            {
                probe.Contents = string.IsNullOrEmpty(s) ? " " : s;
                Touch(probe);
                double h = SafeActualHeight(probe);
                if (h <= oneH * 1.35) return 1;
                if (h <= oneH * 2.4) return 2;
                return 3;
            }

            int asciiN = FindWrapN(n => LineCount(n <= 0 ? "" : new string('A', n)));
            int cjkN = FindWrapN(n => LineCount(n <= 0 ? "" : new string('中', n)));
            int mixedN = FindWrapN(n => LineCount(MixedSample(n)));

            probe.Contents = "中";
            Touch(probe);
            double oneLineMm = Round3(SafeActualHeight(probe));
            probe.Contents = "中\\P中";
            Touch(probe);
            double twoLineMm = Round3(SafeActualHeight(probe));
            probe.Contents = "中\\P\\P中";
            Touch(probe);
            double paraTwo = SafeActualHeight(probe);
            double paraGapMm = Round3(Math.Max(0, paraTwo - twoLineMm));

            probe.Contents = "{\\H" + (TextHeightMm * H1Scale).ToString("0.###", CultureInfo.InvariantCulture) + ";中}";
            Touch(probe);
            double h1HeightMm = Round3(SafeActualHeight(probe));

            probe.Contents = (recipe.Id == "tssd" ? "TSSD 对照（不对齐预览）" : "keep");
            Touch(probe);

            return new CadCompareReportDto
            {
                SchemaVersion = 1,
                Surface = "cad-mtext",
                Recipe = recipe.Id,
                CaseId = "cad-probe",
                FontResolved = string.IsNullOrEmpty(recipe.BigFontFileName)
                    ? recipe.FontFileName
                    : recipe.FontFileName + "+" + recipe.BigFontFileName,
                TextHeightMm = TextHeightMm,
                ColumnWidthMm = ColumnWidthMm,
                LineHeightCss = "Exactly " + LineSpacingFactor.ToString("0.###", CultureInfo.InvariantCulture),
                Glyphs = glyphs,
                Wrap = new CadCompareWrapDto
                {
                    AsciiN = asciiN,
                    CjkN = cjkN,
                    MixedN = mixedN,
                    WidthMm = ColumnWidthMm
                },
                Vertical = new CadCompareVerticalDto
                {
                    OneLineMm = oneLineMm,
                    TwoLineMm = twoLineMm,
                    ParaGapMm = paraGapMm,
                    H1HeightMm = h1HeightMm
                },
                Blocks = new List<CadCompareBlockDto>()
            };
        }

        private static void DrawOverlay(
            Transaction tr,
            BlockTableRecord btr,
            CadCompareBundleDto hymd,
            Point3d origin)
        {
            var dump = hymd.Reports?.FirstOrDefault(r => r.Recipe == "dumpHost")
                ?? hymd.Reports?.FirstOrDefault(r => r.Recipe == "preview")
                ?? hymd.Reports?.FirstOrDefault();
            if (dump?.Blocks == null || dump.Blocks.Count == 0) return;

            ObjectId styleId = ObjectId.Null;
            var db = btr.Database;
            var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (tst.Has("0-hy-compare-T-yahei"))
                styleId = tst["0-hy-compare-T-yahei"];

            foreach (var block in dump.Blocks)
            {
                double x = origin.X + block.XMm;
                double y = origin.Y - block.YMm;
                DrawRect(tr, btr, x, y, block.WidthMm, block.HeightMm, Color.FromRgb(255, 0, 255));
                if (styleId.IsNull || string.IsNullOrWhiteSpace(block.Markdown)) continue;
                var mt = CreateMText(styleId, x, y, 0, Math.Max(TextHeightMm, block.WidthMm), block.Markdown);
                btr.AppendEntity(mt);
                tr.AddNewlyCreatedDBObject(mt, true);
                Touch(mt);
                try
                {
                    var ext = mt.GeometricExtents;
                    double w = ext.MaxPoint.X - ext.MinPoint.X;
                    double h = ext.MaxPoint.Y - ext.MinPoint.Y;
                    DrawRect(tr, btr, ext.MinPoint.X, ext.MaxPoint.Y, w, h, Color.FromRgb(0, 200, 255));
                }
                catch
                {
                    /* 无外接框则只留洋红 HyMd 框 */
                }
            }
        }

        private static void DrawRect(
            Transaction tr,
            BlockTableRecord btr,
            double left,
            double top,
            double width,
            double height,
            Color color)
        {
            var pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(left, top), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(left + width, top), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(left + width, top - height), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(left, top - height), 0, 0, 0);
            pl.Closed = true;
            pl.Color = color;
            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);
        }

        private static MText CreateMText(ObjectId styleId, double x, double y, double z, double width, string contents)
        {
            var mt = new MText();
            mt.SetDatabaseDefaults();
            mt.Location = new Point3d(x, y, z);
            mt.Attachment = AttachmentPoint.TopLeft;
            if (!styleId.IsNull) mt.TextStyleId = styleId;
            mt.TextHeight = TextHeightMm;
            mt.LineSpacingStyle = LineSpacingStyle.Exactly;
            mt.LineSpacingFactor = LineSpacingFactor;
            mt.Width = Math.Max(0, width);
            mt.Contents = contents ?? "";
            return mt;
        }

        private static void Touch(MText mt)
        {
            try { mt.RecordGraphicsModified(true); } catch { }
        }

        private static double SafeActualHeight(MText mt)
        {
            try { return mt.ActualHeight; }
            catch { return 0; }
        }

        private static double SafeActualWidth(MText mt)
        {
            try { return mt.ActualWidth; }
            catch { return 0; }
        }

        private static double Round3(double n) => Math.Round(n, 3);

        private static ObjectId EnsureStyle(Database db, Transaction tr, FontRecipe recipe)
        {
            var tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            TextStyleTableRecord rec;
            if (tst.Has(recipe.StyleName))
            {
                rec = (TextStyleTableRecord)tr.GetObject(tst[recipe.StyleName], OpenMode.ForWrite);
            }
            else
            {
                tst.UpgradeOpen();
                rec = new TextStyleTableRecord { Name = recipe.StyleName, TextSize = 0, XScale = recipe.XScale > 0 ? recipe.XScale : 1.0 };
                tst.Add(rec);
                tr.AddNewlyCreatedDBObject(rec, true);
            }

            string font = recipe.FontFileName ?? "";
            if (font.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
            {
                try { rec.Font = new FontDescriptor(string.Empty, false, false, 0, 0); } catch { }
                rec.FileName = font;
                rec.BigFontFileName = recipe.BigFontFileName ?? "";
            }
            else
            {
                string face = font.IndexOf("YaHei", StringComparison.OrdinalIgnoreCase) >= 0
                    || font.IndexOf("微软雅黑", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "微软雅黑"
                    : font.IndexOf("SimSun", StringComparison.OrdinalIgnoreCase) >= 0
                      || font.IndexOf("宋体", StringComparison.OrdinalIgnoreCase) >= 0
                    ? "宋体"
                    : font;
                rec.Font = new FontDescriptor(face, false, false, 134, 34);
                rec.BigFontFileName = "";
            }
            rec.XScale = recipe.XScale > 0 ? recipe.XScale : 1.0;
            return rec.ObjectId;
        }
    }
}
