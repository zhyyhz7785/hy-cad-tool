using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HyCADTool.Refactored.Domain.Models.Text
{
    public sealed class MarkdownTableData
    {
        public List<List<string>> Rows { get; } = new List<List<string>>();

        public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(r => r?.Count ?? 0);
    }

    /// <summary>
    /// 提取 Markdown 顶层表格，并支持去除顶层表格块。
    /// </summary>
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

                    // 只有 Span 可切片时，才将该 block 视为可定位顶层表格
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

                        // Span 异常时回退原文，避免误删
                        return markdown;
                    }

                    if (block is Table)
                        continue;

                    kept.Add(blockText.TrimEnd('\r', '\n'));
                }

                string cleaned = kept.Count == 0 ? string.Empty : string.Join("\n\n", kept);
                return cleaned;
            }
            catch
            {
                return markdown;
            }
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
                {
                    sb.Append(literal.Content.ToString());
                }
                else if (inline is CodeInline code)
                {
                    sb.Append(code.Content);
                }
                else if (inline is LineBreakInline)
                {
                    sb.Append(' ');
                }
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
