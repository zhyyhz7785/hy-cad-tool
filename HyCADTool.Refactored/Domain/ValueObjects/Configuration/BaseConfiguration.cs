using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Configuration
{
    /// <summary>
    /// 基础配置值对象
    /// 对应旧代码中的 BaseConfig
    /// </summary>
    public class BaseConfiguration
    {
        /// <summary>
        /// 比例尺
        /// </summary>
        public double Scale { get; }

        /// <summary>
        /// 标高长度
        /// </summary>
        public double ElevationLength { get; }

        /// <summary>
        /// 容差值（用于浮点数比较）
        /// </summary>
        public double ToleranceDouble { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public BaseConfiguration(double scale, double elevationLength, double tolerance = 1e-2)
        {
            if (scale <= 0)
                throw new ArgumentException("比例尺必须大于 0", nameof(scale));
            
            if (elevationLength <= 0)
                throw new ArgumentException("标高长度必须大于 0", nameof(elevationLength));
            
            if (tolerance <= 0)
                throw new ArgumentException("容差值必须大于 0", nameof(tolerance));

            Scale = scale;
            ElevationLength = elevationLength;
            ToleranceDouble = tolerance;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static BaseConfiguration CreateDefault()
        {
            return new BaseConfiguration(
                scale: 50.0,
                elevationLength: 2.0,
                tolerance: 1e-2
            );
        }

        /// <summary>
        /// 使用新的比例尺创建新配置
        /// </summary>
        public BaseConfiguration WithScale(double newScale)
        {
            return new BaseConfiguration(newScale, ElevationLength, ToleranceDouble);
        }

        /// <summary>
        /// 使用新的标高长度创建新配置
        /// </summary>
        public BaseConfiguration WithElevationLength(double newLength)
        {
            return new BaseConfiguration(Scale, newLength, ToleranceDouble);
        }
    }
}

