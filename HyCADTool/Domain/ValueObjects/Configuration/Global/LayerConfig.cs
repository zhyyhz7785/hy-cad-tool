namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    /// <summary>
    /// 图层配置（平台无关）
    /// </summary>
    public class LayerConfig
    {
        /// <summary>
        /// 图层名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 颜色索引（AutoCAD ACI: 1-255）
        /// </summary>
        public short ColorIndex { get; set; }

        /// <summary>
        /// 线型名称（如 "Continuous", "Dashed"）
        /// </summary>
        public string LineTypeName { get; set; }

        /// <summary>
        /// 线宽（单位：mm）
        /// </summary>
        public double LineWeight { get; set; }

        /// <summary>
        /// 是否可打印
        /// </summary>
        public bool IsPlottable { get; set; }

        /// <summary>
        /// 是否锁定
        /// </summary>
        public bool IsLocked { get; set; }

        /// <summary>
        /// 描述信息
        /// </summary>
        public string Description { get; set; }

        public LayerConfig()
        {
            Name = "0";
            ColorIndex = 7; // 白色/黑色（AutoCAD 默认）
            LineTypeName = "Continuous";
            LineWeight = 0.0;
            IsPlottable = true;
            IsLocked = false;
            Description = string.Empty;
        }

        /// <summary>
        /// 创建默认图层配置
        /// </summary>
        public static LayerConfig CreateDefault(string name, short colorIndex = 7)
        {
            return new LayerConfig
            {
                Name = name,
                ColorIndex = colorIndex,
                LineTypeName = "Continuous",
                LineWeight = 0.0,
                IsPlottable = true,
                IsLocked = false
            };
        }

        public override string ToString()
        {
            return $"LayerConfig[{Name}, Color={ColorIndex}]";
        }
    }
}

