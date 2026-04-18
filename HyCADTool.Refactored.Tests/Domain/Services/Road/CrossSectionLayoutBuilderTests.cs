using System;
using System.Linq;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Services.Road
{
    /// <summary>
    /// <see cref="CrossSectionLayoutBuilder"/>：条带 ↔ Template ↔ Figure 三向转换的几何正确性。
    /// </summary>
    public class CrossSectionLayoutBuilderTests
    {
        // 简化构造：对称双向 2 车道 + 中分带 2m
        private static CrossSectionLayout BuildSymmetricLayout()
        {
            return CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(3.5, 1.5), CrossSectionBand.Lane(3.5, 1.5) },
                new[] { CrossSectionBand.Lane(3.5, 1.5, BandSide.Right), CrossSectionBand.Lane(3.5, 1.5, BandSide.Right) },
                centerMedianWidth: 2.0,
                designSpeed: 60);
        }

        // =========================================================================
        //  ToTemplate
        // =========================================================================

        [Fact]
        public void ToTemplate_PointCount_MatchesBandsPlusMedianEdges()
        {
            var layout = BuildSymmetricLayout();
            var tpl = CrossSectionLayoutBuilder.ToTemplate(layout);

            // 左 2 外缘 + 左中心 + 右中心 + 右 2 外缘 = 6
            tpl.Points.Count.Should().Be(layout.LeftBands.Count + layout.RightBands.Count + 2);
            tpl.Components.Count.Should().Be(tpl.Points.Count - 1);
        }

        [Fact]
        public void ToTemplate_WithoutMedian_MergesCenterPoint()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(3.5), CrossSectionBand.Lane(3.5) },
                new[] { CrossSectionBand.Lane(3.5, side: BandSide.Right), CrossSectionBand.Lane(3.5, side: BandSide.Right) },
                centerMedianWidth: 0,
                designSpeed: 60);
            var tpl = CrossSectionLayoutBuilder.ToTemplate(layout);

            // 无中分带时：左 2 + 合并中心 1 + 右 2 = 5
            tpl.Points.Count.Should().Be(5);
            tpl.Components.Count.Should().Be(4);
        }

        [Fact]
        public void ToTemplate_TotalSpanMatchesLayoutTotalWidth()
        {
            var layout = BuildSymmetricLayout();
            var tpl = CrossSectionLayoutBuilder.ToTemplate(layout);

            double xs = tpl.Points.Max(p => p.HorizontalOffset) - tpl.Points.Min(p => p.HorizontalOffset);
            xs.Should().BeApproximately(layout.TotalWidth, 1e-6);
        }

        [Fact]
        public void ToTemplate_CrossSlope_Sinks_Outer_Y()
        {
            // 单条 3.5m、1.5% 的机动车道（右侧）
            var layout = CrossSectionLayout.Create(
                new CrossSectionBand[] { },
                new[] { CrossSectionBand.Lane(3.5, 1.5, BandSide.Right) },
                centerMedianWidth: 0,
                designSpeed: 60);
            var tpl = CrossSectionLayoutBuilder.ToTemplate(layout);

            // 预期 3 个点？不，左侧 0 条，右侧 1 条，中心合并 → 2 个点（中心 + 右外缘）
            tpl.Points.Count.Should().Be(2);
            double dy = tpl.Points.Last().VerticalOffset - tpl.Points.First().VerticalOffset;
            dy.Should().BeApproximately(-3.5 * 0.015, 1e-9);
        }

        [Fact]
        public void ToTemplate_AssignsSuppliedIdAndName()
        {
            var layout = BuildSymmetricLayout();
            var id = Guid.NewGuid();
            var tpl = CrossSectionLayoutBuilder.ToTemplate(layout, templateId: id, name: "我的模板");
            tpl.Id.Should().Be(id);
            tpl.Name.Should().Be("我的模板");
        }

        // =========================================================================
        //  FromTemplate  + 往返
        // =========================================================================

        [Fact]
        public void FromTemplate_Roundtrip_Preserves_Widths_And_Slopes()
        {
            var layout = BuildSymmetricLayout();
            var tpl = CrossSectionLayoutBuilder.ToTemplate(layout);

            var back = CrossSectionLayoutBuilder.FromTemplate(tpl);
            back.Should().NotBeNull();
            back!.LeftBands.Count.Should().Be(layout.LeftBands.Count);
            back.RightBands.Count.Should().Be(layout.RightBands.Count);
            back.CenterMedianWidth.Should().BeApproximately(layout.CenterMedianWidth, 1e-6);

            for (int i = 0; i < layout.LeftBands.Count; i++)
            {
                back.LeftBands[i].Width.Should().BeApproximately(layout.LeftBands[i].Width, 1e-6);
                back.LeftBands[i].CrossSlopePct.Should().BeApproximately(layout.LeftBands[i].CrossSlopePct, 1e-6);
                back.LeftBands[i].Kind.Should().Be(layout.LeftBands[i].Kind);
            }

            for (int i = 0; i < layout.RightBands.Count; i++)
            {
                back.RightBands[i].Width.Should().BeApproximately(layout.RightBands[i].Width, 1e-6);
                back.RightBands[i].CrossSlopePct.Should().BeApproximately(layout.RightBands[i].CrossSlopePct, 1e-6);
                back.RightBands[i].Kind.Should().Be(layout.RightBands[i].Kind);
            }
        }

        [Fact]
        public void FromTemplate_RejectsMalformedPoints()
        {
            // 点数据故意非单调：左外比左中心还右
            var tpl = new Template { Name = "bad" };
            var a = new TemplatePoint { Name = "a", HorizontalOffset = 0, VerticalOffset = 0 };
            var b = new TemplatePoint { Name = "b", HorizontalOffset = -1, VerticalOffset = 0 };
            tpl.Points.Add(a);
            tpl.Points.Add(b);
            tpl.Components.Add(new TemplateComponent
            {
                StartPointId = a.Id,
                EndPointId = b.Id,
                Kind = TemplateComponentKind.Pavement,
            });

            var back = CrossSectionLayoutBuilder.FromTemplate(tpl);
            back.Should().BeNull();
        }

        // =========================================================================
        //  ToFigure
        // =========================================================================

        [Fact]
        public void ToFigure_PanelCount_Matches_Bands_AndOptionalMedian()
        {
            var layout = BuildSymmetricLayout();
            var fig = CrossSectionLayoutBuilder.ToFigure(layout);

            fig.Panels.Count.Should().Be(layout.LeftBands.Count + layout.RightBands.Count + 1);

            var layoutNoMedian = layout.WithCenterMedianWidth(0);
            var figNoMedian = CrossSectionLayoutBuilder.ToFigure(layoutNoMedian);
            figNoMedian.Panels.Count.Should().Be(layoutNoMedian.LeftBands.Count + layoutNoMedian.RightBands.Count);
        }

        [Fact]
        public void ToFigure_VerticesMonotonicInX()
        {
            var layout = BuildSymmetricLayout();
            var fig = CrossSectionLayoutBuilder.ToFigure(layout);
            fig.Vertices.Should().HaveCountGreaterThan(2);
            for (int i = 1; i < fig.Vertices.Count; i++)
            {
                fig.Vertices[i].X.Should().BeGreaterOrEqualTo(fig.Vertices[i - 1].X,
                    "顶点应按 X 从最左到最右单调递增");
            }
        }

        [Fact]
        public void ToFigure_TopLabelCountMatchesBandsPlusMedian()
        {
            var layout = BuildSymmetricLayout();
            var fig = CrossSectionLayoutBuilder.ToFigure(layout);
            fig.TopLabels.Count.Should().Be(layout.LeftBands.Count + layout.RightBands.Count + 1);

            var noMed = layout.WithCenterMedianWidth(0);
            var figNoMed = CrossSectionLayoutBuilder.ToFigure(noMed);
            figNoMed.TopLabels.Count.Should().Be(noMed.LeftBands.Count + noMed.RightBands.Count);
        }

        [Fact]
        public void ToFigure_DimensionSegments_IncludeAllTiers()
        {
            var layout = BuildSymmetricLayout();
            var fig = CrossSectionLayoutBuilder.ToFigure(layout);
            fig.DimensionSegments.Any(d => d.Tier == 0).Should().BeTrue();
            fig.DimensionSegments.Any(d => d.Tier == 1).Should().BeTrue();
            fig.DimensionSegments.Any(d => d.Tier == 2).Should().BeTrue();
        }

        [Fact]
        public void ToFigure_SlopeLabels_OnlyForSloped_Lanes()
        {
            var layout = CrossSectionLayout.Create(
                new[] { CrossSectionBand.Lane(3.5, 1.5), CrossSectionBand.Kerb(0.15) },
                new[] { CrossSectionBand.Lane(3.5, 1.5, BandSide.Right) },
                centerMedianWidth: 0,
                designSpeed: 60);
            var fig = CrossSectionLayoutBuilder.ToFigure(layout);
            // 只有 2 条有坡的车道
            fig.SlopeLabels.Count.Should().Be(2);
        }

        [Fact]
        public void ToFigure_TotalWidth_ReflectsLayoutTotal()
        {
            var layout = BuildSymmetricLayout();
            var fig = CrossSectionLayoutBuilder.ToFigure(layout);
            fig.TotalWidth.Should().BeApproximately(layout.TotalWidth, 1e-9);
        }

        [Fact]
        public void ToFigure_Title_IncludesScaleSuffix()
        {
            var layout = BuildSymmetricLayout().WithScale(200).WithTitle("我的断面");
            var fig = CrossSectionLayoutBuilder.ToFigure(layout);
            fig.Title.Text.Should().Contain("1:200");
            fig.Title.Text.Should().Contain("我的断面");
        }
    }
}
