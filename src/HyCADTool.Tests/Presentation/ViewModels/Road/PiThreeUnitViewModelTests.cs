using System;
using System.Collections.Generic;
using FluentAssertions;
using HyCAD.Geometry;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Features.Road.PlanAlignment.ViewModels;
using Xunit;

namespace HyCADTool.Tests.Presentation.ViewModels.Road
{
    public class PiThreeUnitViewModelTests
    {
        private static IReadOnlyList<PiElement> BuildSamplePi()
        {
            return new List<PiElement>
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(300, 0), radius: 200, spiralIn: 70, spiralOut: 70),
                new PiElement(new Point2D(300, 300)),
            };
        }

        /// <summary>
        /// 缓转角 30° + 充足直线段的样本，用来构造"所有 6 项全部通过"的理想场景。
        /// θ=30°, R=300, Ls=80, T ≈ 300·tan15° + 40 ≈ 120m，前后直线各 500 够用。
        /// </summary>
        private static IReadOnlyList<PiElement> BuildAllPassSamplePi()
        {
            // PI0=(0,0), PI1=(500,0), PI2 位于 PI1 方向 30° 的 500m 处
            double rad = 30.0 * Math.PI / 180.0;
            var pi2 = new Point2D(500 + 500 * Math.Cos(rad), 500 * Math.Sin(rad));
            return new List<PiElement>
            {
                new PiElement(new Point2D(0, 0)),
                new PiElement(new Point2D(500, 0), radius: 300, spiralIn: 80, spiralOut: 80),
                new PiElement(pi2),
            };
        }

        [Fact]
        public void Constructor_Throws_WhenPiIndexIsEndpoint()
        {
            var elements = BuildSamplePi();
            Action act = () => new PiThreeUnitViewModel(elements, 0);
            act.Should().Throw<ArgumentOutOfRangeException>();

            Action act2 = () => new PiThreeUnitViewModel(elements, elements.Count - 1);
            act2.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Fact]
        public void Constructor_InitializesInputsFromPi()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1);

            vm.Radius.Should().Be(200);
            vm.SpiralIn.Should().Be(70);
            vm.SpiralOut.Should().Be(70);
            vm.IsSymmetric.Should().BeTrue();
            vm.Title.Should().Contain("JD1");
            vm.TotalPi.Should().Be(3);
            vm.PiIndex.Should().Be(1);
        }

        [Fact]
        public void Constructor_PrecomputesDerivedValues()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1);

            vm.TurnAngleDeg.Should().BeApproximately(90, 1e-6);
            vm.PrevTangent.Should().BeApproximately(300, 1e-6);
            vm.NextTangent.Should().BeApproximately(300, 1e-6);
            vm.T1.Should().BeGreaterThan(0);
            vm.T2.Should().BeGreaterThan(0);
            vm.Ly.Should().BeGreaterThan(0);
        }

        [Fact]
        public void SymmetryLock_AutoPropagatesSpiralIn_ToSpiralOut()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1)
            {
                IsSymmetric = true,
            };

            vm.SpiralIn = 100;

            vm.SpiralOut.Should().Be(100);
        }

        [Fact]
        public void AsymmetricMode_SpiralValuesIndependent()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1)
            {
                IsSymmetric = false,
            };

            vm.SpiralIn = 80;
            vm.SpiralOut = 120;

            vm.SpiralIn.Should().Be(80);
            vm.SpiralOut.Should().Be(120);
            vm.LastReport.Items.Should().Contain(i => i.Name.Contains("对称") && !i.Passed);
        }

        [Fact]
        public void PreviewRequested_FiresWheneverInputsChange()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1);
            int count = 0;
            PiDesignResult? lastResult = null;
            vm.PreviewRequested += (_, result) => { count++; lastResult = result; };

            vm.Radius = 250;
            vm.SpiralIn = 60;
            // 对称锁已把 SpiralOut 同步到 60，这里显式赋不同值触发一次独立重算
            vm.IsSymmetric = false;
            vm.SpiralOut = 80;

            count.Should().BeGreaterOrEqualTo(3);
            lastResult.HasValue.Should().BeTrue();
            lastResult.Value.Polyline.VertexCount.Should().BeGreaterThan(2);
        }

        [Fact]
        public void Confirm_ProducesPiElementAndRaisesEvents()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1);
            PiElement? confirmed = null;
            bool? closed = null;
            vm.Confirmed += (_, pi) => confirmed = pi;
            vm.CloseRequested += (_, b) => closed = b;

            vm.Radius = 300;
            vm.SpiralIn = 80;
            vm.SpiralOut = 80;

            vm.ConfirmCommand.Execute(null);

            confirmed.HasValue.Should().BeTrue();
            confirmed.Value.Radius.Should().Be(300);
            confirmed.Value.SpiralIn.Should().Be(80);
            confirmed.Value.SpiralOut.Should().Be(80);
            closed.Should().Be(true);
            vm.ConfirmedElement.HasValue.Should().BeTrue();
        }

        [Fact]
        public void Cancel_RaisesEventsWithoutSettingConfirmed()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1);
            bool cancelled = false;
            bool? closed = null;
            vm.Cancelled += (_, __) => cancelled = true;
            vm.CloseRequested += (_, b) => closed = b;

            vm.CancelCommand.Execute(null);

            cancelled.Should().BeTrue();
            closed.Should().Be(false);
            vm.ConfirmedElement.HasValue.Should().BeFalse();
        }

        [Fact]
        public void CheckItems_ReflectCurrentInputs_WithSixEntries()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1)
            {
                DesignSpeed = 60,
            };

            vm.CheckItems.Should().HaveCount(6);
        }

        [Fact]
        public void ConfirmCommand_IsAlwaysExecutable_EvenWithViolations()
        {
            // v1.1 变更：不再用 CanExecute 硬卡用户。合规靠二次确认弹窗 + 修复建议承担。
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1);

            vm.ConfirmCommand.CanExecute(null).Should().BeTrue();

            // 把参数改到肯定不合规（R=10 < R_min 各档）：依然 CanExecute
            vm.Radius = 10;
            vm.SpiralIn = 1;
            vm.SpiralOut = 1;
            vm.AllChecksPassed.Should().BeFalse();
            vm.ConfirmCommand.CanExecute(null).Should().BeTrue();
        }

        [Fact]
        public void Confirm_WithViolations_UsesCallback_AbortsWhenRejected()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1);
            vm.Radius = 10;
            vm.SpiralIn = 1;
            vm.SpiralOut = 1;
            vm.AllChecksPassed.Should().BeFalse();

            string receivedSummary = null;
            vm.NonCompliantConfirm = summary => { receivedSummary = summary; return false; };

            bool? closed = null;
            vm.CloseRequested += (_, b) => closed = b;

            vm.ConfirmCommand.Execute(null);

            receivedSummary.Should().NotBeNullOrWhiteSpace();
            receivedSummary.Should().Contain("未通过");
            closed.Should().BeNull("用户拒绝，窗口不应关闭");
            vm.ConfirmedElement.HasValue.Should().BeFalse();
        }

        [Fact]
        public void Confirm_WithViolations_UsesCallback_ProceedsWhenAccepted()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1);
            vm.Radius = 10;
            vm.SpiralIn = 1;
            vm.SpiralOut = 1;

            vm.NonCompliantConfirm = _ => true;

            bool? closed = null;
            vm.CloseRequested += (_, b) => closed = b;

            vm.ConfirmCommand.Execute(null);

            closed.Should().Be(true);
            vm.ConfirmedElement.HasValue.Should().BeTrue();
            vm.ConfirmedElement.Value.Radius.Should().Be(10);
        }

        [Fact]
        public void Confirm_WhenAllPassed_DoesNotInvokeCallback()
        {
            // 必须用"肯定全过"的样本（转角 30°、前后直线 500）
            var vm = new PiThreeUnitViewModel(BuildAllPassSamplePi(), 1)
            {
                DesignSpeed = 60,
            };
            vm.AllChecksPassed.Should().BeTrue();

            int callbackInvocations = 0;
            vm.NonCompliantConfirm = _ => { callbackInvocations++; return true; };

            vm.ConfirmCommand.Execute(null);

            callbackInvocations.Should().Be(0);
            vm.ConfirmedElement.HasValue.Should().BeTrue();
        }

        [Fact]
        public void DesignSpeed_Change_TriggersRecalcWithUpdatedThresholds()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1)
            {
                DesignSpeed = 40,
            };
            var before = vm.AllChecksPassed;

            vm.DesignSpeed = 80;

            // 标准提高后报告应该被重新生成
            vm.LastReport.DesignSpeed.Should().Be(80);
        }

        [Fact]
        public void NegativeInputs_ClampedToZero()
        {
            var vm = new PiThreeUnitViewModel(BuildSamplePi(), 1)
            {
                Radius = -50,
                SpiralIn = -10,
            };

            vm.Radius.Should().Be(0);
            vm.SpiralIn.Should().Be(0);
        }
    }
}
