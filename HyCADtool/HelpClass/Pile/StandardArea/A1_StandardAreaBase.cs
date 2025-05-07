using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Interfaces;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace HyCADTool.HelpClass
{
    public abstract class StandardAreaBase
    {
        protected readonly ICadService _cadService;
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
        protected StandardAreaBase(ICadService cadService, Pile pile, Polygon contour, PileArrangementType arrangementType,
            double minPileCenterDistance, (double up, double down, double left, double right) margin,
            double inputDisplacementRate, double inputDistanceFromContour, Polygon insetRect, double scale)
        {
            _cadService = cadService;
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
            try
            {
                using (DocumentLock docLock = Application.DocumentManager.MdiActiveDocument.LockDocument())
                using (Transaction tr = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.StartTransaction())
                {
                    var pileEntities = new List<Entity>();
                    foreach (var point in GetCADPilePoints())
                    {
                        if (Pile.Section == PileSectionType.Circle)
                        {
                            var circle = new Circle(point, Vector3d.ZAxis, Pile.DiameterOrEdge / 2);
                            pileEntities.Add(circle);
                        }
                        else
                        {
                            var square = new Polyline();
                            double halfEdge = Pile.DiameterOrEdge / 2;
                            square.AddVertexAt(0, new Point2d(point.X - halfEdge, point.Y - halfEdge), 0, 0, 0);
                            square.AddVertexAt(1, new Point2d(point.X + halfEdge, point.Y - halfEdge), 0, 0, 0);
                            square.AddVertexAt(2, new Point2d(point.X + halfEdge, point.Y + halfEdge), 0, 0, 0);
                            square.AddVertexAt(3, new Point2d(point.X - halfEdge, point.Y + halfEdge), 0, 0, 0);
                            square.Closed = true;
                            pileEntities.Add(square);
                        }
                    }
                    _cadService.DrawEntities(pileEntities, "02_hy_1桩_主");
                    _cadService.DrawEntities(GetCADSmallRects(), "02_hy_3桩_地基内轮廓");
                    _cadService.DrawEntities(new[] { CADInsetRect }, "02_hy_3桩_地基内轮廓");
                    AddPileIDAnnotations(PileMap, tr);
                    string csvPath = Path.GetTempFileName();
                    Point3d insertionPoint = new Point3d(Contour.ExteriorRing.Coordinates[3].X - 100 * Scale,
                        Contour.ExteriorRing.Coordinates[3].Y + 100 * Scale, 0);
                    CreateTable(insertionPoint, csvPath);
                    _cadService.CreateTable(insertionPoint, csvPath, Scale);
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                _cadService.WriteMessage($"\nError in DrawInCAD: {ex.Message}\n");
            }
        }
        public virtual void CreateTable(Point3d insertionPoint, string csvFilePath)
        {
            double pileArea = CalculatePileArea();
            double calculatedTotalPiles = Contour.Area * InputDisplacementRate / pileArea;
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
        }
        protected double CalculatePileArea()
        {
            if (Pile.Section == PileSectionType.Circle)
            {
                return Math.PI * Math.Pow(Pile.DiameterOrEdge / 2, 2);
            }
            return Math.Pow(Pile.DiameterOrEdge, 2);
        }
        private void AddPileIDAnnotations(Dictionary<int, Pile> pileMap, Transaction tr)
        {
            foreach (var pileEntry in pileMap)
            {
                int id = pileEntry.Key;  // 桩号直接从 pileMap 的键获取
                Pile pile = pileEntry.Value;
                // 假设 Pile 类有 Position 属性表示坐标
                // 如果没有 Position 属性，请告诉我如何获取桩的坐标
                Point3d point = pile.Center.ToAutoCadPoint(); // 需要确保 Pile 类有 Position 属性，或替换为其他方式获取坐标
                var leader = new Leader();
                leader.AppendVertex(point);
                leader.AppendVertex(new Point3d(point.X + 5 * Scale, point.Y + 5 * Scale, 0));
                leader.HasArrowHead = true;
                var mtext = new MText();
                mtext.Contents = id.ToString();
                mtext.Location = new Point3d(point.X + 6 * Scale, point.Y + 6 * Scale, 0);
                mtext.TextHeight = 2.5 * Scale;
                _cadService.DrawEntities(new Entity[] { leader, mtext }, "00_hy_3公共_标注3_引线");
            }
        }
        private void AddXDirectionAnnotations(List<Point3d> pilePoints, Point3d bottomLeft, Point3d bottomRight, Transaction tr)
        {
            // 找到第一排桩（Y坐标最小的点）
            var firstRow = pilePoints.Where(p => Math.Abs(p.Y - bottomLeft.Y) < MinPileCenterDistance)
                                    .OrderBy(p => p.X)
                                    .ToList();
            if (firstRow.Count == 0) return;
            // 获取当前维度样式表记录的 ObjectId
            ObjectId dimStyleId = Application.DocumentManager.MdiActiveDocument.Database.Dimstyle;
            // 第一排标注 (8*Scale)
            var dim1Start = bottomLeft;
            var dim1End = firstRow[0];
            var dim1 = new RotatedDimension(
                0, dim1Start, dim1End,
                new Point3d(dim1Start.X, dim1Start.Y - 8 * Scale, 0),
                "%%P",
                dimStyleId  // 使用 ObjectId 而不是 DimStyleTableRecord
            );
            _cadService.DrawEntities(new[] { dim1 }, "00_hy_3公共_标注1_外3");
            // 第二排标注 (14*Scale，总尺寸)
            var dim2Start = bottomLeft;
            var dim2End = bottomRight;
            var dim2 = new RotatedDimension(
                0, dim2Start, dim2End,
                new Point3d(dim2Start.X, dim2Start.Y - 14 * Scale, 0),
                "%%P",
                dimStyleId
            );
            _cadService.DrawEntities(new[] { dim2 }, "00_hy_3公共_标注1_外3");
        }
        private void AddYDirectionAnnotations(List<Point3d> pilePoints, Point3d bottomLeft, Point3d topLeft, Transaction tr)
        {
            // 找到第一列桩（X坐标最小的点）
            var firstColumn = pilePoints.Where(p => Math.Abs(p.X - bottomLeft.X) < MinPileCenterDistance)
                                      .OrderBy(p => p.Y)
                                      .ToList();
            if (firstColumn.Count == 0) return;
            // 获取当前维度样式表记录的 ObjectId
            ObjectId dimStyleId = Application.DocumentManager.MdiActiveDocument.Database.Dimstyle;
            // 第一排标注 (8*Scale)
            var dim1Start = bottomLeft;
            var dim1End = firstColumn[0];
            var dim1 = new RotatedDimension(
                Math.PI / 2, dim1Start, dim1End,
                new Point3d(dim1Start.X - 8 * Scale, dim1Start.Y, 0),
                "%%P",
                dimStyleId
            );
            _cadService.DrawEntities(new[] { dim1 }, "00_hy_3公共_标注1_外");
            // 第二排标注 (14*Scale，总尺寸)
            var dim2Start = bottomLeft;
            var dim2End = topLeft;
            var dim2 = new RotatedDimension(
                Math.PI / 2, dim2Start, dim2End,
                new Point3d(dim2Start.X - 14 * Scale, dim2Start.Y, 0),
                "%%P",
                dimStyleId
            );
            _cadService.DrawEntities(new[] { dim2 }, "00_hy_3公共_标注1_外");
        }
        private void AddPileIDAnnotations(List<Point3d> pilePoints, Transaction tr)
        {
            int id = 1;
            foreach (var point in pilePoints)
            {
                var leader = new Leader();
                leader.AppendVertex(point);
                leader.AppendVertex(new Point3d(point.X + 5 * Scale, point.Y + 5 * Scale, 0));
                leader.HasArrowHead = true;
                var mtext = new MText();
                mtext.Contents = id.ToString();
                mtext.Location = new Point3d(point.X + 6 * Scale, point.Y + 6 * Scale, 0);
                mtext.TextHeight = 2.5 * Scale;
                _cadService.DrawEntities(new Entity[] { leader, mtext }, "00_hy_3公共_标注3_引线");
                id++;
            }
        }
    }
}