namespace HyCAD.Tables.Structure;

/// <summary>
/// 归一化坐标点（0..1，相对父格）。
/// </summary>
/// <param name="X">X（0=左，1=右）。</param>
/// <param name="Y">Y（0=上，1=下）。</param>
public readonly record struct NormalizedPoint(double X, double Y);
