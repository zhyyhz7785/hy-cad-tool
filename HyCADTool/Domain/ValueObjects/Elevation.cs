using System;

namespace HyCADTool.Domain.ValueObjects
{
    /// <summary>
    /// 标高值对象
    /// 单位：mm（毫米）
    /// 正值：地面以上
    /// 负值：地面以下
    /// 零值：地面标高（±0.000）
    /// </summary>
    public class Elevation : IEquatable<Elevation>, IComparable<Elevation>
    {
        /// <summary>
        /// 标高值（mm）
        /// </summary>
        public double Value { get; }
        
        /// <summary>
        /// 是否为地面标高（±0.000）
        /// </summary>
        public bool IsGroundLevel => Math.Abs(Value) < ToleranceSettings.Instance.ElevationTolerance;
        
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
        public static readonly Elevation Ground = new Elevation(0.0);
        
        private Elevation(double value)
        {
            Value = value;
        }
        
        /// <summary>
        /// 从毫米创建标高
        /// </summary>
        public static Elevation FromMillimeters(double millimeters)
        {
            return new Elevation(millimeters);
        }
        
        /// <summary>
        /// 从米创建标高
        /// </summary>
        public static Elevation FromMeters(double meters)
        {
            return new Elevation(meters * 1000.0);
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
                return $"±{Math.Abs(meters).ToString($"F{decimalPlaces}")}";
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
        public static bool TryParse(string text, out Elevation elevation)
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
        public double HeightDifference(Elevation other)
        {
            return Math.Abs(Value - other.Value);
        }
        
        /// <summary>
        /// 判断两个标高是否在容差范围内相等
        /// </summary>
        public bool EqualsWithTolerance(Elevation other)
        {
            if (other == null) return false;
            
            var tolerance = ToleranceSettings.Instance.ElevationTolerance;
            return Math.Abs(Value - other.Value) < tolerance;
        }
        
        /// <summary>
        /// 判断两个标高是否有显著差异（用于判断是否为墙体）
        /// </summary>
        public bool HasSignificantDifference(Elevation other)
        {
            if (other == null) return false;
            
            var tolerance = ToleranceSettings.Instance.ElevationTolerance;
            return Math.Abs(Value - other.Value) > tolerance;
        }
        
        #region Equality
        
        public bool Equals(Elevation other)
        {
            if (other == null) return false;
            return Math.Abs(Value - other.Value) < double.Epsilon;
        }
        
        public override bool Equals(object obj)
        {
            return obj is Elevation other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }
        
        public static bool operator ==(Elevation left, Elevation right)
        {
            if (ReferenceEquals(left, null))
                return ReferenceEquals(right, null);
            return left.Equals(right);
        }
        
        public static bool operator !=(Elevation left, Elevation right)
        {
            return !(left == right);
        }
        
        #endregion
        
        #region Comparison
        
        public int CompareTo(Elevation other)
        {
            if (other == null) return 1;
            return Value.CompareTo(other.Value);
        }
        
        public static bool operator <(Elevation left, Elevation right)
        {
            return left.CompareTo(right) < 0;
        }
        
        public static bool operator >(Elevation left, Elevation right)
        {
            return left.CompareTo(right) > 0;
        }
        
        public static bool operator <=(Elevation left, Elevation right)
        {
            return left.CompareTo(right) <= 0;
        }
        
        public static bool operator >=(Elevation left, Elevation right)
        {
            return left.CompareTo(right) >= 0;
        }
        
        #endregion
        
        #region Operators
        
        public static Elevation operator +(Elevation left, Elevation right)
        {
            return new Elevation(left.Value + right.Value);
        }
        
        public static Elevation operator -(Elevation left, Elevation right)
        {
            return new Elevation(left.Value - right.Value);
        }
        
        public static Elevation operator *(Elevation elevation, double multiplier)
        {
            return new Elevation(elevation.Value * multiplier);
        }
        
        public static Elevation operator /(Elevation elevation, double divisor)
        {
            if (Math.Abs(divisor) < double.Epsilon)
                throw new DivideByZeroException("除数不能为零");
            return new Elevation(elevation.Value / divisor);
        }
        
        /// <summary>
        /// 一元负号运算符（反转标高）
        /// </summary>
        public static Elevation operator -(Elevation elevation)
        {
            return new Elevation(-elevation.Value);
        }
        
        #endregion
        
        public override string ToString()
        {
            return ToFormattedString();
        }
    }
}














