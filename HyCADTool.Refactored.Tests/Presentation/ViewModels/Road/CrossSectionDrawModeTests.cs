using FluentAssertions;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Presentation.ViewModels.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Presentation.ViewModels.Road
{
    /// <summary>M7.4 绘图模式双向属性回环。</summary>
    public class CrossSectionDrawModeTests
    {
        [Fact]
        public void Default_UseStructureThickness()
        {
            var vm = new CrossSectionDrawViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());
            vm.UseSingleLineMode.Should().BeFalse();
            vm.UseStructureThicknessMode.Should().BeTrue();
        }

        [Fact]
        public void ToggleSingleLine_UpdatesBothPropertiesExclusively()
        {
            var vm = new CrossSectionDrawViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());
            vm.UseSingleLineMode = true;
            vm.UseSingleLineMode.Should().BeTrue();
            vm.UseStructureThicknessMode.Should().BeFalse();

            vm.UseStructureThicknessMode = true;
            vm.UseSingleLineMode.Should().BeFalse();
            vm.UseStructureThicknessMode.Should().BeTrue();
        }

        [Fact]
        public void SettingStructureTrue_WhenAlreadyTrue_NoOp()
        {
            var vm = new CrossSectionDrawViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());
            int changeCount = 0;
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "UseSingleLineMode" || e.PropertyName == "UseStructureThicknessMode")
                    changeCount++;
            };

            vm.UseStructureThicknessMode = true; // 本就 true
            changeCount.Should().Be(0);
        }
    }
}
