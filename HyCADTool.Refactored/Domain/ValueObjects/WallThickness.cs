using System;

namespace HyCADTool.Refactored.Domain.ValueObjects
{
    /// <summary>
    /// 墙体厚度值对象
    /// 默认：300mm
    /// 合理范围：50mm - 3000mm
    /// </summary>
    public class WallThickness : IEquatable<WallThickness>
    {
        private const double MinValue = 50.0;   // 最小 50mm
        private const double MaxValue = 3000.0; // 最大 3000mm
        public static readonly WallThickness Default = new WallThickness(300.0);
        
        /// <summary>
        /// 厚度值（mm）
        /// </summary>
        public double Value { get; }
        
        private WallThickness(double value)
        {
            Value = value;
        }
        
        /// <summary>
        /// 创建墙体厚度（带验证）
        /// </summary>
        public static WallThickness Create(double value)
        {
            if (value < MinValue || value > MaxValue)
            {
                throw new ArgumentException(
                    $"墙体厚度必须在 {MinValue}mm - {MaxValue}mm 之间，当前值：{value}mm");
            }
            
            return new WallThickness(value);
        }
        
        /// <summary>
        /// 尝试创建墙体厚度（不抛出异常）
        /// </summary>
        public static bool TryCreate(double value, out WallThickness wallThickness)
        {
            if (value >= MinValue && value <= MaxValue)
            {
                wallThickness = new WallThickness(value);
                return true;
            }
            
            wallThickness = null;
            return false;
        }
        
        /// <summary>
        /// 检查是否在容差范围内相等
        /// </summary>
        public bool EqualsWithTolerance(WallThickness other)
        {
            if (other == null) return false;
            
            var tolerance = ToleranceSettings.Instance.ThicknessTolerance;
            return Math.Abs(Value - other.Value) < tolerance;
        }
        
        #region Equality
        
        public bool Equals(WallThickness other)
        {
            if (other == null) return false;
            return Math.Abs(Value - other.Value) < double.Epsilon;
        }
        
        public override bool Equals(object obj)
        {
            return obj is WallThickness other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        
        public static bool operator ==(WallThickness left, WallThickness right)
        {
            if (ReferenceEquals(left, null))
                return ReferenceEquals(right, null);
            return left.Equals(right);
        }
        
        public static bool operator !=(WallThickness left, WallThickness right)
        {
            return !(left == right);
        }
        
        #endregion
        
        #region Operators
        
        public static WallThickness operator +(WallThickness left, WallThickness right)
        {
            return Create(left.Value + right.Value);
        }
        
        public static WallThickness operator -(WallThickness left, WallThickness right)
        {
            return Create(left.Value - right.Value);
        }
        
        public static WallThickness operator *(WallThickness thickness, double multiplier)
        {
            return Create(thickness.Value * multiplier);
        }
        
        public static WallThickness operator /(WallThickness thickness, double divisor)
        {
            if (Math.Abs(divisor) < double.Epsilon)
                throw new DivideByZeroException("除数不能为零");
            return Create(thickness.Value / divisor);
        }
        
        #endregion
        
        public override string ToString()
        {
            return $"{Value:F1}mm";
        }
    }
}














