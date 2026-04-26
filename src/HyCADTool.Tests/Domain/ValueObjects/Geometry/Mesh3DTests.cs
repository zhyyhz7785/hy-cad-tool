using System;
using System.Collections.Generic;
using FluentAssertions;
using HyCADTool.Shared.Geometry;
using Xunit;

namespace HyCADTool.Tests.Domain.ValueObjects.Geometry
{
    public class Mesh3DTests
    {
        [Fact]
        public void Empty_HasZeroVerticesAndTriangles()
        {
            var m = Mesh3D.Empty;
            m.Vertices.Count.Should().Be(0);
            m.Indices.Count.Should().Be(0);
            m.TriangleCount.Should().Be(0);
            m.MaterialGroups.Count.Should().Be(0);
        }

        [Fact]
        public void Ctor_WithTriangleMultipleIndices_ComputesTriangleCount()
        {
            var m = new Mesh3D(
                vertices: new[] { new Point3D(0, 0, 0), new Point3D(1, 0, 0), new Point3D(0, 1, 0) },
                indices: new[] { 0, 1, 2 });

            m.TriangleCount.Should().Be(1);
        }

        [Fact]
        public void Ctor_WithNonTriangleMultipleIndices_Throws()
        {
            Action act = () => new Mesh3D(
                vertices: new[] { new Point3D(0, 0, 0), new Point3D(1, 0, 0) },
                indices: new[] { 0, 1 });

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void MaterialGroups_DefaultsToEmpty_NotNull()
        {
            var m = new Mesh3D(new[] { new Point3D() }, new int[0]);
            m.MaterialGroups.Should().NotBeNull();
            m.MaterialGroups.Count.Should().Be(0);
        }

        [Fact]
        public void MaterialRange_ConstructedValuesArePreserved()
        {
            var groups = new Dictionary<string, MaterialRange>
            {
                ["asphalt"] = new MaterialRange(0, 4),
                ["kerb"]    = new MaterialRange(4, 2),
            };
            var m = new Mesh3D(
                new Point3D[0], new int[0], normals: null, materialGroups: groups);

            m.MaterialGroups["asphalt"].StartTriangleIndex.Should().Be(0);
            m.MaterialGroups["asphalt"].TriangleCount.Should().Be(4);
            m.MaterialGroups["kerb"].StartTriangleIndex.Should().Be(4);
        }

        [Fact]
        public void MaterialRange_Negative_Throws()
        {
            Action act = () => new MaterialRange(-1, 0);
            act.Should().Throw<ArgumentOutOfRangeException>();
        }
    }
}
