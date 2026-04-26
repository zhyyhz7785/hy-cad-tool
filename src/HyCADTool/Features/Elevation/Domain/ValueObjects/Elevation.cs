using System;

namespace HyCADTool.Features.Elevation.Domain.ValueObjects
{
    /// <summary>
    /// 标高值对象
    /// 单位：mm（毫米）
    /// 正值：地面以上
    /// 负值：地面以下
    /// 零值：地面标高（±0.000）
    /// </summary>
    public class ElevationValue : IEquatable<ElevationValue>, IComparable<ElevationValue>
    {
        /// <summary>
        /// 标高值（mm）
        /// </summary>
        public double Value { get; }
        
        /// <summary>
        /// 是否为地面标高（±0.000）
        /// </summary>
        public bool IsGroundLevel => System.Math.Abs(Value) < ToleranceSettings.Instance.ElevationTolerance;
        
        /// <summary>
        /// 是否为地下结构（标高 < 0）
        /// </summary>
        public bool IsUnderground => Value < -ToleranceSettings.Instance.ElevationTolerance;
        
        /// <summary>
        /// 是否为地上结构（标高 > 0）
        /// </summary>
        public bool IsAboveGround => Value > ToleranceSettings.Instance.ElevationTolerance;
        
        /// <summary>
        /// 地面标高（±0.000）
        /// </summary>
        public static readonly ElevationValue Ground = new ElevationValue(0.0);
        
        private ElevationValue(double value)
        {
            Value = value;
        }
        
        /// <summary>
        /// 从毫米创建标高
        /// </summary>
        public static ElevationValue FromMillimeters(double millimeters)
        {
            return new ElevationValue(millimeters);
        }
        
        /// <summary>
        /// 从米创建标高
        /// </summary>
        public static ElevationValue FromMeters(double meters)
        {
            return new ElevationValue(meters * 1000.0);
        }
        
        /// <summary>
        /// 转换为米
        /// </summary>
        public double ToMeters()
        {
            return Value / 1000.0;
        }
        
        /// <summary>
        /// 格式化为标准标高文本
        /// 例如：-1.200, +0.500, ±0.000
        /// </summary>
        public string ToFormattedString(int decimalPlaces = 3)
        {
            double meters = ToMeters();
            
            if (IsGroundLevel)
            {
                return $"±{System.Math.Abs(meters).ToString($"F{decimalPlaces}")}";
            }
            else if (meters >= 0)
            {
                return $"+{meters.ToString($"F{decimalPlaces}")}";
            }
            else
            {
                return meters.ToString($"F{decimalPlaces}");
            }
        }
        
        /// <summary>
        /// 从标高文本解析
        /// 支持格式：-1.200, +0.500, ±0.000, 1.200
        /// </summary>
        public static bool TryParse(string text, out ElevationValue elevation)
        {
            elevation = null;
            
            if (string.IsNullOrWhiteSpace(text))
                return false;
            
            // 移除空格和特殊字符
            text = text.Trim().Replace("±", "").Replace("+", "");
            
            // 尝试解析为米
            if (double.TryParse(text, out double meters))
            {
                elevation = FromMeters(meters);
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 计算两个标高之间的高度差（mm）
        /// </summary>
        public double HeightDifference(ElevationValue other)
        {
            return System.Math.Abs(Value - other.Value);
        }
        
        /// <summary>
        /// 判断两个标高是否在容差范围内相等
        /// </summary>
        public bool EqualsWithTolerance(ElevationValue other)
        {
            if (other == null) return false;
            
            var tolerance = ToleranceSettings.Instance.ElevationTolerance;
            return System.Math.Abs(Value - other.Value) < tolerance;
        }
        
        /// <summary>
        /// 判断两个标高是否有显著差异（用于判断是否为墙体）
        /// </summary>
        public bool HasSignificantDifference(ElevationValue other)
        {
            if (other == null) return false;
            
            var tolerance = ToleranceSettings.Instance.ElevationTolerance;
            return System.Math.Abs(Value - other.Value) > tolerance;
        }
        
        #region Equality
        
        public bool Equals(ElevationValue other)
        {
            if (other == null) return false;
            return System.Math.Abs(Value - other.Value) < double.Epsilon;
        }
        
        public override bool Equals(object obj)
        {
            return obj is ElevationValue other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        
        public static bool operator ==(ElevationValue left, ElevationValue right)
        {
            if (ReferenceEquals(left, null))
                return ReferenceEquals(right, null);
            return left.Equals(right);
        }
        
        public static bool operator !=(ElevationValue left, ElevationValue right)
        {
            return !(left == right);
        }
        
        #endregion
        
        #region Comparison
        
        public int CompareTo(ElevationValue other)
        {
            if (other == null) return 1;
            return Value.CompareTo(other.Value);
        }
        
        public static bool operator <(ElevationValue left, ElevationValue right)
        {
            return left.CompareTo(right) < 0;
        }
        
        public static bool operator >(ElevationValue left, ElevationValue right)
        {
            return left.CompareTo(right) > 0;
        }
        
        public static bool operator <=(ElevationValue left, ElevationValue right)
        {
            return left.CompareTo(right) <= 0;
        }
        
        public static bool operator >=(ElevationValue left, ElevationValue right)
        {
            return left.CompareTo(right) >= 0;
        }
        
        #endregion
        
        #region Operators
        
        public static ElevationValue operator +(ElevationValue left, ElevationValue right)
        {
            return new ElevationValue(left.Value + right.Value);
        }
        
        public static ElevationValue operator -(ElevationValue left, ElevationValue right)
        {
            return new ElevationValue(left.Value - right.Value);
        }
        
        public static ElevationValue operator *(ElevationValue e, double multiplier)
        {
            return new ElevationValue(e.Value * multiplier);
        }
        
        public static ElevationValue operator /(ElevationValue e, double divisor)
        {
            if (System.Math.Abs(divisor) < double.Epsilon)
                throw new DivideByZeroException("除数不能为零");
            return new ElevationValue(e.Value / divisor);
        }
        
        /// <summary>
        /// 一元负号运算符（反转标高）
        /// </summary>
        public static ElevationValue operator -(ElevationValue e)
        {
            return new ElevationValue(-e.Value);
        }
        
        #endregion
        
        public override string ToString()
        {
            return ToFormattedString();
        }
    }
}














