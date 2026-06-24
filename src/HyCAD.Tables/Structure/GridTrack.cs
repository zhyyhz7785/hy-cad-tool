namespace HyCAD.Tables.Structure;

/// <summary>
/// 网格轨道（行高或列宽）。
/// </summary>
/// <param name="Size">尺寸（mm）。</param>
/// <param name="Mode">尺寸模式。</param>
public readonly record struct GridTrack(double Size, TrackSizeMode Mode = TrackSizeMode.Fixed);
