namespace HyCAD.Tables.Data;

/// <summary>
/// 富文本片段（Text Run）。
/// </summary>
/// <param name="Text">文本内容。</param>
/// <param name="FontKey">抽象字体键，可空。</param>
/// <param name="TextHeight">字高（mm），可空。</param>
/// <param name="Bold">是否粗体。</param>
public sealed record TextRun(
    string Text,
    string? FontKey = null,
    double? TextHeight = null,
    bool Bold = false);
