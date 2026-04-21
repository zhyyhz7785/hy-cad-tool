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

        // ---------- BandRow 路面结构层（面/基/垫）----------

        [Fact]
        public void BandRow_PavementKind_InjectsDefaultStructureLayers()
        {
            var vm = NewVmWithArterial();
            var pavement = vm.LeftBands.First(r => r.Kind == TemplateComponentKind.Pavement);

            pavement.HasStructureLayers.Should().BeTrue();
            pavement.StructureLayers.Should().NotBeEmpty("Pavement 类型应在 ctor 自动注入默认面/基/垫层");
            pavement.StructureLayers.Any(l => l.LayerKind == StructureLayerKind.Surface).Should().BeTrue();
            pavement.StructureLayers.Any(l => l.LayerKind == StructureLayerKind.Base).Should().BeTrue();
        }

        [Fact]
        public void BandRow_GreenStripKind_DoesNotInjectStructureLayers()
        {
            var vm = NewVmWithArterial();
            var green = vm.LeftBands.FirstOrDefault(r => r.Kind == TemplateComponentKind.GreenStrip)
                        ?? vm.RightBands.FirstOrDefault(r => r.Kind == TemplateComponentKind.GreenStrip);

            if (green != null)
            {
                green.HasStructureLayers.Should().BeFalse();
                green.StructureLayers.Should().BeEmpty();
            }
        }

        [Fact]
        public void BandRow_ChangeKindToPavement_InjectsLayers_ChangeAway_ClearsLayers()
        {
            var vm = NewVmWithArterial();
            // 造一个已有 Pavement 条带并改到 GreenStrip
            var row = vm.LeftBands.First(r => r.Kind == TemplateComponentKind.Pavement);
            row.StructureLayers.Should().NotBeEmpty();

            row.Kind = TemplateComponentKind.GreenStrip;
            row.HasStructureLayers.Should().BeFalse();
            row.StructureLayers.Should().BeEmpty();

            row.Kind = TemplateComponentKind.Pavement;
            row.HasStructureLayers.Should().BeTrue();
            row.StructureLayers.Should().NotBeEmpty();
        }

        [Fact]
        public void BandRow_AddSurfaceLayerCommand_AppendsSurfaceLayer()
        {
            var vm = NewVmWithArterial();
            var row = vm.LeftBands.First(r => r.Kind == TemplateComponentKind.Pavement);
            int before = row.StructureLayers.Count;

            row.AddSurfaceLayerCommand.Execute(null);

            row.StructureLayers.Count.Should().Be(before + 1);
            row.StructureLayers.Last().LayerKind.Should().Be(StructureLayerKind.Surface);
        }

        [Fact]
        public void BandRow_ToBand_RoundTripsStructureScheme()
        {
            var vm = NewVmWithArterial();
            var row = vm.LeftBands.First(r => r.Kind == TemplateComponentKind.Pavement);

            var band = row.ToBand();
            band.StructureScheme.Should().NotBeNull();
            band.StructureScheme.Layers.Count.Should().Be(row.StructureLayers.Count);
        }
    }

    // =====================================================================================
    //  CrossSectionDrawViewModel 专属：OutlineRoot / 三路选中 / MedianOutlineNode
    // =====================================================================================

    public class CrossSectionDrawViewModelTests
    {
        [Fact]
        public void OutlineRoot_HasThreeSections()
        {
            var vm = new CrossSectionDrawViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());

            vm.OutlineRoot.Should().NotBeNull();
            vm.OutlineRoot.Count.Should().Be(3, "依次为：中央隔离带 / 左侧 / 右侧");
            vm.MedianNode.Should().NotBeNull();
            vm.LeftSideNode.Should().NotBeNull();
            vm.RightSideNode.Should().NotBeNull();
        }

        [Fact]
        public void SelectedOutlineNode_WhenBand_MakesSelectedBandEqual()
        {
            var vm = new CrossSectionDrawViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());
            var first = vm.LeftBands.First();

            vm.SelectedOutlineNode = first;

            vm.SelectedBand.Should().BeSameAs(first);
            vm.SelectedMedianNode.Should().BeNull();
            vm.SelectedSideNode.Should().BeNull();
            first.IsSelected.Should().BeTrue();
        }

        [Fact]
        public void SelectedOutlineNode_WhenMedian_ClearsSelectedBand()
        {
            var vm = new CrossSectionDrawViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());
            vm.SelectedOutlineNode = vm.LeftBands.First();

            vm.SelectedOutlineNode = vm.MedianNode;

            vm.SelectedBand.Should().BeNull();
            vm.SelectedMedianNode.Should().BeSameAs(vm.MedianNode);
        }

        [Fact]
        public void MedianNode_IsActive_TogglesCenterMedianWidth()
        {
            var vm = new CrossSectionDrawViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());
            vm.CenterMedianWidth = 2.0;
            vm.MedianNode.IsActive.Should().BeTrue();

            vm.MedianNode.IsActive = false;
            vm.CenterMedianWidth.Should().Be(0);
            vm.MedianNode.IsActive.Should().BeFalse();

            vm.MedianNode.IsActive = true;
            vm.CenterMedianWidth.Should().BeApproximately(2.0, 1e-9, "取消时缓存的非零宽度应恢复");
        }

        [Fact]
        public void MedianNode_Width_SyncsToCenterMedianWidth()
        {
            var vm = new CrossSectionDrawViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());
            vm.CenterMedianWidth = 2.0;

            vm.MedianNode.Width = 3.5;

            vm.CenterMedianWidth.Should().BeApproximately(3.5, 1e-9);
        }

        [Fact]
        public void SideOutlineNode_Children_MirrorsBandCollections()
        {
            var vm = new CrossSectionDrawViewModel(CrossSectionPresets.CreateCjj37UrbanArterial());

            vm.LeftSideNode.Children.Should().BeSameAs(vm.LeftBands);
            vm.RightSideNode.Children.Should().BeSameAs(vm.RightBands);
        }
    }
}
