using System;

namespace HyCADTool.Domain.ValueObjects.Configuration.Modules
{
    /// <summary>
    /// 基础配置值对象
    /// 用于基础设计和绘制的相关参数
    /// </summary>
    public class FoundationConfiguration
    {
        /// <summary>
        /// 基础类型（独立基础、条形基础、筏板基础等）
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 默认基础厚度（mm）
        /// </summary>
        public double DefaultThickness { get; set; }

        /// <summary>
        /// 最小混凝土保护层厚度（mm）
        /// </summary>
        public double MinConcreteCover { get; set; }

        /// <summary>
        /// 默认基础埋深（mm）
        /// </summary>
        public double DefaultDepth { get; set; }

        /// <summary>
        /// 基础底标高（m）
        /// </summary>
        public double BottomElevation { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public FoundationConfiguration()
        {
            Type = "Isolated";
            DefaultThickness = 500.0;
            MinConcreteCover = 50.0;
            DefaultDepth = 1500.0;
            BottomElevation = -1.5;
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid(out string error)
        {
            if (DefaultThickness <= 0)
            {
                error = "基础厚度必须大于 0";
                return false;
            }

            if (MinConcreteCover < 0)
            {
                error = "混凝土保护层厚度不能为负";
                return false;
            }

            if (DefaultDepth <= 0)
            {
                error = "基础埋深必须大于 0";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static FoundationConfiguration CreateDefault()
        {
            return new FoundationConfiguration
            {
                Type = "Isolated",
                DefaultThickness = 500.0,
                MinConcreteCover = 50.0,
                DefaultDepth = 1500.0,
                BottomElevation = -1.5
            };
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public FoundationConfiguration Clone()
        {
            return new FoundationConfiguration
            {
                Type = this.Type,
                DefaultThickness = this.DefaultThickness,
                MinConcreteCover = this.MinConcreteCover,
                DefaultDepth = this.DefaultDepth,
                BottomElevation = this.BottomElevation
            };
        }

        public override string ToString()
        {
            return $"FoundationConfiguration[Type={Type}, Thickness={DefaultThickness}mm]";
        }
    }
}

