using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Configuration.Modules
{
    /// <summary>
    /// 钢筋配置值对象
    /// 用于钢筋绘制和标注的相关参数
    /// </summary>
    public class ReinforcementConfiguration
    {
        /// <summary>
        /// 默认钢筋直径（mm）
        /// </summary>
        public double DefaultDiameter { get; set; }

        /// <summary>
        /// 默认钢筋间距（mm）
        /// </summary>
        public double DefaultSpacing { get; set; }

        /// <summary>
        /// 锚固长度（mm）
        /// </summary>
        public double AnchorageLength { get; set; }

        /// <summary>
        /// 混凝土保护层厚度（mm）
        /// </summary>
        public double ConcreteCover { get; set; }

        /// <summary>
        /// 搭接长度系数
        /// </summary>
        public double LapLengthFactor { get; set; }

        /// <summary>
        /// 钢筋弯钩长度（mm）
        /// </summary>
        public double HookLength { get; set; }

        /// <summary>
        /// 最小钢筋间距（mm）
        /// </summary>
        public double MinSpacing { get; set; }

        /// <summary>
        /// 最大钢筋间距（mm）
        /// </summary>
        public double MaxSpacing { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ReinforcementConfiguration()
        {
            DefaultDiameter = 12.0;
            DefaultSpacing = 200.0;
            AnchorageLength = 300.0;
            ConcreteCover = 50.0;
            LapLengthFactor = 1.4;
            HookLength = 150.0;
            MinSpacing = 70.0;
            MaxSpacing = 300.0;
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid(out string error)
        {
            if (DefaultDiameter <= 0)
            {
                error = "钢筋直径必须大于 0";
                return false;
            }

            if (DefaultSpacing <= 0)
            {
                error = "钢筋间距必须大于 0";
                return false;
            }

            if (AnchorageLength <= 0)
            {
                error = "锚固长度必须大于 0";
                return false;
            }

            if (ConcreteCover < 0)
            {
                error = "混凝土保护层厚度不能为负";
                return false;
            }

            if (MinSpacing > MaxSpacing)
            {
                error = "最小间距不能大于最大间距";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static ReinforcementConfiguration CreateDefault()
        {
            return new ReinforcementConfiguration
            {
                DefaultDiameter = 12.0,
                DefaultSpacing = 200.0,
                AnchorageLength = 300.0,
                ConcreteCover = 50.0,
                LapLengthFactor = 1.4,
                HookLength = 150.0,
                MinSpacing = 70.0,
                MaxSpacing = 300.0
            };
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public ReinforcementConfiguration Clone()
        {
            return new ReinforcementConfiguration
            {
                DefaultDiameter = this.DefaultDiameter,
                DefaultSpacing = this.DefaultSpacing,
                AnchorageLength = this.AnchorageLength,
                ConcreteCover = this.ConcreteCover,
                LapLengthFactor = this.LapLengthFactor,
                HookLength = this.HookLength,
                MinSpacing = this.MinSpacing,
                MaxSpacing = this.MaxSpacing
            };
        }

        public override string ToString()
        {
            return $"ReinforcementConfiguration[Diameter={DefaultDiameter}mm, Spacing={DefaultSpacing}mm]";
        }
    }
}

