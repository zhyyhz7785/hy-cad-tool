using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Presentation.ViewModels.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Presentation.ViewModels.Road
{
    public class BandRowInsertTests
    {
        [Fact]
        public void InsertBandAfterSelected_InsertsBelowCurrentBand()
        {
            var vm = new CrossSectionDrawViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());
            vm.SelectedBand = vm.LeftBands[0];

            vm.InsertBandAfterSelectedCommand.Execute(TemplateComponentKind.GreenStrip);

            vm.LeftBands[1].Kind.Should().Be(TemplateComponentKind.GreenStrip);
            vm.SelectedBand.Should().Be(vm.LeftBands[1]);
        }

        [Fact]
        public void InsertBandAfterSelected_WhenNoBandSelected_DefaultsToLeftSide()
        {
            var vm = new CrossSectionDrawViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());
            vm.SelectedBand = null;

            vm.InsertBandAfterSelectedCommand.Execute(TemplateComponentKind.Sidewalk);

            vm.LeftBands.Should().NotBeEmpty();
            vm.LeftBands[vm.LeftBands.Count - 1].Kind.Should().Be(TemplateComponentKind.Sidewalk);
        }
    }
}
