namespace HyCAD.Tables.Adapters;

/// <summary>
/// 适配器对某特性的支持级别（对应 005 §6.2 能力矩阵）。
/// </summary>
public enum AdapterFeatureLevel
{
    /// <summary>完整支持。</summary>
    Full,

    /// <summary>降级支持（尽力渲染，可能丢语义）。</summary>
    Degraded,

    /// <summary>不支持。</summary>
    NotSupported
}
