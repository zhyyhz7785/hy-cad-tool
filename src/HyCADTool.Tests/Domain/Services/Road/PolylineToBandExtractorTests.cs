using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>M8.1 PolylineToBandExtractor 测试集。</summary>
    public class PolylineToBandExtractorTests
    {
        private static Polyline3D Rect(double w, double h)
        {
            var pts = new[]
            {
                new Point3D(0, 0, 0),
                new Point3D(w, 0, 0),
                new Point3D(w, h, 0),
                new Point3D(0, h, 0),
            };
            return new Polyline3D(pts, isClosed: true, bulges: new double[] { 0, 0, 0, 0 });
        }

        [Fact]
        public void TryExtract_BasicRectangle_Succeeds()
        {
            var poly = Rect(3.5, 0.15);
            bool ok = PolylineToBandExtractor.TryExtract(
                poly, TemplateComponentKind.Pavement, BandSide.Right, "机动车道",
                out var band, out var error);

            ok.Should().BeTrue();
            error.Should().BeNull();
            band.Width.Should().BeApproximately(3.5, 1e-6);
            band.Kind.Should().Be(TemplateComponentKind.Pavement);
            band.Side.Should().Be(BandSide.Right);
            band.Name.Should().Be("机动车道");
        }

        [Fact]
        public void TryExtract_NullPolyline_FailsWithReason()
        {
            bool ok = PolylineToBandExtractor.TryExtract(
                null, TemplateComponentKind.Pavement, BandSide.Left, "X",
                out _, out var error);
            ok.Should().BeFalse();
            error.Should().Contain("null");
        }

        [Fact]
        public void TryExtract_TooFewVertices_Fails()
        {
            var pts = new[] { new Point3D(0, 0, 0), new Point3D(1, 0, 0) };
            var poly = new Polyline3D(pts, isClosed: true, bulges: new double[] { 0, 0 });
            bool ok = PolylineToBandExtractor.TryExtract(
                poly, TemplateComponentKind.Pavement, BandSide.Left, "X", out _, out var error);
            ok.Should().BeFalse();
            error.Should().Contain("3");
        }

        [Fact]
        public void TryExtract_NotClosed_Fails()
        {
            var pts = new[]
            {
                new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(1, 1, 0),
            };
            var poly = new Polyline3D(pts, isClosed: false, bulges: new double[] { 0, 0, 0 });
            bool ok = PolylineToBandExtractor.TryExtract(
                poly, TemplateComponentKind.Pavement, BandSide.Left, "X", out _, out var error);
            ok.Should().BeFalse();
            error.Should().Contain("闭合");
        }

        [Fact]
        public void TryExtract_HasArcs_Fails()
        {
            var pts = new[]
            {
                new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(1, 1, 0), new Point3D(0, 1, 0),
            };
            var poly = new Polyline3D(pts, isClosed: true, bulges: new double[] { 0.3, 0, 0, 0 });
            bool ok = PolylineToBandExtractor.TryExtract(
                poly, TemplateComponentKind.Pavement, BandSide.Left, "X", out _, out var error);
            ok.Should().BeFalse();
            error.Should().Contain("弧");
        }

        [Fact]
        public void TryExtract_VerticalDominantShape_Fails()
        {
            // 水平 0.5 m × 垂直 5 m → 不应被识别为横向板块
            var poly = Rect(0.5, 5.0);
            bool ok = PolylineToBandExtractor.TryExtract(
                poly, TemplateComponentKind.Pavement, BandSide.Left, "X", out _, out var error);
            ok.Should().BeFalse();
            error.Should().Contain("水平");
        }

        [Fact]
        public void TryExtract_EmptyName_UsesKindName()
        {
            var poly = Rect(3, 0.1);
            bool ok = PolylineToBandExtractor.TryExtract(
                poly, TemplateComponentKind.Sidewalk, BandSide.Left, "",
                out var band, out _);
            ok.Should().BeTrue();
            band.Name.Should().Be(TemplateComponentKind.Sidewalk.ToString());
        }

        [Fact]
        public void TryExtract_HorizontalOnlyPolyline_CrossSlopeIsZero()
        {
            var poly = Rect(4.0, 0);  // 完全水平矩形 → height=0 → slope=0
            bool ok = PolylineToBandExtractor.TryExtract(
                poly, TemplateComponentKind.Pavement, BandSide.Left, "x", out var band, out var error);

            // 0 height 会触发"水平跨度 <= height*0.5" 的反相验证？
            // Rect(4, 0) 垂直跨度 = 0，width > height*0.5 = 0 成立 → ok
            // 但 width < 1e-6 不会 → 继续
            // 关键：测试只保证跟 algorithm 行为一致
            ok.Should().BeTrue();
            band.CrossSlopePct.Should().Be(0);
        }

        [Fact]
        public void TryExtract_UsesBoundingBoxWidth()
        {
            var poly = Rect(3.7, 0.2);
            PolylineToBandExtractor.TryExtract(poly, TemplateComponentKind.Pavement, BandSide.Left, "x",
                out var band, out _);
            band.Width.Should().BeApproximately(3.7, 1e-6);
        }

        [Fact]
        public void ApplyToDesign_CreatesFirstTemplateWhenEmpty()
        {
            var design = new RoadDesign();
            var band = CrossSectionBand.Lane(3.5);
            var tid = PolylineToBandExtractor.ApplyToDesign(design, band);
            tid.Should().NotBe(System.Guid.Empty);
            design.Templates.Should().HaveCount(1);
            design.Templates[0].Points.Should().HaveCount(1);
        }

        [Fact]
        public void ApplyToDesign_AppendsToExistingTemplate()
        {
            var design = new RoadDesign();
            design.Templates.Add(new Template { Name = "already" });
            var band = CrossSectionBand.Sidewalk(2.0);
            var tid = PolylineToBandExtractor.ApplyToDesign(design, band);
            tid.Should().Be(design.Templates[0].Id);
            design.Templates.Should().HaveCount(1);
            design.Templates[0].Points.Should().HaveCount(1);
        }

        [Fact]
        public void ApplyToDesign_NullDesign_Throws()
        {
            var band = CrossSectionBand.Lane(3);
            System.Action act = () => PolylineToBandExtractor.ApplyToDesign(null, band);
            act.Should().Throw<System.ArgumentNullException>();
        }
    }
}
