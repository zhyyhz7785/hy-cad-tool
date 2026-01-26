using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class ZTools
    {
        /// <summary>
        /// 将AutoCAD的Polyline转换为NTS的Polygon(假设闭合、单一外环)
        /// </summary>
        private static Polygon ConvertPolylineToNTSPolygon(this Polyline pl)
        {
            // 收集顶点
            var coords = new List<Coordinate>();
            int vn = pl.NumberOfVertices;
            for (int i = 0; i < vn; i++)
            {
                Point2d pt = pl.GetPoint2dAt(i);
                coords.Add(new Coordinate(pt.X, pt.Y));
            }
            coords.Add(new Coordinate(coords[0].X, coords[0].Y));
            // 若已闭合，一般最后一点与第一点重合，可根据需求做校正
            // 构建 Polygon
            var geometryFactory = new GeometryFactory();
            var ring = geometryFactory.CreateLinearRing(coords.ToArray());
            return geometryFactory.CreatePolygon(ring);
        }
        /// <summary>
        /// 简化的: 判断 QuadtreeNode 的矩形与 NTS Polygon 的关系
        /// </summary>
        private static IntersectionType CheckIntersection(Polygon poly, QuadtreeNode node)
        {
            // 先构建 NTS 的 Envelope / Polygon
            var env = new Envelope(node.XMin, node.XMax, node.YMin, node.YMax);
            var gf = new GeometryFactory();
            var rectPoly = gf.ToGeometry(env) as NetTopologySuite.Geometries.Polygon;
            if (!poly.EnvelopeInternal.Intersects(env))
                return IntersectionType.Outside; // 粗判
                                                 // 更精细的判断
            if (poly.Contains(rectPoly))
            {
                return IntersectionType.Inside;
            }
            else if (!poly.Intersects(rectPoly))
            {
                return IntersectionType.Outside;
            }
            else
            {
                return IntersectionType.Partial;
            }
        }
        /// <summary>
        /// 在当前图形中绘制自适应网格(排除Outside，保留Inside和Partial)
        /// </summary>
        private static void DrawAdaptiveGrid(
            List<QuadtreeNode> leaves,
            Polygon ntsPolygon,
            Document doc
        )
        {
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                var gf = new GeometryFactory();
                foreach (var leaf in leaves)
                {
                    if (leaf.Intersection == IntersectionType.Outside)
                        continue; // 跳过不在多边形范围内的网格
                                  // 对于Partial,Inside都画出来，但也可以进一步剪裁
                                  // 先构建一个NTS矩形
                    var env = new Envelope(leaf.XMin, leaf.XMax, leaf.YMin, leaf.YMax);
                    var rectPoly = gf.ToGeometry(env) as NetTopologySuite.Geometries.Polygon;
                    // 如果想只画出多边形内部分，可做裁剪: polygon.Intersection(rectPoly)
                    var intersection = ntsPolygon.Intersection(rectPoly);
                    if (intersection.IsEmpty) continue;
                    // intersection 可能是多边形，也可能是多段多边形
                    // 这里只演示将 bounding box 直接绘制为Polyline
                    // 若要更精细，可以对 intersection 做多段线分解
                    CreateRectPolyline(btr, tr, leaf.XMin, leaf.YMin, leaf.XMax, leaf.YMax);
                }
                tr.Commit();
            }
        }
        /// <summary>
        /// 在当前图形中绘制自适应网格，仅绘制与多边形相交的交集部分。
        /// </summary>
        private static void DrawAdaptiveGrid1(
            List<QuadtreeNode> leaves,
            Polygon ntsPolygon,
            Document doc
        )
        {
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                var gf = new GeometryFactory();
                foreach (var leaf in leaves)
                {
                    // 若 Outside，跳过
                    if (leaf.Intersection == IntersectionType.Outside)
                        continue;
                    // 构造子矩形的 NTS 几何
                    var env = new Envelope(leaf.XMin, leaf.XMax, leaf.YMin, leaf.YMax);
                    var rectPoly = gf.ToGeometry(env) as NetTopologySuite.Geometries.Polygon;
                    // 计算交集
                    var intersection = ntsPolygon.Intersection(rectPoly);
                    // 若交集为空，不画
                    if (intersection.IsEmpty)
                        continue;
                    // 将交集转换为 AutoCAD 多段线
                    DrawIntersectionGeometry(intersection, btr, tr);
                }
                tr.Commit();
            }
        }
        /// <summary>
        /// 在图形中创建一个矩形多段线
        /// </summary>
        private static void CreateRectPolyline(BlockTableRecord btr, Transaction tr, double xMin, double yMin, double xMax, double yMax)
        {
            using (Polyline rectPl = new Polyline())
            {
                rectPl.AddVertexAt(0, new Point2d(xMin, yMin), 0, 0, 0);
                rectPl.AddVertexAt(1, new Point2d(xMax, yMin), 0, 0, 0);
                rectPl.AddVertexAt(2, new Point2d(xMax, yMax), 0, 0, 0);
                rectPl.AddVertexAt(3, new Point2d(xMin, yMax), 0, 0, 0);
                rectPl.Closed = true;
                // 设置线颜色（举例：绿色）
                rectPl.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
                // 将实体添加到 BlockTableRecord
                btr.AppendEntity(rectPl);
                // 这里改为用当前 Transaction 来登记新对象
                tr.AddNewlyCreatedDBObject(rectPl, true);
            }
        }
        /// <summary>
        /// 将 NTS geometry 的面状部分转换为 AutoCAD Polyline（或多段Polyline）并绘制。
        /// </summary>
        private static void DrawIntersectionGeometry(
            NetTopologySuite.Geometries.Geometry geometry,
            BlockTableRecord btr,
            Transaction tr
        )
        {
            // 根据 geometry 的类型，做不同处理
            var geoType = geometry.OgcGeometryType;
            switch (geoType)
            {
                case OgcGeometryType.Polygon:
                    // 直接处理单一多边形
                    var poly = geometry as NetTopologySuite.Geometries.Polygon;
                    if (poly != null)
                    {
                        DrawPolygon(poly, btr, tr);
                    }
                    break;
                case OgcGeometryType.MultiPolygon:
                    // 多多边形
                    var mpoly = geometry as MultiPolygon;
                    if (mpoly != null)
                    {
                        for (int i = 0; i < mpoly.NumGeometries; i++)
                        {
                            var g = mpoly.GetGeometryN(i) as NetTopologySuite.Geometries.Polygon;
                            if (g != null) DrawPolygon(g, btr, tr);
                        }
                    }
                    break;
                case OgcGeometryType.GeometryCollection:
                    // 可能包含多种类型(面/线/点)
                    var gc = geometry as GeometryCollection;
                    if (gc != null)
                    {
                        for (int i = 0; i < gc.NumGeometries; i++)
                        {
                            DrawIntersectionGeometry(gc.GetGeometryN(i), btr, tr);
                        }
                    }
                    break;
                case OgcGeometryType.LineString:
                case OgcGeometryType.MultiLineString:
                    // 如果需要画线，可在此处理
                    // 例如将线段转为轻量Polyline(不闭合) 
                    // 也可忽略只画面
                    break;
                case OgcGeometryType.Point:
                case OgcGeometryType.MultiPoint:
                    // 如果需要画点可自行处理
                    break;
                default:
                    // 其他类型忽略
                    break;
            }
        }
        /// <summary>
        /// 将一个 NTS Polygon(可能有外环+内环) 转为 AutoCAD 多段线
        /// </summary>
        private static void DrawPolygon(
            NetTopologySuite.Geometries.Polygon poly,
            BlockTableRecord btr,
            Transaction tr
        )
        {
            // 1) 先处理外环
            var shell = poly.ExteriorRing; // ILineString
            CreatePolylineFromLineString(shell, btr, tr, closed: true);
            // 2) 再处理内环（若不需要洞，可以跳过）
            int holesCount = poly.NumInteriorRings;
            for (int i = 0; i < holesCount; i++)
            {
                var hole = poly.GetInteriorRingN(i); // ILineString
                                                     // 也可以画洞，颜色或图层自行区分
                CreatePolylineFromLineString(hole, btr, tr, closed: true);
            }
        }
        /// <summary>
        /// 将 NTS 的 ILineString 转为 AutoCAD Polyline
        /// </summary>
        /// <summary>
        /// 将 NTS 的 LineString 转为 AutoCAD Polyline
        /// </summary>
        private static void CreatePolylineFromLineString(
            LineString lineString,
            BlockTableRecord btr,
            Transaction tr,
            bool closed
        )
        {
            // 提取坐标
            var coords = lineString.Coordinates;
            if (coords == null || coords.Length < 2)
                return;
            using (Polyline acadPl = new Polyline())
            {
                for (int i = 0; i < coords.Length; i++)
                {
                    double x = coords[i].X;
                    double y = coords[i].Y;
                    acadPl.AddVertexAt(i, new Point2d(x, y), 0, 0, 0);
                }
                // 设置闭合状态
                acadPl.Closed = closed;
                // 颜色可自定义
                acadPl.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
                // 添加到模型空间
                btr.AppendEntity(acadPl);
                tr.AddNewlyCreatedDBObject(acadPl, true);
            }
        }
    }
}
