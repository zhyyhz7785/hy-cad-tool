namespace HyCAD.Tables.Inference;

/// <summary>表格线框识别状态（AC9 V1）。</summary>
public enum InferStatus
{
    Success,
    IncompleteGrid,
    AmbiguousMerge,
    TooManySegments,
    NoTextNoStructure,
}
