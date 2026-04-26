using System;
using System.Linq;
using System.Xml.Linq;
using FluentAssertions;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using Xunit;

namespace HyCADTool.Tests.Domain.Services.Road
{
    /// <summary>
    /// B1 验收：<see cref="LandXmlExportService"/> 导出 LandXML 1.2 的结构与数值正确性。
    /// 覆盖：纯直线、直-圆-直、直-缓-圆-缓-直、桩号方程、方位换算。
    /// </summary>
    public class LandXmlExportServiceTests
    {
        private static readonly XNamespace Ns = LandXmlExportService.LandXmlNamespace;

        private static Alignment MakePiAlignment(params (double X, double Y, double R, double LsIn, double LsOut)[] pis)
        {
            var aln = new Alignment
            {
                Name = "TEST",
                StartStation = 0,
                Source = new AlignmentSource { Kind = AlignmentSourceKind.PiTable },
            };
            foreach (var p in pis)
            {
                aln.Source.PiElements.Add(new AlignmentPiInput
                {
                    P = new Point2D(p.X, p.Y),
                    Radius = p.R,
                    SpiralIn = p.LsIn,
                    SpiralOut = p.LsOut,
                });
            }
            // 填充 Centerline（走 Designer），便于 length 属性有值
            var els = aln.Source.PiElements.Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag)).ToList();
            aln.Centerline = AlignmentPiDesigner.Build(els, null).Polyline;
            return aln;
        }

        [Fact]
        public void BuildXmlString_StraightLineOnly_EmitsSingleLineWithCorrectDir()
        {
            // 东向水平直线 100 m：bearing = 0 rad → LandXML azimuth = 90°
            var aln = MakePiAlignment(
                (0, 0, 0, 0, 0),
                (100, 0, 0, 0, 0));

            var xml = LandXmlExportService.BuildXmlString(aln);
            xml.Should().Contain("LandXML");
            var doc = XDocument.Parse(xml);
            var lines = doc.Descendants(Ns + "Line").ToList();
            lines.Should().HaveCount(1);
            double dir = double.Parse(lines[0].Attribute("dir").Value, System.Globalization.CultureInfo.InvariantCulture);
            dir.Should().BeApproximately(90.0, 1e-6, "东向 bearing=0 → azimuth=90°");
            double len = double.Parse(lines[0].Attribute("length").Value, System.Globalization.CultureInfo.InvariantCulture);
            len.Should().BeApproximately(100, 1e-6);
        }

        [Fact]
        public void BuildXmlString_CurvedPath_EmitsCurveWithCenterAndRotation()
        {
            // 直-圆-直：三点 PI，R=50，无缓和
            var aln = MakePiAlignment(
                (0, 0, 0, 0, 0),
                (100, 0, 50, 0, 0),
                (100, 100, 0, 0, 0));

            var xml = LandXmlExportService.BuildXmlString(aln);
            var doc = XDocument.Parse(xml);
            var curves = doc.Descendants(Ns + "Curve").ToList();
            curves.Should().HaveCount(1);
            var c = curves[0];
            double radius = double.Parse(c.Attribute("radius").Value, System.Globalization.CultureInfo.InvariantCulture);
            radius.Should().BeApproximately(50, 1e-6);
            c.Attribute("rot").Value.Should().BeOneOf("cw", "ccw");
            c.Elements(Ns + "Start").Should().HaveCount(1);
            c.Elements(Ns + "Center").Should().HaveCount(1);
            c.Elements(Ns + "End").Should().HaveCount(1);
        }

        [Fact]
        public void BuildXmlString_WithSpirals_EmitsSpiralsWithInfRadius()
        {
            // 三点 PI：R=80，Ls_in=Ls_out=20
            var aln = MakePiAlignment(
                (0, 0, 0, 0, 0),
                (200, 0, 80, 20, 20),
                (200, 200, 0, 0, 0));

            var xml = LandXmlExportService.BuildXmlString(aln);
            var doc = XDocument.Parse(xml);
            var spirals = doc.Descendants(Ns + "Spiral").ToList();
            spirals.Should().NotBeEmpty();
            spirals[0].Attribute("radiusStart").Value.Should().Be("INF");
            spirals[0].Attribute("spiType").Value.Should().Be("clothoid");
        }

        [Fact]
        public void BuildXmlString_WithStaEquations_EmitsOnePerEquation()
        {
            var aln = MakePiAlignment(
                (0, 0, 0, 0, 0),
                (500, 0, 0, 0, 0));
            aln.StationEquations.Add(new StationEquation(100, 1000));
            aln.StationEquations.Add(new StationEquation(300, 5000));

            var xml = LandXmlExportService.BuildXmlString(aln);
            var doc = XDocument.Parse(xml);
            var eqs = doc.Descendants(Ns + "StaEquation").ToList();
            eqs.Should().HaveCount(2);
            double staAhead0 = double.Parse(eqs[0].Attribute("staAhead").Value, System.Globalization.CultureInfo.InvariantCulture);
            staAhead0.Should().BeApproximately(1000, 1e-6);
            double staInternal1 = double.Parse(eqs[1].Attribute("staInternal").Value, System.Globalization.CultureInfo.InvariantCulture);
            staInternal1.Should().BeApproximately(300, 1e-6);
        }

        [Fact]
        public void BearingToAzimuthDeg_And_Inverse_AreConsistent()
        {
            // 关键角度： East(bearing=0) → 90°；North(bearing=π/2) → 0°；West(bearing=π) → 270°；South(bearing=-π/2) → 180°
            LandXmlExportService.BearingToAzimuthDeg(0).Should().BeApproximately(90, 1e-9);
            LandXmlExportService.BearingToAzimuthDeg(Math.PI / 2).Should().BeApproximately(0, 1e-9);
            LandXmlExportService.BearingToAzimuthDeg(Math.PI).Should().BeApproximately(270, 1e-9);
            LandXmlExportService.BearingToAzimuthDeg(-Math.PI / 2).Should().BeApproximately(180, 1e-9);

            // 往返
            var angles = new[] { 0.0, 0.3, 1.0, 2.0, 3.0, -1.5 };
            foreach (var a in angles)
            {
                var back = LandXmlExportService.AzimuthDegToBearing(
                    LandXmlExportService.BearingToAzimuthDeg(a));
                // 规范化 a 到 (-π, π]
                double aNorm = a;
                while (aNorm > Math.PI) aNorm -= 2 * Math.PI;
                while (aNorm <= -Math.PI) aNorm += 2 * Math.PI;
                back.Should().BeApproximately(aNorm, 1e-9, $"a={a}");
            }
        }

        [Fact]
        public void ArcCenter_SemicircleEastward_IsAboveChordCenter_ForCcw()
        {
            // 弦 (0,0)→(100,0)，半径=60，ccw → 圆心在左法向（+Y）
            var s = new Point2D(0, 0);
            var e = new Point2D(100, 0);
            var c = LandXmlExportService.ArcCenter(s, e, 60, "ccw");
            c.X.Should().BeApproximately(50, 1e-9);
            c.Y.Should().BeGreaterThan(0, "ccw 圆心在左侧（+Y）");

            var cw = LandXmlExportService.ArcCenter(s, e, 60, "cw");
            cw.Y.Should().BeLessThan(0, "cw 圆心在右侧（−Y）");
        }

        [Fact]
        public void Document_HasLandXmlNamespace()
        {
            var aln = MakePiAlignment((0, 0, 0, 0, 0), (10, 0, 0, 0, 0));
            var xml = LandXmlExportService.BuildXmlString(aln);
            xml.Should().Contain("http://www.landxml.org/schema/LandXML-1.2");
        }
    }
}
