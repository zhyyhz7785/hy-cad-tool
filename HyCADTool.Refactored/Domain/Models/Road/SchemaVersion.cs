using System;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// <c>.roaddesign.json</c> / <c>.roadproject.json</c> 数据 schema 版本常量。
    ///
    /// 版本演进策略（对应 04Pipeline_CAD_Blender_Lumion.md + 045 项目树设计）：
    /// - v1 (1.0): AutoCAD 出图 / Domain 骨架 / 事件总线；仅序列化 Alignment / Profile / Template / Corridor 基础字段。
    /// - v1.1: CrossSectionBand 扩展（内部过渡）。
    /// - v1.2: M6 引入 Controls / StructureLayerSchemes / IntersectionBandMergeRules。
    /// - v2.0 (本次 · 045 M2): 引入 <see cref="RoadProject"/> 根聚合 + Civil 系列占位集合
    ///   （Surfaces / Bridges / Tunnels / Culverts / Interchanges / Landscapes / ExternalTrafficFacilities / Geologies）；
    ///   Data Shortcut 值对象；字段向下兼容（Newtonsoft 缺省值为空 List）。
    /// - v3 (3.0+): Blender-first；剥离 AutoCAD 专有字段；双向实时同步。
    ///
    /// JSON 读写时需校验 <see cref="Current"/> / <see cref="CurrentProject"/>。读入更低 / 未知版本时允许兼容或拒绝。
    /// </summary>
    public static class SchemaVersion
    {
        /// <summary>
        /// 当前写入 <c>.roaddesign.json</c> 的 schema 版本（v2.0）。
        /// <para>2.0（045 / M2）：在 1.2 基础上新增 Civil 占位集合。字段均可选，Newtonsoft 读取 1.0 / 1.1 / 1.2 文件
        /// 时缺失字段会自动填空 List，故老文件无需迁移即可读入；写回时自动升级为 2.0。</para>
        /// <para>1.2（M6 旧版）：新增 Controls / StructureLayerSchemes / IntersectionBandMergeRules。</para>
        /// <para>1.0：v1 初版（Alignment / Profile / Template / Corridor 基础字段）。</para>
        /// </summary>
        public const string Current = "2.0";

        /// <summary>
        /// 当前写入 <c>.roadproject.json</c> 的 schema 版本。始终与 <see cref="Current"/> 同步。
        /// </summary>
        public const string CurrentProject = "2.0";

        /// <summary>v1 最低兼容版本（读取时支持）。</summary>
        public const string MinimumSupported = "1.0";

        /// <summary>
        /// 判断给定的 schema 字符串是否属于 v1.x（需要迁移到 v2.0 包装）。
        /// 空 / null 当作 v1.0 处理（兼容没写 Schema 字段的早期文件）。
        /// </summary>
        public static bool IsLegacyV1(string schema)
        {
            if (string.IsNullOrWhiteSpace(schema)) return true;
            var trimmed = schema.Trim();
            // 仅关心大版本号；"1" / "1.0" / "1.1" / "1.2" 均视为 legacy
            return trimmed.StartsWith("1.", StringComparison.Ordinal) || trimmed == "1";
        }
    }
}
