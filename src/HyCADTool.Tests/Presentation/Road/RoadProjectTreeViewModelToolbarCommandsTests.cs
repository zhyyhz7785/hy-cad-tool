using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Features.Road.Plan.ViewModels;
using Xunit;

namespace HyCADTool.Tests.Presentation.Road
{
    public class RoadProjectTreeViewModelToolbarCommandsTests
    {
        [Fact]
        public void ToolbarCommands_WithNullHandler_ExecuteWithoutThrow()
        {
            var vm = new RoadProjectTreeViewModel(new NullRoadTreeInteractionHandler())
            {
                Project = new RoadProject { Name = "p", Designs = { new RoadDesign { ProjectName = "d" } } }
            };
            vm.NewAlignmentCommand.CanExecute(null).Should().BeTrue();
            vm.AssignCrossSectionCommand.CanExecute(null).Should().BeTrue();
            vm.GeneratePlanCommand.CanExecute(null).Should().BeTrue();
            vm.DetectIntersectionsCommand.CanExecute(null).Should().BeTrue();
            vm.NewAlignmentCommand.Execute(null);
            vm.AssignCrossSectionCommand.Execute(null);
            vm.GeneratePlanCommand.Execute(null);
            vm.DetectIntersectionsCommand.Execute(null);
        }
    }
}
