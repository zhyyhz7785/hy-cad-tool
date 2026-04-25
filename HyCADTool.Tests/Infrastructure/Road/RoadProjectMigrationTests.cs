using System;
using System.IO;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Models.Road.Civil;
using HyCADTool.Domain.Models.Road.Serialization;
using HyCADTool.Shared.Geometry;
using HyCADTool.Shared.AutoCAD.Services.Road;
using Xunit;

namespace HyCADTool.Tests.Infrastructure.Road
{
    /// <summary>
    /// 045 / M2：<see cref="RoadProject"/> + <see cref="RoadProjectMigration"/> 的
    /// JSON 读写与 v1.x 自动迁移回归防护。
    /// </summary>
    public class RoadProjectMigrationTests : IDisposable
    {
        private readonly string _tempDir;

        public RoadProjectMigrationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "hycad-proj-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        // =============================================================
        //  SchemaVersion
        // =============================================================

        [Fact]
        public void SchemaVersion_Current_IsV2_0()
        {
            SchemaVersion.Current.Should().Be("2.0");
            SchemaVersion.CurrentProject.Should().Be("2.0");
            SchemaVersion.MinimumSupported.Should().Be("1.0");
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData(" ", true)]
        [InlineData("1.0", true)]
        [InlineData("1.1", true)]
        [InlineData("1.2", true)]
        [InlineData("2.0", false)]
        [InlineData("2.0.0", false)]
        [InlineData("3.0", false)]
        public void SchemaVersion_IsLegacyV1_ClassifiesCorrectly(string schema, bool expectLegacy)
        {
            SchemaVersion.IsLegacyV1(schema).Should().Be(expectLegacy);
        }

        // =============================================================
        //  WrapSingleDesign
        // =============================================================

        [Fact]
        public void WrapSingleDesign_Null_ReturnsEmptyProject()
        {
            var project = RoadProjectMigration.WrapSingleDesign(null);
            project.Should().NotBeNull();
            project.Designs.Should().BeEmpty();
            project.Shortcuts.Should().BeEmpty();
            project.IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void WrapSingleDesign_LegacyV1Design_UpgradesSchemaAndNames()
        {
            var design = new RoadDesign
            {
                ProjectName = "Legacy Demo",
                Schema = "1.2"
            };
            design.Alignments.Add(new Alignment { Name = "A1" });

            var project = RoadProjectMigration.WrapSingleDesign(design);

            project.Should().NotBeNull();
            project.Name.Should().Be("Legacy Demo");
            project.Schema.Should().Be(SchemaVersion.CurrentProject);
            project.Designs.Should().HaveCount(1);
            project.Designs[0].Should().BeSameAs(design);
            project.Designs[0].Schema.Should().Be(SchemaVersion.Current, "内层 RoadDesign.Schema 透明升到 v2.0");
            project.IsEmpty.Should().BeFalse();
        }

        [Fact]
        public void WrapSingleDesign_V2Design_KeepsSchema()
        {
            var design = new RoadDesign { ProjectName = "v2-already", Schema = "2.0" };
            var project = RoadProjectMigration.WrapSingleDesign(design);
            design.Schema.Should().Be("2.0");
            project.Schema.Should().Be("2.0");
        }

        // =============================================================
        //  RoadProject.IsEmpty 联动
        // =============================================================

        [Fact]
        public void RoadProject_IsEmpty_WhenAllDesignsEmpty()
        {
            var project = new RoadProject();
            project.IsEmpty.Should().BeTrue();

            project.Designs.Add(new RoadDesign { ProjectName = "blank" });
            project.IsEmpty.Should().BeTrue("内含空 design 仍视为空项目");
        }

        [Fact]
        public void RoadProject_IsEmpty_False_WhenAnyDesignHasContent()
        {
            var project = new RoadProject();
            var design = new RoadDesign { ProjectName = "d1" };
            design.Alignments.Add(new Alignment { Name = "A" });
            project.Designs.Add(design);

            project.IsEmpty.Should().BeFalse();
        }

        [Fact]
        public void RoadProject_IsEmpty_False_WhenHasShortcuts()
        {
            var project = new RoadProject();
            project.Shortcuts.Add(new DataShortcut
            {
                Kind = DataShortcutKind.Alignment,
                SourcePath = "other.roaddesign.json",
                SourceId = Guid.NewGuid(),
                LocalAlias = "Ref A"
            });

            project.IsEmpty.Should().BeFalse("仅引用也算有内容");
        }

        // =============================================================
        //  RoadDesign.IsEmpty：v2.0 新增占位集合不应在空时破坏兼容
        // =============================================================

        [Fact]
        public void RoadDesign_IsEmpty_TrueByDefault_AfterV2Schema()
        {
            new RoadDesign().IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void RoadDesign_IsEmpty_False_WhenAnyV2CollectionHasItem()
        {
            var d = new RoadDesign();
            d.Surfaces.Add(new Surface { Name = "初设地形" });
            d.IsEmpty.Should().BeFalse();

            d = new RoadDesign();
            d.Bridges.Add(new Bridge { Name = "A桥" });
            d.IsEmpty.Should().BeFalse();

            d = new RoadDesign();
            d.Interchanges.Add(new Interchange { Name = "北虹立交" });
            d.IsEmpty.Should().BeFalse();

            d = new RoadDesign();
            d.Landscapes.Add(new Landscape { Name = "湘江", Kind = LandscapeKind.Water });
            d.IsEmpty.Should().BeFalse();

            d = new RoadDesign();
            d.Geologies.Add(new Geology { Name = "ZK-01", Kind = GeologyKind.BoreHole });
            d.IsEmpty.Should().BeFalse();
        }

        // =============================================================
        //  SaveProject / LoadProject 原子往返
        // =============================================================

        [Fact]
        public void SaveProject_ThenLoadProject_RoundtripsDesignsAndShortcuts()
        {
            var svc = new RoadJsonExportService();
            var path = Path.Combine(_tempDir, "demo.roadproject.json");

            var design = new RoadDesign { ProjectName = "D1" };
            design.Alignments.Add(new Alignment
            {
                Name = "中华大街A",
                Centerline = new Polyline3D(new[]
                {
                    new Point3D(0, 0, 0),
                    new Point3D(100, 0, 0),
                })
            });
            design.Surfaces.Add(new Surface { Name = "初设地形", Kind = SurfaceKind.ExistingGround });
            design.Interchanges.Add(new Interchange { Name = "北虹立交", Kind = InterchangeKind.OverPass });

            var project = new RoadProject
            {
                Name = "中华大街南延",
                RootDirectory = _tempDir,
            };
            project.Designs.Add(design);
            project.Shortcuts.Add(new DataShortcut
            {
                Kind = DataShortcutKind.Alignment,
                SourcePath = @"..\neighbour.roaddesign.json",
                SourceId = Guid.NewGuid(),
                LocalAlias = "邻接主线"
            });

            svc.SaveProject(project, path);
            var back = svc.LoadProject(path);

            back.Should().NotBeNull();
            back.Name.Should().Be("中华大街南延");
            back.Schema.Should().Be(SchemaVersion.CurrentProject);
            back.Designs.Should().HaveCount(1);
            back.Designs[0].Alignments[0].Name.Should().Be("中华大街A");
            back.Designs[0].Surfaces.Should().HaveCount(1);
            back.Designs[0].Surfaces[0].Kind.Should().Be(SurfaceKind.ExistingGround);
            back.Designs[0].Interchanges.Should().HaveCount(1);
            back.Shortcuts.Should().HaveCount(1);
            back.Shortcuts[0].LocalAlias.Should().Be("邻接主线");
        }

        [Fact]
        public void SaveProject_EmptyProject_StillWritesSchema()
        {
            var svc = new RoadJsonExportService();
            var path = Path.Combine(_tempDir, "empty.roadproject.json");

            var project = new RoadProject { Name = "empty" };
            svc.SaveProject(project, path);

            File.Exists(path).Should().BeTrue();
            var back = svc.LoadProject(path);
            back.Name.Should().Be("empty");
            back.Schema.Should().Be(SchemaVersion.CurrentProject);
            back.IsEmpty.Should().BeTrue();
        }

        // =============================================================
        //  LoadProject 自动识别 .roaddesign.json（v1.x 迁移）
        // =============================================================

        [Fact]
        public void LoadProject_OnLegacyRoadDesignJson_AutoWrapsAsSingleDesign()
        {
            var svc = new RoadJsonExportService();
            var legacyPath = Path.Combine(_tempDir, "old.roaddesign.json");

            var legacy = new RoadDesign { ProjectName = "Old Project", Schema = "1.2" };
            legacy.Alignments.Add(new Alignment
            {
                Name = "A1",
                Centerline = new Polyline3D(new[]
                {
                    new Point3D(0, 0, 0),
                    new Point3D(50, 0, 0),
                })
            });
            svc.Save(legacy, legacyPath);

            // 关键断言：LoadProject 应能吃下 .roaddesign.json 并返回 RoadProject
            var project = svc.LoadProject(legacyPath);

            project.Should().NotBeNull();
            project.Designs.Should().HaveCount(1);
            project.Designs[0].ProjectName.Should().Be("Old Project");
            project.Designs[0].Alignments[0].Name.Should().Be("A1");
            project.Designs[0].Schema.Should().Be(SchemaVersion.Current, "包装时内层 schema 透明升级");

            // 非破坏：物理 .roaddesign.json 文件未被改名 / 删除
            File.Exists(legacyPath).Should().BeTrue();
        }

        [Fact]
        public void LoadProject_NonExistent_ReturnsNull()
        {
            var svc = new RoadJsonExportService();
            svc.LoadProject(Path.Combine(_tempDir, "nope.roadproject.json")).Should().BeNull();
            svc.LoadProject(Path.Combine(_tempDir, "nope.roaddesign.json")).Should().BeNull();
        }

        // =============================================================
        //  LoadProjectForDocument 优先级：project > design
        // =============================================================

        [Fact]
        public void LoadProjectForDocument_PrefersProjectJsonOverDesignJson()
        {
            var svc = new RoadJsonExportService();
            var dwg = Path.Combine(_tempDir, "Demo.dwg");
            var designPath = Path.Combine(_tempDir, "Demo.roaddesign.json");
            var projectPath = Path.Combine(_tempDir, "Demo.roadproject.json");

            var legacy = new RoadDesign { ProjectName = "LegacyInner", Schema = "1.2" };
            legacy.Alignments.Add(new Alignment { Name = "legacy-A" });
            svc.Save(legacy, designPath);

            var project = new RoadProject { Name = "ModernOuter" };
            var inner = new RoadDesign { ProjectName = "ModernInner", Schema = "2.0" };
            inner.Alignments.Add(new Alignment { Name = "modern-A" });
            project.Designs.Add(inner);
            svc.SaveProject(project, projectPath);

            var loaded = svc.LoadProjectForDocument(dwg);
            loaded.Should().NotBeNull();
            loaded.Name.Should().Be("ModernOuter", "存在 .roadproject.json 时优先于 .roaddesign.json");
            loaded.Designs[0].ProjectName.Should().Be("ModernInner");
        }

        [Fact]
        public void LoadProjectForDocument_FallsBackToLegacyDesignJson()
        {
            var svc = new RoadJsonExportService();
            var dwg = Path.Combine(_tempDir, "OnlyLegacy.dwg");
            var designPath = Path.Combine(_tempDir, "OnlyLegacy.roaddesign.json");

            var legacy = new RoadDesign { ProjectName = "OnlyLegacy", Schema = "1.0" };
            legacy.Alignments.Add(new Alignment { Name = "A-legacy" });
            svc.Save(legacy, designPath);

            var loaded = svc.LoadProjectForDocument(dwg);
            loaded.Should().NotBeNull();
            loaded.Designs[0].Alignments[0].Name.Should().Be("A-legacy");
        }

        [Fact]
        public void LoadProjectForDocument_NothingExists_ReturnsNull()
        {
            var svc = new RoadJsonExportService();
            var dwg = Path.Combine(_tempDir, "Empty.dwg");
            svc.LoadProjectForDocument(dwg).Should().BeNull();
        }

        // =============================================================
        //  SaveProjectForDocument
        // =============================================================

        [Fact]
        public void SaveProjectForDocument_EmptyProject_SkipsWriteAndReturnsNull()
        {
            var svc = new RoadJsonExportService();
            var dwg = Path.Combine(_tempDir, "Empty.dwg");
            svc.SaveProjectForDocument(new RoadProject { Name = "e" }, dwg).Should().BeNull();
            File.Exists(Path.Combine(_tempDir, "Empty.roadproject.json")).Should().BeFalse();
        }

        [Fact]
        public void SaveProjectForDocument_WithContent_WritesFile()
        {
            var svc = new RoadJsonExportService();
            var dwg = Path.Combine(_tempDir, "Demo.dwg");

            var project = new RoadProject { Name = "Demo" };
            var design = new RoadDesign { ProjectName = "d" };
            design.Alignments.Add(new Alignment { Name = "A" });
            project.Designs.Add(design);

            var written = svc.SaveProjectForDocument(project, dwg);
            written.Should().EndWith("Demo.roadproject.json");
            File.Exists(written).Should().BeTrue();
        }
    }
}
