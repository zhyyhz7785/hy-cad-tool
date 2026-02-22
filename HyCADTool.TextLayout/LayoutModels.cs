using System;
using System.Collections.Generic;

namespace HyCADTool.TextLayout
{
    public enum DocumentBlockType
    {
        Unknown = 0,
        Heading = 1,
        Paragraph = 2,
        List = 3,
        BlockQuote = 4,
        Table = 5,
        CodeBlock = 6,
        HorizontalRule = 7
    }

    public sealed class DocumentBlock
    {
        public int Index { get; set; }
        public DocumentBlockType Type { get; set; } = DocumentBlockType.Unknown;
        public int HeadingLevel { get; set; }
        public string SourceText { get; set; } = "";
        public string PlainText { get; set; } = "";
        public int DisplayUnits { get; set; }
        public int ParagraphCount { get; set; } = 1;
        public bool IsBreakable { get; set; } = true;
    }

    public sealed class LayoutResult
    {
        public int SchemaVersion { get; set; } = 1;
        public double[] ColumnWidthsMm { get; set; } = Array.Empty<double>();
        public LayoutPage[] Pages { get; set; } = Array.Empty<LayoutPage>();
        public IncrementalLayoutMetadata IncrementalMetadata { get; set; }

        public int PageCount => Pages?.Length ?? 0;

        public string ToColumnParagraphIndicesText(int pageIndex)
        {
            if (Pages == null || Pages.Length == 0) return string.Empty;
            int idx = Math.Max(0, Math.Min(Pages.Length - 1, pageIndex));
            var cols = Pages[idx].ColumnBlockIndices ?? Array.Empty<int[]>();
            var parts = new List<string>(cols.Length);
            foreach (var col in cols)
            {
                parts.Add(col == null ? string.Empty : string.Join(",", col));
            }
            return string.Join("|", parts);
        }
    }

    public sealed class LayoutPage
    {
        public int PageIndex { get; set; }
        public int[][] ColumnBlockIndices { get; set; } = Array.Empty<int[]>();
        public double[][] ColumnBlockHeightsMm { get; set; } = Array.Empty<double[]>();
        public double[] ColumnUsedHeightsMm { get; set; } = Array.Empty<double>();
        public int[] CharsPerColumn { get; set; } = Array.Empty<int>();
        /// <summary>每栏顶部偏移（mm，相对于页面内边距顶部），由预览拖拽产生</summary>
        public double[] ColumnTopOffsetsMm { get; set; } = Array.Empty<double>();
        /// <summary>每栏可用高度（mm），由预览拖拽产生；0 表示使用全高</summary>
        public double[] ColumnHeightsMm { get; set; } = Array.Empty<double>();
    }

    public sealed class IncrementalLayoutMetadata
    {
        public int DirtyBlockStart { get; set; } = -1;
        public int DirtyBlockEnd { get; set; } = -1;
        public int StableBlockIndex { get; set; } = -1;
        public int[] AffectedPageIndices { get; set; } = Array.Empty<int>();
        public LayoutColumnDelta[] ColumnDeltas { get; set; } = Array.Empty<LayoutColumnDelta>();
        public LayoutMovedBlock[] MovedBlocks { get; set; } = Array.Empty<LayoutMovedBlock>();
    }

    public sealed class LayoutColumnDelta
    {
        public int PageIndex { get; set; }
        public int ColumnIndex { get; set; }
        public double OldHeightMm { get; set; }
        public double NewHeightMm { get; set; }
        public double HeightDeltaMm => NewHeightMm - OldHeightMm;
    }

    public sealed class LayoutMovedBlock
    {
        public int BlockIndex { get; set; } = -1;
        public int OldPageIndex { get; set; } = -1;
        public int OldColumnIndex { get; set; } = -1;
        public int OldPositionInColumn { get; set; } = -1;
        public int NewPageIndex { get; set; } = -1;
        public int NewColumnIndex { get; set; } = -1;
        public int NewPositionInColumn { get; set; } = -1;
    }
}
