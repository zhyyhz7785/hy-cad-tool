using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Interfaces;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
namespace HyCADTool.HelpClass
{
    public class CircularStandardArea : StandardAreaBase
    {
        public override int NX { get; protected set; } // 修改为 get 和 protected set
        public override double NXD { get; protected set; }
        public override int NY { get; protected set; }
        public override double NYD { get; protected set; }
        public List<Point> SmallRectPoints { get; private set; } = new List<Point>();
        public List<Point> Centroids { get; private set; } = new List<Point>();
        public List<Polygon> SmallRects { get; private set; } = new List<Polygon>();
        public List<Polyline> CADSmallRects { get; private set; } = new List<Polyline>();
        public List<Point3d> CADSmallRectPoints { get; private set; } = new List<Point3d>();
        public List<Point3d> CADPilePoints { get; private set; } = new List<Point3d>();
        public CircularStandardArea(ICadService cadService, Pile pile, Polygon contour, double minPileCenterDistance,
        (double up, double down, double left, double right) margin, double inputDisplacementRate,
        double inputDistanceFromContour, Polygon insetRect, double scale)
        : base(cadService, pile, contour, PileArrangementType.Circular, minPileCenterDistance, margin,
              inputDisplacementRate, inputDistanceFromContour, insetRect, scale)
        {
            ArrangePiles(pile);
            NetTopologySuiteToCAD();
            ActualDisplacementRate = (CADPilePoints.Count * CalculatePileArea()) / Contour.Area;
        }
        public override void ArrangePiles(Pile pile)
        {
            int nX, nY;
            double cellWidth, cellHeight;
            List<Point> points = new List<Point>();
            List<Point> centroids = new List<Point>();
            GeometryUtils.CalculateGridSizeCircular(this, pile, 0.5, _cadService, out nX, out nY);
            var smallRects = GeometryUtils.GenerateRectPolygons(InsetRect, nX, nY, out points, out centroids, out cellWidth, out cellHeight);
            SmallRects = smallRects;
            NX = nX + 1;
            NY = nY + 1;
            NXD = cellWidth;
            NYD = cellHeight;
            SmallRectPoints = points;
            Centroids.Clear();
            Centroids.AddRange(points);
            Centroids.AddRange(centroids);
        }
        public override void AdjustGridSize(int nx, int ny)
        {
            if (nx <= 0 || ny <= 0) throw new ArgumentException("NX 和 NY 必须大于 0");
            NX = nx;
            NY = ny;
            List<Point> points, centroids;
            double cellWidth, cellHeight;
            SmallRects = GeometryUtils.GenerateRectPolygons(InsetRect, nx, ny, out points, out centroids, out cellWidth, out cellHeight);
            NXD = cellWidth;
            NYD = cellHeight;
            SmallRectPoints = points;
            Centroids.Clear();
            Centroids.AddRange(points);
            Centroids.AddRange(centroids);
            NetTopologySuiteToCAD();
            ActualDisplacementRate = (CADPilePoints.Count * CalculatePileArea()) / Contour.Area;
        }
        public override void NetTopologySuiteToCAD()
        {
            CADSmallRects = SmallRects.ToAutoCadPolyline();
            CADPilePoints = Centroids.ToAutoCadPoints();
            CADSmallRectPoints = SmallRectPoints.ToAutoCadPoints();
            CADInsetRect = InsetRect.ToAutoCadPolyline();
        }
        public override List<Point> GetCentroids() => Centroids;
        public override List<Point> GetSmallRectPoints() => SmallRectPoints;
        public override List<Polyline> GetCADSmallRects() => CADSmallRects;
        public override List<Point3d> GetCADPilePoints() => CADPilePoints;
    }
}