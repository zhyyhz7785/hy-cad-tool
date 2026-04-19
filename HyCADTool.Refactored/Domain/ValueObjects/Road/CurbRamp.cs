using System;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.ValueObjects.Road
{
    /// <summary>
    /// 缘石坡道（CurbRamp）类型枚举 —— GB 50763-2012《无障碍设计规范》§3.2。
    ///
    /// <list type="bullet">
    /// <item><see cref="SingleFace"/>：单面坡缘石坡道（最常见，布置于 CornerArc 中部）；全宽 ≥ 1.50 m。</item>
    /// <item><see cref="ThreeFace"/>：三面坡缘石坡道（T 形路口端部最常用）；正面宽 ≥ 1.20 m。</item>
    /// <item><see cref="Fan"/>：扇形 / 全宽缘石坡道（整段 CornerArc 做坡）；下口宽 ≥ 1.50 m。</item>
    /// </list>
    /// </summary>
    public enum CurbRampKind
    {
        SingleFace = 0,
        ThreeFace = 1,
        Fan = 2,
    }

    /// <summary>
    /// 缘石坡道（CurbRamp）—— 交叉口转角处把 "人行道高程" 平顺过渡到 "车行道高程" 的小型坡道。
    ///
    /// <para><b>几何语义（简化矩形坡道）</b></para>
    /// <list type="bullet">
    /// <item><see cref="FrontCenter"/>：前沿（车行道侧）中心点，一般落在所属 <see cref="CornerArc"/> 的路缘线上；</item>
    /// <item><see cref="Tangent"/>：前沿切线方向单位向量（横向，沿路缘线切向）；<see cref="Width"/> 即沿此方向展开；</item>
    /// <item><see cref="OutwardNormal"/>：从前沿指向<b>人行道侧</b>（= 路缘线外法线方向）的单位向量；
    /// <see cref="Depth"/> 即沿此方向"向后"退到"上口"的水平距离。</item>
    /// <item>矩形四角坐标 = FrontCenter ± Tangent·(Width/2) + OutwardNormal·(0 或 Depth)。</item>
    /// </list>
    ///
    /// <para><b>规范约束（GB 50763 §3.2 + 本项目默认）</b></para>
    /// <list type="bullet">
    /// <item>单面坡：<see cref="Width"/> ≥ 1.50 m（默认 <see cref="DefaultWidth"/>）；</item>
    /// <item>三面坡：正面宽 ≥ 1.20 m；</item>
    /// <item>坡度 <see cref="Slope"/> ≤ 1:12（默认 <see cref="DefaultSlope"/> = 1/12）；考虑弱势群体可取 1:20；</item>
    /// <item>坡道深度 <see cref="Depth"/> = 缘石高差 / <see cref="Slope"/>；默认 <see cref="DefaultDepth"/> 1.80 m（15 cm × 12）。</item>
    /// </list>
    ///
    /// <para><b>与 AutoCAD 解耦</b></para>
    /// 只使用 <see cref="Point2D"/> / <see cref="Vector2D"/>；可由 Infrastructure 层用 <see cref="FrontCenter"/> + 两单位向量 +
    /// Width/Depth 画成闭合多段线或 Hatch。
    /// </summary>
    public readonly struct CurbRamp : IEquatable<CurbRamp>
    {
        /// <summary>缺省宽度（横向，m）—— GB 50763 §3.2 单面坡全宽最低 1.50 m。</summary>
        public const double DefaultWidth = 1.5;

        /// <summary>缺省深度（纵向，m）—— 按 15 cm 缘石 + 1:12 坡度反算。</summary>
        public const double DefaultDepth = 1.8;

        /// <summary>缺省坡度 —— GB 50763 §3.2.3 最大容许值 1:12。</summary>
        public const double DefaultSlope = 1.0 / 12.0;

        /// <summary>所属 <c>Intersection.CornerArcs</c> 的索引；-1 表示不绑定（独立布置）。</summary>
        public int CornerArcIndex { get; }

        /// <summary>坡道类型（单面 / 三面 / 扇形）。</summary>
        public CurbRampKind Kind { get; }

        /// <summary>前沿（车行道侧）中心点（米）。</summary>
        public Point2D FrontCenter { get; }

        /// <summary>前沿切线方向（单位向量，横向）；<see cref="Width"/> 即沿此方向展开。</summary>
        public Vector2D Tangent { get; }

        /// <summary>指向人行道侧（= 路缘外法线）的单位向量；<see cref="Depth"/> 即沿此方向向"后"延伸到上口。</summary>
        public Vector2D OutwardNormal { get; }

        /// <summary>坡道宽度（横向，m）。</summary>
        public double Width { get; }

        /// <summary>坡道深度（纵向，从前沿到人行道侧上口的水平距离，m）。</summary>
        public double Depth { get; }

        /// <summary>坡度（无量纲 = rise/run，例如 1/12 ≈ 0.0833）。</summary>
        public double Slope { get; }

        public CurbRamp(
            int cornerArcIndex,
            CurbRampKind kind,
            Point2D frontCenter,
            Vector2D tangent,
            Vector2D outwardNormal,
            double width = DefaultWidth,
            double depth = DefaultDepth,
            double slope = DefaultSlope)
        {
            if (width <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Width 必须 > 0");
            if (depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(depth), depth, "Depth 必须 > 0");
            if (slope <= 0)
                throw new ArgumentOutOfRangeException(nameof(slope), slope, "Slope 必须 > 0");
            if (tangent.IsZero(1e-9))
                throw new ArgumentException("Tangent 不能是零向量", nameof(tangent));
            if (outwardNormal.IsZero(1e-9))
                throw new ArgumentException("OutwardNormal 不能是零向量", nameof(outwardNormal));

            CornerArcIndex = cornerArcIndex;
            Kind = kind;
            FrontCenter = frontCenter;
            Tangent = tangent;
            OutwardNormal = outwardNormal;
            Width = width;
            Depth = depth;
            Slope = slope;
        }

        /// <summary>坡道"上口"（人行道侧）中心 = <see cref="FrontCenter"/> + <see cref="OutwardNormal"/> · <see cref="Depth"/>。</summary>
        public Point2D BackCenter => FrontCenter.Add(OutwardNormal * Depth);

        /// <summary>前沿左端点 = <see cref="FrontCenter"/> − <see cref="Tangent"/> · (<see cref="Width"/>/2)。</summary>
        public Point2D FrontLeft => FrontCenter.Add(Tangent * (-Width / 2.0));

        /// <summary>前沿右端点 = <see cref="FrontCenter"/> + <see cref="Tangent"/> · (<see cref="Width"/>/2)。</summary>
        public Point2D FrontRight => FrontCenter.Add(Tangent * (Width / 2.0));

        /// <summary>上口左端点。</summary>
        public Point2D BackLeft => BackCenter.Add(Tangent * (-Width / 2.0));

        /// <summary>上口右端点。</summary>
        public Point2D BackRight => BackCenter.Add(Tangent * (Width / 2.0));

        /// <summary>坡道覆盖面积（m²）= Width · Depth。</summary>
        public double Area => Width * Depth;

        public bool Equals(CurbRamp other)
            => CornerArcIndex == other.CornerArcIndex
               && Kind == other.Kind
               && FrontCenter.IsEqualTo(other.FrontCenter, 1e-9)
               && Tangent.Equals(other.Tangent)
               && OutwardNormal.Equals(other.OutwardNormal)
               && Math.Abs(Width - other.Width) < 1e-9
               && Math.Abs(Depth - other.Depth) < 1e-9
               && Math.Abs(Slope - other.Slope) < 1e-9;

        public override bool Equals(object obj) => obj is CurbRamp other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = CornerArcIndex;
                h = (h * 397) ^ (int)Kind;
                h = (h * 397) ^ FrontCenter.GetHashCode();
                h = (h * 397) ^ Tangent.GetHashCode();
                h = (h * 397) ^ OutwardNormal.GetHashCode();
                h = (h * 397) ^ Width.GetHashCode();
                h = (h * 397) ^ Depth.GetHashCode();
                h = (h * 397) ^ Slope.GetHashCode();
                return h;
            }
        }

        public override string ToString()
            => $"CurbRamp[{Kind}, arc#{CornerArcIndex}, front={FrontCenter}, W={Width:F2}, D={Depth:F2}, s=1:{1.0 / Slope:F1}]";
    }
}
