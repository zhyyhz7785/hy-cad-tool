using System;
using System.Collections.Generic;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Road
{
    public class CorridorSweepServiceAssignmentTests
    {
        [Fact]
        public void SweepWithAssignments_OneSegmentCoversLine_ProducesPolylines()
        {
            var layout = CrossSectionPresets.CreateCjj37UrbanArterial();
            var tpl = CrossSectionLayoutBuilder.ToTemplate(layout, Guid.NewGuid(), "t1");

            var aln = new Alignment { Name = "X" };
            aln.Centerline = new Polyline3D(
                new[] { new Point3D(0, 0, 0), new Point3D(50, 0, 0) }, isClosed: false);
            aln.CrossSectionAssignments.Add(new CrossSectionAssignment
            {
                TemplateId = tpl.Id,
                StartStation = 0,
                EndStation = 50
            });

            var sweep = new CorridorSweepService();
            var (plan, used) = sweep.SweepWithAssignments(aln, new List<Template> { tpl }, aln.CrossSectionAssignments, 5.0);
            plan.LeftRedLine.Should().NotBeNull();
            plan.LeftRedLine.VertexCount.Should().BeGreaterOrEqualTo(2);
            used.Should().BeTrue();
        }
    }
}
