using HyCAD.Tables.Layout;

namespace HyCAD.Tables.Inference;

/// <summary>文字包围盒与内容（AC9 采集 DTO）。</summary>
public readonly record struct TextBox2d(LayoutRect Bounds, string Content, double HeightMm);
