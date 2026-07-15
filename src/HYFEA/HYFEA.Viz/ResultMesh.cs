using System.Collections.Generic;

namespace HYFEA.Viz;

/// <summary>可视化用结果网格（与求解器解耦；可写出 VTU）。</summary>
public sealed class ResultMesh
{
    /// <summary>交错 XYZ，长度 3×NumberOfPoints（Z 常为 0）。</summary>
    public double[] Points { get; init; } = System.Array.Empty<double>();

    /// <summary>线段连接：每单元两个点索引，长度 2×NumberOfLines。</summary>
    public int[] LineConnectivity { get; init; } = System.Array.Empty<int>();

    /// <summary>点标量场（每场长度 = NumberOfPoints）。</summary>
    public IReadOnlyDictionary<string, double[]> PointData { get; init; }
        = new Dictionary<string, double[]>();

    /// <summary>点向量场（每场长度 = 3×NumberOfPoints，如位移 U）。</summary>
    public IReadOnlyDictionary<string, double[]> PointVectors { get; init; }
        = new Dictionary<string, double[]>();

    public int NumberOfPoints => Points.Length / 3;

    public int NumberOfLines => LineConnectivity.Length / 2;
}
