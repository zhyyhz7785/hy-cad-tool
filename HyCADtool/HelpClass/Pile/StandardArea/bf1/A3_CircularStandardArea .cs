using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.IO;
using HyCADTool.Tools;

namespace HyCADTool.HelpClass
{
    public class CircularStandardArea : StandardAreaBase
    {
        public int NX { get; private set; }
        public double NXD { get; private set; }
        public int NY { get; private set; }
        public double NYD { get; private set; }
        public List<Point> SmallRectPoints { get; private set; } = new List<Point>();
        public List<Point> Centroids { get; private set; } = new List<Point>();
        public List<Polygon> SmallRects { get; private set; } = new List<Polygon>();
        public List<Polyline> CADSmallRects { get; private set; } = new List<Polyline>();
        public List<Point3d> CADSmallRectPoints { get; private set; } = new List<Point3d>();
        public List<Point3d> CADPilePoints { get; private set; } = new List<Point3d>();

        public CircularStandardArea(Pile pile, Polygon contour, double minPileCenterDistance,
            (double up, double down, double left, double right) margin, double inputDisplacementRate,
            double inputDistanceFromContour, Polygon insetRect, double scale = 1.0)
            : base(pile, contour, PileArrangementType.Circular, minPileCenterDistance, margin,
                  inputDisplacementRate, inputDistanceFromContour, insetRect, scale)
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
            List<Point> combined = new List<Point>();
            List<Point> points = new List<Point>();
            List<Point> centroids = new List<Point>();
            GeometryUtils.CalculateGridSizeCircular(this, out nX, out nY);
            var smallRects = GeometryUtils.GenerateRectPolygons(InsetRect, nX, nY, out points, out centroids, out cellWidth, out cellHeight);
            SmallRects = smallRects;
            NX = nX;
            NY = nY;
            NXD = cellWidth;
            NYD = cellHeight;
            SmallRectPoints = points;
            combined.AddRange(points);
            combined.AddRange(centroids);
            Centroids = combined;
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
            NetTopologySuiteToCAD(); // 更新 CAD 数据
            double pileArea = CalculatePileArea();
            ActualDisplacementRate = (CADPilePoints.Count * pileArea) / Contour.Area;
        }

        public override void NetTopologySuiteToCAD()
        {
            CADSmallRects = SmallRects.ToAutoCadPolyline();
            CADPilePoints = Centroids.ToAutoCadPoints();
            CADSmallRectPoints = SmallRectPoints.ToAutoCadPoints();
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
                sw.WriteLine("布置类型,圆形");
                sw.WriteLine($"桩直径,{Pile.DiameterOrEdge}");
                sw.WriteLine($"比例,{Scale:F2}"); // 新增比例
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
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                Table table = new Table();
                table.SetSize(13, 2); // 增加一行显示比例
                table.Position = insertionPoint;
                table.Columns[0].Width = 100 * Scale; // 表格宽度随比例变化
                table.Columns[1].Width = 100 * Scale;

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

        public override void DrawInCAD()
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                // 1. 绘制桩 (图层: 02_hy_1桩_主)
                EtGpt.CreateLayer("02_hy_1桩_主", 7);
                foreach (var point in CADPilePoints)
                {
                    if (Pile.Section == SectionType.Circle)
                    {
                        Circle circle = new Circle(point, Vector3d.ZAxis, Pile.DiameterOrEdge * Scale / 2);
                        circle.SetLayer("02_hy_1桩_主");
                        btr.AppendEntity(circle);
                        tr.AddNewlyCreatedDBObject(circle, true);
                    }
                    else // Square
                    {
                        Polyline square = new Polyline();
                        double halfEdge = Pile.DiameterOrEdge * Scale / 2;
                        square.AddVertexAt(0, new Point2d(point.X - halfEdge, point.Y - halfEdge), 0, 0, 0);
                        square.AddVertexAt(1, new Point2d(point.X + halfEdge, point.Y - halfEdge), 0, 0, 0);
                        square.AddVertexAt(2, new Point2d(point.X + halfEdge, point.Y + halfEdge), 0, 0, 0);
                        square.AddVertexAt(3, new Point2d(point.X - halfEdge, point.Y + halfEdge), 0, 0, 0);
                        square.Closed = true;
                        square.SetLayer("02_hy_1桩_主");
                        btr.AppendEntity(square);
                        tr.AddNewlyCreatedDBObject(square, true);
                    }
                }

                // 2. 绘制 CADSmallRects (图层: 02_hy_1桩_服务区域, 颜色150)
                EtGpt.CreateLayer("02_hy_1桩_服务区域", 150);
                foreach (var rect in CADSmallRects)
                {
                    rect.SetLayer("02_hy_1桩_服务区域");
                    btr.AppendEntity(rect);
                    tr.AddNewlyCreatedDBObject(rect, true);
                }

                // 3. 绘制 CADInsetRect (图层: 02_hy_3桩_地基内轮廓, 颜色150)
                EtGpt.CreateLayer("02_hy_3桩_地基内轮廓", 150);
                CADInsetRect.SetLayer("02_hy_3桩_地基内轮廓");
                btr.AppendEntity(CADInsetRect);
                tr.AddNewlyCreatedDBObject(CADInsetRect, true);

                // 4. 绘制表格 (图层: 00_hy_4公共_表格)
                EtGpt.CreateLayer("00_hy_4公共_表格", 7);
                string csvPath = Path.GetTempFileName();
                Point3d insertionPoint = new Point3d(Contour.ExteriorRing.Coordinates[0].X - 1500, Contour.ExteriorRing.Coordinates[0].Y + 1500, 0);
                CreateTable(insertionPoint, csvPath); // 调用 CreateTable 创建表格
                                                      // 直接获取刚创建的 Table 对象
                foreach (ObjectId id in btr) // 遍历 BlockTableRecord
                {
                    var entity = tr.GetObject(id, OpenMode.ForWrite);
                    if (entity is Table table) // 找到 Table 对象
                    {
                        table.SetLayer("00_hy_4公共_表格"); // 设置图层
                        break; // 找到后退出循环
                    }
                }

                tr.Commit();
            }
        }
    }
}