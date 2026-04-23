using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.ValueObjects.Configuration.User
{
    /// <summary>
    /// 持久化在 <c>hy-settings.json</c> 中的用户图层表（版本 + 条目列表）。
    /// </summary>
    public sealed class UserLayerSettings
    {
        /// <summary>结构版本，用于迁移。</summary>
        public int Version { get; set; } = 1;

        public List<LayerDefinitionItem> Items { get; set; } = new List<LayerDefinitionItem>();
    }
}
