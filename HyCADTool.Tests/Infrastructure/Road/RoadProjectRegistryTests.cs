using System;
using System.IO;
using FluentAssertions;
using HyCADTool.Domain.Events.Road;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Models.Road.Civil;
using HyCADTool.Shared.AutoCAD.Services.Road;
using Xunit;

namespace HyCADTool.Tests.Infrastructure.Road
{
    /// <summary>
    /// 045 / M5：<see cref="RoadProjectRegistry"/> 与 <see cref="DataShortcut"/> 解析回归防护。
    /// </summary>
    public class RoadProjectRegistryTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly RoadJsonExportService _io = new RoadJsonExportService();
        private readonly IRoadEventBus _bus = new RoadEventBus();

        public RoadProjectRegistryTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "hycad-m5-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        private RoadProjectRegistry MakeRegistry()
            => new RoadProjectRegistry(_io, _bus, new RoadDesignRegistry(_bus));

        // =============================================================
        //  GetOrCreateForDocument
        // =============================================================

        [Fact]
        public void GetOrCreateForDocument_NoFile_WrapsInnerDesign()
        {
            var reg = MakeRegistry();
            var project = reg.GetOrCreateForDocument(Path.Combine(_tempDir, "New.dwg"));

            project.Should().NotBeNull();
            project.Designs.Should().HaveCount(1, "没有磁盘文件时用 InnerRegistry 的空 design 包装成一个 project");
            reg.InnerRegistry.TryGet(Path.Combine(_tempDir, "New.dwg"), out var _).Should().BeTrue();
        }

        [Fact]
        public void GetOrCreateForDocument_WithLegacyRoadDesignJson_LoadsAndSyncsInner()
        {
            var dwg = Path.Combine(_tempDir, "Legacy.dwg");
            var designPath = Path.Combine(_tempDir, "Legacy.roaddesign.json");

            var inner = new RoadDesign { ProjectName = "Legacy", Schema = "1.2" };
            inner.Alignments.Add(new Alignment { Name = "A-legacy" });
            _io.Save(inner, designPath);

            var reg = MakeRegistry();
            var project = reg.GetOrCreateForDocument(dwg);

            project.Should().NotBeNull();
            project.Designs.Should().HaveCount(1);
            project.Designs[0].Alignments[0].Name.Should().Be("A-legacy");

            reg.InnerRegistry.TryGet(dwg, out var innerLoaded).Should().BeTrue();
            innerLoaded.Alignments[0].Name.Should().Be("A-legacy", "主 design 同步到 InnerRegistry");
        }

        [Fact]
        public void GetOrCreateForDocument_WithRoadProjectJson_LoadsDirectly()
        {
            var dwg = Path.Combine(_tempDir, "Modern.dwg");
            var projectPath = Path.Combine(_tempDir, "Modern.roadproject.json");

            var project = new RoadProject { Name = "Modern" };
            var inner = new RoadDesign { ProjectName = "inner", Schema = "2.0" };
            inner.Alignments.Add(new Alignment { Name = "A-modern" });
            project.Designs.Add(inner);
            _io.SaveProject(project, projectPath);

            var reg = MakeRegistry();
            var loaded = reg.GetOrCreateForDocument(dwg);

            loaded.Name.Should().Be("Modern");
            loaded.Designs[0].Alignments[0].Name.Should().Be("A-modern");
        }

        [Fact]
        public void GetOrCreateForDocument_Cached_ReturnsSameInstance()
        {
            var reg = MakeRegistry();
            var dwg = Path.Combine(_tempDir, "cached.dwg");
            var first = reg.GetOrCreateForDocument(dwg);
            var second = reg.GetOrCreateForDocument(dwg);
            first.Should().BeSameAs(second);
        }

        // =============================================================
        //  Replace / Remove
        // =============================================================

        [Fact]
        public void Replace_SyncsPrimaryDesignIntoInnerRegistry()
        {
            var reg = MakeRegistry();
            var dwg = Path.Combine(_tempDir, "rep.dwg");

            var project = new RoadProject { Name = "rep" };
            var inner = new RoadDesign { ProjectName = "primary" };
            inner.Alignments.Add(new Alignment { Name = "P1" });
            project.Designs.Add(inner);

            reg.Replace(dwg, project);

            reg.TryGet(dwg, out var back).Should().BeTrue();
            back.Should().BeSameAs(project);

            reg.InnerRegistry.TryGet(dwg, out var innerBack).Should().BeTrue();
            innerBack.Should().BeSameAs(inner);
        }

        [Fact]
        public void Remove_ClearsBothLayers()
        {
            var reg = MakeRegistry();
            var dwg = Path.Combine(_tempDir, "toRemove.dwg");
            reg.GetOrCreateForDocument(dwg);

            reg.Remove(dwg);

            reg.TryGet(dwg, out _).Should().BeFalse();
            reg.InnerRegistry.TryGet(dwg, out _).Should().BeFalse();
        }

        // =============================================================
        //  Data Shortcut 解析
        // =============================================================

        [Fact]
        public void Resolve_BrokenPath_ReturnsBrokenLinkResult()
        {
            var reg = MakeRegistry();
            var result = reg.Resolve(
                new DataShortcut { Kind = DataShortcutKind.Alignment, SourcePath = "nope.roaddesign.json" },
                _tempDir);

            result.IsOk.Should().BeFalse();
            result.Error.Should().Contain("找不到");
        }

        [Fact]
        public void Resolve_EmptyShortcut_ReturnsEmpty()
        {
            var reg = MakeRegistry();
            var result = reg.Resolve(null, _tempDir);
            result.IsOk.Should().BeFalse();
            result.Should().BeSameAs(DataShortcutResolveResult.Empty);
        }

        [Fact]
        public void Resolve_ValidAlignmentShortcut_ReturnsTargetAndCaches()
        {
            // 准备远端 .roaddesign.json
            var neighbourDir = Path.Combine(_tempDir, "sub");
            Directory.CreateDirectory(neighbourDir);
            var remotePath = Path.Combine(neighbourDir, "neighbour.roaddesign.json");

            var remote = new RoadDesign { ProjectName = "neighbour" };
            var aln = new Alignment { Name = "远端主线" };
            remote.Alignments.Add(aln);
            _io.Save(remote, remotePath);

            var reg = MakeRegistry();
            var sc = new DataShortcut
            {
                Kind = DataShortcutKind.Alignment,
                SourcePath = Path.Combine("sub", "neighbour.roaddesign.json"), // 相对路径
                SourceId = aln.Id,
                LocalAlias = "邻接"
            };

            var result = reg.Resolve(sc, _tempDir);
            result.IsOk.Should().BeTrue(result.Error);
            result.Target.Should().BeOfType<Alignment>();
            ((Alignment)result.Target).Name.Should().Be("远端主线");
            result.ResolvedPath.Should().EndWith("neighbour.roaddesign.json");

            // 第二次解析应命中缓存（同引用）
            var second = reg.Resolve(sc, _tempDir);
            second.SourceDesign.Should().BeSameAs(result.SourceDesign);
        }

        [Fact]
        public void Resolve_WrongSourceId_ReturnsBrokenLink()
        {
            var remotePath = Path.Combine(_tempDir, "wrong-id.roaddesign.json");
            var remote = new RoadDesign { ProjectName = "x" };
            remote.Alignments.Add(new Alignment { Name = "A" });
            _io.Save(remote, remotePath);

            var reg = MakeRegistry();
            var sc = new DataShortcut
            {
                Kind = DataShortcutKind.Alignment,
                SourcePath = "wrong-id.roaddesign.json",
                SourceId = Guid.NewGuid() // 与 remote.Alignments[0].Id 不同
            };

            var result = reg.Resolve(sc, _tempDir);
            result.IsOk.Should().BeFalse();
            result.Error.Should().Contain("未找到");
        }

        [Fact]
        public void Resolve_ProfileShortcut_FindsInsideAlignmentProfiles()
        {
            var remotePath = Path.Combine(_tempDir, "has-prof.roaddesign.json");
            var remote = new RoadDesign { ProjectName = "x" };
            var aln = new Alignment { Name = "A" };
            var prof = new Profile { Name = "F1", IsDesignProfile = true };
            aln.Profiles.Add(prof);
            remote.Alignments.Add(aln);
            _io.Save(remote, remotePath);

            var reg = MakeRegistry();
            var sc = new DataShortcut
            {
                Kind = DataShortcutKind.Profile,
                SourcePath = "has-prof.roaddesign.json",
                SourceId = prof.Id
            };

            var r = reg.Resolve(sc, _tempDir);
            r.IsOk.Should().BeTrue(r.Error);
            r.Target.Should().BeOfType<Profile>();
            ((Profile)r.Target).Name.Should().Be("F1");
        }

        [Fact]
        public void Resolve_AbsoluteSourcePath_Works()
        {
            var remotePath = Path.Combine(_tempDir, "abs.roaddesign.json");
            var remote = new RoadDesign { ProjectName = "x" };
            var aln = new Alignment { Name = "abs-A" };
            remote.Alignments.Add(aln);
            _io.Save(remote, remotePath);

            var reg = MakeRegistry();
            var sc = new DataShortcut
            {
                Kind = DataShortcutKind.Alignment,
                SourcePath = remotePath, // 绝对路径
                SourceId = aln.Id
            };

            var r = reg.Resolve(sc, rootDirectory: null);
            r.IsOk.Should().BeTrue(r.Error);
        }

        [Fact]
        public void ClearExternalCache_AllowsReload()
        {
            var remotePath = Path.Combine(_tempDir, "cache.roaddesign.json");
            var remote = new RoadDesign { ProjectName = "x" };
            remote.Alignments.Add(new Alignment { Name = "cA", Id = Guid.NewGuid() });
            _io.Save(remote, remotePath);

            var reg = MakeRegistry();
            var sc = new DataShortcut
            {
                Kind = DataShortcutKind.Alignment,
                SourcePath = "cache.roaddesign.json",
                SourceId = remote.Alignments[0].Id
            };

            var r1 = reg.Resolve(sc, _tempDir);
            var firstDesign = r1.SourceDesign;

            reg.ClearExternalCache();
            var r2 = reg.Resolve(sc, _tempDir);

            r2.IsOk.Should().BeTrue(r2.Error);
            r2.SourceDesign.Should().NotBeSameAs(firstDesign, "清理外部缓存后重新从磁盘加载");
        }
    }
}
