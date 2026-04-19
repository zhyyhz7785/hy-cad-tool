using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Drawing;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Models.Drawing
{
    /// <summary>
    /// 验收：<see cref="ScaleContext"/> 派生公式 / 样式名工厂 / 小数位夹紧。
    /// 对应 plan "副比例与单位联动模式" §三 验收用例矩阵（10 个 Case）。
    /// 
    /// 不变量：
    /// <list type="bullet">
    /// <item>纸面标记大小：TextSize × UnitFactor × MainScale = paper_mm （与 Sub 无关）</item>
    /// <item>标注读数：geom_model × DIMLFAC = real_world（与 Sub 是否开启都成立）</item>
    /// <item>几何放大：UseSub → model_geom = world × GeomMul（= M/S）</item>
    /// </list>
    /// </summary>
    public class ScaleContextTests
    {
        // ================================================================
        //  A. 基础派生公式
        // ================================================================

        [Fact]
        public void Case1_NoSub_Mm_BaseDerivations()
        {
            // 1:100 mm p=0 —— 最典型施工图
            var ctx = new ScaleContext(100, 100, false, DrawingUnit.Millimeter, 0);

            ctx.MainScale.Should().Be(100);
            ctx.EffectiveSubScale.Should().Be(100, "UseSub=false 时应归一为 MainScale");
            ctx.UnitFactor.Should().Be(1.0);
            ctx.DimLfac.Should().Be(1.0, "无副比例时 DIMLFAC=1");
            ctx.GeomMul.Should().Be(1.0, "无副比例时 GeomMul=1");
            ctx.Precision.Should().Be(0);
            ctx.InsUnitsCode.Should().Be(4);
        }

        [Fact]
        public void Case2_SubOn_Mm_LfacAndGeomMul()
        {
            // 1:100 主 + 1:20 副 —— 用户核心场景（大图中嵌入局部放大）
            var ctx = new ScaleContext(100, 20, true, DrawingUnit.Millimeter, 0);

            ctx.DimLfac.Should().Be(20.0 / 100.0, "DIMLFAC = S/M");
            ctx.GeomMul.Should().Be(100.0 / 20.0, "GeomMul = M/S = 5");
            ctx.EffectiveSubScale.Should().Be(20);
        }

        [Theory]
        [InlineData(DrawingUnit.Millimeter, 1.0, 4)]
        [InlineData(DrawingUnit.Centimeter, 0.1, 5)]
        [InlineData(DrawingUnit.Meter, 0.001, 6)]
        public void UnitFactor_And_InsUnitsCode(DrawingUnit unit, double expectedFactor, short expectedInsUnits)
        {
            var ctx = new ScaleContext(50, 50, false, unit, 0);
            ctx.UnitFactor.Should().BeApproximately(expectedFactor, 1e-12);
            ctx.InsUnitsCode.Should().Be(expectedInsUnits);
        }

        // ================================================================
        //  B. 不变量交叉验证
        // ================================================================

        [Fact]
        public void Invariant_PaperMarkSize_UnaffectedBySub()
        {
            // 纸面文字 = TextSize(paper_mm) × UnitFactor × MainScale，应独立于 Sub/UseSub
            const double paperText = 2.5;
            var a = new ScaleContext(100, 100, false, DrawingUnit.Millimeter, 0);
            var b = new ScaleContext(100, 20, true, DrawingUnit.Millimeter, 0);

            double modelA = paperText * a.UnitFactor * a.MainScale;
            double modelB = paperText * b.UnitFactor * b.MainScale;
            modelB.Should().Be(modelA, "开启副比例不应改变纸面标记在模型中的字高");
        }

        [Fact]
        public void Invariant_DimReading_EqualsRealWorld_WhenGeomScaledByGeomMul()
        {
            // 1:100 M, 1:20 S, 真实尺寸 5000mm
            // 用户把几何画为 5000 × GeomMul = 25000 模型单位
            // AutoCAD 显示值 = geom × DIMLFAC = 25000 × (20/100) = 5000 ✓
            var ctx = new ScaleContext(100, 20, true, DrawingUnit.Millimeter, 0);
            const double real = 5000.0;
            double geomDrawn = real * ctx.GeomMul;
            double dimDisplayed = geomDrawn * ctx.DimLfac;
            dimDisplayed.Should().BeApproximately(real, 1e-9);
        }

        [Fact]
        public void Invariant_DimReading_OneToOne_WhenNoSub()
        {
            // 无副比例：几何直接画 world 值，DIMLFAC=1
            var ctx = new ScaleContext(100, 100, false, DrawingUnit.Millimeter, 0);
            const double real = 1234.5;
            (real * ctx.GeomMul * ctx.DimLfac).Should().BeApproximately(real, 1e-9);
        }

        // ================================================================
        //  C. 样式名工厂
        // ================================================================

        [Theory]
        [InlineData(50, 50, false, DrawingUnit.Millimeter, 0, "0-Hy-50-50-Dim-mm-0")]
        [InlineData(100, 20, true, DrawingUnit.Millimeter, 0, "0-Hy-100-20-Dim-mm-0")]
        [InlineData(100, 100, false, DrawingUnit.Meter, 3, "0-Hy-100-100-Dim-m-3")]
        [InlineData(50, 50, false, DrawingUnit.Centimeter, 2, "0-Hy-50-50-Dim-cm-2")]
        [InlineData(500, 100, true, DrawingUnit.Meter, 2, "0-Hy-500-100-Dim-m-2")]
        public void BuildDimStyleName_FormatsCorrectly(
            double m, double s, bool useSub, DrawingUnit u, int p, string expected)
        {
            var ctx = new ScaleContext(m, s, useSub, u, p);
            ctx.BuildDimStyleName().Should().Be(expected);
        }

        [Fact]
        public void BuildMLeaderAndTableStyleName_FollowSameConvention()
        {
            var ctx = new ScaleContext(100, 20, true, DrawingUnit.Centimeter, 1);
            ctx.BuildMLeaderStyleName().Should().Be("0-Hy-100-20-Mleader-cm-1");
            ctx.BuildTableStyleName().Should().Be("0-Hy-100-20-Table-cm");
        }

        [Fact]
        public void BuildDimStyleName_UsesMainWhenSubOff()
        {
            // UseSub=false 时 SubScale 字段被忽略，样式名中 S 段 = M
            var ctx = new ScaleContext(100, 20, false, DrawingUnit.Millimeter, 0);
            ctx.BuildDimStyleName().Should().Be("0-Hy-100-100-Dim-mm-0");
        }

        // ================================================================
        //  D. 小数位夹紧
        // ================================================================

        [Theory]
        [InlineData(DrawingUnit.Millimeter, 0, 0)]
        [InlineData(DrawingUnit.Millimeter, 3, 0)]   // 超出 → 夹到 0
        [InlineData(DrawingUnit.Millimeter, -5, 0)]
        [InlineData(DrawingUnit.Centimeter, 0, 1)]   // 低于最小 → 1
        [InlineData(DrawingUnit.Centimeter, 1, 1)]
        [InlineData(DrawingUnit.Centimeter, 2, 2)]
        [InlineData(DrawingUnit.Centimeter, 5, 2)]   // 高于最大 → 2
        [InlineData(DrawingUnit.Meter, 0, 1)]
        [InlineData(DrawingUnit.Meter, 1, 1)]
        [InlineData(DrawingUnit.Meter, 2, 2)]
        [InlineData(DrawingUnit.Meter, 3, 3)]
        [InlineData(DrawingUnit.Meter, 99, 3)]
        public void ClampPrecision_EnforcesAllowedSet(DrawingUnit u, int input, int expected)
        {
            ScaleContext.ClampPrecision(u, input).Should().Be(expected);
        }

        [Fact]
        public void Constructor_ClampsPrecisionAutomatically()
        {
            // 构造时给 mm 单位传入 3，应自动夹紧到 0
            var ctx = new ScaleContext(100, 100, false, DrawingUnit.Millimeter, 3);
            ctx.Precision.Should().Be(0);
        }

        [Fact]
        public void GetAllowedPrecisions_ReturnsExpectedSets()
        {
            ScaleContext.GetAllowedPrecisions(DrawingUnit.Millimeter).Should().Equal(new[] { 0 });
            ScaleContext.GetAllowedPrecisions(DrawingUnit.Centimeter).Should().Equal(new[] { 1, 2 });
            ScaleContext.GetAllowedPrecisions(DrawingUnit.Meter).Should().Equal(new[] { 1, 2, 3 });
        }

        // ================================================================
        //  E. Equality
        // ================================================================

        [Fact]
        public void Equality_ByValue()
        {
            var a = new ScaleContext(100, 20, true, DrawingUnit.Centimeter, 1);
            var b = new ScaleContext(100, 20, true, DrawingUnit.Centimeter, 1);
            a.Should().Be(b);
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void Inequality_OnAnyFieldDiff()
        {
            var baseCtx = new ScaleContext(100, 20, true, DrawingUnit.Centimeter, 1);
            baseCtx.Should().NotBe(new ScaleContext(101, 20, true, DrawingUnit.Centimeter, 1));
            baseCtx.Should().NotBe(new ScaleContext(100, 25, true, DrawingUnit.Centimeter, 1));
            baseCtx.Should().NotBe(new ScaleContext(100, 20, false, DrawingUnit.Centimeter, 1));
            baseCtx.Should().NotBe(new ScaleContext(100, 20, true, DrawingUnit.Meter, 1));
            baseCtx.Should().NotBe(new ScaleContext(100, 20, true, DrawingUnit.Centimeter, 2));
        }
    }
}
