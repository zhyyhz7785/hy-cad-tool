using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Features.Road.PlanProfile.Domain;
using HyCADTool.Features.Road.PlanProfile.ViewModels;
using Xunit;

namespace HyCADTool.Tests.Presentation.ViewModels.Road
{
    /// <summary>
    /// ProfileEditorViewModel 行为测试。
    ///
    /// 验证维度：
    /// - 构造：复用既有 PVI / 空 Profile 自动注入端点；
    /// - 重算：PVI 主字段变化触发 FG 与规范同步；
    /// - 排序：桩号倒序输入会被自动按桩号升序重排；
    /// - 端点强制 R = 0：与 ProfileFgDesigner 行为一致；
    /// - Add/Remove/Insert 命令；
    /// - Confirm/Cancel + 二次确认回调；
    /// - FgGeometry 反映几何（包含 Tangent / Bezier 段）。
    /// </summary>
    public class ProfileEditorViewModelTests
    {
        private static Alignment BuildAlignment(double length = 1000, string name = "测试线")
        {
            var a = new Alignment { Name = name };
            a.Centerline.AddVertex(new Point3D(0, 0, 0));
            a.Centerline.AddVertex(new Point3D(length, 0, 0));
            return a;
        }

        private static Profile BuildEmptyProfile() => new Profile
        {
            Name = "设计标高",
            IsDesignProfile = true,
            DesignSpeed = 60,
        };

        /// <summary>构造一个 3 PVI 全合规样本（v=60 km/h，R=15000 凹/凸均过关）。</summary>
        private static Profile BuildAllPassProfile()
        {
            var p = BuildEmptyProfile();
            p.Vertices.Add(new ProfileVertex { Station = 0, Elevation = 100 });
            p.Vertices.Add(new ProfileVertex { Station = 500, Elevation = 110, CurveRadius = 15000 });
            p.Vertices.Add(new ProfileVertex { Station = 1000, Elevation = 105 });
            return p;
        }

        // ============================ 构造 ============================

        [Fact]
        public void Constructor_EmptyProfile_AutoSeedsTwoEndpointPvis()
        {
            var alignment = BuildAlignment(800);

            var vm = new ProfileEditorViewModel(alignment, BuildEmptyProfile());

            vm.Vertices.Should().HaveCount(2);
            vm.Vertices[0].Station.Should().Be(0);
            vm.Vertices[1].Station.Should().Be(800);
            vm.AlignmentLength.Should().Be(800);
            vm.Title.Should().Contain("测试线");
        }

        [Fact]
        public void Constructor_ExistingProfile_CopiesPvisAndDesignSpeed()
        {
            var alignment = BuildAlignment();
            var profile = BuildAllPassProfile();
            profile.DesignSpeed = 80;

            var vm = new ProfileEditorViewModel(alignment, profile);

            vm.Vertices.Should().HaveCount(3);
            vm.Vertices[1].Station.Should().Be(500);
            vm.Vertices[1].Elevation.Should().Be(110);
            vm.Vertices[1].CurveRadius.Should().Be(15000);
            vm.DesignSpeed.Should().Be(80);
        }

        [Fact]
        public void Constructor_DoesNotMutateSourceProfile()
        {
            var alignment = BuildAlignment();
            var profile = BuildAllPassProfile();
            int originalCount = profile.Vertices.Count;

            var vm = new ProfileEditorViewModel(alignment, profile);
            vm.Vertices[1].Station = 600;

            profile.Vertices.Count.Should().Be(originalCount);
            profile.Vertices[1].Station.Should().Be(500); // 未被修改
        }

        // ============================ 重算 ============================

        [Fact]
        public void Recalculate_PopulatesFgResultAndCheckItems()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());

            vm.LastFgResult.Should().NotBeNull();
            vm.LastFgResult.IsValid.Should().BeTrue();
            vm.LastFgResult.Pvis.Should().HaveCount(3);
            vm.LastFgResult.Segments.Should().NotBeEmpty();
            vm.CheckItems.Should().HaveCount(4);
            vm.LastCheckReport.Should().NotBeNull();
            vm.AllChecksPassed.Should().BeTrue();
        }

        [Fact]
        public void Recalculate_OnPviStationChange_TriggersFreshFg()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());
            int gradeBefore = (int)(vm.Vertices[0].GradePercentOut * 1000);

            vm.Vertices[1].Station = 800;

            // grade 应当变化（500→800 让 0~middle 段变缓）
            int gradeAfter = (int)(vm.Vertices[0].GradePercentOut * 1000);
            gradeAfter.Should().NotBe(gradeBefore);
        }

        [Fact]
        public void Recalculate_OnElevationChange_TriggersFreshFg()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());

            vm.Vertices[1].Elevation = 200;

            // 第一段坡度从 (110-100)/500 = 2% 变成 (200-100)/500 = 20%
            vm.Vertices[0].GradePercentOut.Should().BeApproximately(20.0, 1e-6);
        }

        [Fact]
        public void Recalculate_OnCurveRadiusChange_UpdatesCurveLength()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());
            double before = vm.Vertices[1].CurveLength;

            vm.Vertices[1].CurveRadius = 30000;

            vm.Vertices[1].CurveLength.Should().BeApproximately(before * 2, 1e-6);
        }

        [Fact]
        public void Endpoints_AlwaysHaveCurveLengthZero_RegardlessOfRadiusInput()
        {
            // PVI[0] 即使误填了 R，几何上也应忽略
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());
            vm.Vertices[0].CurveRadius = 5000;
            vm.Vertices[2].CurveRadius = 5000;

            vm.Vertices[0].CurveLength.Should().Be(0);
            vm.Vertices[2].CurveLength.Should().Be(0);
        }

        // ============================ 排序 ============================

        [Fact]
        public void StationOutOfOrder_AutoSortedAscending()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());

            vm.Vertices[0].Station = 700; // 大于第二个 PVI 的 500

            // 自动按桩号重排
            for (int i = 1; i < vm.Vertices.Count; i++)
            {
                vm.Vertices[i].Station.Should().BeGreaterThan(vm.Vertices[i - 1].Station);
            }
        }

        [Fact]
        public void DesignSpeedChange_RecalcCheckReport()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile())
            {
                DesignSpeed = 60,
            };

            vm.DesignSpeed = 80;

            vm.LastCheckReport.DesignSpeed.Should().Be(80);
        }

        // ============================ 命令 ============================

        [Fact]
        public void AddPviCommand_AppendsRow()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());
            int before = vm.Vertices.Count;

            vm.AddPviCommand.Execute(null);

            vm.Vertices.Count.Should().Be(before + 1);
        }

        [Fact]
        public void RemovePviCommand_RemovesSpecifiedRow()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());
            var middle = vm.Vertices[1];

            vm.RemovePviCommand.Execute(middle);

            vm.Vertices.Should().HaveCount(2);
            vm.Vertices.Should().NotContain(middle);
        }

        [Fact]
        public void RemovePviCommand_KeepsAtLeastTwoEndpoints()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildEmptyProfile());

            vm.RemovePviCommand.Execute(vm.Vertices[0]);
            vm.RemovePviCommand.Execute(vm.Vertices[1]);

            vm.Vertices.Should().HaveCount(2, "至少保留 2 个端点");
        }

        [Fact]
        public void InsertPviAt_InsertsRowWithFgElevation()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());

            // 桩号 250 在两个 PVI 之间，FG 高程应介于两端之间
            var row = vm.InsertPviAt(250);

            row.Station.Should().Be(250);
            row.Elevation.Should().BeGreaterThan(99).And.BeLessThan(111);
            vm.Vertices.Should().Contain(row);
            vm.Vertices.Count.Should().Be(4);
            // 仍然按桩号升序
            for (int i = 1; i < vm.Vertices.Count; i++)
            {
                vm.Vertices[i].Station.Should().BeGreaterThan(vm.Vertices[i - 1].Station);
            }
        }

        // ============================ Confirm / Cancel ============================

        [Fact]
        public void ConfirmCommand_AllPassed_RaisesConfirmedAndCloses()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());
            vm.AllChecksPassed.Should().BeTrue();

            IList<ProfileVertex> confirmed = null;
            bool? closed = null;
            vm.Confirmed += (_, v) => confirmed = v;
            vm.CloseRequested += (_, b) => closed = b;

            vm.ConfirmCommand.Execute(null);

            confirmed.Should().NotBeNull();
            confirmed.Should().HaveCount(3);
            confirmed.Should().BeInAscendingOrder(v => v.Station);
            closed.Should().Be(true);
            vm.ConfirmedVertices.Should().NotBeNull();
        }

        [Fact]
        public void ConfirmCommand_NonCompliant_ConsultsCallback_AbortsWhenRejected()
        {
            var alignment = BuildAlignment();
            var profile = BuildEmptyProfile();
            // 故意构造超大坡（100 m 高差 / 100 m 长度 = 100%）
            profile.Vertices.Add(new ProfileVertex { Station = 0, Elevation = 0 });
            profile.Vertices.Add(new ProfileVertex { Station = 100, Elevation = 100 });
            var vm = new ProfileEditorViewModel(alignment, profile);
            vm.AllChecksPassed.Should().BeFalse();

            string capturedSummary = null;
            vm.NonCompliantConfirm = s => { capturedSummary = s; return false; };

            bool? closed = null;
            vm.CloseRequested += (_, b) => closed = b;

            vm.ConfirmCommand.Execute(null);

            capturedSummary.Should().NotBeNullOrWhiteSpace();
            closed.Should().BeNull("用户拒绝时窗口不关闭");
            vm.ConfirmedVertices.Should().BeNull();
        }

        [Fact]
        public void ConfirmCommand_NonCompliant_ProceedsWhenCallbackAccepts()
        {
            var alignment = BuildAlignment();
            var profile = BuildEmptyProfile();
            profile.Vertices.Add(new ProfileVertex { Station = 0, Elevation = 0 });
            profile.Vertices.Add(new ProfileVertex { Station = 100, Elevation = 100 });
            var vm = new ProfileEditorViewModel(alignment, profile);

            vm.NonCompliantConfirm = _ => true;

            bool? closed = null;
            vm.CloseRequested += (_, b) => closed = b;

            vm.ConfirmCommand.Execute(null);

            closed.Should().Be(true);
            vm.ConfirmedVertices.Should().NotBeNull();
        }

        [Fact]
        public void CancelCommand_RaisesCancelled_AndDoesNotProduceVertices()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());

            bool cancelled = false;
            bool? closed = null;
            vm.Cancelled += (_, __) => cancelled = true;
            vm.CloseRequested += (_, b) => closed = b;

            vm.CancelCommand.Execute(null);

            cancelled.Should().BeTrue();
            closed.Should().Be(false);
            vm.ConfirmedVertices.Should().BeNull();
        }

        // ============================ Geometry ============================

        [Fact]
        public void RenderSegments_HasAtLeastOneSegment_WhenTwoPvisAvailable()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());

            vm.RenderSegments.Should().NotBeNull();
            vm.RenderSegments.Should().NotBeEmpty();
        }

        [Fact]
        public void RenderSegments_HasVerticalCurve_WhenInnerPviHasRadius()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());

            vm.RenderSegments.Should().Contain(s => s.Kind == ProfileRenderSegmentKind.VerticalCurve,
                "中间 PVI R=15000 应该生成竖曲线段");
            vm.RenderSegments.Should().Contain(s => s.Kind == ProfileRenderSegmentKind.Tangent,
                "首尾应至少各有一段直坡");
        }

        [Fact]
        public void RenderSegments_VerticalCurveControlPoint_EqualsPviStationElevation()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());

            var bezier = vm.RenderSegments.First(s => s.Kind == ProfileRenderSegmentKind.VerticalCurve);

            // 控制点 = PVI[1] = (500, 110)
            bezier.ControlStation.Should().Be(500);
            bezier.ControlElevation.Should().Be(110);
        }

        [Fact]
        public void RenderSegments_AreContiguous_AcrossPipeline()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());

            for (int i = 1; i < vm.RenderSegments.Count; i++)
            {
                vm.RenderSegments[i].StartStation.Should().BeApproximately(vm.RenderSegments[i - 1].EndStation, 1e-6);
                vm.RenderSegments[i].StartElevation.Should().BeApproximately(vm.RenderSegments[i - 1].EndElevation, 1e-6);
            }
        }

        [Fact]
        public void SnapshotVertices_ReturnsAscendingByStation()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());
            vm.Vertices[0].Station = 700; // 触发自动重排

            var snapshot = vm.SnapshotVertices();

            snapshot.Should().BeInAscendingOrder(v => v.Station);
            snapshot.Should().HaveCount(3);
        }

        [Fact]
        public void AvailableSpeeds_AlignsWithProfileCodeChecker()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());

            vm.AvailableSpeeds.Should().BeEquivalentTo(ProfileCodeChecker.SupportedSpeeds);
        }

        [Fact]
        public void SummaryText_IsNonEmpty_AfterRecalc()
        {
            var vm = new ProfileEditorViewModel(BuildAlignment(), BuildAllPassProfile());

            vm.SummaryText.Should().NotBeNullOrWhiteSpace();
            vm.SummaryText.Should().Contain("通过");
        }
    }
}
