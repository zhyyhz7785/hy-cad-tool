using System;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    /// <summary>
    /// P3-I2 独立停止线（<see cref="StopLineDesigner"/>）几何单测。
    ///
    /// <para><b>验收要点</b></para>
    /// <list type="bullet">
    /// <item><see cref="StopLineDesigner.Create"/>：Left / Right 重合或线宽非正时抛；</item>
    /// <item><see cref="StopLineDesigner.ExtendSymmetric"/>：中点 + 方向 + 总长 + 线宽中心对称展开，Left/Right 距中点各为 length/2；</item>
    /// <item><see cref="StopLine.Direction"/> / <see cref="StopLine.Center"/> / <see cref="StopLine.Length"/> 派生正确；</item>
    /// <item><see cref="StopLineDesigner.IsValidLength"/> / <see cref="StopLineDesigner.IsValidWidth"/> 阈值校核（GB 5768-2009 §5.2.2）。</item>
    /// </list>
    /// </summary>
    public class StopLineDesignerTests
    {
        [Fact]
        public void Create_WithTwoValidPoints_ReturnsStopLineWithExpectedGeometry()
        {
            var left = new Point2D(0, 0);
            var right = new Point2D(10, 0);

            var sl = StopLineDesigner.Create(left, right, 0.40);

            sl.Id.Should().NotBe(Guid.Empty);
            sl.Left.Should().Be(left);
            sl.Right.Should().Be(right);
            sl.StripeWidth.Should().Be(0.40);
            sl.Length.Should().BeApproximately(10.0, 1e-9);
            sl.Center.X.Should().BeApproximately(5.0, 1e-9);
            sl.Center.Y.Should().BeApproximately(0.0, 1e-9);
            sl.Direction.X.Should().BeApproximately(1.0, 1e-9);
            sl.Direction.Y.Should().BeApproximately(0.0, 1e-9);
        }

        [Fact]
        public void Create_WithCoincidentPoints_Throws()
        {
            var p = new Point2D(1, 2);
            Action act = () => StopLineDesigner.Create(p, p);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-0.1)]
        public void Create_WithNonPositiveWidth_Throws(double w)
        {
            Action act = () => StopLineDesigner.Create(new Point2D(0, 0), new Point2D(10, 0), w);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void ExtendSymmetric_WithOrthogonalDirection_BuildsCenteredSegment()
        {
            var center = new Point2D(5, 5);
            var dir = new Vector2D(1, 0);

            var sl = StopLineDesigner.ExtendSymmetric(center, dir, length: 8.0, stripeWidth: 0.30);

            sl.Left.X.Should().BeApproximately(1.0, 1e-9);
            sl.Left.Y.Should().BeApproximately(5.0, 1e-9);
            sl.Right.X.Should().BeApproximately(9.0, 1e-9);
            sl.Right.Y.Should().BeApproximately(5.0, 1e-9);
            sl.Length.Should().BeApproximately(8.0, 1e-9);
            sl.Center.X.Should().BeApproximately(5.0, 1e-9);
            sl.Center.Y.Should().BeApproximately(5.0, 1e-9);
            sl.StripeWidth.Should().Be(0.30);
        }

        [Fact]
        public void ExtendSymmetric_NormalizesNonUnitDirection()
        {
            var center = new Point2D(0, 0);
            var dir = new Vector2D(3, 4); // length = 5，非单位

            var sl = StopLineDesigner.ExtendSymmetric(center, dir, length: 10.0);

            sl.Length.Should().BeApproximately(10.0, 1e-9);
            sl.Right.X.Should().BeApproximately(3.0, 1e-9); // (3/5)*5 = 3
            sl.Right.Y.Should().BeApproximately(4.0, 1e-9); // (4/5)*5 = 4
            sl.Left.X.Should().BeApproximately(-3.0, 1e-9);
            sl.Left.Y.Should().BeApproximately(-4.0, 1e-9);
        }

        [Fact]
        public void ExtendSymmetric_WithZeroDirection_Throws()
        {
            Action act = () => StopLineDesigner.ExtendSymmetric(
                new Point2D(0, 0), new Vector2D(0, 0), 1.0);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData(-1.0)]
        [InlineData(0.0)]
        public void ExtendSymmetric_WithNonPositiveLength_Throws(double length)
        {
            Action act = () => StopLineDesigner.ExtendSymmetric(
                new Point2D(0, 0), new Vector2D(1, 0), length);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(0.5, false)]  // 小于 MinLength=1.0
        [InlineData(1.0, true)]   // 恰好等于 MinLength
        [InlineData(10.0, true)]
        public void IsValidLength_RespectsMinLength(double length, bool expected)
        {
            var sl = StopLineDesigner.ExtendSymmetric(
                new Point2D(0, 0), new Vector2D(1, 0), length);
            StopLineDesigner.IsValidLength(sl).Should().Be(expected);
        }

        [Theory]
        [InlineData(0.10, false)] // 小于 0.20
        [InlineData(0.20, true)]
        [InlineData(0.40, true)]
        [InlineData(0.50, false)] // 大于 0.40
        public void IsValidWidth_ChecksGbRange(double width, bool expected)
        {
            var sl = StopLineDesigner.Create(new Point2D(0, 0), new Point2D(10, 0), width);
            StopLineDesigner.IsValidWidth(sl).Should().Be(expected);
        }

        [Fact]
        public void StopLine_Equals_ComparesByAllFields()
        {
            var id = Guid.NewGuid();
            var a = new StopLine(id, new Point2D(0, 0), new Point2D(10, 0), 0.4);
            var b = new StopLine(id, new Point2D(0, 0), new Point2D(10, 0), 0.4);
            var c = new StopLine(id, new Point2D(0, 0), new Point2D(10, 0), 0.3); // 不同宽度
            var d = new StopLine(Guid.NewGuid(), new Point2D(0, 0), new Point2D(10, 0), 0.4); // 不同 Id

            a.Should().Be(b);
            a.Should().NotBe(c);
            a.Should().NotBe(d);
            a.GetHashCode().Should().Be(b.GetHashCode());
        }
    }
}
