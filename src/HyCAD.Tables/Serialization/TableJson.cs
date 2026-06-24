using System.Text.Json;
using System.Text.Json.Serialization;

namespace HyCAD.Tables.Serialization;

/// <summary>
/// 表格 JSON 序列化（Table JSON）：稀疏字典、CellAddr 用 "r,c" 键、D1 只存 Anchor。
/// </summary>
public static class TableJson
{
    /// <summary>序列化文档。</summary>
    public static string Serialize(TableDocument document, bool indented = false)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        var dto = TableJsonMapper.ToDto(document);
        return JsonSerializer.Serialize(dto, CreateOptions(indented));
    }

    /// <summary>反序列化文档（加载后重建 SameValue 镜像）。</summary>
    public static TableDocument Deserialize(string json)
    {
        if (json == null)
            throw new ArgumentNullException(nameof(json));

        var dto = JsonSerializer.Deserialize<TableDocumentDto>(json, CreateOptions(false))
            ?? throw new JsonException("JSON 反序列化结果为 null。");

        var document = TableJsonMapper.FromDto(dto);
        var tables = new TableGrid[document.Tables.Count];
        for (var i = 0; i < document.Tables.Count; i++)
            tables[i] = TableJsonMapper.RebuildMirrors(document.Tables[i]);

        return document with { Tables = tables };
    }

    /// <summary>序列化单张表。</summary>
    public static string SerializeGrid(TableGrid grid, bool indented = false)
    {
        if (grid == null)
            throw new ArgumentNullException(nameof(grid));

        var dto = TableJsonMapper.ToDto(grid);
        return JsonSerializer.Serialize(dto, CreateOptions(indented));
    }

    /// <summary>反序列化单张表（加载后重建 SameValue 镜像）。</summary>
    public static TableGrid DeserializeGrid(string json)
    {
        if (json == null)
            throw new ArgumentNullException(nameof(json));

        var dto = JsonSerializer.Deserialize<TableGridDto>(json, CreateOptions(false))
            ?? throw new JsonException("JSON 反序列化结果为 null。");

        return TableJsonMapper.RebuildMirrors(TableJsonMapper.FromDto(dto));
    }

    private static JsonSerializerOptions CreateOptions(bool indented)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
