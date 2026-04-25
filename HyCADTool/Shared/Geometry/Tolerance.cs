using System;

namespace HyCADTool.Shared.Geometry
{
    /// <summary>
    /// 几何容差配置类（平台无关）
    /// 用于几何比较和判断
    /// </summary>
    public class Tolerance
    {
        /// <summary>
        /// 默认容差值（1e-6）
        /// </summary>
        public static readonly Tolerance Default = new Tolerance(1e-6);

        /// <summary>
        /// 严格容差值（1e-9）
        /// </summary>
        public static readonly Tolerance Strict = new Tolerance(1e-9);

        /// <summary>
        /// 宽松容差值（1e-3）
        /// </summary>
        public static readonly Tolerance Loose = new Tolerance(1e-3);

        /// <summary>
        /// 容差值
        /// </summary>
        public double Value { get; }

        public Tolerance(double value)
        {
            if (value < 0)
                throw new ArgumentException("Tolerance must be non-negative", nameof(value));

            Value = value;
        }

        /// <summary>
        /// 判断两个double值是否在容差范围内相等
        /// </summary>
        public bool AreEqual(double a, double b)
        {
            return Math.Abs(a - b) < Value;
        }

        /// <summary>
        /// 判断一个double值是否接近于零
        /// </summary>
        public bool IsZero(double value)
        {
            return Math.Abs(value) < Value;
        }

        /// <summary>
        /// 判断 a 是否小于 b（考虑容差）
        /// </summary>
        public bool IsLessThan(double a, double b)
        {
            return (a - b) < -Value;
        }

        /// <summary>
        /// 判断 a 是否大于 b（考虑容差）
        /// </summary>
        public bool IsGreaterThan(double a, double b)
        {
            return (a - b) > Value;
        }

        public override string ToString()
        {
            return $"Tolerance({Value:E2})";
        }
    }
}

