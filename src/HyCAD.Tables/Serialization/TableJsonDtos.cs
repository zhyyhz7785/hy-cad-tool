using HyCAD.Tables.Data;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Serialization;

internal sealed class TableDocumentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<TableGridDto> Tables { get; set; } = new();
    public TableSourceInfoDto Source { get; set; } = new();
    public Dictionary<string, string>? Metadata { get; set; }
}

internal sealed class TableSourceInfoDto
{
    public TableSourceKind Kind { get; set; } = TableSourceKind.Unknown;
    public string? Location { get; set; }
}

internal sealed class TableGridDto
{
    public Guid Id { get; set; }
    public GridStructureDto Structure { get; set; } = new();
    public GridDataDto Data { get; set; } = new();
    public Dictionary<string, string>? Metadata { get; set; }
}

internal sealed class GridStructureDto
{
    public GridTopologyDto Topology { get; set; } = new();
    public List<MergeRegionDto> Merges { get; set; } = new();
    public Dictionary<string, DiagonalSplitDto>? Diagonals { get; set; }
    public Dictionary<string, CellStyleDto>? Styles { get; set; }
    public Dictionary<string, CellRole>? Roles { get; set; }
    public Dictionary<string, string>? FieldIndex { get; set; }
}

internal sealed class GridTopologyDto
{
    public List<GridTrackDto> Rows { get; set; } = new();
    public List<GridTrackDto> Cols { get; set; } = new();
    public GrowDirection Direction { get; set; } = GrowDirection.Down;
    public BorderSetDto? DefaultBorder { get; set; }
}

internal sealed class GridTrackDto
{
    public double Size { get; set; }
    public TrackSizeMode Mode { get; set; } = TrackSizeMode.Fixed;
}

internal sealed class MergeRegionDto
{
    public string TopLeft { get; set; } = "0,0";
    public int RowSpan { get; set; }
    public int ColSpan { get; set; }
    public MergeValuePolicy ValuePolicy { get; set; } = MergeValuePolicy.AnchorOnly;
}

internal sealed class DiagonalSplitDto
{
    public DiagonalDirection Direction { get; set; }
    public List<SubCellDto> Parts { get; set; } = new();
}

internal sealed class SubCellDto
{
    public CellValueDto Value { get; set; } = new();
    public string? FieldKey { get; set; }
    public NormalizedPointDto TextAnchor { get; set; } = new();
    public TextAlign Align { get; set; } = TextAlign.Center;
}

internal sealed class CellStyleDto
{
    public TextOrientation Orientation { get; set; } = TextOrientation.Horizontal;
    public TextAlign HAlign { get; set; } = TextAlign.Start;
    public TextAlign VAlign { get; set; } = TextAlign.Center;
    public double TextHeight { get; set; } = 3.5;
    public string FontKey { get; set; } = "default";
    public BorderSetDto? Borders { get; set; }
    public string? BackColor { get; set; }
}

internal sealed class BorderSetDto
{
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
    public double Left { get; set; }
}

internal sealed class CellValueDto
{
    public string Text { get; set; } = string.Empty;
    public CellValueKind Kind { get; set; } = CellValueKind.Static;
    public string? Formula { get; set; }
    public string? BindingExpr { get; set; }
    public List<TextRunDto>? Runs { get; set; }
}

internal sealed class TextRunDto
{
    public string Text { get; set; } = string.Empty;
    public string? FontKey { get; set; }
    public double? TextHeight { get; set; }
    public bool Bold { get; set; }
}

internal sealed class GridDataDto
{
    public Dictionary<string, CellValueDto>? Cells { get; set; }
}

internal sealed class NormalizedPointDto
{
    public double X { get; set; }
    public double Y { get; set; }
}
