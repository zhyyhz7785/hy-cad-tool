using System;
using System.Linq;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Presentation.ViewModels.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Presentation.ViewModels.Road
{
    public class CrossSectionDesignerViewModelTests
    {
        private static CrossSectionDesignerViewModel NewVmWithArterial()
            => new CrossSectionDesignerViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());

        [Fact]
        public void Constructor_WithNull_LoadsArterialPreset()
        {
            var vm = new CrossSectionDesignerViewModel();

            vm.LeftBands.Should().NotBeEmpty();
            vm.RightBands.Should().NotBeEmpty();
            vm.DesignSpeed.Should().BeGreaterThan(0);
            vm.ScaleDenominator.Should().BeGreaterThan(0);
            vm.TotalWidth.Should().BeGreaterThan(0);
            vm.LastFigure.Should().NotBeNull();
            vm.LastReport.Should().NotBeNull();
        }

        [Fact]
        public void LoadLayout_ReplacesAllFields_AndFiresPreviewOnce()
        {
            var vm = NewVmWithArterial();
            int preview = 0;
            vm.PreviewRequested += (_, __) => preview++;

            vm.LoadLayout(CrossSectionPresets.CreateCjj37LocalRoad());

            // 加载过程中只应触发一次预览（LoadLayout 末尾一次）
            preview.Should().Be(1);
            vm.LeftBands.Should().NotBeEmpty();
            vm.RightBands.Should().NotBeEmpty();
            vm.Title.Should().Contain("支路");
        }

        [Fact]
        public void PropertyChange_OnBandRow_TriggersRecalcAndPreview()
        {
            var vm = NewVmWithArterial();
            int preview = 0;
            vm.PreviewRequested += (_, __) => preview++;

            var first = vm.LeftBands.First();
            first.Width = first.Width + 1.0;

            preview.Should().BeGreaterOrEqualTo(1);
            vm.LastLayout.LeftBands.First().Width.Should().BeApproximately(first.Width, 1e-9);
        }

        [Fact]
        public void AddLeftBandCommand_AppendsLaneBand()
        {
            var vm = NewVmWithArterial();
            int beforeCount = vm.LeftBands.Count;

            vm.AddLeftBandCommand.Execute(null);

            vm.LeftBands.Should().HaveCount(beforeCount + 1);
            vm.SelectedBand.Should().Be(vm.LeftBands.Last());
        }

        [Fact]
        public void RemoveBandCommand_RespectsSelection()
        {
            var vm = NewVmWithArterial();
            vm.RemoveBandCommand.CanExecute(null).Should().BeFalse("未选中时不能删除");

            vm.SelectedBand = vm.LeftBands.First();
            int before = vm.LeftBands.Count;

            vm.RemoveBandCommand.Execute(null);

            vm.LeftBands.Should().HaveCount(before - 1);
            vm.SelectedBand.Should().BeNull();
        }

        [Fact]
        public void MoveUp_MoveDown_ReorderCurrentCollection()
        {
            var vm = NewVmWithArterial();
            if (vm.LeftBands.Count < 2) return;

            var second = vm.LeftBands[1];
            vm.SelectedBand = second;
            vm.MoveUpCommand.Execute(null);
            vm.LeftBands[0].Should().BeSameAs(second);

            vm.MoveDownCommand.Execute(null);
            vm.LeftBands[1].Should().BeSameAs(second);
        }

        [Fact]
        public void Mirror_On_KeepsLeftAndRight_Symmetric()
        {
            var vm = NewVmWithArterial();
            vm.IsMirror = true;

            vm.LeftBands.First().Width = 99.0;

            vm.RightBands.First().Width.Should().BeApproximately(99.0, 1e-9);
            vm.LastLayout.IsSymmetric.Should().BeTrue();
        }

        [Fact]
        public void Mirror_On_Add_AutoAddsOpposite()
        {
            var vm = NewVmWithArterial();
            vm.IsMirror = true;
            int leftBefore = vm.LeftBands.Count;
            int rightBefore = vm.RightBands.Count;

            vm.AddLeftBandCommand.Execute(null);

            vm.LeftBands.Should().HaveCount(leftBefore + 1);
            vm.RightBands.Should().HaveCount(rightBefore + 1);
        }

        [Fact]
        public void Mirror_On_Remove_AutoRemovesOpposite()
        {
            var vm = NewVmWithArterial();
            vm.IsMirror = true;
            int leftBefore = vm.LeftBands.Count;
            int rightBefore = vm.RightBands.Count;
            vm.SelectedBand = vm.LeftBands.First();

            vm.RemoveBandCommand.Execute(null);

            vm.LeftBands.Should().HaveCount(leftBefore - 1);
            vm.RightBands.Should().HaveCount(rightBefore - 1);
        }

        [Fact]
        public void NegativeMedianWidth_ClampedToZero()
        {
            var vm = NewVmWithArterial();
            vm.CenterMedianWidth = -5;
            vm.CenterMedianWidth.Should().Be(0);
        }

        [Fact]
        public void DesignSpeed_Update_UpdatesReportThresholds()
        {
            var vm = NewVmWithArterial();
            vm.DesignSpeed = 40;
            vm.DesignSpeed.Should().Be(40);
            vm.LastLayout.DesignSpeed.Should().Be(40);
        }

        [Fact]
        public void ScaleDenominator_Invalid_FallsBackTo100()
        {
            var vm = NewVmWithArterial();
            vm.ScaleDenominator = 0;
            vm.ScaleDenominator.Should().Be(100);

            vm.ScaleDenominator = -1;
            vm.ScaleDenominator.Should().Be(100);
        }

        [Fact]
        public void ConfirmCommand_IsAlwaysExecutable_EvenWithViolations()
        {
            var vm = NewVmWithArterial();
            vm.ConfirmCommand.CanExecute(null).Should().BeTrue();

            vm.LeftBands.Clear();
            vm.RightBands.Clear();
            vm.Recalculate();

            vm.ConfirmCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void Confirm_WithAllPass_EmitsConfirmed_AndClose_WithoutCallback()
        {
            var vm = NewVmWithArterial();
            vm.AllChecksPassed.Should().BeTrue();

            int callback = 0;
            vm.NonCompliantConfirm = _ => { callback++; return true; };

            CrossSectionDesignerResult result = null;
            bool? closed = null;
            vm.Confirmed += (_, r) => result = r;
            vm.CloseRequested += (_, b) => closed = b;

            vm.ConfirmCommand.Execute(null);

            callback.Should().Be(0);
            result.Should().NotBeNull();
            result.Template.Should().NotBeNull();
            result.Figure.Should().NotBeNull();
            result.Layout.Should().NotBeNull();
            closed.Should().Be(true);
        }

        [Fact]
        public void Confirm_WithViolations_AbortsWhenCallbackRejects()
        {
            var vm = NewVmWithArterial();
            // 造违规：车道宽度大到离谱
            vm.LeftBands.First().Width = 20;
            vm.Recalculate();
            vm.AllChecksPassed.Should().BeFalse();

            string received = null;
            vm.NonCompliantConfirm = s => { received = s; return false; };
            bool? closed = null;
            vm.CloseRequested += (_, b) => closed = b;

            vm.ConfirmCommand.Execute(null);

            received.Should().NotBeNullOrWhiteSpace();
            received.Should().Contain("未通过");
            closed.Should().BeNull("用户拒绝，窗口不应关闭");
        }

        [Fact]
        public void Confirm_WithViolations_ProceedsWhenCallbackAccepts()
        {
            var vm = NewVmWithArterial();
            vm.LeftBands.First().Width = 20;
            vm.Recalculate();
            vm.AllChecksPassed.Should().BeFalse();

            vm.NonCompliantConfirm = _ => true;
            bool? closed = null;
            vm.CloseRequested += (_, b) => closed = b;

            vm.ConfirmCommand.Execute(null);

            closed.Should().Be(true);
        }

        [Fact]
        public void Cancel_EmitsCancelled_AndCloseWithFalse()
        {
            var vm = NewVmWithArterial();
            bool cancelled = false;
            bool? closed = null;
            vm.Cancelled += (_, __) => cancelled = true;
            vm.CloseRequested += (_, b) => closed = b;

            vm.CancelCommand.Execute(null);

            cancelled.Should().BeTrue();
            closed.Should().Be(false);
        }

        [Fact]
        public void LoadPresetCommand_Switches_Layout()
        {
            var vm = NewVmWithArterial();
            var local = vm.Presets.First(p => p.DisplayName.Contains("支路"));

            vm.LoadPresetCommand.Execute(local);

            vm.Title.Should().Contain("支路");
            vm.TotalWidth.Should().BeLessThan(50);
        }

        [Fact]
        public void ConfirmedTemplate_UsesExistingId_WhenProvided()
        {
            var id = Guid.NewGuid();
            var vm = new CrossSectionDesignerViewModel(
                CrossSectionPresets.CreateCjj37UrbanArterial(),
                existingTemplateId: id);

            CrossSectionDesignerResult result = null;
            vm.Confirmed += (_, r) => result = r;

            vm.ConfirmCommand.Execute(null);

            result.Should().NotBeNull();
            result.Template.Id.Should().Be(id);
        }

        [Fact]
        public void RecalculateIsIdempotent_DoesNotExplode_OnEmptyBands()
        {
            var vm = NewVmWithArterial();
            vm.LeftBands.Clear();
            vm.RightBands.Clear();
            // 确保事件链不炸
            vm.Invoking(v => v.Recalculate()).Should().NotThrow();
            vm.TotalWidth.Should().Be(vm.CenterMedianWidth);
        }
    }
}
