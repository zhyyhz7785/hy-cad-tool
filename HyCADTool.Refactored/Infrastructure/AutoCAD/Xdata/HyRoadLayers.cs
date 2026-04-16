namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata
{
    /// <summary>
    /// 道路模块使用的 AutoCAD 标准图层名 + 颜色索引常量。
    ///
    /// 命名规范：<c>05_hy_道路_子模块</c>（紧邻既有 <c>04_hy_*</c> 前缀后面）。
    /// - <see cref="AlignmentLayer"/>：P1 平面线位中心线（从 JSON 反向绘制时使用）。
    /// - 其他预留：P2 纵断面 / P5 走廊 / P3 标线。命令层 v1 暂不强制使用，面向 v2 扩展。
    ///
    /// 配合 <c>PluginInitializer.GetRequiredLayers()</c> 在插件启动时创建。
    /// </summary>
    public static class HyRoadLayers
    {
        public const string AlignmentLayer = "05_hy_道路_平面线位";
        public const short AlignmentColor = 6; // 品红：视觉上与既有道路人行横道图层区分

        public const string ProfileLayer = "05_hy_道路_纵断面";
        public const short ProfileColor = 2; // 黄：P2 预留

        public const string CorridorLayer = "05_hy_道路_走廊";
        public const short CorridorColor = 8; // 浅灰：P5 预留

        public const string MarkingLayer = "05_hy_道路_标线";
        public const short MarkingColor = 3; // 绿：P3 预留

        /// <summary>
        /// 返回本模块需要注册的所有图层（(name, color) 对）。
        /// </summary>
        public static (string layerName, short colorIndex)[] GetAll()
        {
            return new[]
            {
                (AlignmentLayer, AlignmentColor),
                (ProfileLayer, ProfileColor),
                (CorridorLayer, CorridorColor),
                (MarkingLayer, MarkingColor),
            };
        }
    }
}
