using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HyCADTool.TextLayout
{
    public sealed class MarkdownTableData
    {
        public List<List<string>> Rows { get; } = new List<List<string>>();
        public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(r => r?.Count ?? 0);
    }

    public static class MarkdownTableExtractor
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public static List<MarkdownTableData> ExtractTopLevelTables(string markdown)
        {
            var result = new List<MarkdownTableData>();
            if (string.IsNullOrWhiteSpace(markdown))
                return result;

            try
            {
                var document = Markdown.Parse(markdown, Pipeline);
                if (document == null || document.Count == 0)
                    return result;

                foreach (var block in document)
                {
                    if (!(block is Table table))
                        continue;

                    var blockText = SliceBySpan(markdown, block);
                    if (string.IsNullOrWhiteSpace(blockText))
                        continue;

                    var data = ToTableData(table);
                    if (data.Rows.Count > 0 && data.ColumnCount > 0)
                        result.Add(data);
                }
            }
            catch
            {
                // 提取失败不影响主流程
            }

            return result;
        }

        public static string RemoveTopLevelTables(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return markdown ?? string.Empty;

            try
            {
                var document = Markdown.Parse(markdown, Pipeline);
                if (document == null || document.Count == 0)
                    return markdown;

                var kept = new List<string>();
                foreach (var block in document)
                {
                    var blockText = SliceBySpan(markdown, block);
                    if (string.IsNullOrWhiteSpace(blockText))
                    {
                        if (IsIgnorableMetaBlock(block))
                            continue;
                        return markdown;
                    }

                    if (block is Table)
                        continue;

                    kept.Add(blockText.TrimEnd('\r', '\n'));
                }

                return kept.Count == 0 ? string.Empty : string.Join("\n\n", kept);
            }
            catch
            {
                return markdown;
            }
        }

        public static int[] GetColumnDisplayUnits(MarkdownTableData tableData)
        {
            if (tableData == null || tableData.Rows.Count == 0 || tableData.ColumnCount <= 0)
                return Array.Empty<int>();

            int[] units = Enumerable.Repeat(2, tableData.ColumnCount).ToArray();
            foreach (var row in tableData.Rows)
            {
                if (row == null) continue;
                for (int c = 0; c < row.Count && c < units.Length; c++)
                {
                    int cellUnits = DisplayWidthCalculator.GetDisplayUnits(row[c] ?? string.Empty);
                    if (cellUnits > units[c])
                        units[c] = cellUnits;
                }
            }

            return units;
        }

        public static int[] EstimateRowLineCounts(MarkdownTableData tableData, int[] charsPerLineByColumn)
        {
            if (tableData == null || tableData.Rows.Count == 0)
                return Array.Empty<int>();
            if (charsPerLineByColumn == null || charsPerLineByColumn.Length == 0)
                return Enumerable.Repeat(1, tableData.Rows.Count).ToArray();

            var rowLines = new int[tableData.Rows.Count];
            for (int r = 0; r < tableData.Rows.Count; r++)
            {
                var row = tableData.Rows[r] ?? new List<string>();
                int maxLines = 1;
                for (int c = 0; c < charsPerLineByColumn.Length; c++)
                {
                    int charsPerLine = Math.Max(1, charsPerLineByColumn[c]);
                    string text = c < row.Count ? row[c] ?? string.Empty : string.Empty;
                    int units = Math.Max(1, DisplayWidthCalculator.GetDisplayUnits(text));
                    int lines = Math.Max(1, (int)Math.Ceiling(units / (double)charsPerLine));
                    if (lines > maxLines) maxLines = lines;
                }
                rowLines[r] = maxLines;
            }

            return rowLines;
        }

        private static MarkdownTableData ToTableData(Table table)
        {
            var data = new MarkdownTableData();
            foreach (var rowObj in table)
            {
                if (!(rowObj is TableRow row))
                    continue;

                var rowCells = new List<string>();
                foreach (var cellObj in row)
                {
                    if (!(cellObj is TableCell cell))
                        continue;

                    var sb = new StringBuilder();
                    foreach (var child in cell)
                    {
                        if (child is ParagraphBlock p && p.Inline != null)
                            AppendInlines(sb, p.Inline);
                    }
                    rowCells.Add(sb.ToString().Trim());
                }

                if (rowCells.Count > 0)
                    data.Rows.Add(rowCells);
            }

            return data;
        }

        private static void AppendInlines(StringBuilder sb, ContainerInline inlines)
        {
            foreach (var inline in inlines)
            {
                if (inline is LiteralInline literal)
                    sb.Append(literal.Content.ToString());
                else if (inline is CodeInline code)
                    sb.Append(code.Content);
                else if (inline is LineBreakInline)
                    sb.Append(' ');
                else if (inline is EmphasisInline emphasis)
                {
                    foreach (var child in emphasis)
                    {
                        if (child is LiteralInline eLit)
                            sb.Append(eLit.Content.ToString());
                    }
                }
                else if (inline is LinkInline link)
                {
                    if (link.FirstChild is ContainerInline linkContent)
                        AppendInlines(sb, linkContent);
                }
                else if (inline is ContainerInline container)
                {
                    AppendInlines(sb, container);
                }
            }
        }

        private static string SliceBySpan(string markdown, Block block)
        {
            if (block == null || markdown.Length == 0)
                return string.Empty;

            int start = block.Span.Start;
            int end = block.Span.End;
            if (start < 0 || end < start || start >= markdown.Length)
                return string.Empty;

            end = Math.Min(end, markdown.Length - 1);
            int len = end - start + 1;
            if (len <= 0)
                return string.Empty;

            return markdown.Substring(start, len);
        }

        private static bool IsIgnorableMetaBlock(Block block)
        {
            string typeName = block?.GetType().Name ?? string.Empty;
            return string.Equals(typeName, "LinkReferenceDefinitionGroup", StringComparison.Ordinal);
        }
    }
}
