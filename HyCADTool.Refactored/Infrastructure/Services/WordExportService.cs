using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HyCADTool.Refactored.Infrastructure.Services
{
    /// <summary>
    /// 将 Markdown 计算书导出为 Word (.docx)
    /// 支持标题、段落、表格和等宽字体公式
    /// </summary>
    public class WordExportService
    {
        /// <summary>
        /// 将 Markdown 文本导出为 .docx 文件
        /// </summary>
        /// <param name="markdown">Markdown 格式的计算书文本</param>
        /// <param name="outputPath">输出 .docx 文件路径</param>
        public void Export(string markdown, string outputPath)
        {
            using (var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
            {
                var mainPart = doc.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = new Body();

                AddStyles(mainPart);

                var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                int i = 0;

                while (i < lines.Length)
                {
                    string line = lines[i];

                    // 标题
                    if (line.StartsWith("#"))
                    {
                        int level = 0;
                        while (level < line.Length && line[level] == '#') level++;
                        string text = line.Substring(level).Trim();
                        body.Append(CreateHeading(text, level));
                        i++;
                        continue;
                    }

                    // 表格（| 开头）
                    if (line.TrimStart().StartsWith("|"))
                    {
                        int tableStart = i;
                        while (i < lines.Length && lines[i].TrimStart().StartsWith("|"))
                            i++;
                        body.Append(CreateTable(lines, tableStart, i));
                        continue;
                    }

                    // 引用块（> 开头）
                    if (line.TrimStart().StartsWith(">"))
                    {
                        string quoteText = line.TrimStart().Substring(1).Trim();
                        body.Append(CreateQuote(quoteText));
                        i++;
                        continue;
                    }

                    // 代码块/公式块（四空格缩进）
                    if (line.StartsWith("    ") && line.Trim().Length > 0)
                    {
                        body.Append(CreateCodeBlock(line.Substring(4)));
                        i++;
                        continue;
                    }

                    // 分隔线
                    if (line.Trim() == "---" || line.Trim() == "***")
                    {
                        body.Append(CreateHorizontalRule());
                        i++;
                        continue;
                    }

                    // 列表项（- 或数字）
                    if (Regex.IsMatch(line.TrimStart(), @"^[-*]\s") || Regex.IsMatch(line.TrimStart(), @"^\d+\.\s"))
                    {
                        string listText = Regex.Replace(line.TrimStart(), @"^[-*]\s|^\d+\.\s", "");
                        body.Append(CreateListItem(listText));
                        i++;
                        continue;
                    }

                    // 空行
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        i++;
                        continue;
                    }

                    // 普通段落
                    body.Append(CreateParagraph(line));
                    i++;
                }

                // 页面设置
                var sectionProps = new SectionProperties(
                    new PageSize { Width = 11906U, Height = 16838U },
                    new PageMargin
                    {
                        Top = 1440,
                        Right = 1440U,
                        Bottom = 1440,
                        Left = 1440U,
                        Header = 720U,
                        Footer = 720U
                    }
                );
                body.Append(sectionProps);

                mainPart.Document.Append(body);
                mainPart.Document.Save();
            }
        }

        private void AddStyles(MainDocumentPart mainPart)
        {
            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            var styles = new Styles();

            // Heading styles
            for (int level = 1; level <= 4; level++)
            {
                int fontSize = level == 1 ? 36 : level == 2 ? 28 : level == 3 ? 24 : 22;
                var style = new Style
                {
                    Type = StyleValues.Paragraph,
                    StyleId = $"Heading{level}",
                    StyleName = new StyleName { Val = $"heading {level}" },
                    BasedOn = new BasedOn { Val = "Normal" },
                    NextParagraphStyle = new NextParagraphStyle { Val = "Normal" }
                };
                style.Append(new StyleRunProperties(
                    new Bold(),
                    new FontSize { Val = (fontSize * 2).ToString() },
                    new Color { Val = "2E3440" }
                ));
                style.Append(new StyleParagraphProperties(
                    new SpacingBetweenLines { Before = "240", After = "120" }
                ));
                styles.Append(style);
            }

            // Code style (monospace)
            var codeStyle = new Style
            {
                Type = StyleValues.Character,
                StyleId = "CodeChar",
                StyleName = new StyleName { Val = "Code Char" }
            };
            codeStyle.Append(new StyleRunProperties(
                new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                new FontSize { Val = "20" },
                new Color { Val = "4C566A" }
            ));
            styles.Append(codeStyle);

            stylesPart.Styles = styles;
            stylesPart.Styles.Save();
        }

        private Paragraph CreateHeading(string text, int level)
        {
            var para = new Paragraph();
            para.ParagraphProperties = new ParagraphProperties(
                new ParagraphStyleId { Val = $"Heading{Math.Min(level, 4)}" }
            );
            para.Append(new Run(new Text(text)));
            return para;
        }

        private Paragraph CreateParagraph(string text)
        {
            var para = new Paragraph();
            ProcessInlineFormatting(para, text);
            return para;
        }

        private Paragraph CreateListItem(string text)
        {
            var para = new Paragraph();
            var props = new ParagraphProperties(
                new Indentation { Left = "720", Hanging = "360" }
            );
            para.ParagraphProperties = props;

            var bulletRun = new Run(new Text("• ") { Space = SpaceProcessingModeValues.Preserve });
            para.Append(bulletRun);
            ProcessInlineFormatting(para, text);
            return para;
        }

        private Paragraph CreateQuote(string text)
        {
            var para = new Paragraph();
            para.ParagraphProperties = new ParagraphProperties(
                new Indentation { Left = "720" },
                new ParagraphBorders(
                    new LeftBorder { Val = BorderValues.Single, Size = 12, Color = "5E81AC", Space = 4U }
                )
            );
            var run = new Run(new Text(text));
            run.RunProperties = new RunProperties(
                new Italic(),
                new Color { Val = "4C566A" }
            );
            para.Append(run);
            return para;
        }

        private Paragraph CreateCodeBlock(string text)
        {
            var para = new Paragraph();
            para.ParagraphProperties = new ParagraphProperties(
                new Indentation { Left = "360" },
                new Shading { Fill = "ECEFF4", Val = ShadingPatternValues.Clear }
            );
            var run = new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            run.RunProperties = new RunProperties(
                new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas", EastAsia = "等线" },
                new FontSize { Val = "20" }
            );
            para.Append(run);
            return para;
        }

        private Paragraph CreateHorizontalRule()
        {
            var para = new Paragraph();
            para.ParagraphProperties = new ParagraphProperties(
                new ParagraphBorders(
                    new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "D8DEE9", Space = 1U }
                ),
                new SpacingBetweenLines { Before = "120", After = "120" }
            );
            return para;
        }

        private Table CreateTable(string[] lines, int start, int end)
        {
            var table = new Table();

            var tblProps = new TableProperties(
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4, Color = "4C566A" },
                    new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "4C566A" },
                    new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "4C566A" },
                    new RightBorder { Val = BorderValues.Single, Size = 4, Color = "4C566A" },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 2, Color = "D8DEE9" },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 2, Color = "D8DEE9" }
                ),
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }
            );
            table.Append(tblProps);

            bool isFirstRow = true;
            for (int i = start; i < end; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Skip separator rows (|---|---|)
                if (Regex.IsMatch(line, @"^\|[\s\-:]+\|"))
                {
                    if (Regex.IsMatch(line, @"^\|[\s\-:|]+\|$"))
                        continue;
                }

                var cells = line.Split('|')
                    .Where(c => !string.IsNullOrEmpty(c.Trim()) || c.Contains(" "))
                    .Select(c => c.Trim())
                    .ToArray();

                if (cells.Length == 0) continue;
                if (cells.All(c => Regex.IsMatch(c, @"^[\s\-:]+$"))) continue;

                var row = new TableRow();
                foreach (var cellText in cells)
                {
                    var cell = new TableCell();
                    var para = new Paragraph();

                    if (isFirstRow)
                    {
                        var run = new Run(new Text(cellText));
                        run.RunProperties = new RunProperties(new Bold(), new FontSize { Val = "20" });
                        para.Append(run);
                        cell.Append(new TableCellProperties(
                            new Shading { Fill = "3B4252", Val = ShadingPatternValues.Clear }
                        ));
                        // White text for header
                        if (para.Elements<Run>().FirstOrDefault()?.RunProperties != null)
                            para.Elements<Run>().First().RunProperties.Append(new Color { Val = "ECEFF4" });
                    }
                    else
                    {
                        var run = new Run(new Text(cellText));
                        run.RunProperties = new RunProperties(new FontSize { Val = "20" });
                        para.Append(run);
                    }

                    para.ParagraphProperties = new ParagraphProperties(
                        new SpacingBetweenLines { Before = "40", After = "40" }
                    );
                    cell.Append(para);
                    row.Append(cell);
                }

                table.Append(row);
                isFirstRow = false;
            }

            return table;
        }

        private void ProcessInlineFormatting(Paragraph para, string text)
        {
            // Handle **bold** and simple text
            var parts = Regex.Split(text, @"(\*\*[^*]+\*\*)");
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (part.StartsWith("**") && part.EndsWith("**"))
                {
                    var boldText = part.Substring(2, part.Length - 4);
                    var run = new Run(new Text(boldText) { Space = SpaceProcessingModeValues.Preserve });
                    run.RunProperties = new RunProperties(new Bold());
                    para.Append(run);
                }
                else
                {
                    var run = new Run(new Text(part) { Space = SpaceProcessingModeValues.Preserve });
                    para.Append(run);
                }
            }
        }
    }
}
