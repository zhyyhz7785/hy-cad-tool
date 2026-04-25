using System;

namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    /// <summary>
    /// 比例配置值对象
    /// </summary>
    public class ScaleConfig
    {
        /// <summary>
        /// 默认比例值
        /// </summary>
        public double Default { get; set; }

        /// <summary>
        /// 最小允许比例
        /// </summary>
        public double MinValue { get; set; }

        /// <summary>
        /// 最大允许比例
        /// </summary>
        public double MaxValue { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ScaleConfig()
        {
            Default = 40.0;
            MinValue = 1.0;
            MaxValue = 200.0;
        }

        /// <summary>
        /// 验证比例是否有效
        /// </summary>
        public bool IsValid(out string error)
        {
            if (Default <= 0)
            {
                error = "默认比例必须大于 0";
                return false;
            }

            if (Default < MinValue || Default > MaxValue)
            {
                error = $"默认比例必须在 {MinValue} 到 {MaxValue} 之间";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static ScaleConfig CreateDefault()
        {
            return new ScaleConfig
            {
                Default = 40.0,
                MinValue = 1.0,
                MaxValue = 200.0
            };
        }

        public override string ToString()
        {
            return $"ScaleConfig[Default={Default}, Range={MinValue}-{MaxValue}]";
        }
    }
}

