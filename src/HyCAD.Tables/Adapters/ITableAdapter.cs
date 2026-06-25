namespace HyCAD.Tables.Adapters;

/// <summary>
/// 表格端口-适配器（005 §6.1）：外部格式 ↔ Domain <see cref="TableGrid"/>。
/// </summary>
/// <typeparam name="TExternal">外部载体类型（如 HTML 字符串、AutoCAD 实体组等）。</typeparam>
public interface ITableAdapter<TExternal>
{
    /// <summary>外部 → Domain。</summary>
    TableGrid Import(TExternal source);

    /// <summary>Domain → 外部。</summary>
    TExternal Export(TableGrid model);

    /// <summary>该端支持哪些特性。</summary>
    AdapterCapability Capability { get; }
}
