using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Domain
{
    /// <summary>
    /// 按间距在线段上均匀布点的归一化参数（平台无关）。
    /// </summary>
    public static class EvenlySpacedLayout
    {
        /// <summary>
        /// 计算在给定长度上按间距均匀分布的归一化参数，结果落在 [0, 1] 且含端点、无重复。
        /// 对应旧代码 GetRangeNumbersByLengthSeparatin。
        /// </summary>
        public static double[] GetRatios(double length, double separation)
        {
            if (length <= 0 || separation <= 0 || length <= separation)
                return new[] { 0.0, 1.0 };

            double normalizedSep = separation / length;
            int divCount = (int)(1.0 / normalizedSep);
            if (divCount < 1)
                return new[] { 0.0, 1.0 };

            double remainder = 1.0 - normalizedSep * divCount;
            var result = new List<double>();

            if (remainder < 1e-6)
            {
                result.Add(0);
                for (int i = 1; i < divCount; i++)
                    result.Add(i * normalizedSep);
                result.Add(1.0);
            }
            else if (divCount == 1)
            {
                result.Add(0);
                result.Add(1.0);
            }
            else
            {
                double halfGap = (normalizedSep - remainder) / 2;
                result.Add(0);
                double current = normalizedSep - halfGap;
                result.Add(current);
                for (int i = 2; i < divCount; i++)
                {
                    current += normalizedSep;
                    result.Add(current);
                }
                result.Add(1.0 - normalizedSep + halfGap);
                result.Add(1.0);
            }

            return ClampAndDedupe(result);
        }

        private static double[] ClampAndDedupe(List<double> values)
        {
            var cleaned = new List<double>(values.Count);
            foreach (var raw in values)
            {
                double c = raw;
                if (c < 0) c = 0;
                else if (c > 1) c = 1;

                if (cleaned.Count == 0 || Math.Abs(cleaned[cleaned.Count - 1] - c) > 1e-9)
                    cleaned.Add(c);
            }

            if (cleaned.Count == 0)
                return new[] { 0.0, 1.0 };
            if (cleaned[0] > 1e-9)
                cleaned.Insert(0, 0);
            if (cleaned[cleaned.Count - 1] < 1.0 - 1e-9)
                cleaned.Add(1.0);

            return cleaned.ToArray();
        }
    }
}
