using System;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Models.Road
{
    /// <summary>
    /// 领域聚合根 <see cref="RoadDesign"/> 与相关对象的默认值/行为测试。
    /// 只验证纯 C# 行为，不触达 AutoCAD。
    /// </summary>
    public class RoadDesignTests
    {
        [Fact]
        public void NewRoadDesign_HasSchemaAndNonEmptyId()
        {
            var d = new RoadDesign();
            d.Id.Should().NotBe(Guid.Empty);
            d.Schema.Should().Be(SchemaVersion.Current);
            d.Alignments.Should().BeEmpty();
            d.Templates.Should().BeEmpty();
            d.Corridors.Should().BeEmpty();
            d.Nodes.Should().BeEmpty();
        }

        [Fact]
        public void TwoNewRoadDesigns_HaveDistinctIds()
        {
            new RoadDesign().Id.Should().NotBe(new RoadDesign().Id);
        }

        [Fact]
        public void NewAlignment_HasIdAndEmptyElements()
        {
            var a = new Alignment();
            a.Id.Should().NotBe(Guid.Empty);
            a.Elements.Should().BeEmpty();
            a.Profiles.Should().BeEmpty();
            a.Centerline.Should().NotBeNull();
        }

        [Fact]
        public void NewTemplate_HasIdAndEmptyCollections()
        {
            var t = new Template();
            t.Id.Should().NotBe(Guid.Empty);
            t.Points.Should().BeEmpty();
            t.Components.Should().BeEmpty();
        }

        [Fact]
        public void TemplatePoint_MaterialKeyAndBlenderHint_DefaultsToNull_ButAreAssignable()
        {
            var tp = new TemplatePoint();
            tp.MaterialKey.Should().BeNull();
            tp.BlenderExtrudeHint.Should().BeNull();

            tp.MaterialKey = "asphalt_base";
            tp.BlenderExtrudeHint = "pavement";

            tp.MaterialKey.Should().Be("asphalt_base");
            tp.BlenderExtrudeHint.Should().Be("pavement");
        }

        [Fact]
        public void SchemaVersion_CurrentIsNonEmpty()
        {
            SchemaVersion.Current.Should().NotBeNullOrEmpty();
            SchemaVersion.MinimumSupported.Should().NotBeNullOrEmpty();
        }
    }
}
