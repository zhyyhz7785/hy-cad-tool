using System;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    /// <summary>
    /// P3-I2 导流箭头（<see cref="ArrowMarkingDesigner"/>）几何单测。
    ///
    /// <para><b>验收要点</b></para>
    /// <list type="bullet">
    /// <item>构造校验（长度非正 / 零向量方向）；</item>
    /// <item>Direction 归一化：非单位向量输入后 <see cref="ArrowMarking.Direction"/> 长度 ≈ 1；</item>
    /// <item>Straight 多边形：7 顶点、闭合、箭尖在 <c>anchor + direction * length</c>；</item>
    /// <item>Left / Right：9 顶点、闭合、箭尖在左/右侧（Left 的 y>0，Right 的 y&lt;0）；</item>
    /// <item>Left 与 Right 关于 Direction 镜像（y 对称）；</item>
    /// <item>组合型 StraightLeft/StraightRight/LeftRight：两多边形；</item>
    /// <item>BuildFootprint 参数越界抛出 <see cref="ArgumentException"/>。</item>
    /// </list>
    /// </summary>
    public class ArrowMarkingDesignerTests
    {
        private static readonly Point2D Origin = new Point2D(0, 0);
        private static readonly Vector2D EastDir = new Vector2D(1, 0);

        // ---------------------------------------------------------------
        //  构造 / 校验
        // ---------------------------------------------------------------

        [Fact]
        public void Create_WithValidInputs_Ok()
        {
            var a = ArrowMarkingDesigner.Create(Origin, EastDir, ArrowMarkingKind.Straight, 6.0);
            a.Id.Should().NotBe(Guid.Empty);
            a.Length.Should().Be(6.0);
            a.Kind.Should().Be(ArrowMarkingKind.Straight);
            a.Direction.X.Should().BeApproximately(1, 1e-9);
            a.Direction.Y.Should().BeApproximately(0, 1e-9);
        }

        [Fact]
        public void Create_NormalizesNonUnitDirection()
        {
            var a = ArrowMarkingDesigner.Create(Origin, new Vector2D(3, 4), ArrowMarkingKind.Straight);
            a.Direction.X.Should().BeApproximately(0.6, 1e-9);
            a.Direction.Y.Should().BeApproximately(0.8, 1e-9);
        }

        [Fact]
        public void Create_WithZeroDirection_Throws()
        {
            Action act = () => ArrowMarkingDesigner.Create(Origin, new Vector2D(0, 0), ArrowMarkingKind.Straight);
            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Create_WithNonPositiveLength_Throws(double length)
        {
            Action act = () => ArrowMarkingDesigner.Create(Origin, EastDir, ArrowMarkingKind.Straight, length);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        // ---------------------------------------------------------------
        //  Straight
        // ---------------------------------------------------------------

        [Fact]
        public void BuildFootprint_Straight_HasSevenClosedVertices_AndTipAtAnchorPlusLengthAlongDirection()
        {
            var a = ArrowMarkingDesigner.Create(Origin, EastDir, ArrowMarkingKind.Straight, 6.0);
            var polys = ArrowMarkingDesigner.BuildFootprint(a);
            polys.Should().HaveCount(1);

            var p = polys[0];
            p.IsClosed.Should().BeTrue();
            p.VertexCount.Should().Be(7);

            // 箭尖 = V3 = (L, 0)（直行箭头定义）
            p.GetPointAt(3).X.Should().BeApproximately(6.0, 1e-9);
            p.GetPointAt(3).Y.Should().BeApproximately(0.0, 1e-9);

            // 箭尾左右两点对称 y
            p.GetPointAt(0).Y.Should().BeApproximately(-0.15, 1e-9);
            p.GetPointAt(6).Y.Should().BeApproximately(+0.15, 1e-9);
            p.GetPointAt(0).X.Should().BeApproximately(0.0, 1e-9);
            p.GetPointAt(6).X.Should().BeApproximately(0.0, 1e-9);
        }

        [Fact]
        public void BuildFootprint_Straight_WithNorthwardDirection_TipIsRotatedCorrectly()
        {
            // direction = +Y：箭尖应在 (anchor.x, anchor.y + length)
            var a = ArrowMarkingDesigner.Create(
                new Point2D(10, 5), new Vector2D(0, 1), ArrowMarkingKind.Straight, 4.0);
            var p = ArrowMarkingDesigner.BuildFootprint(a)[0];
            p.GetPointAt(3).X.Should().BeApproximately(10.0, 1e-9);
            p.GetPointAt(3).Y.Should().BeApproximately(9.0, 1e-9);
        }

        // ---------------------------------------------------------------
        //  Left / Right
        // ---------------------------------------------------------------

        [Fact]
        public void BuildFootprint_Left_HasNineClosedVertices_AndTipOnLeftSide()
        {
            var a = ArrowMarkingDesigner.Create(Origin, EastDir, ArrowMarkingKind.Left, 6.0);
            var polys = ArrowMarkingDesigner.BuildFootprint(a);
            polys.Should().HaveCount(1);

            var p = polys[0];
            p.IsClosed.Should().BeTrue();
            p.VertexCount.Should().Be(9);

            // 箭尖 = V4 = (L - branch, branch)；左转时 y > 0
            var tip = p.GetPointAt(4);
            tip.Y.Should().BeGreaterThan(0);
            // branch = 0.4 * 6 = 2.4，L_main = 3.6
            tip.X.Should().BeApproximately(3.6, 1e-9);
            tip.Y.Should().BeApproximately(2.4, 1e-9);
        }

        [Fact]
        public void BuildFootprint_Right_HasNineClosedVertices_AndTipOnRightSide()
        {
            var a = ArrowMarkingDesigner.Create(Origin, EastDir, ArrowMarkingKind.Right, 6.0);
            var p = ArrowMarkingDesigner.BuildFootprint(a)[0];
            p.VertexCount.Should().Be(9);
            p.IsClosed.Should().BeTrue();

            // Right 顶点顺序被反转（以维持 CCW），箭尖索引变到 V4（9-1-4 = 4，对称位置）
            // 我们只断言"箭尖应在 y < 0 侧"
            bool anyTipOnRight = false;
            double maxAbsY = 0;
            int tipIdx = -1;
            for (int i = 0; i < p.VertexCount; i++)
            {
                double absY = Math.Abs(p.GetPointAt(i).Y);
                if (absY > maxAbsY) { maxAbsY = absY; tipIdx = i; }
            }
            tipIdx.Should().BeGreaterOrEqualTo(0);
            p.GetPointAt(tipIdx).Y.Should().BeLessThan(0);
            anyTipOnRight = true;
            anyTipOnRight.Should().BeTrue();
        }

        [Fact]
        public void BuildFootprint_Left_And_Right_AreMirrorAboutDirectionAxis()
        {
            // 取两者顶点集合，按 |y| 排序后 y 值应互为相反数（集合镜像）。
            var aLeft = ArrowMarkingDesigner.Create(Origin, EastDir, ArrowMarkingKind.Left, 6.0);
            var aRight = ArrowMarkingDesigner.Create(Origin, EastDir, ArrowMarkingKind.Right, 6.0);
            var pL = ArrowMarkingDesigner.BuildFootprint(aLeft)[0];
            var pR = ArrowMarkingDesigner.BuildFootprint(aRight)[0];

            pL.VertexCount.Should().Be(pR.VertexCount);

            // 收集两组 (x, y)，并为 Right 做 y → -y；排序后应与 Left 一致
            var l = new (double X, double Y)[pL.VertexCount];
            var r = new (double X, double Y)[pR.VertexCount];
            for (int i = 0; i < pL.VertexCount; i++) l[i] = (pL.GetPointAt(i).X, pL.GetPointAt(i).Y);
            for (int i = 0; i < pR.VertexCount; i++) r[i] = (pR.GetPointAt(i).X, -pR.GetPointAt(i).Y);

            Array.Sort(l, (a, b) => a.X == b.X ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            Array.Sort(r, (a, b) => a.X == b.X ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

            for (int i = 0; i < l.Length; i++)
            {
                l[i].X.Should().BeApproximately(r[i].X, 1e-9);
                l[i].Y.Should().BeApproximately(r[i].Y, 1e-9);
            }
        }

        // ---------------------------------------------------------------
        //  组合型
        // ---------------------------------------------------------------

        [Theory]
        [InlineData(ArrowMarkingKind.StraightLeft)]
        [InlineData(ArrowMarkingKind.StraightRight)]
        [InlineData(ArrowMarkingKind.LeftRight)]
        public void BuildFootprint_Composite_HasTwoClosedPolygons(ArrowMarkingKind kind)
        {
            var a = ArrowMarkingDesigner.Create(Origin, EastDir, kind, 6.0);
            var polys = ArrowMarkingDesigner.BuildFootprint(a);
            polys.Should().HaveCount(2);
            foreach (var p in polys)
            {
                p.IsClosed.Should().BeTrue();
                p.VertexCount.Should().BeGreaterOrEqualTo(7); // 分支至少 7 顶点
            }
        }

        [Fact]
        public void BuildFootprint_StraightLeft_ShapesEqualsStraightPlusLeftBranch()
        {
            // 第 1 个多边形 = Straight 那一款（7 顶点）；第 2 个 = 分支（7 顶点）。
            var a = ArrowMarkingDesigner.Create(Origin, EastDir, ArrowMarkingKind.StraightLeft, 6.0);
            var polys = ArrowMarkingDesigner.BuildFootprint(a);
            polys[0].VertexCount.Should().Be(7); // Straight
            polys[1].VertexCount.Should().Be(7); // Branch

            // 分支箭尖位于 V4 = (L_main, branch) = (3.6, 2.4)，在左侧（y > 0）
            polys[1].GetPointAt(4).X.Should().BeApproximately(3.6, 1e-9);
            polys[1].GetPointAt(4).Y.Should().BeApproximately(2.4, 1e-9);
        }

        [Fact]
        public void BuildFootprint_LeftRight_BranchesAreYMirror()
        {
            var a = ArrowMarkingDesigner.Create(Origin, EastDir, ArrowMarkingKind.LeftRight, 6.0);
            var polys = ArrowMarkingDesigner.BuildFootprint(a);
            polys.Should().HaveCount(2);

            // 两多边形都是 Turn 型 = 9 顶点
            polys[0].VertexCount.Should().Be(9);
            polys[1].VertexCount.Should().Be(9);
        }

        // ---------------------------------------------------------------
        //  参数校核
        // ---------------------------------------------------------------

        [Fact]
        public void BuildFootprint_InvalidRatios_Throws()
        {
            var a = ArrowMarkingDesigner.Create(Origin, EastDir, ArrowMarkingKind.Straight, 6.0);
            Action actRatio = () => ArrowMarkingDesigner.BuildFootprint(a, arrowHeadLengthRatio: 1.5);
            actRatio.Should().Throw<ArgumentOutOfRangeException>();

            Action actHalfWidth = () => ArrowMarkingDesigner.BuildFootprint(a, arrowHeadHalfWidth: 0.1); // <= shaftWidth/2 = 0.15
            actHalfWidth.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void BuildFootprint_LeftWithTooLargeBranch_Throws()
        {
            // branch >= L - shaftWidth/2 时 L_main <= 0，抛错
            var a = ArrowMarkingDesigner.Create(Origin, EastDir, ArrowMarkingKind.Left, 6.0);
            Action act = () => ArrowMarkingDesigner.BuildFootprint(a, turnBranchRatio: 0.99);
            act.Should().Throw<ArgumentException>();
        }
    }
}
