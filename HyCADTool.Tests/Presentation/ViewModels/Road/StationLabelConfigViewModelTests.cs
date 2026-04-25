using FluentAssertions;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Presentation.ViewModels.Road;
using Xunit;

namespace HyCADTool.Tests.Presentation.ViewModels.Road
{
    /// <summary>
    /// T6 验收：<see cref="StationLabelConfigViewModel"/> 的数据流与关闭事件。
    /// </summary>
    public class StationLabelConfigViewModelTests
    {
        [Fact]
        public void Ctor_CopiesInitialValues()
        {
            var initial = new RoadStationLabelOptions
            {
                MainInterval = 50,
                SubInterval = 10,
                TickLengthMain = 6,
                TickLengthSub = 2,
                TextHeight = 3.5,
                TextMargin = 0.8,
                RotateTextAlongTangent = false,
                TextSide = StationTextSide.Right,
            };
            var vm = new StationLabelConfigViewModel(initial);
            vm.MainInterval.Should().Be(50);
            vm.SubInterval.Should().Be(10);
            vm.TickLengthMain.Should().Be(6);
            vm.TickLengthSub.Should().Be(2);
            vm.TextHeight.Should().Be(3.5);
            vm.TextMargin.Should().Be(0.8);
            vm.RotateTextAlongTangent.Should().BeFalse();
            vm.SideIsLeft.Should().BeFalse();
        }

        [Fact]
        public void Ctor_Null_UsesDefault()
        {
            var vm = new StationLabelConfigViewModel(null);
            vm.MainInterval.Should().Be(20);
            vm.SubInterval.Should().Be(5);
            vm.SideIsLeft.Should().BeTrue();
        }

        [Fact]
        public void Snapshot_RoundTripsSide()
        {
            var vm = new StationLabelConfigViewModel(new RoadStationLabelOptions());
            vm.SideIsLeft = false;
            var snap = vm.Snapshot();
            snap.TextSide.Should().Be(StationTextSide.Right);

            vm.SideIsLeft = true;
            snap = vm.Snapshot();
            snap.TextSide.Should().Be(StationTextSide.Left);
        }

        [Fact]
        public void Confirm_EmitsCloseRequested_True()
        {
            var vm = new StationLabelConfigViewModel(new RoadStationLabelOptions());
            bool? result = null;
            vm.CloseRequested += (_, r) => result = r;
            vm.ConfirmCommand.Execute(null);
            result.Should().Be(true);
        }

        [Fact]
        public void Confirm_InvalidConfig_Blocks()
        {
            var vm = new StationLabelConfigViewModel(new RoadStationLabelOptions());
            vm.MainInterval = -1;
            bool raised = false;
            vm.CloseRequested += (_, __) => raised = true;
            vm.ConfirmCommand.Execute(null);
            raised.Should().BeFalse();
            vm.StatusMessage.Should().StartWith("校验失败");
        }

        [Fact]
        public void Cancel_EmitsFalse()
        {
            var vm = new StationLabelConfigViewModel(new RoadStationLabelOptions());
            bool? result = null;
            vm.CloseRequested += (_, r) => result = r;
            vm.CancelCommand.Execute(null);
            result.Should().Be(false);
        }

        [Fact]
        public void Reset_RestoresFactoryDefault()
        {
            var vm = new StationLabelConfigViewModel(new RoadStationLabelOptions { MainInterval = 888 });
            vm.ResetCommand.Execute(null);
            vm.MainInterval.Should().Be(20);
            vm.SideIsLeft.Should().BeTrue();
            vm.RotateTextAlongTangent.Should().BeTrue();
            vm.StatusMessage.Should().Contain("默认");
        }
    }
}
