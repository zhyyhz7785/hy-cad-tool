using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.TextLayout
{
    /// <summary>
    /// 统一分栏分页布局引擎。
    /// 采用与 CAD 导出一致的文本度量口径，避免预览与导出分裂。
    /// </summary>
    public sealed class LayoutEngine
    {
        public LayoutResult Distribute(IReadOnlyList<DocumentBlock> blocks, DesignSpecConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Normalize();
            config.Validate();

            var area = TextAreaCalculator.Calculate(config);
            double[] colWidths = area.ColumnWidths ?? Array.Empty<double>();
            int cols = Math.Max(1, area.ColumnCount);
            if (colWidths.Length != cols)
            {
                colWidths = Enumerable.Range(0, cols)
                    .Select(config.GetColumnWidth)
                    .ToArray();
            }

            var safeBlocks = blocks ?? Array.Empty<DocumentBlock>();
            double pageContentHeight = Math.Max(
                config.ActualTextHeight * 2,
                (config.PageHeightMm - config.MarginTopMm - config.MarginBottomMm) * config.Scale);

            var pages = new List<MutablePage> { CreatePage(cols, colWidths, config) };
            int pageIndex = 0;
            int colIndex = 0;

            foreach (var block in safeBlocks)
            {
                double blockHeight = EstimateBlockHeightMm(block, config, colWidths[colIndex]);
                int guard = 0;
                while (guard++ < cols * 1000)
                {
                    var page = pages[pageIndex];
                    bool fits = page.ColumnUsedHeights[colIndex] + blockHeight <= pageContentHeight
                                || page.ColumnBlockIndices[colIndex].Count == 0;

                    if (fits)
                    {
                        page.ColumnBlockIndices[colIndex].Add(block.Index);
                        page.ColumnBlockHeights[colIndex].Add(blockHeight);
                        page.ColumnUsedHeights[colIndex] += blockHeight;
                        break;
                    }

                    colIndex++;
                    if (colIndex >= cols)
                    {
                        colIndex = 0;
                        pageIndex++;
                        if (pageIndex >= pages.Count)
                            pages.Add(CreatePage(cols, colWidths, config));
                    }
                }
            }

            return new LayoutResult
            {
                SchemaVersion = 1,
                ColumnWidthsMm = colWidths,
                Pages = pages.Select((p, idx) => p.ToImmutable(idx)).ToArray()
            };
        }

        private static double EstimateBlockHeightMm(DocumentBlock block, DesignSpecConfig cfg, double colWidthMm)
        {
            if (block == null) return cfg.ActualTextHeight * cfg.LineSpacingFactor;

            int charsPerLine = Math.Max(1, (int)Math.Floor(colWidthMm / Math.Max(0.01, cfg.ActualTextHeight * cfg.TextXScale)));
            int displayUnits = Math.Max(1, block.DisplayUnits);
            double lineHeight = cfg.ActualTextHeight * cfg.LineSpacingFactor;

            switch (block.Type)
            {
                case DocumentBlockType.Heading:
                    double headingHeight = cfg.ActualTextHeight;
                    if (block.HeadingLevel == 1) headingHeight = cfg.H1Height;
                    else if (block.HeadingLevel == 2) headingHeight = cfg.H2Height;
                    else if (block.HeadingLevel == 3) headingHeight = cfg.H3Height;

                    int headingLines = Math.Max(1, (int)Math.Ceiling((double)displayUnits / charsPerLine));
                    return headingLines * headingHeight * cfg.LineSpacingFactor
                        + cfg.GetHeadingSpaceBefore(block.HeadingLevel)
                        + cfg.GetHeadingSpaceAfter(block.HeadingLevel);

                case DocumentBlockType.List:
                    int listLines = Math.Max(1, (int)Math.Ceiling((double)displayUnits / charsPerLine));
                    return listLines * lineHeight + cfg.ActualLiSpaceAfter * Math.Max(1, block.ParagraphCount);

                case DocumentBlockType.BlockQuote:
                    int quoteLines = Math.Max(1, (int)Math.Ceiling((double)displayUnits / charsPerLine));
                    return quoteLines * lineHeight + cfg.ActualQuoteSpaceBefore + cfg.ActualQuoteSpaceAfter;

                case DocumentBlockType.Table:
                    int tableRows = Math.Max(2, block.SourceText?.Split('\n').Count(l => l.Contains("|")) ?? 2);
                    return tableRows * lineHeight * 1.3;

                case DocumentBlockType.CodeBlock:
                    int codeLines = Math.Max(1, block.ParagraphCount);
                    return codeLines * lineHeight * 1.1;

                case DocumentBlockType.HorizontalRule:
                    return lineHeight * 1.2;

                default:
                    int lines = Math.Max(1, (int)Math.Ceiling((double)displayUnits / charsPerLine));
                    return lines * lineHeight + cfg.ActualPSpaceAfter;
            }
        }

        private static MutablePage CreatePage(int cols, double[] colWidths, DesignSpecConfig cfg)
        {
            var page = new MutablePage
            {
                ColumnBlockIndices = Enumerable.Range(0, cols).Select(_ => new List<int>()).ToArray(),
                ColumnBlockHeights = Enumerable.Range(0, cols).Select(_ => new List<double>()).ToArray(),
                ColumnUsedHeights = new double[cols],
                CharsPerColumn = colWidths
                    .Select(w => Math.Max(1, (int)Math.Floor(w / Math.Max(0.01, cfg.ActualTextHeight * cfg.TextXScale))))
                    .ToArray()
            };
            return page;
        }

        private sealed class MutablePage
        {
            public List<int>[] ColumnBlockIndices { get; set; }
            public List<double>[] ColumnBlockHeights { get; set; }
            public double[] ColumnUsedHeights { get; set; }
            public int[] CharsPerColumn { get; set; }

            public LayoutPage ToImmutable(int pageIndex)
            {
                return new LayoutPage
                {
                    PageIndex = pageIndex,
                    ColumnBlockIndices = ColumnBlockIndices.Select(x => x.ToArray()).ToArray(),
                    ColumnBlockHeightsMm = ColumnBlockHeights.Select(x => x.ToArray()).ToArray(),
                    ColumnUsedHeightsMm = ColumnUsedHeights.ToArray(),
                    CharsPerColumn = CharsPerColumn.ToArray()
                };
            }
        }
    }
}
