namespace HyCADTool.Refactored.Domain.ValueObjects.Configuration.Global
{
    /// <summary>
    /// 容差配置值对象
    /// 用于几何计算中的精度控制
    /// </summary>
    public class ToleranceConfig
    {
        /// <summary>
        /// 双精度浮点数容差
        /// </summary>
        public double Double { get; set; }

        /// <summary>
        /// 向量容差
        /// </summary>
        public double Vector { get; set; }

        /// <summary>
        /// 点容差
        /// </summary>
        public double Point { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ToleranceConfig()
        {
            Double = 1e-2;
            Vector = 1e-2;
            Point = 1e-2;
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid(out string error)
        {
            if (Double <= 0 || Vector <= 0 || Point <= 0)
            {
                error = "所有容差值必须大于 0";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static ToleranceConfig CreateDefault()
        {
            return new ToleranceConfig
            {
                Double = 1e-2,
                Vector = 1e-2,
                Point = 1e-2
            };
        }

        public override string ToString()
        {
            return $"ToleranceConfig[Double={Double:E2}, Vector={Vector:E2}, Point={Point:E2}]";
        }
    }
}

