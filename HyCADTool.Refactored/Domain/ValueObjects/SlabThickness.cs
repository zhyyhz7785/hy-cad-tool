using System;

namespace HyCADTool.Refactored.Domain.ValueObjects
{
    /// <summary>
    /// 筏板厚度值对象
    /// 默认：400mm
    /// 合理范围：100mm - 2000mm
    /// </summary>
    public class SlabThickness : IEquatable<SlabThickness>
    {
        private const double MinValue = 100.0;  // 最小 100mm
        private const double MaxValue = 2000.0; // 最大 2000mm
        public static readonly SlabThickness Default = new SlabThickness(400.0);
        
        /// <summary>
        /// 厚度值（mm）
        /// </summary>
        public double Value { get; }
        
        private SlabThickness(double value)
        {
            Value = value;
        }
        
        /// <summary>
        /// 创建筏板厚度（带验证）
        /// </summary>
        public static SlabThickness Create(double value)
        {
            if (value < MinValue || value > MaxValue)
            {
                throw new ArgumentException(
                    $"筏板厚度必须在 {MinValue}mm - {MaxValue}mm 之间，当前值：{value}mm");
            }
            
            return new SlabThickness(value);
        }
        
        /// <summary>
        /// 尝试创建筏板厚度（不抛出异常）
        /// </summary>
        public static bool TryCreate(double value, out SlabThickness slabThickness)
        {
            if (value >= MinValue && value <= MaxValue)
            {
                slabThickness = new SlabThickness(value);
                return true;
            }
            
            slabThickness = null;
            return false;
        }
        
        /// <summary>
        /// 检查是否在容差范围内相等
        /// </summary>
        public bool EqualsWithTolerance(SlabThickness other)
        {
            if (other == null) return false;
            
            var tolerance = ToleranceSettings.Instance.ThicknessTolerance;
            return Math.Abs(Value - other.Value) < tolerance;
        }
        
        #region Equality
        
        public bool Equals(SlabThickness other)
        {
            if (other == null) return false;
            return Math.Abs(Value - other.Value) < double.Epsilon;
        }
        
        public override bool Equals(object obj)
        {
            return obj is SlabThickness other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        
        public static bool operator ==(SlabThickness left, SlabThickness right)
        {
            if (ReferenceEquals(left, null))
                return ReferenceEquals(right, null);
            return left.Equals(right);
        }
        
        public static bool operator !=(SlabThickness left, SlabThickness right)
        {
            return !(left == right);
        }
        
        #endregion
        
        #region Operators
        
        public static SlabThickness operator +(SlabThickness left, SlabThickness right)
        {
            return Create(left.Value + right.Value);
        }
        
        public static SlabThickness operator -(SlabThickness left, SlabThickness right)
        {
            return Create(left.Value - right.Value);
        }
        
        public static SlabThickness operator *(SlabThickness thickness, double multiplier)
        {
            return Create(thickness.Value * multiplier);
        }
        
        public static SlabThickness operator /(SlabThickness thickness, double divisor)
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














