using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HyCADTool.Features.DesignSpec.Services
{
    /// <summary>
    /// 对照 InsertFromHyMdDump 公式编出的 plan 与 HyMd TS plan：Contents / 坐标 / 行距 / 列宽。
    /// </summary>
    public static class CadStampPlanGolden
    {
        public const double Tol = 0.001;

        public static List<string> Diff(CadStampPlan baseline, CadStampPlan other)
        {
            var diffs = new List<string>();
            if (baseline == null)
            {
                diffs.Add("基准 plan 为空");
                return diffs;
            }
            if (other == null)
            {
                diffs.Add("对照 plan 为空");
                return diffs;
            }
            Near(diffs, "scale", baseline.Scale, other.Scale);
            Eq(diffs, "styleName", baseline.StyleName, other.StyleName);
            Eq(diffs, "fontFileName", baseline.FontFileName, other.FontFileName);
            Eq(diffs, "bigFontFileName", baseline.BigFontFileName, other.BigFontFileName);
            Near(diffs, "textXScale", baseline.TextXScale, other.TextXScale);
            Near(diffs, "pageWidthMm", baseline.PageWidthMm, other.PageWidthMm);
            Near(diffs, "pageHeightMm", baseline.PageHeightMm, other.PageHeightMm);
            NearArr(diffs, "marginMm", baseline.MarginMm, other.MarginMm);
            if (baseline.Columns != other.Columns)
                diffs.Add($"columns {baseline.Columns} != {other.Columns}");
            Near(diffs, "gutterMm", baseline.GutterMm, other.GutterMm);

            var bp = baseline.Pages ?? new List<CadStampPagePlan>();
            var op = other.Pages ?? new List<CadStampPagePlan>();
            if (bp.Count != op.Count)
                diffs.Add($"pages.Count {bp.Count} != {op.Count}");
            int n = Math.Min(bp.Count, op.Count);
            for (int i = 0; i < n; i++)
                DiffPage(diffs, $"pages[{i}]", bp[i], op[i]);
            return diffs;
        }

        private static void DiffPage(List<string> diffs, string prefix, CadStampPagePlan a, CadStampPagePlan b)
        {
            if (a == null || b == null)
            {
                diffs.Add(prefix + " 缺页");
                return;
            }
            if (a.PageIndex != b.PageIndex)
                diffs.Add($"{prefix}.pageIndex {a.PageIndex} != {b.PageIndex}");
            Near(diffs, prefix + ".pageTopMm", a.PageTopMm, b.PageTopMm);

            var af = a.Frames ?? new List<CadStampFrame>();
            var bf = b.Frames ?? new List<CadStampFrame>();
            if (af.Count != bf.Count)
                diffs.Add($"{prefix}.frames.Count {af.Count} != {bf.Count}");
            int nf = Math.Min(af.Count, bf.Count);
            for (int i = 0; i < nf; i++)
            {
                Eq(diffs, $"{prefix}.frames[{i}].kind", af[i]?.Kind, bf[i]?.Kind);
                NearPts(diffs, $"{prefix}.frames[{i}].pointsMm", af[i]?.PointsMm, bf[i]?.PointsMm);
            }

            var am = a.Mtexts ?? new List<CadStampMText>();
            var bm = b.Mtexts ?? new List<CadStampMText>();
            if (am.Count != bm.Count)
                diffs.Add($"{prefix}.mtexts.Count {am.Count} != {bm.Count}");
            int nm = Math.Min(am.Count, bm.Count);
            for (int i = 0; i < nm; i++)
            {
                var x = am[i];
                var y = bm[i];
                string p = $"{prefix}.mtexts[{i}]";
                if (x == null || y == null)
                {
                    diffs.Add(p + " 空");
                    continue;
                }
                Near(diffs, p + ".xMm", x.XMm, y.XMm);
                Near(diffs, p + ".yMm", x.YMm, y.YMm);
                Near(diffs, p + ".heightMm", x.HeightMm, y.HeightMm);
                Near(diffs, p + ".width", x.Width, y.Width);
                Near(diffs, p + ".lineSpacingExactly", x.LineSpacingExactly, y.LineSpacingExactly);
                Eq(diffs, p + ".attachment", x.Attachment, y.Attachment);
                Eq(diffs, p + ".contents", x.Contents, y.Contents);
            }

            var at = a.Tables ?? new List<CadStampTablePlan>();
            var bt = b.Tables ?? new List<CadStampTablePlan>();
            if (at.Count != bt.Count)
                diffs.Add($"{prefix}.tables.Count {at.Count} != {bt.Count}");
            int nt = Math.Min(at.Count, bt.Count);
            for (int i = 0; i < nt; i++)
            {
                var x = at[i];
                var y = bt[i];
                string p = $"{prefix}.tables[{i}]";
                if (x == null || y == null)
                {
                    diffs.Add(p + " 空");
                    continue;
                }
                Near(diffs, p + ".xMm", x.XMm, y.XMm);
                Near(diffs, p + ".yMm", x.YMm, y.YMm);
                Near(diffs, p + ".fontSizeMm", x.FontSizeMm, y.FontSizeMm);
                Near(diffs, p + ".cellPadXMm", x.CellPadXMm, y.CellPadXMm);
                Near(diffs, p + ".cellPadYMm", x.CellPadYMm, y.CellPadYMm);
                NearArr(diffs, p + ".columnWidthsMm", x.ColumnWidthsMm, y.ColumnWidthsMm);
                NearArr(diffs, p + ".rowHeightsMm", x.RowHeightsMm, y.RowHeightsMm);
                DiffCells(diffs, p + ".cells", x.Cells, y.Cells);
                DiffCells(diffs, p + ".aligns", x.Aligns, y.Aligns);
            }
        }

        private static void DiffCells(List<string> diffs, string prefix, List<List<string>> a, List<List<string>> b)
        {
            int ra = a != null ? a.Count : 0;
            int rb = b != null ? b.Count : 0;
            if (ra != rb)
                diffs.Add($"{prefix}.rows {ra} != {rb}");
            int n = Math.Min(ra, rb);
            for (int r = 0; r < n; r++)
            {
                var ar = a[r] ?? new List<string>();
                var br = b[r] ?? new List<string>();
                if (ar.Count != br.Count)
                    diffs.Add($"{prefix}[{r}].cols {ar.Count} != {br.Count}");
                int cN = Math.Min(ar.Count, br.Count);
                for (int c = 0; c < cN; c++)
                    Eq(diffs, $"{prefix}[{r}][{c}]", ar[c], br[c]);
            }
        }

        private static void Near(List<string> diffs, string name, double a, double b)
        {
            if (Math.Abs(a - b) > Tol)
                diffs.Add($"{name} {F(a)} != {F(b)}");
        }

        private static void NearArr(List<string> diffs, string name, double[] a, double[] b)
        {
            int na = a != null ? a.Length : 0;
            int nb = b != null ? b.Length : 0;
            if (na != nb)
            {
                diffs.Add($"{name}.length {na} != {nb}");
                return;
            }
            for (int i = 0; i < na; i++)
                Near(diffs, $"{name}[{i}]", a[i], b[i]);
        }

        private static void NearPts(List<string> diffs, string name, List<double[]> a, List<double[]> b)
        {
            int na = a != null ? a.Count : 0;
            int nb = b != null ? b.Count : 0;
            if (na != nb)
            {
                diffs.Add($"{name}.length {na} != {nb}");
                return;
            }
            for (int i = 0; i < na; i++)
            {
                var pa = a[i];
                var pb = b[i];
                int la = pa != null ? pa.Length : 0;
                int lb = pb != null ? pb.Length : 0;
                if (la != lb)
                {
                    diffs.Add($"{name}[{i}].length {la} != {lb}");
                    continue;
                }
                for (int k = 0; k < la; k++)
                    Near(diffs, $"{name}[{i}][{k}]", pa[k], pb[k]);
            }
        }

        private static void Eq(List<string> diffs, string name, string a, string b)
        {
            if (!string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal))
                diffs.Add($"{name} «{a}» != «{b}»");
        }

        private static string F(double v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static string FormatDiffs(List<string> diffs)
        {
            if (diffs == null || diffs.Count == 0)
                return "OK";
            var sb = new StringBuilder();
            sb.Append(diffs.Count).Append(" 处不等");
            int n = Math.Min(40, diffs.Count);
            for (int i = 0; i < n; i++)
                sb.Append("\n  ").Append(diffs[i]);
            if (diffs.Count > n)
                sb.Append("\n  …");
            return sb.ToString();
        }
    }
}
