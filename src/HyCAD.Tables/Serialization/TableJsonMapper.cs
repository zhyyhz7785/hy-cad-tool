using System.Globalization;
using HyCAD.Tables.Data;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;

namespace HyCAD.Tables.Serialization;

internal static class TableJsonMapper
{
    public static string FormatAddr(CellAddr addr) =>
        string.Format(CultureInfo.InvariantCulture, "{0},{1}", addr.Row, addr.Col);

    public static CellAddr ParseAddr(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new FormatException($"CellAddr 键不能为空。");

        var parts = key.Split(',');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var col))
        {
            throw new FormatException($"CellAddr 键格式无效：\"{key}\"，期望 \"r,c\"。");
        }

        return new CellAddr(row, col);
    }

    public static TableDocumentDto ToDto(TableDocument document) =>
        new()
        {
            Id = document.Id,
            Name = document.Name,
            Tables = document.Tables.Select(ToDto).ToList(),
            Source = ToDto(document.Source),
            Metadata = ToMetadataDto(document.Metadata)
        };

    public static TableDocument FromDto(TableDocumentDto dto) =>
        new()
        {
            Id = dto.Id,
            Name = dto.Name ?? string.Empty,
            Tables = (dto.Tables ?? new List<TableGridDto>()).Select(FromDto).ToList(),
            Source = FromDto(dto.Source),
            Metadata = FromMetadataDto(dto.Metadata)
        };

    public static TableGridDto ToDto(TableGrid grid) =>
        new()
        {
            Id = grid.Id,
            Structure = ToDto(grid.Structure),
            Data = ToDto(grid.Structure, grid.Data),
            Metadata = ToMetadataDto(grid.Metadata)
        };

    public static TableGrid FromDto(TableGridDto dto) =>
        new()
        {
            Id = dto.Id,
            Structure = FromDto(dto.Structure),
            Data = FromDto(dto.Data),
            Metadata = FromMetadataDto(dto.Metadata)
        };

    public static TableGrid RebuildMirrors(TableGrid grid)
    {
        var result = grid;
        foreach (var merge in grid.Structure.Merges)
        {
            if (merge.ValuePolicy != MergeValuePolicy.SameValue)
                continue;

            if (!result.Data.Cells.TryGetValue(merge.Anchor, out var anchorValue))
                continue;

            result = GridEditor.SetValue(result, merge.Anchor, anchorValue);
        }

        return result;
    }

    private static TableSourceInfoDto ToDto(TableSourceInfo source) =>
        new()
        {
            Kind = source.Kind,
            Location = source.Location
        };

    private static TableSourceInfo FromDto(TableSourceInfoDto? dto) =>
        dto == null
            ? new TableSourceInfo(TableSourceKind.Unknown)
            : new TableSourceInfo(dto.Kind, dto.Location);

    private static GridStructureDto ToDto(GridStructure structure) =>
        new()
        {
            Topology = ToDto(structure.Topology),
            Merges = structure.Merges.Select(ToDto).ToList(),
            Diagonals = ToAddrDictionary(structure.Diagonals, ToDto),
            Styles = ToAddrDictionary(structure.Styles, ToDto),
            Roles = ToAddrDictionary(structure.Roles, role => role),
            FieldIndex = ToFieldIndexDto(structure.FieldIndex)
        };

    private static GridStructure FromDto(GridStructureDto dto)
    {
        var topology = FromDto(dto.Topology);
        return new GridStructure(
            topology,
            (dto.Merges ?? new List<MergeRegionDto>()).Select(FromDto).ToList(),
            FromAddrDictionary(dto.Diagonals, FromDto),
            FromAddrDictionary(dto.Styles, FromDto),
            FromAddrDictionary(dto.Roles, role => role),
            FromFieldIndexDto(dto.FieldIndex));
    }

    private static GridTopologyDto ToDto(GridTopology topology) =>
        new()
        {
            Rows = topology.Rows.Select(ToDto).ToList(),
            Cols = topology.Cols.Select(ToDto).ToList(),
            Direction = topology.Direction,
            DefaultBorder = ToDto(topology.DefaultBorder ?? BorderSet.None)
        };

    private static GridTopology FromDto(GridTopologyDto dto) =>
        new(
            (dto.Rows ?? new List<GridTrackDto>()).Select(FromDto).ToList(),
            (dto.Cols ?? new List<GridTrackDto>()).Select(FromDto).ToList(),
            dto.Direction,
            FromDto(dto.DefaultBorder));

    private static GridTrackDto ToDto(GridTrack track) =>
        new()
        {
            Size = track.Size,
            Mode = track.Mode
        };

    private static GridTrack FromDto(GridTrackDto dto) =>
        new(dto.Size, dto.Mode);

    private static MergeRegionDto ToDto(MergeRegion merge) =>
        new()
        {
            TopLeft = FormatAddr(merge.TopLeft),
            RowSpan = merge.RowSpan,
            ColSpan = merge.ColSpan,
            ValuePolicy = merge.ValuePolicy
        };

    private static MergeRegion FromDto(MergeRegionDto dto) =>
        new(
            ParseAddr(dto.TopLeft),
            dto.RowSpan,
            dto.ColSpan,
            dto.ValuePolicy);

    private static DiagonalSplitDto ToDto(DiagonalSplit split) =>
        new()
        {
            Direction = split.Direction,
            Parts = split.Parts.Select(ToDto).ToList()
        };

    private static DiagonalSplit FromDto(DiagonalSplitDto dto) =>
        new(
            dto.Direction,
            (dto.Parts ?? new List<SubCellDto>()).Select(FromDto).ToList());

    private static SubCellDto ToDto(SubCell subCell) =>
        new()
        {
            Value = ToDto(subCell.Value),
            FieldKey = subCell.FieldKey,
            TextAnchor = ToDto(subCell.TextAnchor),
            Align = subCell.Align
        };

    private static SubCell FromDto(SubCellDto dto) =>
        new(
            FromDto(dto.Value),
            dto.FieldKey,
            FromDto(dto.TextAnchor),
            dto.Align);

    private static CellStyleDto ToDto(CellStyle style) =>
        new()
        {
            Orientation = style.Orientation,
            HAlign = style.HAlign,
            VAlign = style.VAlign,
            TextHeight = style.TextHeight,
            FontKey = style.FontKey,
            Borders = style.Borders == null ? null : ToDto(style.Borders),
            BackColor = style.BackColor
        };

    private static CellStyle FromDto(CellStyleDto dto) =>
        new(
            dto.Orientation,
            dto.HAlign,
            dto.VAlign,
            dto.TextHeight,
            dto.FontKey ?? "default",
            dto.Borders == null ? null : FromDto(dto.Borders),
            dto.BackColor);

    private static BorderSetDto ToDto(BorderSet borders) =>
        new()
        {
            Top = borders.Top,
            Right = borders.Right,
            Bottom = borders.Bottom,
            Left = borders.Left
        };

    private static BorderSet FromDto(BorderSetDto? dto) =>
        dto == null ? BorderSet.None : new BorderSet(dto.Top, dto.Right, dto.Bottom, dto.Left);

    private static CellValueDto ToDto(CellValue value) =>
        new()
        {
            Text = value.Text,
            Kind = value.Kind,
            Formula = value.Formula,
            BindingExpr = value.BindingExpr,
            Runs = value.Runs?.Select(ToDto).ToList()
        };

    private static CellValue FromDto(CellValueDto dto) =>
        new(
            dto.Text ?? string.Empty,
            dto.Kind,
            dto.Formula,
            dto.BindingExpr,
            dto.Runs?.Select(FromDto).ToList());

    private static TextRunDto ToDto(TextRun run) =>
        new()
        {
            Text = run.Text,
            FontKey = run.FontKey,
            TextHeight = run.TextHeight,
            Bold = run.Bold
        };

    private static TextRun FromDto(TextRunDto dto) =>
        new(
            dto.Text ?? string.Empty,
            dto.FontKey,
            dto.TextHeight,
            dto.Bold);

    private static NormalizedPointDto ToDto(NormalizedPoint point) =>
        new()
        {
            X = point.X,
            Y = point.Y
        };

    private static NormalizedPoint FromDto(NormalizedPointDto dto) =>
        new(dto.X, dto.Y);

    private static GridDataDto ToDto(GridStructure structure, GridData data)
    {
        if (data.Cells.Count == 0)
            return new GridDataDto();

        var cells = new Dictionary<string, CellValueDto>();
        foreach (var entry in data.Cells)
        {
            if (structure.IsHidden(entry.Key))
                continue;

            cells[FormatAddr(entry.Key)] = ToDto(entry.Value);
        }

        return new GridDataDto { Cells = cells.Count == 0 ? null : cells };
    }

    private static GridData FromDto(GridDataDto? dto)
    {
        if (dto?.Cells == null || dto.Cells.Count == 0)
            return GridData.Empty;

        var cells = new Dictionary<CellAddr, CellValue>();
        foreach (var entry in dto.Cells)
            cells[ParseAddr(entry.Key)] = FromDto(entry.Value);

        return new GridData(cells);
    }

    private static Dictionary<string, string>? ToFieldIndexDto(
        IReadOnlyDictionary<string, CellAddr> fieldIndex)
    {
        if (fieldIndex.Count == 0)
            return null;

        var dto = new Dictionary<string, string>(fieldIndex.Count, StringComparer.Ordinal);
        foreach (var entry in fieldIndex)
            dto[entry.Key] = FormatAddr(entry.Value);
        return dto;
    }

    private static Dictionary<string, CellAddr> FromFieldIndexDto(
        Dictionary<string, string>? dto)
    {
        if (dto == null || dto.Count == 0)
            return new Dictionary<string, CellAddr>(StringComparer.Ordinal);

        var fieldIndex = new Dictionary<string, CellAddr>(dto.Count, StringComparer.Ordinal);
        foreach (var entry in dto)
            fieldIndex[entry.Key] = ParseAddr(entry.Value);
        return fieldIndex;
    }

    private static Dictionary<string, TDto>? ToAddrDictionary<TValue, TDto>(
        IReadOnlyDictionary<CellAddr, TValue> source,
        Func<TValue, TDto> map)
    {
        if (source.Count == 0)
            return null;

        var dto = new Dictionary<string, TDto>(source.Count);
        foreach (var entry in source)
            dto[FormatAddr(entry.Key)] = map(entry.Value);
        return dto;
    }

    private static Dictionary<CellAddr, TValue> FromAddrDictionary<TDto, TValue>(
        Dictionary<string, TDto>? source,
        Func<TDto, TValue> map)
    {
        if (source == null || source.Count == 0)
            return new Dictionary<CellAddr, TValue>();

        var result = new Dictionary<CellAddr, TValue>(source.Count);
        foreach (var entry in source)
            result[ParseAddr(entry.Key)] = map(entry.Value);
        return result;
    }

    private static Dictionary<string, string>? ToMetadataDto(
        IReadOnlyDictionary<string, string> metadata)
    {
        if (metadata.Count == 0)
            return null;

        var dto = new Dictionary<string, string>(metadata.Count, StringComparer.Ordinal);
        foreach (var entry in metadata)
            dto[entry.Key] = entry.Value;
        return dto;
    }

    private static IReadOnlyDictionary<string, string> FromMetadataDto(
        Dictionary<string, string>? metadata)
    {
        if (metadata == null || metadata.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        return new Dictionary<string, string>(metadata, StringComparer.Ordinal);
    }
}
