using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Presentation.ViewModels.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Presentation.Road
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
