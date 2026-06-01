using System;
using System.IO;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// B2 验收：<see cref="LandXmlImportService"/> 解析 LandXML 1.2 的基础结构 + 与 Export 的往返。
    /// </summary>
    public class LandXmlImportServiceTests
    {
        [Fact]
        public void ParseXmlString_EmptyOrMalformed_Throws()
        {
            ((Action)(() => LandXmlImportService.ParseXmlString("")))
                .Should().Throw<ArgumentException>();
            ((Action)(() => LandXmlImportService.ParseXmlString("<notLandXml/>")))
                .Should().Throw<InvalidDataException>();
            ((Action)(() => LandXmlImportService.ParseXmlString("<LandXML><foo/></LandXML>")))
                .Should().Throw<InvalidDataException>();
        }

        [Fact]
        public void ParseXmlString_StraightLine_ReadsStartStationAndGeometry()
        {
            string xml = @"<?xml version='1.0' encoding='UTF-8'?>
<LandXML version='1.2' xmlns='http://www.landxml.org/schema/LandXML-1.2'>
  <Alignments>
    <Alignment name='Imported' length='100' staStart='0'>
      <CoordGeom>
        <Line length='100' dir='90'>
          <Start>0 0</Start>
          <End>0 100</End>
        </Line>
      </CoordGeom>
    </Alignment>
  </Alignments>
</LandXML>";
            var aln = LandXmlImportService.ParseXmlString(xml);
            aln.Name.Should().Be("Imported");
            aln.StartStation.Should().BeApproximately(0, 1e-9);
            // LandXML 点是 "Y X"；"0 0"→(X=0,Y=0)；"0 100"→(X=100,Y=0)
            aln.Centerline.VertexCount.Should().Be(2);
            aln.Centerline.GetPointAt(1).X.Should().BeApproximately(100, 1e-6);
            aln.Centerline.GetPointAt(1).Y.Should().BeApproximately(0, 1e-6);
            aln.Elements.Should().HaveCount(1);
            aln.Elements[0].Kind.Should().Be(AlignmentElementKind.Line);
            aln.Elements[0].Length.Should().BeApproximately(100, 1e-6);
        }

        [Fact]
        public void ParseXmlString_WithStaEquations_ReadsAll()
        {
            string xml = @"<?xml version='1.0'?>
<LandXML version='1.2'>
  <Alignment name='A' length='1000' staStart='0'>
    <CoordGeom>
      <Line length='1000' dir='90'>
        <Start>0 0</Start><End>0 1000</End>
      </Line>
    </CoordGeom>
    <StaEquation staAhead='5000' staBack='100' staInternal='100' />
    <StaEquation staAhead='8000' staBack='200' staInternal='300' />
  </Alignment>
</LandXML>";
            var aln = LandXmlImportService.ParseXmlString(xml);
            aln.StationEquations.Should().HaveCount(2);
            aln.StationEquations[0].BeforeRaw.Should().BeApproximately(100, 1e-9);
            aln.StationEquations[0].AheadStation.Should().BeApproximately(5000, 1e-9);
            aln.StationEquations[1].BeforeRaw.Should().BeApproximately(300, 1e-9);
            aln.StationEquations[1].AheadStation.Should().BeApproximately(8000, 1e-9);
        }

        [Fact]
        public void ParseXmlString_Curve_ReadsRadiusAndElements()
        {
            string xml = @"<?xml version='1.0'?>
<LandXML version='1.2'>
  <Alignment name='A' length='78.5398' staStart='0'>
    <CoordGeom>
      <Curve length='78.5398' radius='50' rot='ccw'>
        <Start>0 0</Start>
        <Center>0 50</Center>
        <End>50 50</End>
      </Curve>
    </CoordGeom>
  </Alignment>
</LandXML>";
            var aln = LandXmlImportService.ParseXmlString(xml);
            aln.Elements.Should().HaveCount(1);
            aln.Elements[0].Kind.Should().Be(AlignmentElementKind.CircularArc);
            aln.Elements[0].Radius.Should().BeApproximately(50, 1e-9);
            aln.Centerline.VertexCount.Should().Be(2);
            // bulge 应非零
            aln.Centerline.HasArcs.Should().BeTrue();
        }

        [Fact]
        public void ParseXmlString_Spiral_ReadsLengthAndAParameter()
        {
            // Ls=20, R=50 → A=√(50·20)=√1000≈31.623
            string xml = @"<?xml version='1.0'?>
<LandXML version='1.2'>
  <Alignment name='A' length='20' staStart='0'>
    <CoordGeom>
      <Spiral length='20' radiusStart='INF' radiusEnd='50' rot='ccw' spiType='clothoid'>
        <Start>0 0</Start>
        <End>0 20</End>
      </Spiral>
    </CoordGeom>
  </Alignment>
</LandXML>";
            var aln = LandXmlImportService.ParseXmlString(xml);
            aln.Elements.Should().HaveCount(1);
            aln.Elements[0].Kind.Should().Be(AlignmentElementKind.Spiral);
            aln.Elements[0].Length.Should().BeApproximately(20, 1e-9);
            aln.Elements[0].SpiralParameterA.Should().BeApproximately(Math.Sqrt(50 * 20), 1e-9);
        }

        [Fact]
        public void RoundTrip_ExportThenImport_PreservesStartStationAndEquations()
        {
            // 构造源 alignment：三点 PI（一个圆），加两条方程
            var src = new Alignment
            {
                Name = "RT",
                StartStation = 1000,
                Source = new AlignmentSource { Kind = AlignmentSourceKind.PiTable },
            };
            src.Source.PiElements.Add(new AlignmentPiInput { P = new Point2D(0, 0) });
            src.Source.PiElements.Add(new AlignmentPiInput { P = new Point2D(100, 0), Radius = 50 });
            src.Source.PiElements.Add(new AlignmentPiInput { P = new Point2D(100, 100) });
            var els = System.Linq.Enumerable.ToList(
                System.Linq.Enumerable.Select(src.Source.PiElements,
                    e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag)));
            src.Centerline = AlignmentPiDesigner.Build(els, null).Polyline;
            src.StationEquations.Add(new StationEquation(50, 7000));

            var xml = LandXmlExportService.BuildXmlString(src);
            var imported = LandXmlImportService.ParseXmlString(xml);

            imported.Name.Should().Be("RT");
            imported.StartStation.Should().BeApproximately(1000, 1e-6);
            imported.StationEquations.Should().HaveCount(1);
            imported.StationEquations[0].BeforeRaw.Should().BeApproximately(50, 1e-6);
            imported.StationEquations[0].AheadStation.Should().BeApproximately(7000, 1e-6);
            imported.Elements.Should().NotBeEmpty();
            imported.Centerline.VertexCount.Should().BeGreaterOrEqualTo(3);
        }
    }
}
