using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices; // 用于 Application
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace HyCADTool.HelpClass
{
    public class RectangularStandardArea : StandardAreaBase
    {
        public List<Polygon> SmallRects { get; private set; } = new List<Polygon>();
        public int NX { get; private set; }
        public double NXD { get; private set; }
        public int NY { get; private set; }
        public double NYD { get; private set; }
        public List<Point> SmallRectPoints { get; private set; } = new List<Point>();
        public List<Point> Centroids { get; private set; } = new List<Point>();
        public List<Polyline> CADSmallRects { get; private set; } = new List<Polyline>();
        public List<Point3d> CADPilePoints { get; private set; } = new List<Point3d>();
        public List<Point3d> CADCentroids { get; private set; } = new List<Point3d>();

        public RectangularStandardArea(Pile pile, Polygon contour, double minPileCenterDistance,
            (double up, double down, double left, double right) margin, double inputDisplacementRate,
            double inputDistanceFromContour, Polygon insetRect)
            : base(pile, contour, PileArrangementType.Rectangle, minPileCenterDistance, margin,
                  inputDisplacementRate, inputDistanceFromContour, insetRect)
        {
            ArrangePiles(pile);
            NetTopologySuiteToCAD();
            double pileArea = CalculatePileArea();
            ActualDisplacementRate = (CADPilePoints.Count * pileArea) / Contour.Area;
        }

        public override void ArrangePiles(Pile pile)
        {
            int nX, nY;
            double cellWidth, cellHeight;
            List<Point> points = new List<Point>();
            List<Point> centroids = new List<Point>();
            GeometryUtils.CalculateGridSizeRect(this, pile,out nX, out nY);
            var smallRects = GeometryUtils.GenerateRectPolygons(InsetRect, nX, nY, out points, out centroids, out cellWidth, out cellHeight);
            SmallRects = smallRects;
            NX = nX;
            NY = nY;
            NXD = cellWidth;
            NYD = cellHeight;
            SmallRectPoints = points;
            Centroids = centroids;
        }

        public override void NetTopologySuiteToCAD()
        {
            CADSmallRects = SmallRects.ToAutoCadPolyline();
            CADPilePoints = SmallRectPoints.ToAutoCadPoints();
            CADCentroids = Centroids.ToAutoCadPoints();
            CADInsetRect = InsetRect.ToAutoCadPolyline();
        }

        public override void CreateTable(Point3d insertionPoint, string csvFilePath)
        {
            double pileArea = CalculatePileArea();
            double calculatedTotalPiles = Contour.Area * InputDisplacementRate / pileArea;

            // 生成 CSV 文件
            using (StreamWriter sw = new StreamWriter(csvFilePath))
            {
                sw.WriteLine("参数名称,值");
                sw.WriteLine("布置类型,矩形");
                sw.WriteLine($"桩直径,{Pile.DiameterOrEdge}");
                sw.WriteLine($"桩面积,{pileArea:F2}");
                sw.WriteLine($"X 方向桩数,{NX}");
                sw.WriteLine($"Y 方向桩数,{NY}");
                sw.WriteLine($"X 间距,{NXD:F2}");
                sw.WriteLine($"Y 间距,{NYD:F2}");
                sw.WriteLine($"计算总桩数,{calculatedTotalPiles:F2}");
                sw.WriteLine($"实际总桩数,{CADPilePoints.Count}");
                sw.WriteLine($"输入置换率,{InputDisplacementRate:F2}");
                sw.WriteLine($"实际置换率,{ActualDisplacementRate:F2}");
            }

            // 将 CSV 转换为 AutoCAD 表格
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction()) // 使用 TransactionManager.StartTransaction
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                Table table = new Table();
                table.SetSize(12, 2); // 12 行 2 列
                table.Position = insertionPoint;
                table.Columns[0].Width = 100; // 使用新 API 设置列宽
                table.Columns[1].Width = 100;

                string[] lines = File.ReadAllLines(csvFilePath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] parts = lines[i].Split(',');
                    table.Cells[i, 0].TextString = parts[0];
                    table.Cells[i, 1].TextString = parts[1];
                }

                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);
                tr.Commit();
            }
        }
    }
}