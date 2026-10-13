using System;
using System.Collections.Generic;
using System.IO;
using HyCAD.Geometry;
using HyCADTool.Features.Fem.Integration;
using HYFEA.Core.Loads;
using HYFEA.Core.Model;
using HYFEA.Hosting;
using Xunit;

namespace HyCADTool.LinuxTests
{
    public sealed class HyfeaAxisGeometryTests
    {
        [Fact]
        public void SampleByLength_scales_SI_and_keeps_MmN()
        {
            var verts = new[] { new Point2D(0, 0), new Point2D(1000, 0) };

            var mm = HyfeaAxisGeometry.SampleByLength(verts, 2, UnitSystem.MmN);
            Assert.Equal(3, mm.Count);
            Assert.Equal(0, mm[0].X, 9);
            Assert.Equal(500, mm[1].X, 9);
            Assert.Equal(1000, mm[2].X, 9);

            var si = HyfeaAxisGeometry.SampleByLength(verts, 2, UnitSystem.SI);
            Assert.Equal(3, si.Count);
            Assert.Equal(0, si[0].X, 9);
            Assert.Equal(0.5, si[1].X, 9);
            Assert.Equal(1.0, si[2].X, 9);
        }

        [Fact]
        public void InMemory_axis_through_HyfeaAxisGeometry_solves_via_session()
        {
            var inner = InMemoryAxisGeometry.Horizontal(10000.0, segments: 4);
            IFemHostGeometry geom = new HyfeaAxisGeometry(inner.GetAxisNodes());
            var request = new BeamSolveRequest
            {
                YoungsModulus = 2.06e5,
                Area = 8e4,
                InertiaZ = 1.067e9,
                Q = 10.0,
                LoadDirection = BeamLoadDirection.GlobalNegY,
                EndA = BeamSupportKind.Fixed,
                EndB = BeamSupportKind.Free,
                Units = UnitSystem.MmN,
            };

            var result = new BeamSolveSession().Run(geom, request);
            Assert.True(result.Success, result.Error != null ? result.Error.Message : "unknown");
            Assert.NotNull(result.Displacements);
        }

        [Fact]
        public void Cad_integration_files_implement_host_interfaces()
        {
            var root = RepoRoot();
            var mapper = File.ReadAllText(Path.Combine(
                root, "src", "HyCADTool", "Features", "Fem", "Integration", "HyfeaGeometryMapper.cs"));
            var renderer = File.ReadAllText(Path.Combine(
                root, "src", "HyCADTool", "Features", "Fem", "Renderer", "BeamResultRenderer.cs"));
            var panel = File.ReadAllText(Path.Combine(
                root, "src", "HyCADTool", "Features", "Fem", "Views", "BeamMvpPanelViewModel.cs"));

            Assert.Contains("IFemHostGeometry", mapper);
            Assert.Contains("IFemResultSink", renderer);
            Assert.Contains("void Present(", renderer);
            Assert.DoesNotContain("new LinearStaticAnalysis()", panel);
            Assert.DoesNotContain("HYFEA.Core.Model.BeamSupportKind", panel);
            Assert.Contains("(BeamSupportKind)EndA", panel);
            Assert.Contains("BeamSolveSession", panel);
            Assert.Contains("_session.Compute", panel);
        }

        [Fact]
        public void N7_tooltip_is_not_placeholder()
        {
            var root = RepoRoot();
            var fea = File.ReadAllText(Path.Combine(root, "src", "HyCADTool", "Modules", "fea", "commands.json"));
            var recall = File.ReadAllText(Path.Combine(root, "src", "ReCall", "commands.json"));
            Assert.DoesNotContain("N7 占位", fea);
            Assert.DoesNotContain("N7 占位", recall);
            Assert.Contains("\"N7\"", fea);
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "HyCADtoolGpt.sln")))
                dir = dir.Parent;
            if (dir == null)
                throw new InvalidOperationException("找不到 HyCADtoolGpt.sln");
            return dir.FullName;
        }
    }
}
