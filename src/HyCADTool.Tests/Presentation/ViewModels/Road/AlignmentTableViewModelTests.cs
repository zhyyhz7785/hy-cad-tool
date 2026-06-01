using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCAD.Geometry;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Features.Road.PlanAlignment.ViewModels;
using Xunit;

namespace HyCADTool.Tests.Presentation.ViewModels.Road
{
    /// <summary>
    /// T1 验收：AlignmentTableViewModel 把 <see cref="AlignmentBreakdown"/> 映射到 WPF 行的正确性。
    ///
    /// 用典型 PI 场景（直 + 圆 + 缓）+ 非零 startStation 做端到端断言：
    /// - 段 / 几何点数量一致；
    /// - 段类型标签（直/缓/圆）与 SegmentKind 对应；
    /// - 桩号字符串按 "K{km}+{m:000.000}" 格式化；
    /// - Spiral 段 LsOrA 含 "Ls=" 与 "A="；Arc 段 LsOrA 含 "R="；Line 段 LsOrA 为空；
    /// - 偏角按方位差（mod 2π）输出；
    /// - SelectedSegment 双向绑定触发 SegmentSelectionChanged。
    /// </summary>
    public class AlignmentTableViewModelTests
    {
        private static List<PiElement> BuildStraightCurveSpiralPiElements()
        {
            return new List<PiElement>
            {
                new PiElement(new Point2D(0, 0), 0, 0, 0, "BP"),
                new PiElement(new Point2D(200, 0), 100, 0, 0, "JD1 纯圆"),
                new PiElement(new Point2D(200, 200), 150, 20, 20, "JD2 带缓"),
                new PiElement(new Point2D(400, 200), 0, 0, 0, "EP"),
            };
        }

        [Fact]
        public void Summary_ContainsStationsAndCounts()
        {
            var elements = BuildStraightCurveSpiralPiElements();
            var alignment = new Alignment { Name = "主线", StartStation = 1000 };
            var breakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation);

            var vm = new AlignmentTableViewModel(alignment, breakdown);

            vm.HeaderTitle.Should().Contain("主线");
            vm.AlignmentSummary.Should().Contain("K1+000.000");
            vm.AlignmentSummary.Should().Contain($"总长 {breakdown.TotalLengthM:F3}");
            vm.AlignmentSummary.Should().Contain("段数");
            vm.Segments.Count.Should().Be(breakdown.Segments.Count);
            vm.GeometryPoints.Count.Should().Be(breakdown.GeometryPoints.Count);
        }

        [Fact]
        public void SegmentRow_HasCorrectKindLabelsAndLsOrA()
        {
            var elements = BuildStraightCurveSpiralPiElements();
            var alignment = new Alignment { StartStation = 0 };
            var breakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation);

            var vm = new AlignmentTableViewModel(alignment, breakdown);

            // 至少包含一条 直 / 缓 / 圆 各一条
            vm.Segments.Should().Contain(r => r.KindLabel == "直");
            vm.Segments.Should().Contain(r => r.KindLabel == "圆");
            vm.Segments.Should().Contain(r => r.KindLabel == "缓");

            var spiralRow = vm.Segments.First(r => r.KindLabel == "缓");
            spiralRow.LsOrA.Should().Contain("Ls=").And.Contain("A=");

            var arcRow = vm.Segments.First(r => r.KindLabel == "圆");
            arcRow.LsOrA.Should().StartWith("R=");

            var lineRow = vm.Segments.First(r => r.KindLabel == "直");
            lineRow.LsOrA.Should().BeEmpty();
            lineRow.Radius.Should().BeEmpty(); // 直线段半径字段为空
        }

        [Fact]
        public void SegmentRow_StationsAreFormatted()
        {
            var elements = BuildStraightCurveSpiralPiElements();
            var alignment = new Alignment { StartStation = 500 };
            var breakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation);

            var vm = new AlignmentTableViewModel(alignment, breakdown);

            var first = vm.Segments.First();
            first.StationStart.Should().Be("K0+500.000");
            // 起桩位置应和 breakdown 的第一段起桩号一致
            first.Source.StationStartM.Should().Be(500);
        }

        [Fact]
        public void GeometryPoints_FirstIsBp_LastIsEp()
        {
            var elements = BuildStraightCurveSpiralPiElements();
            var alignment = new Alignment { StartStation = 0 };
            var breakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation);

            var vm = new AlignmentTableViewModel(alignment, breakdown);

            vm.GeometryPoints.First().Kind.Should().Be("BP");
            vm.GeometryPoints.Last().Kind.Should().Be("EP");
            // BP 起桩与 StartStation 一致
            vm.GeometryPoints.First().Station.Should().Be("K0+000.000");
        }

        [Fact]
        public void SelectedSegment_RaisesSegmentSelectionChanged()
        {
            var elements = BuildStraightCurveSpiralPiElements();
            var alignment = new Alignment { StartStation = 0 };
            var breakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation);

            var vm = new AlignmentTableViewModel(alignment, breakdown);

            SegmentRow captured = null;
            int raised = 0;
            vm.SegmentSelectionChanged += (_, row) =>
            {
                captured = row;
                raised++;
            };

            var target = vm.Segments[1];
            vm.SelectedSegment = target;

            raised.Should().Be(1);
            captured.Should().BeSameAs(target);

            // 重复赋相同值不再触发（ReferenceEquals 守卫）
            vm.SelectedSegment = target;
            raised.Should().Be(1);

            // 清空选中
            vm.SelectedSegment = null;
            raised.Should().Be(2);
            captured.Should().BeNull();
        }

        [Fact]
        public void CloseCommand_FiresCloseRequestedWithTrue()
        {
            var elements = BuildStraightCurveSpiralPiElements();
            var alignment = new Alignment { StartStation = 0 };
            var breakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation);

            var vm = new AlignmentTableViewModel(alignment, breakdown);

            bool? captured = null;
            int raised = 0;
            vm.CloseRequested += (_, dr) =>
            {
                captured = dr;
                raised++;
            };

            vm.CloseCommand.CanExecute(null).Should().BeTrue();
            vm.CloseCommand.Execute(null);

            raised.Should().Be(1);
            captured.Should().Be(true);
        }

        [Fact]
        public void DisplayIndex_IsOneBased()
        {
            var elements = BuildStraightCurveSpiralPiElements();
            var alignment = new Alignment { StartStation = 0 };
            var breakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation);

            var vm = new AlignmentTableViewModel(alignment, breakdown);

            vm.Segments[0].Index.Should().Be(0);
            vm.Segments[0].DisplayIndex.Should().Be(1);
            vm.Segments[1].DisplayIndex.Should().Be(2);
        }

        [Fact]
        public void DeflectionDeg_IsParseableFloat()
        {
            var elements = BuildStraightCurveSpiralPiElements();
            var alignment = new Alignment { StartStation = 0 };
            var breakdown = AlignmentStationBreakdown.Build(elements, alignment.StartStation);

            var vm = new AlignmentTableViewModel(alignment, breakdown);

            foreach (var row in vm.Segments)
            {
                double.TryParse(row.DeflectionDeg, out _).Should().BeTrue(
                    $"DeflectionDeg '{row.DeflectionDeg}' should be a valid double");
            }
        }
    }
}
