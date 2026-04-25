using FluentAssertions;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Domain.Road
{
    public class CrossSectionAssignmentTests
    {
        [Fact]
        public void IsContains_Inclusive()
        {
            var a = new CrossSectionAssignment { StartStation = 0, EndStation = 100, TemplateId = System.Guid.NewGuid() };
            a.IsContains(0).Should().BeTrue();
            a.IsContains(100).Should().BeTrue();
            a.IsContains(50).Should().BeTrue();
            a.IsContains(100.01).Should().BeFalse();
        }

        [Fact]
        public void Validate_ReversedRange_StillValid()
        {
            var a = new CrossSectionAssignment { StartStation = 100, EndStation = 0, TemplateId = System.Guid.NewGuid() };
            var (ok, _) = a.Validate(150);
            ok.Should().BeTrue();
        }
    }
}
