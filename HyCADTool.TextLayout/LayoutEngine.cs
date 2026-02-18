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

        public LayoutResult DistributeIncremental(
            IReadOnlyList<DocumentBlock> blocks,
            DesignSpecConfig config,
            LayoutResult previousResult,
            int dirtyBlockStart,
            int dirtyBlockEnd)
        {
            var next = Distribute(blocks, config);
            if (previousResult == null
                || previousResult.Pages == null
                || previousResult.Pages.Length == 0
                || dirtyBlockStart < 0)
            {
                next.IncrementalMetadata = new IncrementalLayoutMetadata
                {
                    DirtyBlockStart = dirtyBlockStart,
                    DirtyBlockEnd = dirtyBlockEnd,
                    StableBlockIndex = -1,
                    AffectedPageIndices = Enumerable.Range(0, next.PageCount).ToArray()
                };
                return next;
            }

            next.IncrementalMetadata = BuildIncrementalMetadata(previousResult, next, dirtyBlockStart, dirtyBlockEnd);
            return next;
        }

        private static IncrementalLayoutMetadata BuildIncrementalMetadata(
            LayoutResult previousResult,
            LayoutResult nextResult,
            int dirtyBlockStart,
            int dirtyBlockEnd)
        {
            var oldMap = BuildPlacementMap(previousResult);
            var newMap = BuildPlacementMap(nextResult);
            var moved = new List<LayoutMovedBlock>();
            var affectedPages = new HashSet<int>();
            int stableBlock = -1;
            int searchStart = Math.Max(0, dirtyBlockEnd + 1);
            int maxBlockIndex = newMap.Count > 0 ? newMap.Keys.Max() : -1;

            foreach (var kv in newMap.OrderBy(x => x.Key))
            {
                int blockIndex = kv.Key;
                if (blockIndex < Math.Max(0, dirtyBlockStart))
                    continue;

                var nextPlacement = kv.Value;
                if (!oldMap.TryGetValue(blockIndex, out var oldPlacement))
                {
                    moved.Add(new LayoutMovedBlock
                    {
                        BlockIndex = blockIndex,
                        OldPageIndex = -1,
                        OldColumnIndex = -1,
                        OldPositionInColumn = -1,
                        NewPageIndex = nextPlacement.PageIndex,
                        NewColumnIndex = nextPlacement.ColumnIndex,
                        NewPositionInColumn = nextPlacement.PositionInColumn
                    });
                    affectedPages.Add(nextPlacement.PageIndex);
                    continue;
                }

                if (oldPlacement.PageIndex != nextPlacement.PageIndex
                    || oldPlacement.ColumnIndex != nextPlacement.ColumnIndex
                    || oldPlacement.PositionInColumn != nextPlacement.PositionInColumn)
                {
                    moved.Add(new LayoutMovedBlock
                    {
                        BlockIndex = blockIndex,
                        OldPageIndex = oldPlacement.PageIndex,
                        OldColumnIndex = oldPlacement.ColumnIndex,
                        OldPositionInColumn = oldPlacement.PositionInColumn,
                        NewPageIndex = nextPlacement.PageIndex,
                        NewColumnIndex = nextPlacement.ColumnIndex,
                        NewPositionInColumn = nextPlacement.PositionInColumn
                    });
                    if (oldPlacement.PageIndex >= 0) affectedPages.Add(oldPlacement.PageIndex);
                    if (nextPlacement.PageIndex >= 0) affectedPages.Add(nextPlacement.PageIndex);
                }
            }

            for (int i = searchStart; i <= maxBlockIndex; i++)
            {
                if (!oldMap.TryGetValue(i, out var oldPlacement) || !newMap.TryGetValue(i, out var newPlacement))
                    continue;

                bool equal = oldPlacement.PageIndex == newPlacement.PageIndex
                    && oldPlacement.ColumnIndex == newPlacement.ColumnIndex
                    && oldPlacement.PositionInColumn == newPlacement.PositionInColumn;
                if (!equal)
                    continue;

                stableBlock = i;
                break;
            }

            var deltas = BuildColumnDeltas(previousResult, nextResult, affectedPages);
            if (affectedPages.Count == 0 && deltas.Length > 0)
            {
                foreach (var delta in deltas)
                    affectedPages.Add(delta.PageIndex);
            }

            int[] affected = affectedPages
                .Where(p => p >= 0)
                .Distinct()
                .OrderBy(p => p)
                .ToArray();

            return new IncrementalLayoutMetadata
            {
                DirtyBlockStart = dirtyBlockStart,
                DirtyBlockEnd = dirtyBlockEnd,
                StableBlockIndex = stableBlock,
                AffectedPageIndices = affected,
                ColumnDeltas = deltas,
                MovedBlocks = moved.ToArray()
            };
        }

        private static LayoutColumnDelta[] BuildColumnDeltas(
            LayoutResult previousResult,
            LayoutResult nextResult,
            HashSet<int> affectedPages)
        {
            var deltas = new List<LayoutColumnDelta>();
            int oldPageCount = previousResult?.Pages?.Length ?? 0;
            int nextPageCount = nextResult?.Pages?.Length ?? 0;
            int maxPages = Math.Max(oldPageCount, nextPageCount);
            const double epsilon = 0.001;

            for (int p = 0; p < maxPages; p++)
            {
                var oldPage = p < oldPageCount ? previousResult.Pages[p] : null;
                var newPage = p < nextPageCount ? nextResult.Pages[p] : null;
                int oldCols = oldPage?.ColumnUsedHeightsMm?.Length ?? 0;
                int newCols = newPage?.ColumnUsedHeightsMm?.Length ?? 0;
                int maxCols = Math.Max(oldCols, newCols);

                for (int c = 0; c < maxCols; c++)
                {
                    double oldHeight = c < oldCols ? oldPage.ColumnUsedHeightsMm[c] : 0;
                    double newHeight = c < newCols ? newPage.ColumnUsedHeightsMm[c] : 0;
                    if (Math.Abs(oldHeight - newHeight) <= epsilon)
                        continue;

                    affectedPages?.Add(p);
                    deltas.Add(new LayoutColumnDelta
                    {
                        PageIndex = p,
                        ColumnIndex = c,
                        OldHeightMm = oldHeight,
                        NewHeightMm = newHeight
                    });
                }
            }

            return deltas.ToArray();
        }

        private static Dictionary<int, BlockPlacement> BuildPlacementMap(LayoutResult result)
        {
            var map = new Dictionary<int, BlockPlacement>();
            if (result?.Pages == null)
                return map;

            foreach (var page in result.Pages)
            {
                if (page?.ColumnBlockIndices == null)
                    continue;

                for (int c = 0; c < page.ColumnBlockIndices.Length; c++)
                {
                    var blocks = page.ColumnBlockIndices[c];
                    if (blocks == null)
                        continue;

                    for (int i = 0; i < blocks.Length; i++)
                    {
                        int blockIndex = blocks[i];
                        map[blockIndex] = new BlockPlacement
                        {
                            PageIndex = page.PageIndex,
                            ColumnIndex = c,
                            PositionInColumn = i
                        };
                    }
                }
            }

            return map;
        }

        private sealed class BlockPlacement
        {
            public int PageIndex { get; set; }
            public int ColumnIndex { get; set; }
            public int PositionInColumn { get; set; }
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
                    return EstimateTableHeightMm(block, cfg, colWidthMm, lineHeight);

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

        private static double EstimateTableHeightMm(DocumentBlock block, DesignSpecConfig cfg, double colWidthMm, double lineHeight)
        {
            var tables = MarkdownTableExtractor.ExtractTopLevelTables(block?.SourceText ?? string.Empty);
            var table = tables.FirstOrDefault();
            if (table == null || table.Rows.Count == 0 || table.ColumnCount <= 0)
            {
                int fallbackRows = Math.Max(2, block?.SourceText?.Split('\n').Count(l => l.Contains("|")) ?? 2);
                return fallbackRows * lineHeight * 1.3;
            }

            int[] colUnits = MarkdownTableExtractor.GetColumnDisplayUnits(table);
            if (colUnits == null || colUnits.Length == 0)
                colUnits = Enumerable.Repeat(2, table.ColumnCount).ToArray();

            double totalUnits = Math.Max(1, colUnits.Sum(u => Math.Max(1, u)));
            int[] charsPerLine = new int[colUnits.Length];
            for (int i = 0; i < colUnits.Length; i++)
            {
                double ratio = Math.Max(1, colUnits[i]) / totalUnits;
                double cellWidth = Math.Max(cfg.ActualTextHeight * 2, colWidthMm * ratio);
                charsPerLine[i] = Math.Max(1, (int)Math.Floor(cellWidth / Math.Max(0.01, cfg.ActualTextHeight * cfg.TextXScale)));
            }

            int[] rowLines = MarkdownTableExtractor.EstimateRowLineCounts(table, charsPerLine);
            double totalRowLines = Math.Max(1, rowLines.Sum(v => Math.Max(1, v)));
            double rowBaseHeight = Math.Max(lineHeight * 1.3, cfg.ActualTextHeight);
            return totalRowLines * rowBaseHeight;
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
