using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HyCADTool.Refactored.Domain.Models.Text
{
    /// <summary>
    /// 将 Markdown 文本转换为 AutoCAD MText 格式码字符串
    /// 纯算法，平台无关（不引用 AutoCAD API）
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

        /// <summary>
        /// 将 Markdown 源文本转换为 MText 格式码字符串
        /// </summary>
        public string Convert(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            _sb.Clear();

            var pipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            var document = Markdown.Parse(markdown, pipeline);

            // 全局默认：设置基础字高和宽度系数，包裹整个内容
            string hStr = F(_config.ActualTextHeight);
            string wStr = F(_config.TextXScale);
            _sb.Append("{\\H").Append(hStr).Append(";\\W").Append(wStr).Append(";");

            foreach (var block in document)
            {
                RenderBlock(block);
            }

            _sb.Append("}");

            // 移除末尾多余的 \P（不能用 TrimEnd 逐字符，会误删内容）
            string result = _sb.ToString();
            while (result.EndsWith("\\P}"))
                result = result.Substring(0, result.Length - 3) + "}";
            return result;
        }

        #region Block 级渲染

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
                    // 未知 block → 尝试渲染子 block
                    if (block is ContainerBlock container)
                    {
                        foreach (var child in container)
                            RenderBlock(child);
                    }
                    break;
            }
        }

        /// <summary>标题 H1-H3 → 放大字号，标题后只留一个换行</summary>
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

            string hStr = F(height);
            string wStr = F(_config.TextXScale);

            _sb.Append("{\\H").Append(hStr).Append(";\\W").Append(wStr).Append(";");
            RenderInlines(heading.Inline);
            _sb.Append("}\\P");
        }

        /// <summary>普通段落，段后换行</summary>
        private void RenderParagraph(ParagraphBlock paragraph)
        {
            RenderInlines(paragraph.Inline);
            _sb.Append("\\P");
        }

        /// <summary>列表（有序/无序），使用简单前缀缩进</summary>
        private void RenderList(ListBlock list)
        {
            bool ordered = list.IsOrdered;
            int index = 1;

            foreach (var item in list)
            {
                if (item is ListItemBlock listItem)
                {
                    string bullet = ordered ? $"{index}. " : "  \\U+2022 ";

                    _sb.Append(bullet);

                    foreach (var child in listItem)
                    {
                        if (child is ParagraphBlock p)
                            RenderInlines(p.Inline);
                        else
                            RenderBlock(child);
                    }

                    _sb.Append("\\P");

                    if (ordered) index++;
                }
            }
        }

        /// <summary>分隔线 --- → 虚线</summary>
        private void RenderThematicBreak()
        {
            string halfH = F(_config.ActualTextHeight * 0.5);
            _sb.Append("\\P{\\H").Append(halfH).Append(";");
            _sb.Append("────────────────────────────────");
            _sb.Append("}\\P");
        }

        /// <summary>引用块 > → 缩进</summary>
        private void RenderQuote(QuoteBlock quote)
        {
            string indent = F(_config.ActualQuoteIndent);
            _sb.Append("\\pxi0,l").Append(indent).Append(";");

            foreach (var child in quote)
            {
                if (child is ParagraphBlock p)
                {
                    RenderInlines(p.Inline);
                }
                else
                {
                    RenderBlock(child);
                }
            }

            _sb.Append("\\P");
        }

        /// <summary>表格 → 等宽文本模拟对齐</summary>
        private void RenderTable(Table table)
        {
            // 收集所有行/列的文本
            var rows = new List<List<string>>();
            foreach (var rowObj in table)
            {
                if (rowObj is TableRow row)
                {
                    var cells = new List<string>();
                    foreach (var cellObj in row)
                    {
                        if (cellObj is TableCell cell)
                        {
                            var cellSb = new StringBuilder();
                            foreach (var child in cell)
                            {
                                if (child is ParagraphBlock p && p.Inline != null)
                                {
                                    foreach (var inline in p.Inline)
                                    {
                                        if (inline is LiteralInline lit)
                                            cellSb.Append(lit.Content.ToString());
                                        else if (inline is EmphasisInline em)
                                        {
                                            foreach (var c in em)
                                                if (c is LiteralInline cl)
                                                    cellSb.Append(cl.Content.ToString());
                                        }
                                    }
                                }
                            }
                            cells.Add(cellSb.ToString().Trim());
                        }
                    }
                    rows.Add(cells);
                }
            }

            if (rows.Count == 0) return;

            // 计算最大列数和列宽（按字符数，中文算2）
            int maxCols = rows.Max(r => r.Count);
            var colWidths = new int[maxCols];
            foreach (var row in rows)
            {
                for (int i = 0; i < row.Count; i++)
                {
                    int w = GetDisplayWidth(row[i]);
                    if (w > colWidths[i]) colWidths[i] = w;
                }
            }

            // 渲染每行
            _sb.Append("\\P");
            foreach (var row in rows)
            {
                for (int i = 0; i < maxCols; i++)
                {
                    string cell = i < row.Count ? row[i] : "";
                    int pad = colWidths[i] - GetDisplayWidth(cell);
                    _sb.Append(EscapeMText(cell));
                    if (i < maxCols - 1)
                    {
                        _sb.Append(new string(' ', Math.Max(pad + 2, 2)));
                    }
                }
                _sb.Append("\\P");
            }
        }

        /// <summary>计算显示宽度（中文字符算2，ASCII算1）</summary>
        private static int GetDisplayWidth(string text)
        {
            int width = 0;
            foreach (char c in text)
                width += c > 127 ? 2 : 1;
            return width;
        }

        /// <summary>代码块 → 原样输出（等宽缩进）</summary>
        private void RenderCodeBlock(FencedCodeBlock code)
        {
            string indent = F(_config.ActualListIndent);
            var lines = code.Lines;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines.Lines[i];
                string text = line.Slice.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    _sb.Append("\\pxi0,l").Append(indent).Append(";");
                    _sb.Append(EscapeMText(text));
                    _sb.Append("\\P");
                }
            }
        }

        #endregion

        #region Inline 级渲染

        private void RenderInlines(ContainerInline inlines)
        {
            if (inlines == null) return;

            foreach (var inline in inlines)
            {
                RenderInline(inline);
            }
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
                    // 行内代码 → 原样输出
                    _sb.Append(EscapeMText(code.Content));
                    break;

                case ContainerInline container:
                    RenderInlines(container);
                    break;

                default:
                    // 未识别 inline → 尝试获取文本
                    break;
            }
        }

        /// <summary>
        /// 粗体/斜体
        /// - **粗体** → {\fSimHei;文字}（切 TTF 黑体模拟粗体）
        /// - *斜体*  → {\Q15;文字\Q0;}（倾斜角 15°）
        /// </summary>
        private void RenderEmphasis(EmphasisInline emphasis)
        {
            bool isBold = emphasis.DelimiterCount >= 2;

            if (isBold)
            {
                // 粗体：切换到 TTF 黑体
                _sb.Append("{\\f").Append(_config.BoldFontName).Append(";");
                foreach (var child in emphasis)
                    RenderInline(child);
                _sb.Append("}");
            }
            else
            {
                // 斜体：倾斜角
                _sb.Append("{\\Q15;");
                foreach (var child in emphasis)
                    RenderInline(child);
                _sb.Append("\\Q0;}");
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 转义 MText 特殊字符
        /// MText 中 { } \ 需要转义
        /// </summary>
        private static string EscapeMText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            return text
                .Replace("\\", "\\\\")
                .Replace("{", "\\{")
                .Replace("}", "\\}");
        }

        /// <summary>格式化浮点数（小数点后4位，不含本地化逗号）</summary>
        private static string F(double value)
        {
            return value.ToString("F4", CultureInfo.InvariantCulture);
        }

        #endregion
    }
}
