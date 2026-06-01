using System;
using HyCAD.Geometry;

namespace HyCADTool.Domain.ValueObjects.Road
{
    /// <summary>
    /// 交叉口转角圆弧 —— 相邻两条 <see cref="IntersectionLeg"/> 的偏移外边线之间的圆角弧段。
    ///
    /// <para><b>几何定义</b></para>
    /// <list type="bullet">
    /// <item>每个 CornerArc 对应 <c>Intersection.Legs</c> 里按方位角顺序（CCW）相邻的两条 Leg：
    /// 从 <see cref="LegIndexA"/> 的"右侧路缘外边线"过渡到 <see cref="LegIndexB"/> 的"左侧路缘外边线"。
    /// 这里"左 / 右"指的是沿 Leg <c>InwardDirection</c> 前进时的左右手。</item>
    /// <item><see cref="Center"/> / <see cref="Radius"/> 唯一确定圆；<see cref="StartPoint"/> 与 <see cref="EndPoint"/>
    /// 是两条路缘线与圆的切点；<see cref="StartAngle"/> / <see cref="EndAngle"/> / <see cref="SweepAngle"/>
    /// 按 <b>AutoCAD 习惯</b>（X+ 为 0，CCW 为正）组织。</item>
    /// <item><see cref="SweepAngle"/> &gt; 0 时 StartAngle → EndAngle 走 CCW；&lt; 0 走 CW。</item>
    /// </list>
    ///
    /// <para><b>用途</b></para>
    /// <list type="bullet">
    /// <item>Infrastructure 层用 <see cref="Center"/> + <see cref="Radius"/> + 起止角直接实例化 AutoCAD <c>Arc</c>。</item>
    /// <item>参与 JSON 持久化（<see cref="Models.Road.RoadDesign"/> → <see cref="Models.Road.Intersection.CornerArcs"/>）。</item>
    /// <item>参与规范校核（<see cref="Services.Road.IntersectionCodeChecker"/> 按 <see cref="Radius"/> 查 CJJ 37 附录 B / CJJ 152 表 6.4.2）。</item>
    /// </list>
    /// </summary>
    public readonly struct CornerArc : IEquatable<CornerArc>
    {
        /// <summary>对应 <c>Intersection.Legs</c> 中的起始 Leg 索引（按方位角 CCW 排序后的）。</summary>
        public int LegIndexA { get; }

        /// <summary>对应 <c>Intersection.Legs</c> 中的结束 Leg 索引（= (LegIndexA + 1) % LegCount）。</summary>
        public int LegIndexB { get; }

        /// <summary>圆心（平面坐标，米）。</summary>
        public Point2D Center { get; }

        /// <summary>圆角半径（米，&gt; 0）。</summary>
        public double Radius { get; }

        /// <summary>圆弧起点 = Leg A 外边线上的切点。</summary>
        public Point2D StartPoint { get; }

        /// <summary>圆弧终点 = Leg B 外边线上的切点。</summary>
        public Point2D EndPoint { get; }

        /// <summary>起点角（弧度，X+ 为 0，CCW 正），= atan2(StartPoint - Center)。</summary>
        public double StartAngle { get; }

        /// <summary>终点角（弧度，同上），= atan2(EndPoint - Center)。</summary>
        public double EndAngle { get; }

        /// <summary>扫过角（弧度，带符号）：CCW 为正，CW 为负，范围 (-2π, 2π)。</summary>
        public double SweepAngle { get; }

        /// <summary>弧长（米）= |<see cref="SweepAngle"/>| · <see cref="Radius"/>。</summary>
        public double ArcLength => Math.Abs(SweepAngle) * Radius;

        public CornerArc(
            int legIndexA,
            int legIndexB,
            Point2D center,
            double radius,
            Point2D startPoint,
            Point2D endPoint,
            double startAngle,
            double endAngle,
            double sweepAngle)
        {
            if (radius <= 0)
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius 必须 > 0");

            LegIndexA = legIndexA;
            LegIndexB = legIndexB;
            Center = center;
            Radius = radius;
            StartPoint = startPoint;
            EndPoint = endPoint;
            StartAngle = startAngle;
            EndAngle = endAngle;
            SweepAngle = sweepAngle;
        }

        public bool Equals(CornerArc other)
            => LegIndexA == other.LegIndexA
               && LegIndexB == other.LegIndexB
               && Center.IsEqualTo(other.Center, 1e-9)
               && Math.Abs(Radius - other.Radius) < 1e-9
               && StartPoint.IsEqualTo(other.StartPoint, 1e-9)
               && EndPoint.IsEqualTo(other.EndPoint, 1e-9)
               && Math.Abs(StartAngle - other.StartAngle) < 1e-9
               && Math.Abs(EndAngle - other.EndAngle) < 1e-9
               && Math.Abs(SweepAngle - other.SweepAngle) < 1e-9;

        public override bool Equals(object obj) => obj is CornerArc other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = LegIndexA;
                h = (h * 397) ^ LegIndexB;
                h = (h * 397) ^ Center.GetHashCode();
                h = (h * 397) ^ Radius.GetHashCode();
                h = (h * 397) ^ SweepAngle.GetHashCode();
                return h;
            }
        }

        public override string ToString()
            => $"CornerArc[{LegIndexA}→{LegIndexB}, C={Center}, R={Radius:F2}, Δ={SweepAngle * 180 / Math.PI:F1}°, L={ArcLength:F2}]";
    }
}
