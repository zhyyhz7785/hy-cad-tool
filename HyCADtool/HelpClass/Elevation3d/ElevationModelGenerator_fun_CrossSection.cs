using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.HelpClass.CreatBase
{
    public partial class ElevationModelGenerator
    {
        private const string CROSS_SECTION_LAYER = "00-hy-CrossSectionLayer";
        private const string BASE_LAYER = "00-hy-BaseLayer"; // 新增底板图层
        private const string WALL_LAYER = "00-hy-WallLayer"; // 新增墙体图层
        private const double DEFAULT_OFFSET = 6000.0;
        private const double TOLERANCE = 1e-6;
        public void GenerateCrossSection()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    Line sectionLine = GetSectionLine(ed, tr);
                    if (sectionLine == null)
                    {
                        tr.Dispose();
                        return;
                    }
                    double offsetDistance = GetOffsetDistance(ed);
                    if (double.IsNaN(offsetDistance))
                    {
                        tr.Dispose();
                        return;
                    }
                    EnsureLayers(db, tr);
                    GenerateAndDrawCrossSection(sectionLine, offsetDistance, db, ed, tr);
                    tr.Commit();
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n错误：{ex.Message}");
                    tr.Abort();
                }
            }
        }
        private void GenerateAndDrawCrossSection(Line sectionLine, double offsetDistance, Database db, Editor ed, Transaction tr)
        {
            Tuple<Vector3d, double> transformParams = CalculateTransformParameters(sectionLine, offsetDistance);
            Vector3d normalVector = transformParams.Item1;
            double angle = transformParams.Item2;
            var intersectionData = GetIntersectionData(sectionLine, ed, angle, tr, db);
            if (intersectionData.Count < 1)
            {
                ed.WriteMessage("\n警告：交点数量不足，无法生成剖面图");
                return;
            }
            // 生成表面轮廓
            Polyline surfaceLine = CreateSurfacePolyline(intersectionData, ed);
            DrawPolylineToAutoCAD(surfaceLine, sectionLine.StartPoint, normalVector, db, tr, CROSS_SECTION_LAYER);
            // 生成独立的底板剖面和墙体剖面
            // GenerateBase(intersectionData, sectionLine, normalVector, db, ed, tr, MinBaseThickness);
            // GenerateWalls(intersectionData, sectionLine, normalVector, db, ed, tr);
            ed.WriteMessage($"\n成功生成剖面图，包含 {intersectionData.Count} 个点组，偏移距离: {offsetDistance}");
        }
        private List<(double XDistance, double InnerElevation, double OuterElevation, Point3d Point, GeometryData GeomData, WallData WallData)>
            GetIntersectionData(Line sectionLine, Editor ed, double angle, Transaction tr, Database db)
        {
            var data = new List<(double XDistance, double InnerElevation, double OuterElevation, Point3d Point, GeometryData GeomData, WallData WallData)>();
            var uniqueX = new HashSet<double>(new DoubleEqualityComparer(TOLERANCE));
            int intersectionCount = 0;
            if (GeometryDatas != null)
            {
                foreach (GeometryData geometryData in GeometryDatas)
                {
                    foreach (var wallData in geometryData.Walls)
                    {
                        Line edgeLine = new Line(
                            new Point3d(wallData.Edge.StartPoint.X, wallData.Edge.StartPoint.Y, 0),
                            new Point3d(wallData.Edge.EndPoint.X, wallData.Edge.EndPoint.Y, 0));
                        Point3dCollection intersections = new Point3dCollection();
                        sectionLine.IntersectWith(edgeLine, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);
                        foreach (Point3d point in intersections)
                        {
                            intersectionCount++;
                            double xDistance = CalculateXDistance(point, sectionLine, angle);
                            if (uniqueX.Add(xDistance))
                            {
                                data.Add((xDistance, wallData.InnerElevation, wallData.OuterElevation, point, geometryData, wallData));
                            }
                        }
                    }
                }
            }
            ed.WriteMessage($"\n交点总数: {intersectionCount}，去重后保留交点数: {data.Count}");
            return data.OrderBy(p => p.XDistance).ToList();
        }
        private Polyline CreateSurfacePolyline(List<(double XDistance, double InnerElevation, double OuterElevation,
            Point3d Point, GeometryData GeomData, WallData WallData)> points, Editor ed)
        {
            if (points.Count < 1)
                throw new ArgumentException("需要至少一个交点来生成表面轮廓");
            Polyline polyline = new Polyline { Layer = CROSS_SECTION_LAYER };
            double lastZ = points[0].InnerElevation;
            int vertexIndex = 0;
            foreach (var point in points)
            {
                double x = point.XDistance; // 修正：使用明确的字段名
                double inner = point.InnerElevation;
                double outer = point.OuterElevation;
                bool ConnectInnerFirst() => Math.Abs(lastZ - inner) < TOLERANCE;
                bool ConnectOuterFirst() => Math.Abs(lastZ - outer) < TOLERANCE;
                double firstZ = ConnectInnerFirst() ? inner : ConnectOuterFirst() ? outer : inner;
                double secondZ = firstZ == inner ? outer : inner;
                if (vertexIndex == 0 || polyline.GetPoint2dAt(vertexIndex - 1).X != x ||
                    Math.Abs(polyline.GetPoint2dAt(vertexIndex - 1).Y - firstZ) > TOLERANCE)
                {
                    polyline.AddVertexAt(vertexIndex++, new Point2d(x, firstZ), 0, 0, 0);
                }
                if (Math.Abs(firstZ - secondZ) > TOLERANCE)
                {
                    polyline.AddVertexAt(vertexIndex++, new Point2d(x, secondZ), 0, 0, 0);
                }
                lastZ = Math.Abs(lastZ - inner) < TOLERANCE ? outer : inner;
            }
            if (polyline.NumberOfVertices < 2)
                throw new InvalidOperationException("点数不足以生成有效的表面轮廓");
            return polyline;
        }
    }
    internal class DoubleEqualityComparer : IEqualityComparer<double>
    {
        private readonly double _tolerance;
        public DoubleEqualityComparer(double tolerance) { _tolerance = tolerance; }
        public bool Equals(double x, double y) { return Math.Abs(x - y) < _tolerance; }
        public int GetHashCode(double obj) { return obj.GetHashCode(); }
    }
}