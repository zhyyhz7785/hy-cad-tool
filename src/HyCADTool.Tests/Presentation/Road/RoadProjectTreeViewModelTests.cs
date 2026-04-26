using System;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Models.Road.Civil;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Features.Road.Plan.ViewModels;
using Xunit;

namespace HyCADTool.Tests.Presentation.Road
{
    /// <summary>
    /// 045 / M3：<see cref="RoadProjectTreeViewModel"/> 的建树回归防护。
    ///
    /// 目的：锁住「优化版树结构」的节点顺序、分组计数、路线下双分组、走廊 AlignmentId 过滤、
    /// 搜索裁剪等关键不变量，避免未来重构无声改变 UI 层级。
    /// </summary>
    public class RoadProjectTreeViewModelTests
    {
        private static RoadProject MakeDemoProject()
        {
            var design = new RoadDesign { ProjectName = "中华大街南延" };

            design.Surfaces.Add(new Surface { Name = "初设地形", Kind = SurfaceKind.ExistingGround });
            design.Templates.Add(new Template { Name = "双向6车道主干路" });

            var aln = new Alignment
            {
                Name = "中华大街A",
                StartStation = 0,
                Centerline = new Polyline3D(new[] { new Point3D(0, 0, 0), new Point3D(100, 0, 0) })
            };
            aln.Profiles.Add(new Profile { Name = "自然纵断1", IsDesignProfile = false });
            aln.Profiles.Add(new Profile { Name = "设计纵断1", IsDesignProfile = true });
            aln.Profiles[1].Sheets.Add(new ProfileSheet { Name = "0~175", StartStation = 0, EndStation = 175 });
            aln.Profiles[1].Sheets.Add(new ProfileSheet { Name = "175~350", StartStation = 175, EndStation = 350 });
            aln.StationEquations.Add(new StationEquation(500, 450));

            design.Alignments.Add(aln);

            design.Corridors.Add(new Corridor { Name = "A走廊", AlignmentId = aln.Id });
            design.Corridors.Add(new Corridor { Name = "其它走廊", AlignmentId = Guid.NewGuid() });

            design.Intersections.Add(new Intersection { Name = "K0+500 十字" });
            design.Interchanges.Add(new Interchange { Name = "北虹立交", Kind = InterchangeKind.OverPass });
            design.Landscapes.Add(new Landscape { Name = "湘江", Kind = LandscapeKind.Water });
            design.ExternalTrafficFacilities.Add(new TrafficFacility { Name = "信号机 #1", Kind = TrafficFacilityKind.Signal });
            design.Geologies.Add(new Geology { Name = "ZK-01", Kind = GeologyKind.BoreHole });
            design.Bridges.Add(new Bridge { Name = "A桥", OwnerAlignmentId = aln.Id, StartStation = 100, EndStation = 200 });
            design.Bridges.Add(new Bridge { Name = "无主桥", OwnerAlignmentId = null });
            design.Tunnels.Add(new Tunnel { Name = "B隧道", OwnerAlignmentId = Guid.NewGuid() /* 归属其它路线 */ });

            var project = new RoadProject { Name = "中华大街南延" };
            project.Designs.Add(design);
            return project;
        }

        [Fact]
        public void Rebuild_NullProject_ClearsRootsAndStatus()
        {
            var vm = new RoadProjectTreeViewModel { Project = null };
            vm.Roots.Should().BeEmpty();
            vm.StatusLine.Should().Contain("未加载");
        }

        [Fact]
        public void Rebuild_RootHas_ProjectMeta_Surfaces_Templates_Alignments_Intersections_Interchanges_Landscapes_Others_Geologies_Pipes_CodeAudit()
        {
            var vm = new RoadProjectTreeViewModel { Project = MakeDemoProject() };
            vm.Roots.Should().HaveCount(1);

            var root = vm.Roots[0];
            root.Kind.Should().Be(RoadTreeNodeKind.Project);
            root.Header.Should().Be("中华大街南延");
            root.IsExpanded.Should().BeTrue();

            var kinds = root.Children.Select(c => c.Kind).ToArray();
            kinds.Should().Contain(RoadTreeNodeKind.ProjectMeta);
            kinds.Should().Contain(RoadTreeNodeKind.SurfacesGroup);
            kinds.Should().Contain(RoadTreeNodeKind.TemplatesGroup);
            kinds.Should().Contain(RoadTreeNodeKind.AlignmentsGroup);
            kinds.Should().Contain(RoadTreeNodeKind.IntersectionsGroup);
            kinds.Should().Contain(RoadTreeNodeKind.InterchangesGroup);
            kinds.Should().Contain(RoadTreeNodeKind.LandscapesGroup);
            kinds.Should().Contain(RoadTreeNodeKind.OthersGroup);
            kinds.Should().Contain(RoadTreeNodeKind.GeologiesGroup);
            kinds.Should().Contain(RoadTreeNodeKind.PipesGroup);
            kinds.Should().Contain(RoadTreeNodeKind.CodeAudit);
        }

        [Fact]
        public void Rebuild_AlignmentNode_HasGeometryGroup_AndAssetsGroup()
        {
            var vm = new RoadProjectTreeViewModel { Project = MakeDemoProject() };
            var root = vm.Roots[0];

            var alignsGroup = root.Children.First(c => c.Kind == RoadTreeNodeKind.AlignmentsGroup);
            alignsGroup.Children.Should().HaveCount(1);

            var aln = alignsGroup.Children[0];
            aln.Kind.Should().Be(RoadTreeNodeKind.Alignment);
            aln.Header.Should().Be("中华大街A");

            aln.Children.Should().Contain(c => c.Kind == RoadTreeNodeKind.AlignmentGeometryGroup);
            aln.Children.Should().Contain(c => c.Kind == RoadTreeNodeKind.AlignmentAssetsGroup);
        }

        [Fact]
        public void Rebuild_Geometry_Profiles_HaveSheetsSubNode()
        {
            var vm = new RoadProjectTreeViewModel { Project = MakeDemoProject() };
            var root = vm.Roots[0];
            var aln = root.Children.First(c => c.Kind == RoadTreeNodeKind.AlignmentsGroup).Children[0];
            var geom = aln.Children.First(c => c.Kind == RoadTreeNodeKind.AlignmentGeometryGroup);

            var profiles = geom.Children.First(c => c.Kind == RoadTreeNodeKind.ProfilesGroup);
            profiles.Children.Should().HaveCount(2);

            var designProfile = profiles.Children.First(p => p.Header.Contains("设计"));
            var sheets = designProfile.Children.First(c => c.Kind == RoadTreeNodeKind.ProfileSheetsGroup);
            sheets.Children.Should().HaveCount(2);
            sheets.Children.Should().AllSatisfy(s => s.Kind.Should().Be(RoadTreeNodeKind.ProfileSheet));
        }

        [Fact]
        public void Rebuild_Corridors_FilteredByAlignmentId()
        {
            var vm = new RoadProjectTreeViewModel { Project = MakeDemoProject() };
            var root = vm.Roots[0];
            var aln = root.Children.First(c => c.Kind == RoadTreeNodeKind.AlignmentsGroup).Children[0];
            var geom = aln.Children.First(c => c.Kind == RoadTreeNodeKind.AlignmentGeometryGroup);

            var corridors = geom.Children.First(c => c.Kind == RoadTreeNodeKind.CorridorsGroup);
            corridors.Children.Should().HaveCount(1, "只有 AlignmentId 匹配的 Corridor 才挂到这条 Alignment 下");
            corridors.Children[0].Header.Should().Be("A走廊");
        }

        [Fact]
        public void Rebuild_Bridges_FilteredByOwnerAlignmentIdOrNull()
        {
            var vm = new RoadProjectTreeViewModel { Project = MakeDemoProject() };
            var aln = vm.Roots[0].Children.First(c => c.Kind == RoadTreeNodeKind.AlignmentsGroup).Children[0];
            var assets = aln.Children.First(c => c.Kind == RoadTreeNodeKind.AlignmentAssetsGroup);
            var bridges = assets.Children.First(c => c.Kind == RoadTreeNodeKind.BridgesGroup);

            bridges.Children.Should().HaveCount(2, "Owner 匹配的 + Owner 为 null 的都要挂上（后者是未归属构造物）");
            bridges.Children.Should().Contain(b => b.Header == "A桥");
            bridges.Children.Should().Contain(b => b.Header == "无主桥");
        }

        [Fact]
        public void Rebuild_Tunnels_FilteredOutForeignOwners()
        {
            var vm = new RoadProjectTreeViewModel { Project = MakeDemoProject() };
            var aln = vm.Roots[0].Children.First(c => c.Kind == RoadTreeNodeKind.AlignmentsGroup).Children[0];
            var assets = aln.Children.First(c => c.Kind == RoadTreeNodeKind.AlignmentAssetsGroup);
            var tunnels = assets.Children.First(c => c.Kind == RoadTreeNodeKind.TunnelsGroup);

            tunnels.Children.Should().BeEmpty("归属其它路线的隧道不应出现在本路线下");
        }

        [Fact]
        public void Rebuild_SearchText_FiltersTree_ToMatchingSubtreeOnly()
        {
            var vm = new RoadProjectTreeViewModel { Project = MakeDemoProject() };
            vm.SearchText = "湘江";

            var root = vm.Roots[0];
            // 至少应保留 Landscapes 支路到「湘江」
            var landscapesGroup = root.Children.FirstOrDefault(c => c.Kind == RoadTreeNodeKind.LandscapesGroup);
            landscapesGroup.Should().NotBeNull();
            landscapesGroup.Children.Should().ContainSingle(c => c.Header == "湘江");

            // 不相关的分组应被剪掉或空
            root.Children.Should().NotContain(c => c.Kind == RoadTreeNodeKind.SurfacesGroup);
        }

        [Fact]
        public void SearchText_Cleared_RestoresFullTree()
        {
            var vm = new RoadProjectTreeViewModel { Project = MakeDemoProject() };
            vm.SearchText = "湘江";
            vm.SearchText = null;

            var root = vm.Roots[0];
            root.Children.Should().Contain(c => c.Kind == RoadTreeNodeKind.SurfacesGroup);
            root.Children.Should().Contain(c => c.Kind == RoadTreeNodeKind.AlignmentsGroup);
        }

        [Fact]
        public void SettingProject_TriggersRebuild()
        {
            var vm = new RoadProjectTreeViewModel();
            vm.Roots.Should().BeEmpty();
            vm.Project = MakeDemoProject();
            vm.Roots.Should().NotBeEmpty();
        }
    }
}
