using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using HyCADTool.Tools;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.IO;

namespace HyCADTool.HelpClass
{
    public abstract class StandardAreaBase
    {
        public double Scale { get; set; }
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
            double inputDistanceFromContour, Polygon insetRect, double scale)
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

        public abstract void ArrangePiles(Pile pile);
        public abstract void NetTopologySuiteToCAD();
        public abstract void AdjustGridSize(int nx, int ny);
        public abstract List<Point> GetCentroids();
        public abstract List<Point> GetSmallRectPoints();
        public abstract List<Polyline> GetCADSmallRects();
        public abstract List<Point3d> GetCADPilePoints();

        public abstract int NX { get; protected set; }
        public abstract int NY { get; protected set; }
        public abstract double NXD { get; protected set; }
        public abstract double NYD { get; protected set; }

        public virtual void DrawInCAD()
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            Document doc = Application.DocumentManager.MdiActiveDocument; // 获取当前文档

            using (DocumentLock docLock = doc.LockDocument()) // 添加文档锁
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                EtGpt.CreateLayer("02_hy_1桩_主", 7);
                foreach (var point in GetCADPilePoints())
                {
                    if (Pile.Section == PileSectionType.Circle)
                    {
                        Circle circle = new Circle(point, Vector3d.ZAxis, Pile.DiameterOrEdge / 2);
                        circle.SetLayer("02_hy_1桩_主");
                        btr.AppendEntity(circle);
                        tr.AddNewlyCreatedDBObject(circle, true);
                    }
                    else
                    {
                        Polyline square = new Polyline();
                        double halfEdge = Pile.DiameterOrEdge / 2;
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

                EtGpt.CreateLayer("02_hy_1桩_服务区域", 150);
                foreach (var rect in GetCADSmallRects())
                {
                    rect.SetLayer("02_hy_1桩_服务区域");
                    btr.AppendEntity(rect);
                    tr.AddNewlyCreatedDBObject(rect, true);
                }

                EtGpt.CreateLayer("02_hy_3桩_地基内轮廓", 150);
                CADInsetRect.SetLayer("02_hy_3桩_地基内轮廓");
                btr.AppendEntity(CADInsetRect);
                tr.AddNewlyCreatedDBObject(CADInsetRect, true);

                EtGpt.CreateLayer("00_hy_4公共_表格", 7);
                string csvPath = Path.GetTempFileName();
                Point3d insertionPoint = new Point3d(Contour.ExteriorRing.Coordinates[3].X - 100 * Scale, Contour.ExteriorRing.Coordinates[3].Y + 100 * Scale, 0);
                CreateTable(insertionPoint, csvPath);
                foreach (ObjectId id in btr)
                {
                    var entity = tr.GetObject(id, OpenMode.ForWrite);
                    if (entity is Table table)
                    {
                        table.SetLayer("00_hy_4公共_表格");
                        break;
                    }
                }

                tr.Commit();
            } // docLock 在此自动释放
        }

        public virtual void CreateTable(Point3d insertionPoint, string csvFilePath)
        {
            double pileArea = CalculatePileArea();
            double calculatedTotalPiles = Contour.Area * InputDisplacementRate / pileArea;

            // 写入 CSV 文件
            using (StreamWriter sw = new StreamWriter(csvFilePath))
            {
                sw.WriteLine("参数名称,值");
                sw.WriteLine($"布置类型,{(ArrangementType == PileArrangementType.Rectangle ? "矩形" : "圆形")}");
                sw.WriteLine($"桩直径,{Pile.DiameterOrEdge}");
                sw.WriteLine($"桩面积,{pileArea:F2}");
                sw.WriteLine($"X 方向桩数,{NX}");
                sw.WriteLine($"Y 方向桩数,{NY}");
                sw.WriteLine($"X 间距,{NXD:F2}");
                sw.WriteLine($"Y 间距,{NYD:F2}");
                sw.WriteLine($"计算总桩数,{calculatedTotalPiles:F2}");
                sw.WriteLine($"实际总桩数,{GetCADPilePoints().Count}");
                sw.WriteLine($"输入置换率,{InputDisplacementRate:F2}");
                sw.WriteLine($"实际置换率,{ActualDisplacementRate:F4}");
            }

            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                Table table = new Table();
                table.SetSize(12, 2);
                table.Position = insertionPoint;

                // 设置表格的列宽和行高，根据 BaseConfig.Scale 放大
                double baseColumnWidth = 50; // 基础列宽
                double baseRowHeight = 6;    // 基础行高
                table.Columns[0].Width = baseColumnWidth * Scale;
                table.Columns[1].Width = baseColumnWidth * Scale;
                for (int i = 0; i < 12; i++)
                {
                    table.Rows[i].Height = baseRowHeight * Scale;
                }

                // 读取 CSV 文件并填充表格内容
                string[] lines = File.ReadAllLines(csvFilePath);
                for (int i = 0; i < lines.Length && i < 12; i++) // 防止超出表格行数
                {
                    string[] parts = lines[i].Split(',');
                    Cell cell0 = table.Cells[i, 0];
                    Cell cell1 = table.Cells[i, 1];

                    // 设置单元格内容
                    cell0.TextString = parts[0].Trim();
                    cell1.TextString = parts[1].Trim();

                    // 设置文字高度，使用 TextStyleConfig.TextSize 并根据 Scale 缩放
                    double textHeight = BaseConfig.TextStyleConfig.TextSize * Scale;
                    cell0.TextHeight = textHeight;
                    cell1.TextHeight = textHeight;

                    // 设置文字对齐方式：左对齐，上下居中
                    cell0.Alignment = CellAlignment.MiddleCenter;
                    cell1.Alignment = CellAlignment.MiddleCenter;
                }

                // 设置表格的文本样式
                //TextStyleTable tst = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                //if (tst.Has(BaseConfig.TextStyleConfig.Name))
                //{
                //    table.SetTextStyleId() = tst[BaseConfig.TextStyleConfig.Name];
                //}

                // 将表格添加到模型空间
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);
                tr.Commit();
            }
        }

        protected double CalculatePileArea()
        {
            if (Pile.Section == PileSectionType.Circle)
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