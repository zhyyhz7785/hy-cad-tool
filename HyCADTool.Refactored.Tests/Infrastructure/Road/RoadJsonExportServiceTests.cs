using System;
using System.IO;
using FluentAssertions;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using Newtonsoft.Json;
using Xunit;

namespace HyCADTool.Refactored.Tests.Infrastructure.Road
{
    /// <summary>
    /// P1：<see cref="RoadJsonExportService"/> 的 I/O 与序列化行为测试。
    ///
    /// 用临时目录隔离文件系统副作用；每个测试用完立即清理。
    /// </summary>
    public class RoadJsonExportServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public RoadJsonExportServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "hycad-road-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        /// <summary>
        /// Polyline3D 独立 JSON roundtrip（回归防护）：
        /// 这是 P1 落地时踩到的关键坑——<see cref="Point3D"/> 是 readonly struct，
        /// 默认无参构造函数不存在，Newtonsoft 必须通过 <c>[JsonConstructor]</c> 显式调用
        /// <c>(double x, double y, double z)</c> 版本；否则顶点会退化为 default(0,0,0)。
        /// </summary>
        [Fact]
        public void Polyline3D_JsonRoundtrip_PreservesVertices()
        {
            var original = new Polyline3D(new[]
            {
                new Point3D(1, 2, 3),
                new Point3D(4, 5, 6),
            });

            string json = JsonConvert.SerializeObject(original);
            var back = JsonConvert.DeserializeObject<Polyline3D>(json);

            back.Should().NotBeNull();
            back.VertexCount.Should().Be(2);
            back.GetPointAt(0).X.Should().Be(1);
            back.GetPointAt(0).Y.Should().Be(2);
            back.GetPointAt(0).Z.Should().Be(3);
            back.GetPointAt(1).Z.Should().Be(6);
        }

        /// <summary>
        /// P1.b 回归防护：含弧段的多段线 JSON 往返必须保留 bulge 数组。
        /// 此前只读顶点 / 反向只写 bulge=0，导致 AutoCAD 弧段信息丢失。
        /// </summary>
        [Fact]
        public void Polyline3D_JsonRoundtrip_PreservesBulges()
        {
            var original = new Polyline3D(
                vertices: new[]
                {
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(20, 0, 0),
                },
                isClosed: false,
                bulges: new[] { 1.0, 0.5, 0.0 });

            string json = JsonConvert.SerializeObject(original);
            json.Should().Contain("Bulges", "HasArcs 时 ShouldSerializeBulges 应输出该字段（与项目 Pascal 命名风格一致）");

            var back = JsonConvert.DeserializeObject<Polyline3D>(json);

            back.Should().NotBeNull();
            back.VertexCount.Should().Be(3);
            back.Bulges.Should().Equal(new[] { 1.0, 0.5, 0.0 });
            back.HasArcs.Should().BeTrue();
        }

        /// <summary>
        /// 向后兼容：全直线 polyline 不应把 bulges 写进 JSON（节省体积 + 与 P1 老文件二进制一致）。
        /// 反序列化时没有 bulges 字段必须能平滑得到全 0 数组。
        /// </summary>
        [Fact]
        public void Polyline3D_AllStraight_JsonOmitsBulgesField_AndLoadsAsZeroes()
        {
            var original = new Polyline3D(new[]
            {
                new Point3D(0, 0, 0),
                new Point3D(10, 0, 0),
            });

            string json = JsonConvert.SerializeObject(original);
            json.Should().NotContain("Bulges", "全 0 bulges 时必须省略字段以向后兼容 P1 老 JSON");

            var back = JsonConvert.DeserializeObject<Polyline3D>(json);
            back.Bulges.Should().HaveCount(2).And.AllBeEquivalentTo(0.0);
            back.HasArcs.Should().BeFalse();
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); } catch { }
        }

        [Fact]
        public void GetDefaultJsonPath_ReplacesExtensionWithRoadDesignJson()
        {
            RoadJsonExportService.GetDefaultJsonPath("C:\\prj\\demo.dwg")
                .Should().Be(Path.Combine("C:\\prj", "demo.roaddesign.json"));
        }

        [Fact]
        public void GetDefaultJsonPath_WithEmpty_ReturnsNull()
        {
            RoadJsonExportService.GetDefaultJsonPath(null).Should().BeNull();
            RoadJsonExportService.GetDefaultJsonPath(string.Empty).Should().BeNull();
        }

        [Fact]
        public void SaveThenLoad_Roundtrip_PreservesAlignmentCenterline()
        {
            var svc = new RoadJsonExportService();
            var path = Path.Combine(_tempDir, "roundtrip.roaddesign.json");

            var design = new RoadDesign { ProjectName = "test" };
            var alignment = new Alignment
            {
                Name = "A1",
                Centerline = new Polyline3D(new[]
                {
                    new Point3D(0, 0, 0),
                    new Point3D(10, 0, 0),
                    new Point3D(20, 5, 0),
                })
            };
            design.Alignments.Add(alignment);

            svc.Save(design, path);
            var loaded = svc.Load(path);

            loaded.Should().NotBeNull();
            loaded.Id.Should().Be(design.Id);
            loaded.ProjectName.Should().Be("test");
            loaded.Alignments.Should().HaveCount(1);
            loaded.Alignments[0].Id.Should().Be(alignment.Id);
            loaded.Alignments[0].Name.Should().Be("A1");
            loaded.Alignments[0].Centerline.VertexCount.Should().Be(3);
            loaded.Alignments[0].Centerline.GetPointAt(2).X.Should().Be(20);
            loaded.Alignments[0].Centerline.GetPointAt(2).Y.Should().Be(5);
        }

        /// <summary>
        /// P1.b：含弧段的 Alignment 经完整 Save → Load 仍保留 bulge，
        /// 这是 AutoCAD 拾取含弧多段线后反向绘制能恢复弧段的前提。
        /// </summary>
        [Fact]
        public void SaveThenLoad_AlignmentWithArcBulge_PreservesBulges()
        {
            var svc = new RoadJsonExportService();
            var path = Path.Combine(_tempDir, "arc-roundtrip.roaddesign.json");

            var design = new RoadDesign { ProjectName = "arc-case" };
            var alignment = new Alignment
            {
                Name = "A-arc",
                Centerline = new Polyline3D(
                    vertices: new[]
                    {
                        new Point3D(0, 0, 0),
                        new Point3D(10, 0, 0),
                        new Point3D(20, 10, 0),
                    },
                    isClosed: false,
                    bulges: new[] { 0.414213562, 0.0, 0.0 })
            };
            design.Alignments.Add(alignment);

            svc.Save(design, path);
            var loaded = svc.Load(path);

            loaded.Alignments[0].Centerline.Bulges[0].Should().BeApproximately(0.414213562, 1e-9);
            loaded.Alignments[0].Centerline.HasArcs.Should().BeTrue();
            loaded.Alignments[0].Centerline.VertexCount.Should().Be(3);
        }

        [Fact]
        public void Load_NonExistentFile_ReturnsNull()
        {
            var svc = new RoadJsonExportService();
            svc.Load(Path.Combine(_tempDir, "missing.json")).Should().BeNull();
        }

        [Fact]
        public void Load_BelowMinSchema_Throws()
        {
            var svc = new RoadJsonExportService();
            var path = Path.Combine(_tempDir, "old-schema.json");
            File.WriteAllText(path, "{ \"Id\": \"" + Guid.NewGuid() + "\", \"Schema\": \"0.0.1\" }");

            Action act = () => svc.Load(path);
            act.Should().Throw<InvalidOperationException>().WithMessage("*schema*");
        }

        [Fact]
        public void Save_CreatesMissingDirectory()
        {
            var svc = new RoadJsonExportService();
            var nested = Path.Combine(_tempDir, "nested", "deeper");
            var path = Path.Combine(nested, "x.roaddesign.json");

            svc.Save(new RoadDesign(), path);

            File.Exists(path).Should().BeTrue();
        }

        [Fact]
        public void Save_AtomicReplace_DoesNotLoseExistingFile_WhenOverwriting()
        {
            var svc = new RoadJsonExportService();
            var path = Path.Combine(_tempDir, "atomic.roaddesign.json");

            var first = new RoadDesign { ProjectName = "v1" };
            svc.Save(first, path);

            var second = new RoadDesign { ProjectName = "v2" };
            svc.Save(second, path);

            File.Exists(path).Should().BeTrue();
            File.Exists(path + ".tmp").Should().BeFalse("临时文件必须在原子替换后消失");
            svc.Load(path).ProjectName.Should().Be("v2");
        }

        // =============================================================
        //  SaveForDocument：命令收尾同步写盘（v1.1 取代防抖持久化服务）
        // =============================================================

        [Fact]
        public void SaveForDocument_WithContent_WritesFileAndReturnsPath()
        {
            var svc = new RoadJsonExportService();
            var dwg = Path.Combine(_tempDir, "Demo.dwg");
            var design = new RoadDesign { ProjectName = "demo" };
            design.Alignments.Add(new Alignment
            {
                Name = "A-1",
                Centerline = new Polyline3D(new[] { new Point3D(0, 0, 0), new Point3D(10, 0, 0) })
            });

            var written = svc.SaveForDocument(design, dwg);

            written.Should().EndWith("Demo.roaddesign.json");
            File.Exists(written).Should().BeTrue();
            svc.Load(written).Alignments.Should().HaveCount(1);
        }

        [Fact]
        public void SaveForDocument_WhenDesignIsEmpty_SkipsWriteAndReturnsNull()
        {
            var svc = new RoadJsonExportService();
            var dwg = Path.Combine(_tempDir, "Empty.dwg");
            var design = new RoadDesign { ProjectName = "empty" };

            var written = svc.SaveForDocument(design, dwg);

            written.Should().BeNull("空 design 绝不落盘，避免生成误导性空 JSON");
            File.Exists(Path.Combine(_tempDir, "Empty.roaddesign.json")).Should().BeFalse();
        }

        [Fact]
        public void SaveForDocument_WhenDesignIsNull_ReturnsNullWithoutThrowing()
        {
            var svc = new RoadJsonExportService();
            var dwg = Path.Combine(_tempDir, "Null.dwg");

            Action act = () => svc.SaveForDocument(null, dwg);

            act.Should().NotThrow();
            svc.SaveForDocument(null, dwg).Should().BeNull();
        }

        [Fact]
        public void SaveForDocument_WhenDocumentNameBlank_SkipsWriteAndReturnsNull()
        {
            var svc = new RoadJsonExportService();
            var design = new RoadDesign { ProjectName = "x" };
            design.Alignments.Add(new Alignment { Name = "A-1" });

            svc.SaveForDocument(design, null).Should().BeNull();
            svc.SaveForDocument(design, "").Should().BeNull();
            svc.SaveForDocument(design, "   ").Should().BeNull("空白 path 对应 DWG 未保存场景，静默跳过");
        }
    }
}
