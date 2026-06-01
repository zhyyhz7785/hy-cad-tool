using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using Newtonsoft.Json;
using Xunit;

namespace HyCADTool.Tests.Infrastructure.Road
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

        // =============================================================
        //  P3-I1-B：Intersection JSON 往返
        // =============================================================

        /// <summary>
        /// P3-I1-B 回归防护：<see cref="RoadDesign.Intersections"/> 中的
        /// <see cref="Intersection"/> 含 <see cref="IntersectionLeg"/>（readonly struct）+
        /// <see cref="CornerArc"/>（readonly struct）嵌套。
        ///
        /// Save → Load 往返后：Id / Name / Center / Legs / CornerArcs 全部保留，
        /// 不发生"struct 退化为 default"的 Point3D 同款坑。
        /// </summary>
        [Fact]
        public void SaveThenLoad_Roundtrip_PreservesIntersection()
        {
            var svc = new RoadJsonExportService();
            var path = Path.Combine(_tempDir, "intersection-roundtrip.roaddesign.json");

            var alignmentId = Guid.NewGuid();
            var legs = new[]
            {
                new IntersectionLeg(
                    alignmentId,
                    approachRawDistance: 0,
                    approachPoint: new Point2D(-50, 0),
                    inwardDirection: new Vector2D(1, 0),
                    halfWidth: 7.5,
                    tag: "W"),
                new IntersectionLeg(
                    alignmentId,
                    approachRawDistance: 100,
                    approachPoint: new Point2D(50, 0),
                    inwardDirection: new Vector2D(-1, 0),
                    halfWidth: 7.5,
                    tag: "E"),
            };
            var arc = new CornerArc(
                legIndexA: 0,
                legIndexB: 1,
                center: new Point2D(0, 7.5),
                radius: 5.0,
                startPoint: new Point2D(-5, 7.5),
                endPoint: new Point2D(5, 7.5),
                startAngle: Math.PI,
                endAngle: 0,
                sweepAngle: -Math.PI);

            var intersection = new Intersection
            {
                Name = "K0+100 @ 东湖路",
                Center = new Point2D(0, 0),
                DefaultCornerRadius = 20.0,
                DesignSpeed = 30.0,
            };
            intersection.Legs.AddRange(legs);
            intersection.CornerArcs.Add(arc);

            var design = new RoadDesign { ProjectName = "p3-i1-b" };
            design.Intersections.Add(intersection);

            svc.Save(design, path);
            var loaded = svc.Load(path);

            loaded.Should().NotBeNull();
            loaded.Intersections.Should().HaveCount(1);
            var back = loaded.Intersections[0];

            back.Id.Should().Be(intersection.Id);
            back.Name.Should().Be("K0+100 @ 东湖路");
            back.Center.X.Should().Be(0);
            back.Center.Y.Should().Be(0);
            back.DefaultCornerRadius.Should().Be(20.0);
            back.DesignSpeed.Should().Be(30.0);

            back.Legs.Should().HaveCount(2);
            back.Legs[0].AlignmentId.Should().Be(alignmentId);
            back.Legs[0].ApproachPoint.X.Should().Be(-50);
            back.Legs[0].InwardDirection.X.Should().Be(1);
            back.Legs[0].HalfWidth.Should().Be(7.5);
            back.Legs[0].Tag.Should().Be("W");
            back.Legs[1].Tag.Should().Be("E");

            back.CornerArcs.Should().HaveCount(1);
            var ba = back.CornerArcs[0];
            ba.LegIndexA.Should().Be(0);
            ba.LegIndexB.Should().Be(1);
            ba.Radius.Should().Be(5.0);
            ba.Center.X.Should().Be(0);
            ba.Center.Y.Should().Be(7.5);
            ba.SweepAngle.Should().BeApproximately(-Math.PI, 1e-9);
        }

        /// <summary>
        /// P3-I1-C 回归防护：<see cref="Intersection.CurbRamps"/>（readonly struct <see cref="CurbRamp"/>）+
        /// <see cref="Intersection.TactilePavings"/>（class <see cref="TactilePaving"/>，含 <see cref="TactilePaving.Centerline"/>
        /// <c>List&lt;Point2D&gt;</c>）的 JSON 往返。
        /// </summary>
        [Fact]
        public void SaveThenLoad_Roundtrip_PreservesCurbRampsAndTactilePavings()
        {
            var svc = new RoadJsonExportService();
            var path = Path.Combine(_tempDir, "i1c-roundtrip.roaddesign.json");

            var intersection = new Intersection { Name = "accessibility-case" };
            intersection.CurbRamps.Add(new CurbRamp(
                cornerArcIndex: 0,
                kind: CurbRampKind.Fan,
                frontCenter: new Point2D(3.0, 4.0),
                tangent: new Vector2D(1, 0),
                outwardNormal: new Vector2D(0, 1),
                width: 1.5,
                depth: 2.0,
                slope: 1.0 / 12.0));
            intersection.TactilePavings.Add(new TactilePaving
            {
                Kind = TactilePavingKind.Advance,
                Centerline = new List<Point2D>
                {
                    new Point2D(0, 0),
                    new Point2D(10, 0),
                },
                Width = 0.30,
                CornerArcIndex = -1,
            });
            intersection.TactilePavings.Add(new TactilePaving
            {
                Kind = TactilePavingKind.Stop,
                Centerline = new List<Point2D>
                {
                    new Point2D(5, 7.5),
                    new Point2D(6.5, 7.5),
                },
                Width = 0.60,
                CornerArcIndex = 0,
            });

            var design = new RoadDesign { ProjectName = "p3-i1-c" };
            design.Intersections.Add(intersection);

            svc.Save(design, path);
            var loaded = svc.Load(path);

            var back = loaded.Intersections[0];
            back.CurbRamps.Should().HaveCount(1);
            back.CurbRamps[0].Kind.Should().Be(CurbRampKind.Fan);
            back.CurbRamps[0].FrontCenter.X.Should().Be(3.0);
            back.CurbRamps[0].FrontCenter.Y.Should().Be(4.0);
            back.CurbRamps[0].Depth.Should().Be(2.0);

            back.TactilePavings.Should().HaveCount(2);
            back.TactilePavings[0].Kind.Should().Be(TactilePavingKind.Advance);
            back.TactilePavings[0].Centerline.Should().HaveCount(2);
            back.TactilePavings[0].Centerline[1].X.Should().Be(10);
            back.TactilePavings[0].Width.Should().Be(0.30);
            back.TactilePavings[1].Kind.Should().Be(TactilePavingKind.Stop);
            back.TactilePavings[1].CornerArcIndex.Should().Be(0);
        }

        /// <summary>
        /// v1.1 回归防护：<see cref="Intersection.HasKerbChain"/> 布尔开关的 JSON 往返。
        /// 本字段是唯一需要持久化的 Kerb 状态（KerbSegment 本身为派生量，不落 JSON）。
        /// </summary>
        [Fact]
        public void SaveThenLoad_Roundtrip_PreservesHasKerbChain()
        {
            var svc = new RoadJsonExportService();
            var path = Path.Combine(_tempDir, "v11-kerbchain-roundtrip.roaddesign.json");

            var design = new RoadDesign { ProjectName = "v11-kerb" };
            design.Intersections.Add(new Intersection { Name = "kerb-on", HasKerbChain = true });
            design.Intersections.Add(new Intersection { Name = "kerb-off" /* 默认 false */ });

            svc.Save(design, path);
            var loaded = svc.Load(path);

            loaded.Intersections.Should().HaveCount(2);
            loaded.Intersections[0].HasKerbChain.Should().BeTrue();
            loaded.Intersections[1].HasKerbChain.Should().BeFalse();
        }

        /// <summary>
        /// v1.1 回归防护：<see cref="Intersection.Crosswalks"/>（readonly struct <see cref="Crosswalk"/>）
        /// 的 JSON 往返 —— base 点 / Outward / 所有参数字段都必须复原。
        /// </summary>
        [Fact]
        public void SaveThenLoad_Roundtrip_PreservesCrosswalks()
        {
            var svc = new RoadJsonExportService();
            var path = Path.Combine(_tempDir, "v11-crosswalk-roundtrip.roaddesign.json");

            var intersection = new Intersection { Name = "crosswalk-case" };
            intersection.Crosswalks.Add(new Crosswalk(
                legIndex: 0,
                baseLeft: new Point2D(-7.5, 20.0),
                baseRight: new Point2D(7.5, 20.0),
                outward: new Vector2D(0, 1),
                gapWidth: 1.0,
                width: 5.0,
                stopLineDistance: 2.0,
                stripeSpacing: 0.6,
                stripeWidth: 0.4));
            intersection.Crosswalks.Add(new Crosswalk(
                legIndex: 2,
                baseLeft: new Point2D(-20.0, -7.5),
                baseRight: new Point2D(-20.0, 7.5),
                outward: new Vector2D(-1, 0),
                gapWidth: 0.5,
                width: 4.0,
                stopLineDistance: 1.5,
                stripeSpacing: 0.45,
                stripeWidth: 0.4));

            var design = new RoadDesign { ProjectName = "v11-crosswalk" };
            design.Intersections.Add(intersection);

            svc.Save(design, path);
            var loaded = svc.Load(path);

            var back = loaded.Intersections[0];
            back.Crosswalks.Should().HaveCount(2);

            back.Crosswalks[0].LegIndex.Should().Be(0);
            back.Crosswalks[0].BaseLeft.X.Should().Be(-7.5);
            back.Crosswalks[0].BaseRight.X.Should().Be(7.5);
            back.Crosswalks[0].Outward.Y.Should().Be(1);
            back.Crosswalks[0].GapWidth.Should().Be(1.0);
            back.Crosswalks[0].Width.Should().Be(5.0);
            back.Crosswalks[0].StopLineDistance.Should().Be(2.0);
            back.Crosswalks[0].StripeSpacing.Should().Be(0.6);

            back.Crosswalks[1].LegIndex.Should().Be(2);
            back.Crosswalks[1].Outward.X.Should().Be(-1);
            back.Crosswalks[1].Width.Should().Be(4.0);
            back.Crosswalks[1].StripeSpacing.Should().Be(0.45);
        }

        /// <summary>
        /// IsEmpty 联动：仅含 Intersection（无 Alignment / Template / ...）的 design
        /// 必须被 <see cref="RoadJsonExportService.SaveForDocument"/> 当作非空而落盘，
        /// 否则用户只画交叉口时 JSON 不会生成，下次打开丢数据。
        /// </summary>
        [Fact]
        public void SaveForDocument_WithOnlyIntersection_WritesFile()
        {
            var svc = new RoadJsonExportService();
            var dwg = Path.Combine(_tempDir, "ix-only.dwg");
            var design = new RoadDesign { ProjectName = "ix-only" };
            design.Intersections.Add(new Intersection { Name = "solo" });

            var written = svc.SaveForDocument(design, dwg);

            written.Should().NotBeNull();
            File.Exists(written).Should().BeTrue();
            svc.Load(written).Intersections.Should().HaveCount(1);
        }
    }
}
