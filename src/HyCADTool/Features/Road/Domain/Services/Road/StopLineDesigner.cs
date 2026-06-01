using System;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 独立停止线几何服务（纯函数 / 无状态）。
    ///
    /// <para><b>职责</b></para>
    /// <list type="bullet">
    /// <item><see cref="Create"/>：给定两端点 + 可选线宽，校验并构造 <see cref="StopLine"/>；Id 自动生成；</item>
    /// <item><see cref="ExtendSymmetric"/>：给定中点 + 方向 + 总长 + 线宽，按中心对称展开两端点；</item>
    /// <item><see cref="IsValidLength"/> / <see cref="IsValidWidth"/>：规范阈值校验（GB 5768-2009）。</item>
    /// </list>
    ///
    /// <para><b>Domain 纯净</b></para>
    /// 只依赖 <see cref="Point2D"/> / <see cref="Vector2D"/>；不引用 AutoCAD。
    /// </summary>
    public static class StopLineDesigner
    {
        /// <summary>GB 5768-2009 §5.2.2：停止线长度合理下限（米）—— 小于一个车道（3.0 m）视为无效。</summary>
        public const double MinLength = 1.0;

        /// <summary>GB 5768-2009 §5.2.2：停止线线宽合理区间（米）。</summary>
        public const double MinWidth = 0.20;

        /// <summary>GB 5768-2009 §5.2.2：停止线线宽上限（米）。</summary>
        public const double MaxWidth = 0.40;

        /// <summary>按两端点构造 <see cref="StopLine"/>（Id 自动生成）。</summary>
        public static StopLine Create(
            Point2D left,
            Point2D right,
            double stripeWidth = StopLine.DefaultStripeWidth)
        {
            return new StopLine(Guid.NewGuid(), left, right, stripeWidth);
        }

        /// <summary>按中点 + 跨车道方向 + 总长 + 线宽中心对称展开；方向非单位向量会自动归一。</summary>
        public static StopLine ExtendSymmetric(
            Point2D center,
            Vector2D direction,
            double length,
            double stripeWidth = StopLine.DefaultStripeWidth)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length), length, "length 必须 > 0");
            if (!direction.TryNormalize(out var unit, 1e-9))
                throw new ArgumentException("direction 不能是零向量", nameof(direction));

            double half = length / 2.0;
            var left = new Point2D(center.X - unit.X * half, center.Y - unit.Y * half);
            var right = new Point2D(center.X + unit.X * half, center.Y + unit.Y * half);
            return Create(left, right, stripeWidth);
        }

        /// <summary>停止线长度是否 ≥ <see cref="MinLength"/>。</summary>
        public static bool IsValidLength(StopLine stopLine) => stopLine.Length >= MinLength;

        /// <summary>停止线线宽是否 ∈ [<see cref="MinWidth"/>, <see cref="MaxWidth"/>]。</summary>
        public static bool IsValidWidth(StopLine stopLine)
            => stopLine.StripeWidth >= MinWidth - 1e-9 && stopLine.StripeWidth <= MaxWidth + 1e-9;
    }
}
