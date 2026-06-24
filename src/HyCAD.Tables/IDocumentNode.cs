namespace HyCAD.Tables;

/// <summary>
/// UDOM 文档节点接缝（Unified Document Object Model Document Node Seam）。
/// 本期仅 Table 落地，其余 NodeKind 预留。
/// </summary>
public interface IDocumentNode
{
    /// <summary>节点唯一标识。</summary>
    Guid Id { get; }

    /// <summary>节点种类。</summary>
    NodeKind Kind { get; }

    /// <summary>扩展元数据键值对。</summary>
    IReadOnlyDictionary<string, string> Metadata { get; }
}
