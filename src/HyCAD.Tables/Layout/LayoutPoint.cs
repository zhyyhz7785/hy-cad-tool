namespace HyCAD.Tables.Layout;

/// <summary>
/// 布局坐标点（模型空间 mm，1:1）。
/// </summary>
/// <param name="X">X 坐标（mm）。</param>
/// <param name="Y">Y 坐标（mm）。</param>
public readonly record struct LayoutPoint(double X, double Y);
