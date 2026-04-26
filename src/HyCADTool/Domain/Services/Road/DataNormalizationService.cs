using System;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 数据整理算法（M8.3）：取整 / 保留 N 位小数 / 放大缩小 N 倍 / 去小数点。
    ///
    /// <para><b>职责边界</b></para>
    /// 纯函数、无副作用、无 AutoCAD 依赖。命令层（<c>RoadDataCleanCommand</c>）负责 UI + 遍历 Domain 字段调用本服务。
    /// </summary>
    public static class DataNormalizationService
    {
        /// <summary>数据整理模式枚举。</summary>
        public enum Mode
        {
            /// <summary>取整（四舍五入到整数）。</summary>
            RoundToInt = 0,
            /// <summary>保留 N 位小数（四舍五入）。</summary>
            RoundToDecimals = 1,
            /// <summary>乘以 N 倍。</summary>
            Scale = 2,
            /// <summary>除以 N 倍。</summary>
            Divide = 3,
            /// <summary>去小数点：把 3.45 变成 345（等价于乘 10^n，n=小数位数）。</summary>
            DropDecimal = 4,
        }

        /// <summary>
        /// 对 <paramref name="value"/> 按 <paramref name="mode"/> 执行整理；<paramref name="parameter"/> 的含义依赖 mode。
        /// </summary>
        public static double Normalize(double value, Mode mode, double parameter = 0)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return value;

            switch (mode)
            {
                case Mode.RoundToInt:
                    return Math.Round(value, MidpointRounding.AwayFromZero);

                case Mode.RoundToDecimals:
                    {
                        int digits = ClampDigits((int)Math.Round(parameter));
                        return Math.Round(value, digits, MidpointRounding.AwayFromZero);
                    }

                case Mode.Scale:
                    return value * parameter;

                case Mode.Divide:
                    if (Math.Abs(parameter) < 1e-12)
                        throw new DivideByZeroException("Divide 模式的 parameter 不能为 0。");
                    return value / parameter;

                case Mode.DropDecimal:
                    {
                        // 用字符串观察小数位数再乘对应 10 的幂。
                        int digits = CountDecimalDigits(value);
                        if (digits <= 0) return value;
                        double factor = Math.Pow(10, digits);
                        return Math.Round(value * factor, MidpointRounding.AwayFromZero);
                    }

                default:
                    return value;
            }
        }

        /// <summary>批量整理（<paramref name="values"/> 就地返回新数组，不修改入参）。</summary>
        public static double[] NormalizeMany(double[] values, Mode mode, double parameter = 0)
        {
            if (values == null) return null;
            var result = new double[values.Length];
            for (int i = 0; i < values.Length; i++)
                result[i] = Normalize(values[i], mode, parameter);
            return result;
        }

        private static int ClampDigits(int n)
        {
            if (n < 0) return 0;
            if (n > 15) return 15;
            return n;
        }

        /// <summary>估算 value 当前的小数位数（最多 15 位；NaN/Inf → 0）。</summary>
        public static int CountDecimalDigits(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return 0;
            string s = value.ToString("G15", System.Globalization.CultureInfo.InvariantCulture);
            int dot = s.IndexOf('.');
            if (dot < 0) return 0;
            int e = s.IndexOfAny(new[] { 'e', 'E' });
            int end = e > dot ? e : s.Length;
            return Math.Max(0, end - dot - 1);
        }
    }
}
