using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Inference;

/// <summary>线框识别输出。</summary>
public sealed class TableInferResult
{
    public InferStatus Status { get; init; }

    public TableGrid? Grid { get; init; }

    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();

    public IReadOnlyList<CellAddr> UncertainCells { get; init; } = Array.Empty<CellAddr>();

    public double OverallConfidence { get; init; }

    public GridLineSet? DebugGridLines { get; init; }

    public static TableInferResult Fail(
        InferStatus status,
        params string[] messages) =>
        new()
        {
            Status = status,
            Messages = messages ?? Array.Empty<string>(),
        };
}
