using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HyCADTool.TextLayout
{
    public static class MarkdownBlockParser
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();

        public static IReadOnlyList<DocumentBlock> ParseTopLevelBlocks(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return Array.Empty<DocumentBlock>();

            var sourceBlocks = MarkdownColumnSplitter.ExtractTopLevelBlocks(markdown);
            if (sourceBlocks.Length == 0)
                return Array.Empty<DocumentBlock>();

            var document = Markdown.Parse(markdown, Pipeline);
            var typedBlocks = document?.ToList() ?? new List<Block>();
            var result = new List<DocumentBlock>(sourceBlocks.Length);

            for (int i = 0; i < sourceBlocks.Length; i++)
            {
                string blockText = sourceBlocks[i] ?? string.Empty;
                Block ast = i < typedBlocks.Count ? typedBlocks[i] : null;
                var item = new DocumentBlock
                {
                    Index = i,
                    SourceText = blockText,
                    PlainText = ToPlainText(ast, blockText),
                    Type = ToType(ast),
                    HeadingLevel = ast is HeadingBlock hb ? hb.Level : 0
                };
                item.DisplayUnits = DisplayWidthCalculator.GetDisplayUnits(item.PlainText);
                item.ParagraphCount = CountParagraphLikeLines(item);
                item.IsBreakable = item.Type != DocumentBlockType.Table && item.Type != DocumentBlockType.CodeBlock;
                result.Add(item);
            }

            return result;
        }

        private static DocumentBlockType ToType(Block block)
        {
            if (block is HeadingBlock) return DocumentBlockType.Heading;
            if (block is ParagraphBlock) return DocumentBlockType.Paragraph;
            if (block is ListBlock) return DocumentBlockType.List;
            if (block is QuoteBlock) return DocumentBlockType.BlockQuote;
            if (block is Table) return DocumentBlockType.Table;
            if (block is FencedCodeBlock) return DocumentBlockType.CodeBlock;
            if (block is ThematicBreakBlock) return DocumentBlockType.HorizontalRule;
            return DocumentBlockType.Unknown;
        }

        private static int CountParagraphLikeLines(DocumentBlock block)
        {
            if (block == null || string.IsNullOrWhiteSpace(block.SourceText))
                return 1;

            switch (block.Type)
            {
                case DocumentBlockType.List:
                    return Math.Max(1, block.SourceText.Split('\n').Count(line =>
                        line.TrimStart().StartsWith("- ")
                        || line.TrimStart().StartsWith("* ")
                        || line.TrimStart().StartsWith("+ ")
                        || IsOrderedLine(line)));

                case DocumentBlockType.CodeBlock:
                    return Math.Max(1, block.SourceText.Replace("\r\n", "\n").Split('\n').Length - 2);

                default:
                    return 1;
            }
        }

        private static bool IsOrderedLine(string line)
        {
            string t = (line ?? string.Empty).TrimStart();
            int dot = t.IndexOf('.');
            if (dot <= 0) return false;
            for (int i = 0; i < dot; i++)
            {
                if (!char.IsDigit(t[i])) return false;
            }
            return dot + 1 < t.Length && t[dot + 1] == ' ';
        }

        private static string ToPlainText(Block block, string fallback)
        {
            if (block == null) return fallback ?? string.Empty;
            var sb = new StringBuilder();

            if (block is LeafBlock leaf && leaf.Inline != null)
            {
                AppendInlineText(leaf.Inline, sb);
                return sb.ToString();
            }

            if (block is ContainerBlock container)
            {
                foreach (var child in container)
                {
                    if (child is LeafBlock cLeaf && cLeaf.Inline != null)
                        AppendInlineText(cLeaf.Inline, sb);
                    else
                        sb.Append(child.ToString());
                    sb.Append('\n');
                }
                return sb.ToString().Trim();
            }

            return fallback ?? string.Empty;
        }

        private static void AppendInlineText(Markdig.Syntax.Inlines.ContainerInline inline, StringBuilder sb)
        {
            foreach (var item in inline)
            {
                if (item is Markdig.Syntax.Inlines.LiteralInline literal)
                    sb.Append(literal.Content.ToString());
                else if (item is Markdig.Syntax.Inlines.CodeInline code)
                    sb.Append(code.Content);
                else if (item is Markdig.Syntax.Inlines.ContainerInline nested)
                    AppendInlineText(nested, sb);
                else if (item is Markdig.Syntax.Inlines.LineBreakInline)
                    sb.Append(' ');
            }
        }
    }
}
