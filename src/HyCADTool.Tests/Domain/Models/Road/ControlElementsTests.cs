using System;
using System.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Models.Road.ControlElements;
using HyCADTool.Domain.Models.Road.Serialization;
using HyCAD.Geometry;
using Newtonsoft.Json;
using Xunit;

namespace HyCADTool.Tests.Domain.Models.Road
{
    /// <summary>
    /// M6.1 控制体 Domain 行为 + M6.4 JSON 多态序列化回环测试。
    /// </summary>
    public class ControlElementsTests
    {
        [Fact]
        public void ReferencePoint_Defaults()
        {
            var p = new ReferencePoint { Name = "A", Position = new Point3D(1, 2, 3) };
            p.Id.Should().NotBe(Guid.Empty);
            p.Kind.Should().Be(ReferencePoint.KindConstant);
            p.IsTransient.Should().BeFalse();
            ((IHyControl)p).Kind.Should().Be("Control.ReferencePoint");
        }

        [Fact]
        public void ReferenceLine_HasNonNullPolyline()
        {
            var l = new ReferenceLine();
            l.Polyline.Should().NotBeNull();
            l.LineStyle.Should().Be("Dashed");
            l.Kind.Should().Be(ReferenceLine.KindConstant);
        }

        [Fact]
        public void ReferencePlane_DefaultNormalIsUpward()
        {
            var r = new ReferencePlane();
            r.Normal.X.Should().Be(0);
            r.Normal.Y.Should().Be(0);
            r.Normal.Z.Should().Be(1);
            r.DisplaySizeMeters.Should().BeGreaterThan(0);
        }

        [Fact]
        public void SelectionSet_HasEmptyIdListByDefault()
        {
            var s = new SelectionSet();
            s.EntityIds.Should().NotBeNull();
            s.EntityIds.Should().BeEmpty();
            s.IsTransient.Should().BeFalse();
        }

        [Fact]
        public void RoadDesign_ControlsCollection_NotNullByDefault_AndIsEmpty()
        {
            var d = new RoadDesign();
            d.Controls.Should().NotBeNull();
            d.Controls.Should().BeEmpty();
            d.IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void RoadDesign_IsEmpty_FlipsWhenAddingControl()
        {
            var d = new RoadDesign();
            d.Controls.Add(new ReferencePoint { Name = "锚点", Position = new Point3D(0, 0, 0) });
            d.IsEmpty.Should().BeFalse();
        }

        [Fact]
        public void HyControlJsonConverter_RoundTripsAllFourTypes()
        {
            var design = new RoadDesign();
            design.Controls.Add(new ReferencePoint
            {
                Name = "P1",
                Position = new Point3D(10, 20, 0),
                Remark = "测试"
            });
            design.Controls.Add(new ReferenceLine
            {
                Name = "L1",
                LineStyle = "Dotted",
            });
            design.Controls.Add(new ReferencePlane
            {
                Name = "R1",
                Origin = new Point3D(1, 2, 3),
                Normal = new Vector3D(0, 1, 0),
                DisplaySizeMeters = 20
            });
            var sset = new SelectionSet { Name = "S1" };
            sset.EntityIds.Add(Guid.NewGuid());
            sset.EntityIds.Add(Guid.NewGuid());
            design.Controls.Add(sset);

            string json = JsonConvert.SerializeObject(design, Formatting.Indented);
            var back = JsonConvert.DeserializeObject<RoadDesign>(json);

            back.Should().NotBeNull();
            back.Controls.Should().HaveCount(4);
            back.Controls.Should().ContainSingle(c => c is ReferencePoint && c.Name == "P1");
            back.Controls.OfType<ReferencePlane>().Single().Normal.Y.Should().Be(1);
            back.Controls.OfType<SelectionSet>().Single().EntityIds.Should().HaveCount(2);
        }

        [Fact]
        public void HyControlJsonConverter_ThrowsOnMissingKind()
        {
            string json = "{\"Controls\":[{\"Name\":\"X\"}]}";
            Action act = () => JsonConvert.DeserializeObject<RoadDesign>(json);
            act.Should().Throw<JsonSerializationException>()
               .WithMessage("*Kind*");
        }

        [Fact]
        public void HyControlJsonConverter_ThrowsOnUnknownKind()
        {
            string json = "{\"Controls\":[{\"Kind\":\"Control.Alien\",\"Name\":\"X\"}]}";
            Action act = () => JsonConvert.DeserializeObject<RoadDesign>(json);
            act.Should().Throw<JsonSerializationException>()
               .WithMessage("*Control.Alien*");
        }

        [Fact]
        public void HyControlJsonConverter_CanConvert_OnlyIHyControl()
        {
            var c = new HyControlJsonConverter();
            c.CanConvert(typeof(IHyControl)).Should().BeTrue();
            c.CanConvert(typeof(ReferencePoint)).Should().BeTrue();
            c.CanConvert(typeof(RoadDesign)).Should().BeFalse();
        }

        [Fact]
        public void RoadDesign_BackwardCompat_ReadsOldJsonWithoutControlsField()
        {
            // 1.0/1.1 的 JSON 没有 Controls 字段 —— 读取必须不抛、Controls 为空集合
            string oldJson = "{\"Id\":\"" + Guid.NewGuid() + "\",\"ProjectName\":\"Old\",\"Schema\":\"1.0\"}";
            var back = JsonConvert.DeserializeObject<RoadDesign>(oldJson);

            back.Should().NotBeNull();
            back.Controls.Should().NotBeNull();
            back.Controls.Should().BeEmpty();
            back.IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void SchemaVersion_CurrentIsV2()
        {
            // 045 / M2：Schema 升到 v2.0（引入 RoadProject 根聚合 + Civil 占位集合）。
            SchemaVersion.Current.Should().Be("2.0");
            SchemaVersion.CurrentProject.Should().Be("2.0");
            SchemaVersion.MinimumSupported.Should().Be("1.0");
        }
    }
}
