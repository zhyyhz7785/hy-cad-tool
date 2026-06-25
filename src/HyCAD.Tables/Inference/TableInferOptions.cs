namespace HyCAD.Tables.Inference;

/// <summary>线框识别容差（AC9 V1，单位 mm / 度）。</summary>
public sealed class TableInferOptions
{
    public static TableInferOptions Default { get; } = new();

    public double AngleToleranceDeg { get; set; } = 2.0;

    public double EndpointSnapMm { get; set; } = 1.0;

    public double LineMergeGapMm { get; set; } = 1.0;

    public double GridLineMinLengthMm { get; set; } = 5.0;

    public double TextInCellMarginMm { get; set; } = 2.0;

    public double TrackSizeRelativeTolerance { get; set; } = 0.02;

    public int MinRows { get; set; } = 1;

    public int MinCols { get; set; } = 1;

    public int MaxSegmentCount { get; set; } = 500;

    /// <summary>外框边线段覆盖度下限。</summary>
    public double OuterEdgeCoverageRatio { get; set; } = 0.8;

    /// <summary>内部网格边线段覆盖度下限。</summary>
    public double InnerEdgeCoverageRatio { get; set; } = 0.8;

    /// <summary>主网格线最小跨度占表宽/高的比例（过滤 PhotoSlot 内框等）。</summary>
    public double StructuralMinSpanRatio { get; set; } = 0.45;

    /// <summary>外框边匹配容差（Explode 偏移线，mm）。</summary>
    public double OuterEdgeMatchToleranceMm { get; set; } = 2.0;

    /// <summary>网格主轴最小覆盖占表宽/高的比例。</summary>
    public double MinAxisCoverageRatio { get; set; } = 0.12;
}
