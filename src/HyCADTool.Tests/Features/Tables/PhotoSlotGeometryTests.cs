using FluentAssertions;
using HyCAD.Tables.Layout;
using HyCADTool.Features.Tables.Infrastructure.AutoCad;
using Xunit;

namespace HyCADTool.Tests.Features.Tables
{
    public sealed class PhotoSlotGeometryTests
    {
        [Fact]
        public void TryGetInnerRect_inset_2mm()
        {
            var bounds = new LayoutRect(0, 80, 100, 0);

            AcadTablePhotoSlotRenderer.TryGetInnerRect(bounds, 2, out var inner).Should().BeTrue();
            inner.Left.Should().Be(2);
            inner.Right.Should().Be(98);
            inner.Top.Should().Be(78);
            inner.Bottom.Should().Be(2);
            inner.Width.Should().Be(96);
            inner.Height.Should().Be(76);
        }

        [Fact]
        public void TryGetInnerRect_too_large_inset_fails()
        {
            var bounds = new LayoutRect(0, 10, 20, 0);

            AcadTablePhotoSlotRenderer.TryGetInnerRect(bounds, 15, out _).Should().BeFalse();
        }

        [Fact]
        public void GetLabelBounds_uses_inner_when_valid()
        {
            var bounds = new LayoutRect(0, 80, 100, 0);
            var labelBounds = AcadTablePhotoSlotRenderer.GetLabelBounds(bounds, 2);

            labelBounds.Center.X.Should().Be(50);
            labelBounds.Center.Y.Should().Be(40);
        }
    }
}
