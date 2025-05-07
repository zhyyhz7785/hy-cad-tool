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
        public double Scale { get; protected set; } // 新增比例属性，默认为 1.0

        protected StandardAreaBase(Pile pile, Polygon contour, PileArrangementType arrangementType, double minPileCenterDistance,
            (double up, double down, double left, double right) margin, double inputDisplacementRate,
            double inputDistanceFromContour, Polygon insetRect, double scale = 1.0)
        {
            Pile = pile;
            Contour = contour;
            ArrangementType = arrangementType;
            MinPileCenterDistance = minPileCenterDistance;
            Margin = margin;
            InputDistanceFromContour = inputDistanceFromContour;
            InputDisplacementRate = inputDisplacementRate;
            InsetRect = insetRect;
            Scale = scale;
        }

        public abstract void ArrangePiles(Pile pile); // NetTopologySuite 计算
        public abstract void NetTopologySuiteToCAD(); // 转换为 AutoCAD 数据
        public abstract void CreateTable(Point3d insertionPoint, string csvFilePath); // 创建表格
        public abstract void DrawInCAD(); // 新增绘制方法，仅用于 AutoCAD 绘图
        public abstract void AdjustGridSize(int nx, int ny); // 新增手动调整 NX 和 NY

        protected double CalculatePileArea()
        {
            if (Pile.Section == SectionType.Circle)
            {
                return Math.PI * Math.Pow(Pile.DiameterOrEdge / 2, 2) * Scale * Scale; // 比例影响面积
            }
            else
            {
                return Math.Pow(Pile.DiameterOrEdge * Scale, 2); // 比例影响面积
            }
        }
    }
}