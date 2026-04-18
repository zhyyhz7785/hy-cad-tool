using System;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 平面线位"三单元（缓-圆-缓）"常用解析式。纯静态，不依赖 AutoCAD；
    /// 用于 UI 派生量实时回显与规范检查，避免每次都跑完整 <see cref="AlignmentPiDesigner"/>。
    ///
    /// 术语（与 CJJ 37 / 鸿业 / 纬地对齐）：
    /// <list type="bullet">
    ///   <item><c>turn</c>：转角 θ，rad。按 atan2(cross, dot) 约定；左转为正，右转为负。</item>
    ///   <item><c>R</c>：圆曲线半径，m。</item>
    ///   <item><c>Ls</c>：缓和曲线长（Clothoid）。对称时 Ls1 = Ls2 = Ls。</item>
    ///   <item><c>p = Ls² / (24R) - Ls⁴ / (2688R³)</c>：圆心相对切线的内移量。</item>
    ///   <item><c>q = Ls / 2 - Ls³ / (240R²)</c>：切线偏移量（TS 到 PI 距离沿切线的分量）。</item>
    ///   <item><c>T = (R + p) · tan(|θ|/2) + q</c>：对称三单元的切线长（TS→PI 或 PI→ST）。</item>
    ///   <item><c>Ly = R · (|θ| - (Ls1 + Ls2) / (2R))</c>：圆曲线段弧长（当 Ls1 = Ls2 时简化为 R·θ - Ls）。</item>
    /// </list>
    ///
    /// 符号说明（严格按照行业材料）：
    /// - p, q 使用 Taylor 展开至四阶（工程够用，CJJ 37 教材同款）；
    /// - 非对称情况（Ls1 ≠ Ls2）采用入/出侧分别计算 p, q，T 也分 T1 / T2；
    /// - Ly 采用"扣掉两侧缓和曲线所占圆心角"的公式，等价于"圆曲线起止间的弧长"。
    /// </summary>
    public static class RoadGeometryFormulas
    {
        /// <summary>
        /// 计算相邻三点 PI 的转角（rad）。
        /// 返回带符号值 ∈ (-π, π]：左转为正、右转为负；两侧向量退化时返回 0。
        /// </summary>
        /// <param name="prev">前一 PI。</param>
        /// <param name="pi">当前 PI。</param>
        /// <param name="next">后一 PI。</param>
        /// <param name="tolerance">向量退化容差（m）。</param>
        public static double TurnAngle(Point2D prev, Point2D pi, Point2D next, double tolerance = 1e-6)
        {
            var tIn = new Vector2D(pi.X - prev.X, pi.Y - prev.Y);
            var tOut = new Vector2D(next.X - pi.X, next.Y - pi.Y);
            if (!tIn.TryNormalize(out var tInU, tolerance)) return 0;
            if (!tOut.TryNormalize(out var tOutU, tolerance)) return 0;
            double cross = tInU.Cross(tOutU);
            double dot = tInU.Dot(tOutU);
            return Math.Atan2(cross, dot);
        }

        /// <summary>
        /// p = Ls²/(24R) - Ls⁴/(2688R³)
        /// R &lt;= 0 或 Ls &lt;= 0 时返回 0（工程用法，避免除零）。
        /// </summary>
        public static double InnerShift(double radius, double ls)
        {
            if (radius <= 0 || ls <= 0) return 0;
            double r2 = radius * radius;
            return (ls * ls) / (24.0 * radius) - (ls * ls * ls * ls) / (2688.0 * r2 * radius);
        }

        /// <summary>
        /// q = Ls/2 - Ls³/(240R²)
        /// </summary>
        public static double TangentShift(double radius, double ls)
        {
            if (radius <= 0 || ls <= 0) return 0;
            double r2 = radius * radius;
            return ls / 2.0 - (ls * ls * ls) / (240.0 * r2);
        }

        /// <summary>
        /// 入侧切线长 T1 = (R + p1) · tan(|θ|/2) + q1。
        /// Ls1 = 0 时退化为纯圆曲线切线长 T = R · tan(|θ|/2)。
        /// turn = 0 或 R &lt;= 0 时返回 0。
        /// </summary>
        public static double TangentInLength(double radius, double lsIn, double turnRad)
        {
            if (radius <= 0) return 0;
            double absTurn = Math.Abs(turnRad);
            if (absTurn < 1e-9) return 0;
            double tanHalf = Math.Tan(absTurn / 2.0);
            return (radius + InnerShift(radius, lsIn)) * tanHalf + TangentShift(radius, lsIn);
        }

        /// <summary>
        /// 出侧切线长 T2 = (R + p2) · tan(|θ|/2) + q2。
        /// </summary>
        public static double TangentOutLength(double radius, double lsOut, double turnRad)
        {
            if (radius <= 0) return 0;
            double absTurn = Math.Abs(turnRad);
            if (absTurn < 1e-9) return 0;
            double tanHalf = Math.Tan(absTurn / 2.0);
            return (radius + InnerShift(radius, lsOut)) * tanHalf + TangentShift(radius, lsOut);
        }

        /// <summary>
        /// 对称基本型切线长（鸿业截图中的 T1/T2 相同情况）。
        /// </summary>
        public static double TangentLengthSymmetric(double radius, double ls, double turnRad)
            => TangentInLength(radius, ls, turnRad);

        /// <summary>
        /// 圆曲线段弧长 Ly = R · (|θ| - (Ls1 + Ls2) / (2R))。
        /// 当两侧缓和曲线之和 &gt; R·|θ| 时返回 0（意味着圆曲线段被完全吃掉，几何上不合法；
        /// 由 <see cref="AlignmentCodeChecker"/> 负责把它检出为"圆曲线长不足"）。
        /// </summary>
        public static double CircularArcLength(double radius, double lsIn, double lsOut, double turnRad)
        {
            if (radius <= 0) return 0;
            double absTurn = Math.Abs(turnRad);
            if (absTurn < 1e-9) return 0;
            double ly = radius * absTurn - (Math.Max(0, lsIn) + Math.Max(0, lsOut)) / 2.0;
            return ly > 0 ? ly : 0;
        }

        /// <summary>
        /// 把转角（rad）格式化为"左偏/右偏 dd.dddd°"的字符串，便于 UI 与命令行回显。
        /// </summary>
        public static string FormatTurn(double turnRad, int decimals = 3)
        {
            double deg = turnRad * 180.0 / Math.PI;
            string dir = turnRad > 0 ? "左偏" : (turnRad < 0 ? "右偏" : "直行");
            return $"{dir}{Math.Abs(deg).ToString("F" + decimals)}°";
        }

        /// <summary>
        /// Ls ↔ A（回旋线参数）互换：A = sqrt(R · Ls)。
        /// </summary>
        public static double SpiralParameterA(double radius, double ls)
        {
            if (radius <= 0 || ls <= 0) return 0;
            return Math.Sqrt(radius * ls);
        }
    }
}
