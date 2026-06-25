using HyCAD.Tables.Layout;

namespace HyCAD.Tables.Inference;

/// <summary>线框识别输入（平台无关）。</summary>
public sealed class TableInferInput
{
    public IReadOnlyList<LineSegment2d> Segments { get; set; } = Array.Empty<LineSegment2d>();

    public IReadOnlyList<TextBox2d> Texts { get; set; } = Array.Empty<TextBox2d>();

    public LayoutRect? ClipBounds { get; set; }

    public TableInferInput()
    {
    }

    public TableInferInput(
        IReadOnlyList<LineSegment2d> segments,
        IReadOnlyList<TextBox2d> texts,
        LayoutRect? clipBounds = null)
    {
        Segments = segments;
        Texts = texts;
        ClipBounds = clipBounds;
    }
}
