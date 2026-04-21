using System;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.ValueObjects.Road
{
    /// <summary>
    /// <see cref="CrossSectionBand"/> / <see cref="CrossSectionLayout"/> 的基础行为。
    ///
    /// 这一层是 M3 横断面设计器的"用户输入 VO"，所有上游（VM/Preset/Checker/Builder）都依赖它，
    /// 必须保证不可变性 + 构造器参数校验 + 便捷工厂的 Kind 正确。
    /// </summary>
    public class CrossSectionBandTests
    {
        [Fact]
        public void Lane_Factory_UsesPavementKind()
        {
            var b = CrossSectionBand.Lane(width: 3.5, slopePct: 1.5, side: BandSide.Left, name: "机动车道1");
            b.Kind.Should().Be(TemplateComponentKind.Pavement);
            b.Width.Should().BeApproximately(3.5, 1e-9);
            b.CrossSlopePct.Should().BeApproximately(1.5, 1e-9);
            b.Side.Should().Be(BandSide.Left);
            b.Name.Should().Be("机动车道1");
        }

        [Fact]
        public void Sidewalk_Factory_UsesSidewalkKind()
            => CrossSectionBand.Sidewalk(width: 3.0).Kind.Should().Be(TemplateComponentKind.Sidewalk);

        [Fact]
        public void NonMotor_Factory_UsesNonMotorizedKind()
            => CrossSectionBand.NonMotor(width: 3.5).Kind.Should().Be(TemplateComponentKind.NonMotorized);

        [Fact]
        public void Kerb_Factory_Has_ZeroSlope()
        {
            var b = CrossSectionBand.Kerb(width: 0.15);
            b.Kind.Should().Be(TemplateComponentKind.Kerb);
            b.CrossSlopePct.Should().Be(0);
        }

        [Fact]
        public void GreenStrip_Factory_Has_ZeroSlope()
        {
            var b = CrossSectionBand.GreenStrip(width: 1.5);
            b.Kind.Should().Be(TemplateComponentKind.GreenStrip);
            b.CrossSlopePct.Should().Be(0);
        }

        [Fact]
        public void Median_Factory_HasCenterSide()
        {
            var b = CrossSectionBand.Median(width: 2.0);
            b.Kind.Should().Be(TemplateComponentKind.MedianStrip);
            b.Side.Should().Be(BandSide.Center);
            b.CrossSlopePct.Should().Be(0);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Constructor_Rejects_NonPositiveWidth(double width)
        {
            Action a = () => new CrossSectionBand("lane", TemplateComponentKind.Pavement, width, 1.5, BandSide.Left);
            a.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Constructor_Rejects_InfiniteSlope()
        {
            Action a = () => new CrossSectionBand("lane", TemplateComponentKind.Pavement, 3.5, double.PositiveInfinity, BandSide.Left);
            a.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Constructor_Rejects_InsaneSlope()
        {
            Action a = () => new CrossSectionBand("lane", TemplateComponentKind.Pavement, 3.5, 99.0, BandSide.Left);
            a.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Constructor_Rejects_EmptyName()
        {
            Action a = () => new CrossSectionBand(" ", TemplateComponentKind.Pavement, 3.5, 1.5, BandSide.Left);
            a.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void WithWidth_KeepsOtherFields()
        {
            var b = CrossSectionBand.Lane(3.5);
            var nb = b.WithWidth(3.75);
            nb.Width.Should().Be(3.75);
            nb.Name.Should().Be(b.Name);
            nb.Kind.Should().Be(b.Kind);
            nb.CrossSlopePct.Should().Be(b.CrossSlopePct);
        }

        [Fact]
        public void WithSlope_KeepsOtherFields()
        {
            var nb = CrossSectionBand.Lane(3.5).WithSlope(2.0);
            nb.CrossSlopePct.Should().Be(2.0);
        }

        [Fact]
        public void WithKind_KeepsOtherFields()
        {
            var nb = CrossSectionBand.Lane(3.5).WithKind(TemplateComponentKind.Shoulder);
            nb.Kind.Should().Be(TemplateComponentKind.Shoulder);
        }

        [Fact]
        public void WithSide_SwapsSide()
        {
            var nb = CrossSectionBand.Lane(3.5, side: BandSide.Left).WithSide(BandSide.Right);
            nb.Side.Should().Be(BandSide.Right);
        }

        [Fact]
        public void Equals_Works_OnValue()
        {
            var a = CrossSectionBand.Lane(3.5, 1.5);
            var b = CrossSectionBand.Lane(3.5, 1.5);
            a.Should().Be(b);
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        // ---------- StructureScheme 字段扩展（Phase 1：仅 VM/UI 侧使用，JSON 不持久化）----------

        [Fact]
        public void StructureScheme_DefaultsTo_Null()
        {
            var b = CrossSectionBand.Lane(3.5);
            b.StructureScheme.Should().BeNull();
        }

        [Fact]
        public void WithStructureScheme_AssignsAndKeepsOtherFields()
        {
            var scheme = new StructureLayerScheme { Name = "路面结构" };
            var b = CrossSectionBand.Lane(3.5, 1.5, name: "机动车道1");
            var nb = b.WithStructureScheme(scheme);

            nb.StructureScheme.Should().BeSameAs(scheme);
            nb.Width.Should().Be(b.Width);
            nb.CrossSlopePct.Should().Be(b.CrossSlopePct);
            nb.Kind.Should().Be(b.Kind);
            nb.Name.Should().Be(b.Name);
        }

        [Fact]
        public void WithWidth_KeepsStructureScheme()
        {
            var scheme = new StructureLayerScheme { Name = "路面结构" };
            var b = CrossSectionBand.Lane(3.5).WithStructureScheme(scheme);
            var nb = b.WithWidth(4.0);
            nb.StructureScheme.Should().BeSameAs(scheme);
        }

        [Fact]
        public void Equals_ComparesStructureScheme_ByReference()
        {
            var schemeA = new StructureLayerScheme { Name = "A" };
            var schemeB = new StructureLayerScheme { Name = "B" };

            var x = CrossSectionBand.Lane(3.5).WithStructureScheme(schemeA);
            var y = CrossSectionBand.Lane(3.5).WithStructureScheme(schemeA);
            var z = CrossSectionBand.Lane(3.5).WithStructureScheme(schemeB);

            x.Should().Be(y);
            x.Should().NotBe(z);
        }
    }

    public class CrossSectionLayoutTests
    {
        private static CrossSectionBand L(double w) => CrossSectionBand.Lane(w);
        private static CrossSectionBand R(double w) => CrossSectionBand.Lane(w, side: BandSide.Right);

        [Fact]
        public void Create_CalculatesTotalWidth()
        {
            var layout = CrossSectionLayout.Create(
                new[] { L(3.5), L(3.5) },
                new[] { R(3.5), R(3.5) },
                centerMedianWidth: 2.0,
                designSpeed: 60);

            layout.LeftHalfWidth.Should().Be(7.0);
            layout.RightHalfWidth.Should().Be(7.0);
            layout.TotalWidth.Should().Be(16.0);
            layout.IsSymmetric.Should().BeTrue();
        }

        [Fact]
        public void Create_ForcesSideConsistency()
        {
            // 用户把 Right 放到 LeftBands：Create 应静默修正成 Left
            var b = CrossSectionBand.Lane(3.5, side: BandSide.Right);
            var layout = CrossSectionLayout.Create(
                new[] { b },
                new[] { b },
                centerMedianWidth: 0,
                designSpeed: 60);

            layout.LeftBands[0].Side.Should().Be(BandSide.Left);
            layout.RightBands[0].Side.Should().Be(BandSide.Right);
        }

        [Fact]
        public void Create_Rejects_NegativeMedian()
        {
            Action a = () => CrossSectionLayout.Create(
                new[] { L(3.5) }, new[] { R(3.5) }, centerMedianWidth: -1, designSpeed: 60);
            a.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Create_Rejects_NonPositiveSpeed()
        {
            Action a = () => CrossSectionLayout.Create(
                new[] { L(3.5) }, new[] { R(3.5) }, centerMedianWidth: 0, designSpeed: 0);
            a.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void With_Methods_ReturnNewInstance()
        {
            var layout = CrossSectionLayout.Create(
                new[] { L(3.5) }, new[] { R(3.5) }, centerMedianWidth: 2.0, designSpeed: 60);

            var nl = layout.WithCenterMedianWidth(3.0);
            nl.CenterMedianWidth.Should().Be(3.0);
            layout.CenterMedianWidth.Should().Be(2.0); // 原实例不动
            nl.Should().NotBeSameAs(layout);

            layout.WithDesignSpeed(80).DesignSpeed.Should().Be(80);
            layout.WithScale(200).ScaleDenominator.Should().Be(200);
            layout.WithTitle("abc").Title.Should().Be("abc");
        }

        [Fact]
        public void Asymmetric_LeftRight_DetectedByIsSymmetric()
        {
            var layout = CrossSectionLayout.Create(
                new[] { L(3.5), L(3.5) },
                new[] { R(3.5) },
                centerMedianWidth: 0,
                designSpeed: 60);
            layout.IsSymmetric.Should().BeFalse();
        }
    }
}
