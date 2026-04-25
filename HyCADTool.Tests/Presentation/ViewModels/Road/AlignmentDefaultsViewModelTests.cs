using System;
using FluentAssertions;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Presentation.ViewModels.Road;
using Xunit;

namespace HyCADTool.Tests.Presentation.ViewModels.Road
{
    /// <summary>
    /// T4 验收：<see cref="AlignmentDefaultsViewModel"/> 的数据流与关闭事件。
    /// </summary>
    public class AlignmentDefaultsViewModelTests
    {
        [Fact]
        public void Ctor_CopiesInitialValuesIntoProperties()
        {
            var vm = new AlignmentDefaultsViewModel(new AlignmentDefaults
            {
                DefaultRadius = 120,
                DefaultSpiralIn = 40,
                DefaultSpiralOut = 50,
                DefaultStartStation = 1000.5,
            });

            vm.DefaultRadius.Should().Be(120);
            vm.DefaultSpiralIn.Should().Be(40);
            vm.DefaultSpiralOut.Should().Be(50);
            vm.DefaultStartStation.Should().Be(1000.5);
        }

        [Fact]
        public void Ctor_Null_DoesNotThrow_UsesDefaults()
        {
            var vm = new AlignmentDefaultsViewModel(null);
            vm.DefaultRadius.Should().BeGreaterThan(0);
            vm.DefaultStartStation.Should().Be(0);
        }

        [Fact]
        public void Snapshot_RoundTrips_CurrentValues()
        {
            var vm = new AlignmentDefaultsViewModel(new AlignmentDefaults());
            vm.DefaultRadius = 85;
            vm.DefaultSpiralOut = 33;
            var snap = vm.Snapshot();
            snap.DefaultRadius.Should().Be(85);
            snap.DefaultSpiralOut.Should().Be(33);
        }

        [Fact]
        public void ConfirmCommand_RaisesCloseRequested_WithTrue()
        {
            var vm = new AlignmentDefaultsViewModel(new AlignmentDefaults());
            bool? result = null;
            vm.CloseRequested += (_, r) => result = r;
            vm.ConfirmCommand.Execute(null);
            result.Should().Be(true);
        }

        [Fact]
        public void CancelCommand_RaisesCloseRequested_WithFalse()
        {
            var vm = new AlignmentDefaultsViewModel(new AlignmentDefaults());
            bool? result = null;
            vm.CloseRequested += (_, r) => result = r;
            vm.CancelCommand.Execute(null);
            result.Should().Be(false);
        }

        [Fact]
        public void ConfirmCommand_NegativeRadius_BlocksClose_SetsStatusMessage()
        {
            var vm = new AlignmentDefaultsViewModel(new AlignmentDefaults());
            vm.DefaultRadius = -10;
            bool raised = false;
            vm.CloseRequested += (_, __) => raised = true;
            vm.ConfirmCommand.Execute(null);
            raised.Should().BeFalse("校验不过时应阻止关闭");
            vm.StatusMessage.Should().StartWith("校验失败");
        }

        [Fact]
        public void ResetCommand_RestoresFactoryDefaults()
        {
            var vm = new AlignmentDefaultsViewModel(new AlignmentDefaults
            {
                DefaultRadius = 999,
                DefaultSpiralIn = 99,
                DefaultStartStation = 555,
            });
            vm.ResetCommand.Execute(null);
            var factory = new AlignmentDefaults();
            vm.DefaultRadius.Should().Be(factory.DefaultRadius);
            vm.DefaultSpiralIn.Should().Be(factory.DefaultSpiralIn);
            vm.DefaultStartStation.Should().Be(factory.DefaultStartStation);
            vm.StatusMessage.Should().Contain("默认");
        }
    }
}
