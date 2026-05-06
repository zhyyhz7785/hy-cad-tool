namespace HyCADTool.Features.DataExchange.Hyob.Domain.Schemas
{
    /// <summary>
    /// hyob Object 头里的 type_id（2 bytes，little-endian）。
    /// 设计：02 §3.2 / 03 §3.1（白名单 + 黑盒兜底）/ 04 §5。
    ///
    /// 0x0000 ~ 0x0FFF 保留给 TypedObject（M2+ 起逐步实现）。
    /// 0x0F00 ~ 0x0FFF 保留给 Repository 自身对象（Tree / Commit）。
    /// 0xFFFF 固定为 OpaqueObject —— M1 黑盒兜底，所有未识别类型都走这条。
    /// </summary>
    public enum HyobObjectKind : ushort
    {
        Unknown = 0x0000,

        // === Domain TypedObject（M2+ 起逐步实现） ===
        Line               = 0x0001,
        Polyline           = 0x0002,
        Arc                = 0x0003,
        Circle             = 0x0004,
        DBText             = 0x0005,
        MText              = 0x0006,
        BlockReference     = 0x0007,
        AttributeReference = 0x0008,
        Dimension          = 0x0009,
        MLeader            = 0x000A,
        HatchBoundary      = 0x000B,
        Wipeout            = 0x000C,

        // === 表 / 字典（M3+） ===
        LayerDef            = 0x0100,
        LinetypeDef         = 0x0101,
        TextStyleDef        = 0x0102,
        DimStyleDef         = 0x0103,
        BlockDef            = 0x0104,
        HeaderVars          = 0x0110,
        XDataAttachment     = 0x0120,
        ExtensionDictionary = 0x0121,

        // === 共享 token（M3+） ===
        StringToken = 0x0200,

        // === Repository 内部对象 ===
        Tree   = 0x0F01,
        Commit = 0x0F02,

        // === M1 黑盒兜底（首期一切都走这条） ===
        Opaque = 0xFFFF,
    }
}
