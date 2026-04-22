using System;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Models.Road.Civil;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Presentation.ViewModels.Road;
using Xunit;

namespace HyCADTool.Refactored.Tests.Presentation.Road
{
    /// <summary>
    /// 045 / M4：项目树交互（双击 / 右键命令 / 选中高亮）回归防护。
    /// </summary>
    public class RoadProjectTreeInteractionTests
    {
        private static RoadTreeNode MakeNode(RoadTreeNodeKind kind, bool hasTargetId = false)
        {
            var n = new RoadTreeNode(kind, $"N-{kind}", detail: null, tag: null);
            if (hasTargetId) n.TargetId = Guid.NewGuid();
            return n;
        }

        private static RoadProject MakeProjectWithOneAlignment(out Alignment aln)
        {
            var d = new RoadDesign { ProjectName = "m4-demo" };
            aln = new Alignment
            {
                Name = "主线",
                Centerline = new Polyline3D(new[] { new Point3D(0, 0, 0), new Point3D(10, 0, 0) })
            };
            d.Alignments.Add(aln);

            var p = new RoadProject { Name = "m4-demo" };
            p.Designs.Add(d);
            return p;
        }

        // =============================================================
        //  CanOpen / HasTargetEntity / CanRename / CanDelete / CanExportLandXml
        // =============================================================

        [Theory]
        [InlineData(RoadTreeNodeKind.Alignment, true)]
        [InlineData(RoadTreeNodeKind.Profile, true)]
        [InlineData(RoadTreeNodeKind.Template, true)]
        [InlineData(RoadTreeNodeKind.CrossSection, true)]
        [InlineData(RoadTreeNodeKind.Corridor, true)]
        [InlineData(RoadTreeNodeKind.Intersection, true)]
        [InlineData(RoadTreeNodeKind.AlignmentsGroup, false)]
        [InlineData(RoadTreeNodeKind.ProfilesGroup, false)]
        [InlineData(RoadTreeNodeKind.SurfacesGroup, false)]
        [InlineData(RoadTreeNodeKind.StationEquationGroup, false)]
        [InlineData(RoadTreeNodeKind.PipesGroup, false)]
        public void CanOpen_ReturnsTrue_OnlyForLeafEntityLikeNodes(RoadTreeNodeKind kind, bool expected)
        {
            RoadProjectTreeViewModel.CanOpen(MakeNode(kind)).Should().Be(expected);
        }

        [Theory]
        [InlineData(RoadTreeNodeKind.Alignment)]
        [InlineData(RoadTreeNodeKind.Intersection)]
        [InlineData(RoadTreeNodeKind.Corridor)]
        [InlineData(RoadTreeNodeKind.Bridge)]
        public void HasTargetEntity_TrueOnlyWhenTargetIdSet(RoadTreeNodeKind kind)
        {
            RoadProjectTreeViewModel.HasTargetEntity(MakeNode(kind, hasTargetId: false))
                .Should().BeFalse("TargetId 为空时不能 ZoomTo");
            RoadProjectTreeViewModel.HasTargetEntity(MakeNode(kind, hasTargetId: true))
                .Should().BeTrue();
        }

        [Fact]
        public void CanDelete_False_ForProjectRoot()
        {
            RoadProjectTreeViewModel.CanDelete(MakeNode(RoadTreeNodeKind.Project))
                .Should().BeFalse("项目根不允许在树里直接删除");
        }

        [Theory]
        [InlineData(RoadTreeNodeKind.Project, true)]
        [InlineData(RoadTreeNodeKind.Alignment, true)]
        [InlineData(RoadTreeNodeKind.Profile, true)]
        [InlineData(RoadTreeNodeKind.Surface, true)]
        [InlineData(RoadTreeNodeKind.AlignmentsGroup, false)]
        [InlineData(RoadTreeNodeKind.CodeAudit, false)]
        public void CanExportLandXml_Matches_CivilStyleAllowlist(RoadTreeNodeKind kind, bool expected)
        {
            RoadProjectTreeViewModel.CanExportLandXml(MakeNode(kind)).Should().Be(expected);
        }

        // =============================================================
        //  Commands 通过 handler 正确派发
        // =============================================================

        [Fact]
        public void OpenCommand_CallsHandlerOpen()
        {
            var handler = new NullRoadTreeInteractionHandler();
            var vm = new RoadProjectTreeViewModel(handler);

            var node = MakeNode(RoadTreeNodeKind.Alignment, hasTargetId: true);
            vm.OpenCommand.CanExecute(node).Should().BeTrue();
            vm.OpenCommand.Execute(node);

            handler.Calls.Open.Should().Be(1);
        }

        [Fact]
        public void OpenCommand_CanExecuteFalse_ForGroupNode()
        {
            var vm = new RoadProjectTreeViewModel();
            var group = MakeNode(RoadTreeNodeKind.AlignmentsGroup);
            vm.OpenCommand.CanExecute(group).Should().BeFalse();
        }

        [Fact]
        public void OpenCommand_CanExecuteFalse_ForNull()
        {
            var vm = new RoadProjectTreeViewModel();
            vm.OpenCommand.CanExecute(null).Should().BeFalse();
        }

        [Fact]
        public void ZoomToCommand_RequiresNonEmptyTargetId()
        {
            var vm = new RoadProjectTreeViewModel();
            vm.ZoomToCommand.CanExecute(MakeNode(RoadTreeNodeKind.Alignment, hasTargetId: false))
                .Should().BeFalse();
            vm.ZoomToCommand.CanExecute(MakeNode(RoadTreeNodeKind.Alignment, hasTargetId: true))
                .Should().BeTrue();
        }

        [Fact]
        public void RenameCommand_CallsHandlerRename()
        {
            var h = new NullRoadTreeInteractionHandler();
            var vm = new RoadProjectTreeViewModel(h);

            vm.RenameCommand.Execute(MakeNode(RoadTreeNodeKind.Alignment));
            h.Calls.Rename.Should().Be(1);
        }

        [Fact]
        public void DeleteCommand_CallsHandlerDelete()
        {
            var h = new NullRoadTreeInteractionHandler();
            var vm = new RoadProjectTreeViewModel(h);

            vm.DeleteCommand.Execute(MakeNode(RoadTreeNodeKind.Alignment));
            h.Calls.Delete.Should().Be(1);
        }

        [Fact]
        public void DeleteCommand_CanExecuteFalse_ForProjectRoot()
        {
            var vm = new RoadProjectTreeViewModel();
            vm.DeleteCommand.CanExecute(MakeNode(RoadTreeNodeKind.Project)).Should().BeFalse();
        }

        [Fact]
        public void ExportLandXmlCommand_OnlyAllowsAllowlistedKinds()
        {
            var vm = new RoadProjectTreeViewModel();
            vm.ExportLandXmlCommand.CanExecute(MakeNode(RoadTreeNodeKind.Alignment)).Should().BeTrue();
            vm.ExportLandXmlCommand.CanExecute(MakeNode(RoadTreeNodeKind.Surface)).Should().BeTrue();
            vm.ExportLandXmlCommand.CanExecute(MakeNode(RoadTreeNodeKind.Template)).Should().BeFalse();
            vm.ExportLandXmlCommand.CanExecute(MakeNode(RoadTreeNodeKind.AlignmentsGroup)).Should().BeFalse();
        }

        // =============================================================
        //  SelectedNode 设置触发 Highlight 联动
        // =============================================================

        [Fact]
        public void SelectedNodeChange_TriggersHandlerHighlight()
        {
            var h = new NullRoadTreeInteractionHandler();
            var vm = new RoadProjectTreeViewModel(h);

            var node = MakeNode(RoadTreeNodeKind.Alignment, hasTargetId: true);
            vm.SelectedNode = node;

            h.Calls.Highlight.Should().Be(1);
        }

        [Fact]
        public void SelectedNode_Null_DoesNotCrash()
        {
            var h = new NullRoadTreeInteractionHandler();
            var vm = new RoadProjectTreeViewModel(h);

            Action act = () => vm.SelectedNode = null;
            act.Should().NotThrow();
            h.Calls.Highlight.Should().Be(0, "null 节点由 NullRoadTreeInteractionHandler 忽略");
        }

        [Fact]
        public void SelectedNodeChange_RaisesCanExecuteChanged_ForCommands()
        {
            var vm = new RoadProjectTreeViewModel();
            int openRaises = 0, zoomRaises = 0;
            vm.OpenCommand.CanExecuteChanged += (s, e) => openRaises++;
            vm.ZoomToCommand.CanExecuteChanged += (s, e) => zoomRaises++;

            vm.SelectedNode = MakeNode(RoadTreeNodeKind.Alignment, hasTargetId: true);

            openRaises.Should().BeGreaterOrEqualTo(1);
            zoomRaises.Should().BeGreaterOrEqualTo(1);
        }

        // =============================================================
        //  节点 Tag 正确指向 Domain 对象（用于 handler 实际操作）
        // =============================================================

        [Fact]
        public void AlignmentNode_Tag_PointsBackToDomainAlignment()
        {
            var project = MakeProjectWithOneAlignment(out var aln);
            var vm = new RoadProjectTreeViewModel { Project = project };

            var root = vm.Roots[0];
            var alnGroup = System.Linq.Enumerable.First(
                root.Children, c => c.Kind == RoadTreeNodeKind.AlignmentsGroup);
            var alnNode = alnGroup.Children[0];

            alnNode.Tag.Should().BeSameAs(aln);
            alnNode.TargetId.Should().Be(aln.Id);
        }
    }
}
