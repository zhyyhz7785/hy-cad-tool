using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Inference;

/// <summary>纯几何线框识别引擎（AC9 V1）。</summary>
public sealed class TableInferEngine
{
    private readonly TableInferOptions _options;

    public TableInferEngine(TableInferOptions? options = null)
    {
        _options = options ?? TableInferOptions.Default;
    }

    /// <summary>纯几何推断；不访问 AutoCAD。</summary>
    public TableInferResult Infer(TableInferInput input)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        var messages = new List<string>();
        var segments = input.Segments ?? Array.Empty<LineSegment2d>();
        if (segments.Count > _options.MaxSegmentCount)
        {
            return TableInferResult.Fail(
                InferStatus.TooManySegments,
                $"线段数量 {segments.Count} 超出上限 {_options.MaxSegmentCount}。");
        }

        if (segments.Count == 0)
        {
            return TableInferResult.Fail(
                InferStatus.NoTextNoStructure,
                "未提供任何线段。");
        }

        var cluster = LineSegmentClusterer.Process(input, _options);
        messages.AddRange(cluster.Messages);
        if (cluster.Segments.Count == 0)
        {
            return TableInferResult.Fail(
                InferStatus.NoTextNoStructure,
                "过滤后无有效水平/竖直线段。");
        }

        var structural = StructuralGridFilter.Filter(cluster.Segments, _options);
        var extracted = GridLineExtractor.TryExtract(structural, _options);
        messages.AddRange(extracted.Messages);
        if (extracted.Result == null)
        {
            if (messages.Count == 0)
                messages.Add("无法提取完整网格。");

            return new TableInferResult
            {
                Status = InferStatus.IncompleteGrid,
                Messages = messages,
                DebugGridLines = null,
            };
        }

        var gridLines = extracted.Result.GridLines;

        var mergeResult = MergeInferencer.Infer(gridLines, structural, _options);
        if (mergeResult == null)
        {
            messages.Add("合并推断存在歧义。");
            return new TableInferResult
            {
                Status = InferStatus.AmbiguousMerge,
                Messages = messages,
                DebugGridLines = gridLines,
            };
        }

        messages.AddRange(mergeResult.Messages);

        var rowTracks = new List<GridTrack>();
        for (var r = 0; r < gridLines.RowCount; r++)
            rowTracks.Add(new GridTrack(gridLines.YBoundaries[r] - gridLines.YBoundaries[r + 1]));

        var colTracks = new List<GridTrack>();
        for (var c = 0; c < gridLines.ColCount; c++)
            colTracks.Add(new GridTrack(gridLines.XBoundaries[c + 1] - gridLines.XBoundaries[c]));

        var topology = new GridTopology(rowTracks, colTracks, GrowDirection.Down, BorderSet.None);
        var structure = new GridStructure(
            topology,
            mergeResult.Merges,
            new Dictionary<CellAddr, DiagonalSplit>(),
            new Dictionary<CellAddr, CellStyle>(),
            new Dictionary<CellAddr, CellRole>(),
            new Dictionary<string, CellAddr>(),
            new Dictionary<CellAddr, CellOverflowFlags>());

        var assign = TextAssigner.Assign(gridLines, mergeResult.Merges, input.Texts ?? Array.Empty<TextBox2d>(), _options);
        messages.AddRange(assign.Messages);

        var grid = new TableGrid
        {
            Id = Guid.NewGuid(),
            Structure = structure,
            Data = assign.Data,
        };

        var validation = GridInvariants.Validate(grid);
        if (!validation.IsValid)
        {
            foreach (var violation in validation.Violations)
                messages.Add(violation.Message);

            return new TableInferResult
            {
                Status = InferStatus.IncompleteGrid,
                Grid = grid,
                Messages = messages,
                UncertainCells = assign.UncertainCells,
                DebugGridLines = gridLines,
            };
        }

        var visibleCount = CountVisibleCells(structure);
        var confidence = visibleCount == 0
            ? 1.0
            : Math.Max(0, 1.0 - (double)assign.UncertainCells.Count / visibleCount);

        if (confidence < 0.95)
            messages.Add($"置信度 {confidence:P0}，建议人工确认。");

        return new TableInferResult
        {
            Status = InferStatus.Success,
            Grid = grid,
            Messages = messages,
            UncertainCells = assign.UncertainCells,
            OverallConfidence = confidence,
            DebugGridLines = gridLines,
        };
    }

    private static int CountVisibleCells(GridStructure structure)
    {
        var count = 0;
        for (var r = 0; r < structure.Topology.RowCount; r++)
        {
            for (var c = 0; c < structure.Topology.ColCount; c++)
            {
                if (!structure.IsHidden(new CellAddr(r, c)))
                    count++;
            }
        }

        return count;
    }
}
