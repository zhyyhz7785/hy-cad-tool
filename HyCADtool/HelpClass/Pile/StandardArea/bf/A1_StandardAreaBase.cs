using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;

namespace HyCADTool.HelpClass
{
    public abstract class StandardAreaBase
    {
        // 基础属性
        public Polygon Contour { get; protected set; }
        public Polygon InsetRect { get; protected set; }
        public Polyline CADInsetRect { get; protected set; }
        public PileArrangementType ArrangementType { get; protected set; }
        public Dictionary<int, Pile> PileMap { get; protected set; } = new Dictionary<int, Pile>();
        public Pile Pile { get; protected set; }
        public List<Point> Piles { get; protected set; } = new List<Point>();
        public double MinPileCenterDistance { get; protected set; }
        public (double up, double down, double left, double right) Margin { get; protected set; }
        public double InputDistanceFromContour { get; protected set; }
        public double ActualDistanceFromContour { get; protected set; }
        public double InputDisplacementRate { get; protected set; }
        public double ActualDisplacementRate { get; protected set; }

        protected StandardAreaBase(Pile pile, Polygon contour, PileArrangementType arrangementType, double minPileCenterDistance,
            (double up, double down, double left, double right) margin, double inputDisplacementRate,
            double inputDistanceFromContour, Polygon insetRect)
        {
            Pile = pile;
            Contour = contour;
            ArrangementType = arrangementType;
            MinPileCenterDistance = minPileCenterDistance;
            Margin = margin;
            InputDistanceFromContour = inputDistanceFromContour;
            InputDisplacementRate = inputDisplacementRate;
            InsetRect = insetRect;
        }

        public abstract void ArrangePiles(Pile pile);
        public abstract void NetTopologySuiteToCAD();
        public abstract void CreateTable(Point3d insertionPoint, string csvFilePath);

        protected double CalculatePileArea()
        {
            if (Pile.Section == SectionType.Circle)
            {
                return Math.PI * Math.Pow(Pile.DiameterOrEdge / 2, 2);
            }
            else
            {
                return Math.Pow(Pile.DiameterOrEdge, 2);
            }
        }
    }
}