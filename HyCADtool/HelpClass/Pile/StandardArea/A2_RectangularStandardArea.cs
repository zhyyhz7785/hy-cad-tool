using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Interfaces;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
namespace HyCADTool.HelpClass
{
    public class RectangularStandardArea : StandardAreaBase
    {
        public List<Polygon> SmallRects { get; private set; } = new List<Polygon>();
        public override int NX { get; protected set; }
        public override double NXD { get; protected set; }
        public override int NY { get; protected set; }
        public override double NYD { get; protected set; }
        public List<Point> SmallRectPoints { get; private set; } = new List<Point>();
        public List<Point> Centroids { get; private set; } = new List<Point>();
        public List<Polyline> CADSmallRects { get; private set; } = new List<Polyline>();
        public List<Point3d> CADPilePoints { get; private set; } = new List<Point3d>();
        public List<Point3d> CADCentroids { get; private set; } = new List<Point3d>();
        public RectangularStandardArea(ICadService cadService, Pile pile, Polygon contour, double minPileCenterDistance,
            (double up, double down, double left, double right) margin, double inputDisplacementRate,
            double inputDistanceFromContour, Polygon insetRect, double scale)
            : base(cadService, pile, contour, PileArrangementType.Rectangle, minPileCenterDistance, margin,
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
            Dictionary<int, Pile> pileMap = new Dictionary<int, Pile>();
            // 正确调用 CalculateGridSizeRect，提供所有参数
            GeometryUtils.CalculateGridSizeRect(this, pile, 0.5, _cadService, out nX, out nY); // 假设 PileArrangeRate 默认值为 0.5，需从外部传入
            var smallRects = GeometryUtils.GenerateRectPolygons(InsetRect, nX, nY, out points, out centroids, out cellWidth, out cellHeight);
            SmallRects = smallRects;
            NX = nX;
            NY = nY;
            NXD = cellWidth;
            NYD = cellHeight;
            SmallRectPoints = points;
            Centroids = centroids;
            int pileNumber = 1;
            foreach (var p in SmallRectPoints)
            {
                // 创建新的 Pile 对象，复制传入 pile 的属性
                Pile newPile = new Pile(pile.Section, pile.DiameterOrEdge);
                newPile.Center = p;
                pileMap.Add(pileNumber, newPile);
                pileNumber++;
            }
            // 更新类的 PileMap 属性
            PileMap = pileMap;
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
            Centroids = centroids;
            NetTopologySuiteToCAD();
            ActualDisplacementRate = (CADPilePoints.Count * CalculatePileArea()) / Contour.Area;
        }
        public override void NetTopologySuiteToCAD()
        {
            CADSmallRects = SmallRects.ToAutoCadPolyline();
            CADPilePoints = SmallRectPoints.ToAutoCadPoints();
            CADCentroids = Centroids.ToAutoCadPoints();
            CADInsetRect = InsetRect.ToAutoCadPolyline();
        }
        public override List<Point> GetCentroids() => Centroids;
        public override List<Point> GetSmallRectPoints() => SmallRectPoints;
        public override List<Polyline> GetCADSmallRects() => CADSmallRects;
        public override List<Point3d> GetCADPilePoints() => CADPilePoints;
    }
}