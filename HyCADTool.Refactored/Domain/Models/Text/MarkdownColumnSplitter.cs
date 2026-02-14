using Markdig;
using Markdig.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.Models.Text
{
    /// <summary>
    /// 按预览返回的列索引拆分 Markdown。
    /// 统一口径：以 Markdig 顶层 Block 为最小拆分单位，避免代码块/表格被按空行误拆。
    /// </summary>
    public static class MarkdownColumnSplitter
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        /// <summary>
        /// 按列索引拆分 Markdown。
        /// paraIndices 格式: "0,1,2|3,4,5"
        /// </summary>
        public static string[] SplitByColumnIndices(string markdown, string paraIndices)
        {
            if (string.IsNullOrEmpty(markdown))
                return new[] { "" };

            if (string.IsNullOrWhiteSpace(paraIndices))
                return new[] { markdown };

            var blocks = ExtractTopLevelBlocks(markdown);
            if (blocks.Length == 0)
                return new[] { markdown };

            var colGroups = paraIndices.Split('|');
            var result = new string[colGroups.Length];

            for (int c = 0; c < colGroups.Length; c++)
            {
                if (string.IsNullOrWhiteSpace(colGroups[c]))
                {
                    result[c] = "";
                    continue;
                }

                var indices = colGroups[c].Split(',')
                    .Select(s => { int v; return int.TryParse(s.Trim(), out v) ? v : -1; })
                    .Where(v => v >= 0 && v < blocks.Length)
                    .ToArray();

                result[c] = string.Join("\n\n", indices.Select(i => blocks[i]));
            }

            return result.Length > 0 ? result : new[] { markdown };
        }

        /// <summary>
        /// 提取顶层 Block 对应的 Markdown 源文本。
        /// 当 Span 切片不可用时，自动降级到“按空行拆分”以保证流程可用。
        /// </summary>
        public static string[] ExtractTopLevelBlocks(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return Array.Empty<string>();

            try
            {
                var document = Markdown.Parse(markdown, Pipeline);
                if (document == null || document.Count == 0)
                    return SplitMarkdownByBlankLines(markdown);

                var blocks = new List<string>(document.Count);
                foreach (var block in document)
                {
                    var blockText = SliceBySpan(markdown, block);
                    if (string.IsNullOrWhiteSpace(blockText))
                    {
                        if (IsIgnorableMetaBlock(block))
                        {
                            continue;
                        }
                        return SplitMarkdownByBlankLines(markdown);
                    }

                    blocks.Add(blockText.TrimEnd('\r', '\n'));
                }

                return blocks.Count > 0
                    ? blocks.ToArray()
                    : SplitMarkdownByBlankLines(markdown);
            }
            catch
            {
                return SplitMarkdownByBlankLines(markdown);
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

        private static string[] SplitMarkdownByBlankLines(string markdown)
        {
            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var blocks = new List<string>();
            var current = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (current.Length > 0)
                    {
                        blocks.Add(current.ToString().TrimEnd());
                        current.Clear();
                    }
                }
                else
                {
                    if (current.Length > 0)
                        current.Append('\n');
                    current.Append(line);
                }
            }

            if (current.Length > 0)
                blocks.Add(current.ToString().TrimEnd());

            return blocks.ToArray();
        }
    }
}
