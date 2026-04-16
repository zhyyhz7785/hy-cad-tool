namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// <c>.roaddesign.json</c> 数据 schema 版本常量。
    ///
    /// 版本演进策略（对应 04Pipeline_CAD_Blender_Lumion.md）：
    /// - v1 (1.0): AutoCAD 出图 / Domain 骨架 / 事件总线；仅序列化 Alignment / Profile / Template / Corridor 基础字段。
    /// - v2 (1.1): Blender 单向同步；新增 <c>visualHints</c>（颜色、材质 key）、Corridor Mesh 描述。
    /// - v3 (2.0): 双向实时同步；新增 JSON Patch、命名管道协议、Lumion LiveSync。
    /// - v∞ (3.0): Blender-first；剥离 AutoCAD 专有字段（如 Xdata 引用）。
    ///
    /// JSON 读写时需校验 <see cref="Current"/>。读入更低 / 未知版本时允许兼容或拒绝。
    /// </summary>
    public static class SchemaVersion
    {
        public const string Current = "1.0";

        /// <summary>v1 最低兼容版本（读取时支持）。</summary>
        public const string MinimumSupported = "1.0";
    }
}
