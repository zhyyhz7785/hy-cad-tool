using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HyCADTool.TextLayout
{
    /// <summary>
    /// 将 Markdown 文本转换为 AutoCAD MText 格式码字符串
    /// </summary>
    public class MarkdownToMTextRenderer
    {
        private readonly DesignSpecConfig _config;
        private readonly StringBuilder _sb;

        public MarkdownToMTextRenderer(DesignSpecConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _sb = new StringBuilder(4096);
        }

        public string Convert(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            _sb.Clear();
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var document = Markdown.Parse(markdown, pipeline);

            string hStr = F(_config.ActualTextHeight);
            string wStr = F(_config.TextXScale);
            _sb.Append("{\\H").Append(hStr).Append(";\\W").Append(wStr).Append(";");

            foreach (var block in document)
                RenderBlock(block);

            _sb.Append("}");
            string result = _sb.ToString();
            while (result.EndsWith("\\P}"))
                result = result.Substring(0, result.Length - 3) + "}";
            return result;
        }

        private void RenderBlock(Block block)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    RenderHeading(heading);
                    break;
                case ParagraphBlock paragraph:
                    RenderParagraph(paragraph);
                    break;
                case ListBlock list:
                    RenderList(list);
                    break;
                case ThematicBreakBlock _:
                    RenderThematicBreak();
                    break;
                case QuoteBlock quote:
                    RenderQuote(quote);
                    break;
                case Table table:
                    RenderTable(table);
                    break;
                case FencedCodeBlock code:
                    RenderCodeBlock(code);
                    break;
                default:
                    if (block is ContainerBlock container)
                    {
                        foreach (var child in container)
                            RenderBlock(child);
                    }
                    break;
            }
        }

        private void RenderHeading(HeadingBlock heading)
        {
            double height;
            switch (heading.Level)
            {
                case 1: height = _config.H1Height; break;
                case 2: height = _config.H2Height; break;
                case 3: height = _config.H3Height; break;
                default: height = _config.ActualTextHeight; break;
            }

            AppendParaSpacing(_config.GetHeadingSpaceBefore(heading.Level), _config.GetHeadingSpaceAfter(heading.Level));
            _sb.Append("{");
            if (!string.IsNullOrEmpty(_config.BoldFontName))
                _sb.Append("\\f").Append(_config.BoldFontName).Append("|b1|i0;");
            _sb.Append("\\H").Append(F(height)).Append(";\\W").Append(F(_config.TextXScale)).Append(";");
            RenderInlines(heading.Inline);
            _sb.Append("}\\P");
        }

        private void RenderParagraph(ParagraphBlock paragraph)
        {
            AppendParaSpacing(0, _config.ActualPSpaceAfter);
            RenderInlines(paragraph.Inline);
            _sb.Append("\\P");
        }

        private void RenderList(ListBlock list) => RenderList(list, 0);

        private void RenderList(ListBlock list, int depth)
        {
            bool markdownOrdered = list.IsOrdered;
            var mlc = _config.MultilevelList;
            bool ordered = mlc != null
                ? mlc.ResolveIsOrdered(depth, markdownOrdered)
                : markdownOrdered;

            int index = 1;
            foreach (var item in list)
            {
                if (!(item is ListItemBlock listItem)) continue;

                string bullet = FormatBullet(depth, ordered, index);
                string fontOpen = null, fontClose = null;
                if (mlc != null)
                {
                    var levelDef = mlc.GetLevel(depth);
                    if (levelDef.FontSizeOverride.HasValue)
                    {
                        double h = levelDef.FontSizeOverride.Value * _config.Scale;
                        fontOpen = "{\\H" + F(h) + ";";
                        fontClose = "}";
                    }
                }

                bool wrotePrimaryParagraph = false;
                foreach (var child in listItem)
                {
                    if (child is ParagraphBlock p)
                    {
                        if (!wrotePrimaryParagraph)
                        {
                            AppendParaSpacing(0, _config.ActualLiSpaceAfter);
                            AppendListIndent(depth);
                            if (fontOpen != null) _sb.Append(fontOpen);
                            _sb.Append(bullet);
                            RenderInlines(p.Inline);
                            if (fontClose != null) _sb.Append(fontClose);
                            _sb.Append("\\P");
                            wrotePrimaryParagraph = true;
                        }
                        else
                        {
                            AppendListIndent(depth + 1);
                            RenderInlines(p.Inline);
                            _sb.Append("\\P");
                        }
                    }
                    else if (child is ListBlock nested)
                    {
                        if (!wrotePrimaryParagraph)
                        {
                            AppendParaSpacing(0, _config.ActualLiSpaceAfter);
                            AppendListIndent(depth);
                            if (fontOpen != null) _sb.Append(fontOpen);
                            _sb.Append(bullet);
                            if (fontClose != null) _sb.Append(fontClose);
                            _sb.Append("\\P");
                            wrotePrimaryParagraph = true;
                        }
                        RenderList(nested, depth + 1);
                    }
                    else
                    {
                        if (!wrotePrimaryParagraph)
                        {
                            AppendParaSpacing(0, _config.ActualLiSpaceAfter);
                            AppendListIndent(depth);
                            if (fontOpen != null) _sb.Append(fontOpen);
                            _sb.Append(bullet);
                            wrotePrimaryParagraph = true;
                        }
                        RenderBlock(child);
                        if (fontClose != null) _sb.Append(fontClose);
                    }
                }

                if (!wrotePrimaryParagraph)
                {
                    AppendParaSpacing(0, _config.ActualLiSpaceAfter);
                    AppendListIndent(depth);
                    if (fontOpen != null) _sb.Append(fontOpen);
                    _sb.Append(bullet);
                    if (fontClose != null) _sb.Append(fontClose);
                    _sb.Append("\\P");
                }

                if (ordered) index++;
            }
        }

        private string FormatBullet(int depth, bool ordered, int index)
        {
            var mlc = _config.MultilevelList;
            if (mlc == null)
                return ordered ? $"{index}. " : "\\U+2022 ";

            var level = mlc.GetLevel(depth);
            if (!ordered)
            {
                string ch = string.IsNullOrEmpty(level.BulletChar) ? "\u2022" : level.BulletChar;
                return "\\U+" + ((int)ch[0]).ToString("X4") + " ";
            }

            switch (level.NumberStyle)
            {
                case ListNumberStyle.LowerLetter:
                    return (char)('a' + ((index - 1) % 26)) + ") ";
                case ListNumberStyle.UpperLetter:
                    return (char)('A' + ((index - 1) % 26)) + ") ";
                case ListNumberStyle.LowerRoman:
                    return ToRoman(index).ToLowerInvariant() + ". ";
                case ListNumberStyle.UpperRoman:
                    return ToRoman(index) + ". ";
                case ListNumberStyle.Bullet:
                {
                    string ch = string.IsNullOrEmpty(level.BulletChar) ? "\u2022" : level.BulletChar;
                    return "\\U+" + ((int)ch[0]).ToString("X4") + " ";
                }
                default:
                    return $"{index}. ";
            }
        }

        private static string ToRoman(int number)
        {
            if (number <= 0 || number > 3999) return number.ToString();
            var values = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
            var symbols = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };
            var sb = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                while (number >= values[i])
                {
                    sb.Append(symbols[i]);
                    number -= values[i];
                }
            }
            return sb.ToString();
        }

        private void RenderThematicBreak()
        {
            _sb.Append("\\P{\\H").Append(F(_config.ActualTextHeight * 0.5)).Append(";")
               .Append("────────────────────────────────").Append("}\\P");
        }

        private void RenderQuote(QuoteBlock quote)
        {
            string indentInch = F(_config.ActualQuoteIndent / 25.4);
            _sb.Append("\\pxib").Append(F(_config.ActualQuoteSpaceBefore / 25.4))
               .Append(",a").Append(F(_config.ActualQuoteSpaceAfter / 25.4))
               .Append(",l").Append(indentInch).Append(";");

            foreach (var child in quote)
            {
                if (child is ParagraphBlock p) RenderInlines(p.Inline);
                else RenderBlock(child);
            }
            _sb.Append("\\P");
        }

        private void RenderTable(Table table)
        {
            var rows = new List<List<string>>();
            foreach (var rowObj in table)
            {
                if (!(rowObj is TableRow row)) continue;
                var cells = new List<string>();
                foreach (var cellObj in row)
                {
                    if (!(cellObj is TableCell cell)) continue;
                    var cellSb = new StringBuilder();
                    foreach (var child in cell)
                    {
                        if (!(child is ParagraphBlock p) || p.Inline == null) continue;
                        foreach (var inline in p.Inline)
                        {
                            if (inline is LiteralInline lit) cellSb.Append(lit.Content.ToString());
                            else if (inline is EmphasisInline em)
                            {
                                foreach (var c in em)
                                    if (c is LiteralInline cl) cellSb.Append(cl.Content.ToString());
                            }
                        }
                    }
                    cells.Add(cellSb.ToString().Trim());
                }
                rows.Add(cells);
            }

            if (rows.Count == 0) return;

            int maxCols = rows.Max(r => r.Count);
            var colWidths = new int[maxCols];
            foreach (var row in rows)
            {
                for (int i = 0; i < row.Count; i++)
                {
                    int w = DisplayWidthCalculator.GetDisplayUnits(row[i]);
                    if (w > colWidths[i]) colWidths[i] = w;
                }
            }

            _sb.Append("\\P");
            foreach (var row in rows)
            {
                for (int i = 0; i < maxCols; i++)
                {
                    string cell = i < row.Count ? row[i] : "";
                    int pad = colWidths[i] - DisplayWidthCalculator.GetDisplayUnits(cell);
                    _sb.Append(EscapeMText(cell));
                    if (i < maxCols - 1)
                        _sb.Append(new string(' ', Math.Max(pad + 2, 2)));
                }
                _sb.Append("\\P");
            }
        }

        private void RenderCodeBlock(FencedCodeBlock code)
        {
            string indentInch = F(_config.ActualListIndent / 25.4);
            var lines = code.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines.Lines[i];
                string text = line.Slice.ToString();
                if (string.IsNullOrEmpty(text)) continue;

                _sb.Append("\\pxi0,l").Append(indentInch).Append(";")
                   .Append("{\\fConsolas|b0|i0;").Append(EscapeMText(text)).Append("}\\P");
            }
        }

        private void RenderInlines(ContainerInline inlines)
        {
            if (inlines == null) return;
            foreach (var inline in inlines)
                RenderInline(inline);
        }

        private void RenderInline(Inline inline)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    _sb.Append(EscapeMText(literal.Content.ToString()));
                    break;
                case EmphasisInline emphasis:
                    RenderEmphasis(emphasis);
                    break;
                case LineBreakInline _:
                    _sb.Append("\\P");
                    break;
                case CodeInline code:
                    _sb.Append(EscapeMText(code.Content));
                    break;
                case ContainerInline container:
                    RenderInlines(container);
                    break;
            }
        }

        private void RenderEmphasis(EmphasisInline emphasis)
        {
            bool isBold = emphasis.DelimiterCount >= 2;
            if (isBold)
            {
                _sb.Append("{\\f").Append(_config.BoldFontName).Append("|b1|i0;");
                foreach (var child in emphasis) RenderInline(child);
                _sb.Append("}");
            }
            else
            {
                _sb.Append("{\\Q15;");
                foreach (var child in emphasis) RenderInline(child);
                _sb.Append("\\Q0;}");
            }
        }

        private void AppendParaSpacing(double beforeMm, double afterMm)
        {
            if (beforeMm <= 0 && afterMm <= 0) return;
            _sb.Append("\\pxi0");
            if (beforeMm > 0) _sb.Append(",b").Append(F(beforeMm / 25.4));
            if (afterMm > 0) _sb.Append(",a").Append(F(afterMm / 25.4));
            _sb.Append(";");
        }

        private void AppendListIndent(int depth)
        {
            double indentMm = _config.GetListIndentMm(depth);
            _sb.Append("\\pxi0");
            if (indentMm > 0)
                _sb.Append(",l").Append(F(indentMm / 25.4));
            _sb.Append(";");
        }

        private static string EscapeMText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\\", "\\\\").Replace("{", "\\{").Replace("}", "\\}");
        }

        private static string F(double value) => value.ToString("F4", CultureInfo.InvariantCulture);
    }
}
