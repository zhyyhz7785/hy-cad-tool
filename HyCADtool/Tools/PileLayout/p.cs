using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Tools
{
    public static partial class ZTools
    {
        // 主方法，集成所有步骤
        public static void GenerateHexagonsInAutoCAD()
        {
            // 1. 选择一个 Polyline 并获取 Polygon
            Polygon polygon = PromptAndGetPolygon();
            if (polygon == null)
            {
                Application.ShowAlertDialog("未选择有效的多边形!");
                return;
            }
            // 2. 获取桩的参数和数量
            var (diameter, replacementRate, numberOfPiles) = GetPileParametersAndCount(polygon);
            // 3. 创建四边形
            //List<Polygon> hexagons = CreateHexagonsInPolygon(polygon, numberOfPiles, diameter);
            //intersecting与基础相交的多边形，containing内部的多边形
            var (intersecting, containing) = CreateSquaresInPolygonDic(polygon, numberOfPiles);
            var coors1 = intersecting.Keys;
            var coors2 = containing.Keys;
            var coors = coors1.Concat(coors2).ToList();
            coors = GetPointsInsidePolygon(polygon, coors);
            // 4. 取出与x y xy方向相交的多边形字典，键是直线，值为相交正方形
            var (xIntersecting, yIntersecting, xyIntersecting) = CategorizeIntersectingSquares1Dic(polygon, intersecting);
            // 5. 在字典中取出相交的正方形
            var allPolygons = xIntersecting
            .SelectMany(kvp => kvp.Value) // 展开每个 List<Dictionary<Coordinate, Polygon>>
            .SelectMany(dict => dict.Values) // 从每个 Dictionary<Coordinate, Polygon> 中提取 Polygon
            .ToList();
            var ps = new List<Point3d>();
            foreach (var coor in coors)
            {
                var p = new Point3d(coor.X, coor.Y, 0);
                ps.Add(p);
            }
            ps.ToSpace();
            PlacePileAndVoronoiWithLloydOptimization(coors);
            //// 使用 LINQ 按 Coordinate 的 x 坐标对字典进行分组
            //var groupedByX = xIntersecting
            //    .SelectMany(kvp => kvp.Value)  // 展开 List<Dictionary<Coordinate, Polygon>>
            //    .SelectMany(dict => dict.Keys) // 提取每个 Dictionary 中的 Coordinate 键（即坐标）
            //    .GroupBy(coord => coord.X)    // 按 Coordinate.X 进行分组
            //    .ToDictionary(group => group.Key, // 分组键为 X 坐标
            //                  group => group.Select(coord => xIntersecting
            //                                                    .Where(kvp => kvp.Value.Any(d => d.ContainsKey(coord)))
            //                                                    .Select(kvp => kvp.Value.First(d => d.ContainsKey(coord)))
            //                                                    .ToList())); // 返回符合条件的 List<Dictionary<Coordinate, Polygon>>
            // 6. 在cad中绘制正方形
            //DrawHexagonsInAutoCAD(allPolygons);
            // 7. 寻找对边的连线。
            //GroupSquaresAndDrawLines(allPolygons);
        }
        public static void GenerateHexagonsInAutoCAD1()
        {
            // 1. 选择一个 Polyline 并获取 Polygon
            Polygon polygon = PromptAndGetPolygon();
            if (polygon == null)
            {
                Application.ShowAlertDialog("未选择有效的多边形!");
                return;
            }
            // 2. 获取桩的参数和数量
            var (diameter, replacementRate, numberOfPiles) = GetPileParametersAndCount1(polygon);
            // 3. 创建四边形
            var (intersecting, containing) = CreateSquaresInPolygonDic(polygon, numberOfPiles);
            var polygons1 = intersecting.Values;
            var polygons2 = containing.Values;
            var polygons = polygons1.Concat(polygons2).ToList();
            // 4. 取出与x y xy方向相交的多边形字典，键是直线，值为相交正方形
            //var (xIntersecting, yIntersecting, xyIntersecting) = CategorizeIntersectingSquares1Dic(polygon, intersecting);
            // 6. 在cad中绘制正方形
            DrawHexagonsInAutoCAD(polygons);
            // 7. 寻找对边的连线。
            //GroupSquaresAndDrawLines(allPolygons);
        }
        #region 六边形
        private static List<Polygon> CreateHexagonsInPolygon(Polygon polygon, int numberOfPiles, double pileDiameter)
        {
            List<Polygon> hexagons = new List<Polygon>();
            // 获取多边形的质心
            Coordinate polygonCentroid = polygon.Centroid.Coordinate;
            // 计算每个六边形的面积
            double hexagonArea = polygon.Area / numberOfPiles;
            // 计算正六边形的边长
            double hexagonSideLength = Math.Sqrt((2 * hexagonArea) / (3 * Math.Sqrt(3))); // 正六边形面积公式
                                                                                          // 获取多边形的 MBR（最小边界矩形）
            Envelope envelope = polygon.EnvelopeInternal;
            // 从质心开始，计算六边形填充区域
            double startX = envelope.MinX;
            double startY = envelope.MinY;
            // 计算偏移量 (x, y) 用于生成每个六边形的位置
            double offsetX = 1.5 * hexagonSideLength; // 横向偏移量
            double offsetY = Math.Sqrt(3) * hexagonSideLength; // 纵向偏移量
                                                               // 通过平移生成六边形
            for (int i = 0; i < numberOfPiles; i++)
            {
                // 计算每个六边形的中心点
                double x = startX + (i % 2) * offsetX;  // 偶数行偏移
                double y = startY + (i / 2) * offsetY;  // 按纵向偏移
                                                        // 创建六边形并确保其与多边形相交或在内部
                Polygon hexagon = CreateHexagon(new Coordinate(x, y), hexagonSideLength);
                // 检查六边形是否与多边形相交，或者完全在多边形内
                if (polygon.Intersects(hexagon) || polygon.Contains(new Point(x, y)))
                {
                    hexagons.Add(hexagon);
                }
            }
            return hexagons;
        }
        // 创建正六边形的辅助方法
        private static Polygon CreateHexagon(Coordinate center, double sideLength)
        {
            List<Coordinate> hexagonCoords = new List<Coordinate>();
            // 生成六个点
            for (int i = 0; i < 6; i++)
            {
                double angle = Math.PI / 3 * i;
                double x = center.X + sideLength * Math.Cos(angle);
                double y = center.Y + sideLength * Math.Sin(angle);
                hexagonCoords.Add(new Coordinate(x, y));
            }
            // 确保点集合闭合
            if (!hexagonCoords[0].Equals(hexagonCoords[hexagonCoords.Count - 1]))
            {
                hexagonCoords.Add(hexagonCoords[0]);  // 添加第一个点到最后，形成闭合的多边形
            }
            // 创建 LinearRing 对象
            LinearRing ring = new LinearRing(hexagonCoords.ToArray());
            // 使用 LinearRing 创建 Polygon
            return new Polygon(ring);
        }
        // 在 AutoCAD 中绘制六边形
        private static void DrawHexagonsInAutoCAD(List<Polygon> hexagons)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            Editor ed = doc.Editor;
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                CreateLayerIfNotExists(tr, db, "00-HY-Hexagon", Color.FromColorIndex(ColorMethod.ByAci, 5));
                foreach (var hexagon in hexagons)
                {
                    List<Point2d> points = new List<Point2d>();
                    foreach (Coordinate coord in hexagon.Coordinates)
                    {
                        points.Add(new Point2d(coord.X, coord.Y));
                    }
                    // 绘制六边形
                    DrawPolygon(btr, tr, points, "00-HY-Hexagon");
                }
                tr.Commit();
            }
        }
        #endregion
        #region 正方形
        private static (List<Polygon> intersectingSquares, List<Polygon> containingSquares) CreateSquaresInPolygon
            (Polygon polygon, int numberOfPiles)
        {
            List<Polygon> intersectingSquares = new List<Polygon>(); // 与多边形相交的正方形
            List<Polygon> containingSquares = new List<Polygon>();   // 完全包含在多边形内部的正方形
                                                                     // 获取多边形的质心
            Coordinate polygonCentroid = polygon.Centroid.Coordinate;
            // 计算每个正方形的面积
            double squareArea = polygon.Area / numberOfPiles;
            // 计算正方形的边长
            double squareSideLength = Math.Sqrt(squareArea);  // 正方形面积公式 s = sqrt(面积)
                                                              // 获取多边形的 MBR（最小边界矩形）
            Envelope envelope = polygon.EnvelopeInternal;
            // 计算第一个正方形的中心点与多边形形心重合
            double startX = polygonCentroid.X;  // 让正方形的中心对齐多边形的中心
            double startY = polygonCentroid.Y;  // 让正方形的中心对齐多边形的中心
            var p1 = new Point3d(startX, startY, 0);
            p1.ToSpace();
            // 计算正方形的偏移量 (x, y)
            double offsetX = squareSideLength;  // 水平方向的偏移量
            double offsetY = squareSideLength;  // 垂直方向的偏移量
                                                // 计算正方形排列的行数和列数
            int rows = (int)Math.Ceiling(envelope.Height / squareSideLength); // 按纵向填充
            int cols = (int)Math.Ceiling(envelope.Width / squareSideLength);  // 按横向填充        
                                                                              // 让起点左侧、下侧移动，以适应复杂多边形的包络
            startX = startX - (((cols + 3) * squareSideLength / 2));
            startY = startY - (((rows + 3) * squareSideLength / 2));
            var p2 = new Point3d(startX, startY, 0);
            p2.ToSpace();
            // 通过平移生成正方形
            for (int i = 0; i < rows + 6; i++)
            {
                for (int j = 0; j < cols + 6; j++)
                {
                    // 计算每个正方形的中心点
                    double x = startX + j * offsetX;  // 横向偏移
                    double y = startY + i * offsetY;  // 纵向偏移
                                                      // 创建正方形并确保其与多边形相交或在内部
                    Polygon square = CreateSquare(new Coordinate(x, y), squareSideLength);
                    // 检查正方形是否与多边形相交，或者完全在多边形内
                    if (polygon.Contains(square))
                    {
                        containingSquares.Add(square); // 完全在内部的正方形
                    }
                    else if (polygon.Intersects(square))
                    {
                        intersectingSquares.Add(square); // 与多边形相交的正方形
                    }
                }
            }
            return (intersectingSquares, containingSquares);
        }
        private static (Dictionary<Coordinate, Polygon> xIntersecting, Dictionary<Coordinate, Polygon> containingSquares)
         CreateSquaresInPolygonDic(Polygon polygon, int numberOfPiles)
        {
            // 创建两个字典来存储分类结果
            Dictionary<Coordinate, Polygon> xIntersecting = new Dictionary<Coordinate, Polygon>();  // 与多边形相交的正方形
            Dictionary<Coordinate, Polygon> containingSquares = new Dictionary<Coordinate, Polygon>();   // 完全包含在多边形内部的正方形
                                                                                                         // 获取多边形的质心
            Coordinate polygonCentroid = polygon.Centroid.Coordinate;
            // 计算每个正方形的面积
            double squareArea = polygon.Area / numberOfPiles;
            // 计算正方形的边长
            double squareSideLength = Math.Sqrt(squareArea);  // 正方形面积公式 s = sqrt(面积)
                                                              // 获取多边形的 MBR（最小边界矩形）
            Envelope envelope = polygon.EnvelopeInternal;
            // 计算第一个正方形的中心点与多边形形心重合
            double startX = polygonCentroid.X;  // 让正方形的中心对齐多边形的中心
            double startY = polygonCentroid.Y;  // 让正方形的中心对齐多边形的中心
                                                // 计算正方形的偏移量 (x, y)
            double offsetX = squareSideLength;  // 水平方向的偏移量
            double offsetY = squareSideLength;  // 垂直方向的偏移量
                                                // 计算正方形排列的行数和列数
            int rows = (int)Math.Ceiling(envelope.Height / squareSideLength); // 按纵向填充
            int cols = (int)Math.Ceiling(envelope.Width / squareSideLength);  // 按横向填充        
                                                                              // 让起点左侧、下侧移动，以适应复杂多边形的包络
            startX = startX - (((cols + 3) * squareSideLength / 2));
            startY = startY - (((rows + 3) * squareSideLength / 2));
            // 通过平移生成正方形
            for (int i = 0; i < rows + 6; i++)
            {
                for (int j = 0; j < cols + 6; j++)
                {
                    // 计算每个正方形的中心点
                    double x = startX + j * offsetX;  // 横向偏移
                    double y = startY + i * offsetY;  // 纵向偏移
                                                      // 创建正方形并确保其与多边形相交或在内部
                    Polygon square = CreateSquare(new Coordinate(x, y), squareSideLength);
                    // 检查正方形是否与多边形相交，或者完全在多边形内
                    if (polygon.Contains(square))
                    {
                        // 如果正方形完全在多边形内部，将其按质心坐标分类存储
                        if (!containingSquares.ContainsKey(square.Centroid.Coordinate))
                        {
                            containingSquares[square.Centroid.Coordinate] = square;
                        }
                    }
                    else if (polygon.Intersects(square))
                    {
                        // 如果正方形与多边形相交，将其按质心坐标分类存储
                        if (!xIntersecting.ContainsKey(square.Centroid.Coordinate))
                        {
                            xIntersecting[square.Centroid.Coordinate] = square;
                        }
                    }
                }
            }
            // 返回字典，包含按质心分类的正方形
            return (xIntersecting, containingSquares);
        }
        private static Polygon CreateSquare(Coordinate center, double sideLength)
        {
            List<Coordinate> squareCoords = new List<Coordinate>();
            // 计算正方形的四个顶点
            squareCoords.Add(new Coordinate(center.X - sideLength / 2, center.Y - sideLength / 2)); // 左下角
            squareCoords.Add(new Coordinate(center.X + sideLength / 2, center.Y - sideLength / 2)); // 右下角
            squareCoords.Add(new Coordinate(center.X + sideLength / 2, center.Y + sideLength / 2)); // 右上角
            squareCoords.Add(new Coordinate(center.X - sideLength / 2, center.Y + sideLength / 2)); // 左上角
                                                                                                    // 确保正方形闭合
            squareCoords.Add(squareCoords[0]);
            // 创建 LinearRing 对象
            LinearRing ring = new LinearRing(squareCoords.ToArray());
            // 使用 LinearRing 创建 Polygon
            return new Polygon(ring);
        }
        /// <summary>
        /// 简单返回
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="intersectingSquares"></param>
        /// <returns></returns>
        private static (List<Polygon> xIntersecting, List<Polygon> yIntersecting, List<Polygon> xyIntersecting) CategorizeIntersectingSquares
            (Polygon polygon, List<Polygon> intersectingSquares)
        {
            List<Polygon> xIntersecting = new List<Polygon>();  // 与平行X轴的边相交的正方形
            List<Polygon> yIntersecting = new List<Polygon>();  // 与平行Y轴的边相交的正方形
            List<Polygon> xyIntersecting = new List<Polygon>(); // 同时与X、Y轴边相交的正方形
                                                                // 获取多边形的所有边，区分平行于X轴或Y轴的边
            var xAlignedEdges = new List<LineSegment>();
            var yAlignedEdges = new List<LineSegment>();
            for (int i = 0; i < polygon.NumPoints - 1; i++)
            {
                Coordinate p1 = polygon.Coordinates[i];
                Coordinate p2 = polygon.Coordinates[i + 1];
                if (Math.Abs(p1.Y - p2.Y) < 1e-6) // 平行于X轴
                {
                    xAlignedEdges.Add(new LineSegment(p1, p2));
                }
                else if (Math.Abs(p1.X - p2.X) < 1e-6) // 平行于Y轴
                {
                    yAlignedEdges.Add(new LineSegment(p1, p2));
                }
            }
            // 遍历所有相交的正方形并分类
            foreach (var square in intersectingSquares)
            {
                bool intersectsX = false;
                bool intersectsY = false;
                // 检查正方形是否与平行于X轴的边相交
                foreach (var edge in xAlignedEdges)
                {
                    LineString edgeLine = new GeometryFactory().CreateLineString(new Coordinate[] { edge.P0, edge.P1 });
                    if (square.Intersects(edgeLine))
                    {
                        intersectsX = true;
                        break;
                    }
                }
                // 检查正方形是否与平行于Y轴的边相交
                foreach (var edge in yAlignedEdges)
                {
                    LineString edgeLine = new GeometryFactory().CreateLineString(new Coordinate[] { edge.P0, edge.P1 });
                    if (square.Intersects(edgeLine))
                    {
                        intersectsY = true;
                        break;
                    }
                }
                // 分类正方形
                if (intersectsX && intersectsY)
                {
                    xyIntersecting.Add(square); // 同时与X、Y轴平行边相交
                }
                else if (intersectsX)
                {
                    xIntersecting.Add(square);  // 仅与X轴平行边相交
                }
                else if (intersectsY)
                {
                    yIntersecting.Add(square);  // 仅与Y轴平行边相交
                }
            }
            return (xIntersecting, yIntersecting, xyIntersecting);
        }
        /// <summary>
        /// 返回值与x y向直线相交的正方形，  **不**  包括两个方向都相交的
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="intersectingSquares"></param>
        /// <returns></returns>
        private static (Dictionary<LineString, List<Polygon>> xIntersecting,
                Dictionary<LineString, List<Polygon>> yIntersecting,
                Dictionary<Tuple<LineString, LineString>, List<Polygon>> xyIntersecting)
         CategorizeIntersectingSquares1(Polygon polygon, List<Polygon> intersectingSquares)
        {
            // 创建字典来存储每条直线与正方形的关系
            Dictionary<LineString, List<Polygon>> xIntersecting = new Dictionary<LineString, List<Polygon>>();
            Dictionary<LineString, List<Polygon>> yIntersecting = new Dictionary<LineString, List<Polygon>>();
            Dictionary<Tuple<LineString, LineString>, List<Polygon>> xyIntersecting = new Dictionary<Tuple<LineString, LineString>, List<Polygon>>();
            // 获取多边形的所有边，区分平行于X轴或Y轴的边
            var xAlignedEdges = new List<LineSegment>();
            var yAlignedEdges = new List<LineSegment>();
            // 遍历多边形的边，分类平行于X轴和Y轴的边
            for (int i = 0; i < polygon.NumPoints - 1; i++)
            {
                Coordinate p1 = polygon.Coordinates[i];
                Coordinate p2 = polygon.Coordinates[i + 1];
                if (Math.Abs(p1.Y - p2.Y) < 1e-6) // 平行于X轴
                {
                    xAlignedEdges.Add(new LineSegment(p1, p2));
                }
                else if (Math.Abs(p1.X - p2.X) < 1e-6) // 平行于Y轴
                {
                    yAlignedEdges.Add(new LineSegment(p1, p2));
                }
            }
            // 遍历所有相交的正方形并分类
            foreach (var square in intersectingSquares)
            {
                bool intersectsX = false;
                bool intersectsY = false;
                var intersectingXLines = new List<LineString>();
                var intersectingYLines = new List<LineString>();
                // 检查正方形是否与平行于X轴的边相交
                foreach (var edge in xAlignedEdges)
                {
                    LineString edgeLine = new GeometryFactory().CreateLineString(new Coordinate[] { edge.P0, edge.P1 });
                    if (square.Intersects(edgeLine))
                    {
                        intersectsX = true;
                        intersectingXLines.Add(edgeLine);  // 记录相交的X方向直线
                    }
                }
                // 检查正方形是否与平行于Y轴的边相交
                foreach (var edge in yAlignedEdges)
                {
                    LineString edgeLine = new GeometryFactory().CreateLineString(new Coordinate[] { edge.P0, edge.P1 });
                    if (square.Intersects(edgeLine))
                    {
                        intersectsY = true;
                        intersectingYLines.Add(edgeLine);  // 记录相交的Y方向直线
                    }
                }
                // 如果正方形与X轴和Y轴都相交
                if (intersectsX && intersectsY)
                {
                    // 组合键为 X 轴和 Y 轴的交集
                    foreach (var xLine in intersectingXLines)
                    {
                        foreach (var yLine in intersectingYLines)
                        {
                            var combinedKey = new Tuple<LineString, LineString>(xLine, yLine);
                            if (!xyIntersecting.ContainsKey(combinedKey))
                            {
                                xyIntersecting[combinedKey] = new List<Polygon>();
                            }
                            xyIntersecting[combinedKey].Add(square); // 添加与组合直线相交的正方形
                        }
                    }
                }
                // 如果正方形只与X轴方向的直线相交
                else if (intersectsX)
                {
                    foreach (var xLine in intersectingXLines)
                    {
                        if (!xIntersecting.ContainsKey(xLine))
                        {
                            xIntersecting[xLine] = new List<Polygon>();
                        }
                        xIntersecting[xLine].Add(square); // 添加与X轴直线相交的正方形
                    }
                }
                // 如果正方形只与Y轴方向的直线相交
                else if (intersectsY)
                {
                    foreach (var yLine in intersectingYLines)
                    {
                        if (!yIntersecting.ContainsKey(yLine))
                        {
                            yIntersecting[yLine] = new List<Polygon>();
                        }
                        yIntersecting[yLine].Add(square); // 添加与Y轴直线相交的正方形
                    }
                }
            }
            return (xIntersecting, yIntersecting, xyIntersecting);
        }
        /// <summary>
        /// 返回值与x y向直线相交的正方形，  **不**  包括两个方向都相交的
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="intersectingSquares"></param>
        /// <returns></returns>
        private static (Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>> xIntersecting,
                 Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>> yIntersecting,
                 Dictionary<Tuple<LineString, LineString>, List<Dictionary<Coordinate, Polygon>>> xyIntersecting)
                 CategorizeIntersectingSquares1Dic(Polygon polygon, Dictionary<Coordinate, Polygon> polygons)
        {
            // 创建字典来存储每条直线与正方形的关系
            Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>> xIntersecting = new Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>>();
            Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>> yIntersecting = new Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>>();
            Dictionary<Tuple<LineString, LineString>, List<Dictionary<Coordinate, Polygon>>> xyIntersecting = new Dictionary<Tuple<LineString, LineString>, List<Dictionary<Coordinate, Polygon>>>();
            // 获取多边形的所有边，区分平行于X轴或Y轴的边
            var xAlignedEdges = new List<LineSegment>();
            var yAlignedEdges = new List<LineSegment>();
            // 遍历多边形的边，分类平行于X轴和Y轴的边
            for (int i = 0; i < polygon.NumPoints - 1; i++)
            {
                Coordinate p1 = polygon.Coordinates[i];
                Coordinate p2 = polygon.Coordinates[i + 1];
                if (Math.Abs(p1.Y - p2.Y) < 1e-6) // 平行于X轴
                {
                    xAlignedEdges.Add(new LineSegment(p1, p2));
                }
                else if (Math.Abs(p1.X - p2.X) < 1e-6) // 平行于Y轴
                {
                    yAlignedEdges.Add(new LineSegment(p1, p2));
                }
            }
            // 遍历所有相交的正方形并分类
            foreach (var kvp in polygons)
            {
                Polygon square = kvp.Value; // 获取正方形
                bool intersectsX = false;
                bool intersectsY = false;
                var intersectingXLines = new List<LineString>();
                var intersectingYLines = new List<LineString>();
                // 检查正方形是否与平行于X轴的边相交
                foreach (var edge in xAlignedEdges)
                {
                    LineString edgeLine = new GeometryFactory().CreateLineString(new Coordinate[] { edge.P0, edge.P1 });
                    if (square.Intersects(edgeLine))
                    {
                        intersectsX = true;
                        intersectingXLines.Add(edgeLine);  // 记录相交的X方向直线
                    }
                }
                // 检查正方形是否与平行于Y轴的边相交
                foreach (var edge in yAlignedEdges)
                {
                    LineString edgeLine = new GeometryFactory().CreateLineString(new Coordinate[] { edge.P0, edge.P1 });
                    if (square.Intersects(edgeLine))
                    {
                        intersectsY = true;
                        intersectingYLines.Add(edgeLine);  // 记录相交的Y方向直线
                    }
                }
                // 如果正方形与X轴和Y轴都相交
                if (intersectsX && intersectsY)
                {
                    // 将与X轴和Y轴的直线相交的正方形添加到 xyIntersecting 字典
                    foreach (var xLine in intersectingXLines)
                    {
                        foreach (var yLine in intersectingYLines)
                        {
                            var combinedKey = new Tuple<LineString, LineString>(xLine, yLine);
                            if (!xyIntersecting.ContainsKey(combinedKey))
                            {
                                xyIntersecting[combinedKey] = new List<Dictionary<Coordinate, Polygon>>();
                            }
                            xyIntersecting[combinedKey].Add(new Dictionary<Coordinate, Polygon> { { kvp.Key, square } }); // 添加与X和Y轴的直线相交的正方形
                        }
                    }
                }
                // 将与 X 轴方向的直线相交的正方形添加到 xIntersecting 中
                if (intersectsX)
                {
                    foreach (var xLine in intersectingXLines)
                    {
                        if (!xIntersecting.ContainsKey(xLine))
                        {
                            xIntersecting[xLine] = new List<Dictionary<Coordinate, Polygon>>();
                        }
                        xIntersecting[xLine].Add(new Dictionary<Coordinate, Polygon> { { kvp.Key, square } }); // 添加与X轴直线相交的正方形
                    }
                }
                // 将与 Y 轴方向的直线相交的正方形添加到 yIntersecting 中
                if (intersectsY)
                {
                    foreach (var yLine in intersectingYLines)
                    {
                        if (!yIntersecting.ContainsKey(yLine))
                        {
                            yIntersecting[yLine] = new List<Dictionary<Coordinate, Polygon>>();
                        }
                        yIntersecting[yLine].Add(new Dictionary<Coordinate, Polygon> { { kvp.Key, square } }); // 添加与Y轴直线相交的正方形
                    }
                }
            }
            return (xIntersecting, yIntersecting, xyIntersecting);
        }
        /// <summary>
        /// 返回值与x y向直线相交的正方形，包括两个方向都相交的
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="intersectingSquares"></param>
        /// <returns></returns>
        private static (Dictionary<LineString, List<Polygon>> xIntersecting,
                Dictionary<LineString, List<Polygon>> yIntersecting,
                Dictionary<Tuple<LineString, LineString>, List<Polygon>> xyIntersecting)
                CategorizeIntersectingSquares2(Polygon polygon, List<Polygon> intersectingSquares)
        {
            // 创建字典来存储每条直线与正方形的关系
            Dictionary<LineString, List<Polygon>> xIntersecting = new Dictionary<LineString, List<Polygon>>();
            Dictionary<LineString, List<Polygon>> yIntersecting = new Dictionary<LineString, List<Polygon>>();
            Dictionary<Tuple<LineString, LineString>, List<Polygon>> xyIntersecting = new Dictionary<Tuple<LineString, LineString>, List<Polygon>>();
            // 获取多边形的所有边，区分平行于X轴或Y轴的边
            var xAlignedEdges = new List<LineSegment>();
            var yAlignedEdges = new List<LineSegment>();
            // 遍历多边形的边，分类平行于X轴和Y轴的边
            for (int i = 0; i < polygon.NumPoints - 1; i++)
            {
                Coordinate p1 = polygon.Coordinates[i];
                Coordinate p2 = polygon.Coordinates[i + 1];
                if (Math.Abs(p1.Y - p2.Y) < 1e-6) // 平行于X轴
                {
                    xAlignedEdges.Add(new LineSegment(p1, p2));
                }
                else if (Math.Abs(p1.X - p2.X) < 1e-6) // 平行于Y轴
                {
                    yAlignedEdges.Add(new LineSegment(p1, p2));
                }
            }
            // 遍历所有相交的正方形并分类
            foreach (var square in intersectingSquares)
            {
                bool intersectsX = false;
                bool intersectsY = false;
                var intersectingXLines = new List<LineString>();
                var intersectingYLines = new List<LineString>();
                // 检查正方形是否与平行于X轴的边相交
                foreach (var edge in xAlignedEdges)
                {
                    LineString edgeLine = new GeometryFactory().CreateLineString(new Coordinate[] { edge.P0, edge.P1 });
                    if (square.Intersects(edgeLine))
                    {
                        intersectsX = true;
                        intersectingXLines.Add(edgeLine);  // 记录相交的X方向直线
                    }
                }
                // 检查正方形是否与平行于Y轴的边相交
                foreach (var edge in yAlignedEdges)
                {
                    LineString edgeLine = new GeometryFactory().CreateLineString(new Coordinate[] { edge.P0, edge.P1 });
                    if (square.Intersects(edgeLine))
                    {
                        intersectsY = true;
                        intersectingYLines.Add(edgeLine);  // 记录相交的Y方向直线
                    }
                }
                // 将与 X 轴方向的直线相交的正方形添加到 xIntersecting 中
                if (intersectsX)
                {
                    foreach (var xLine in intersectingXLines)
                    {
                        if (!xIntersecting.ContainsKey(xLine))
                        {
                            xIntersecting[xLine] = new List<Polygon>();
                        }
                        xIntersecting[xLine].Add(square); // 添加与X轴直线相交的正方形
                    }
                }
                // 将与 Y 轴方向的直线相交的正方形添加到 yIntersecting 中
                if (intersectsY)
                {
                    foreach (var yLine in intersectingYLines)
                    {
                        if (!yIntersecting.ContainsKey(yLine))
                        {
                            yIntersecting[yLine] = new List<Polygon>();
                        }
                        yIntersecting[yLine].Add(square); // 添加与Y轴直线相交的正方形
                    }
                }
                // 如果正方形与X轴和Y轴都相交
                if (intersectsX && intersectsY)
                {
                    // 将与X轴和Y轴的直线相交的正方形添加到 xyIntersecting 字典
                    foreach (var xLine in intersectingXLines)
                    {
                        foreach (var yLine in intersectingYLines)
                        {
                            var combinedKey = new Tuple<LineString, LineString>(xLine, yLine);
                            if (!xyIntersecting.ContainsKey(combinedKey))
                            {
                                xyIntersecting[combinedKey] = new List<Polygon>();
                            }
                            xyIntersecting[combinedKey].Add(square); // 添加与X和Y轴的直线相交的正方形
                        }
                    }
                }
            }
            return (xIntersecting, yIntersecting, xyIntersecting);
        }
        /// <summary>
        /// 返回值与x y向直线相交的正方形，包括两个方向都相交的
        /// </summary>
        /// 
        /// <param name="polygon"></param>
        /// <param name="intersectingSquares"></param>
        /// <returns></returns>
        private static (Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>> xIntersecting,
                Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>> yIntersecting,
                Dictionary<Tuple<LineString, LineString>, List<Dictionary<Coordinate, Polygon>>> xyIntersecting)
               CategorizeIntersectingSquaresDic(Polygon polygon, Dictionary<Coordinate, Polygon> polygons)
        {
            // 创建字典来存储每条直线与正方形的关系
            Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>> xIntersecting = new Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>>();
            Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>> yIntersecting = new Dictionary<LineString, List<Dictionary<Coordinate, Polygon>>>();
            Dictionary<Tuple<LineString, LineString>, List<Dictionary<Coordinate, Polygon>>> xyIntersecting = new Dictionary<Tuple<LineString, LineString>, List<Dictionary<Coordinate, Polygon>>>();
            // 获取多边形的所有边，区分平行于X轴或Y轴的边
            var xAlignedEdges = new List<LineSegment>();
            var yAlignedEdges = new List<LineSegment>();
            // 遍历多边形的边，分类平行于X轴和Y轴的边
            for (int i = 0; i < polygon.NumPoints - 1; i++)
            {
                Coordinate p1 = polygon.Coordinates[i];
                Coordinate p2 = polygon.Coordinates[i + 1];
                if (Math.Abs(p1.Y - p2.Y) < 1e-6) // 平行于X轴
                {
                    xAlignedEdges.Add(new LineSegment(p1, p2));
                }
                else if (Math.Abs(p1.X - p2.X) < 1e-6) // 平行于Y轴
                {
                    yAlignedEdges.Add(new LineSegment(p1, p2));
                }
            }
            // 遍历所有的正方形并分类
            foreach (var polygonEntry in polygons)
            {
                Polygon square = polygonEntry.Value;  // 每个正方形
                bool intersectsX = false;
                bool intersectsY = false;
                var intersectingXLines = new List<LineString>();
                var intersectingYLines = new List<LineString>();
                // 检查正方形是否与平行于X轴的边相交
                foreach (var edge in xAlignedEdges)
                {
                    LineString edgeLine = new GeometryFactory().CreateLineString(new Coordinate[] { edge.P0, edge.P1 });
                    if (square.Intersects(edgeLine))
                    {
                        intersectsX = true;
                        intersectingXLines.Add(edgeLine);  // 记录相交的X方向直线
                    }
                }
                // 检查正方形是否与平行于Y轴的边相交
                foreach (var edge in yAlignedEdges)
                {
                    LineString edgeLine = new GeometryFactory().CreateLineString(new Coordinate[] { edge.P0, edge.P1 });
                    if (square.Intersects(edgeLine))
                    {
                        intersectsY = true;
                        intersectingYLines.Add(edgeLine);  // 记录相交的Y方向直线
                    }
                }
                // 将与 X 轴方向的直线相交的正方形添加到 xIntersecting 中
                if (intersectsX)
                {
                    foreach (var xLine in intersectingXLines)
                    {
                        var coordPolygonDict = new Dictionary<Coordinate, Polygon>
                {
                    { square.Centroid.Coordinate, square }
                };
                        if (!xIntersecting.ContainsKey(xLine))
                        {
                            xIntersecting[xLine] = new List<Dictionary<Coordinate, Polygon>>();
                        }
                        xIntersecting[xLine].Add(coordPolygonDict); // 添加与X轴直线相交的正方形
                    }
                }
                // 将与 Y 轴方向的直线相交的正方形添加到 yIntersecting 中
                if (intersectsY)
                {
                    foreach (var yLine in intersectingYLines)
                    {
                        var coordPolygonDict = new Dictionary<Coordinate, Polygon>
                {
                    { square.Centroid.Coordinate, square }
                };
                        if (!yIntersecting.ContainsKey(yLine))
                        {
                            yIntersecting[yLine] = new List<Dictionary<Coordinate, Polygon>>();
                        }
                        yIntersecting[yLine].Add(coordPolygonDict); // 添加与Y轴直线相交的正方形
                    }
                }
                // 如果正方形与X轴和Y轴都相交
                if (intersectsX && intersectsY)
                {
                    foreach (var xLine in intersectingXLines)
                    {
                        foreach (var yLine in intersectingYLines)
                        {
                            var combinedKey = new Tuple<LineString, LineString>(xLine, yLine);
                            var coordPolygonDict = new Dictionary<Coordinate, Polygon>
                    {
                        { square.Centroid.Coordinate, square }
                    };
                            if (!xyIntersecting.ContainsKey(combinedKey))
                            {
                                xyIntersecting[combinedKey] = new List<Dictionary<Coordinate, Polygon>>();
                            }
                            xyIntersecting[combinedKey].Add(coordPolygonDict); // 添加与X和Y轴的直线相交的正方形
                        }
                    }
                }
            }
            return (xIntersecting, yIntersecting, xyIntersecting);
        }
        private static void AdjustSquaresPosition(List<Polygon> xIntersecting, List<double> intersectingAreas)
        {
            // 遍历 xIntersecting 中的每一对正方形及其相交面积
            for (int i = 0; i < xIntersecting.Count; i += 2) // 假设每次是两对正方形
            {
                Polygon square1 = xIntersecting[i];
                Polygon square2 = xIntersecting[i + 1];
                // 获取每个正方形的面积
                double squareArea = square1.Area; // 假设两个正方形面积相同
                                                  // 获取每个正方形与多边形相交的面积比例
                double a1 = intersectingAreas[i] / squareArea;
                double a2 = intersectingAreas[i + 1] / squareArea;
                // 获取正方形的y坐标
                double y1 = square1.Centroid.Coordinate.Y;
                double y2 = square2.Centroid.Coordinate.Y;
                // 比较 y 坐标，确定 a1 和 a2
                if (y1 < y2)
                {
                    // y1 对应 a1，y2 对应 a2
                }
                else
                {
                    // 交换 a1 和 a2
                    double temp = a1;
                    a1 = a2;
                    a2 = temp;
                    // 交换 y1 和 y2
                    double tempY = y1;
                    y1 = y2;
                    y2 = tempY;
                }
                // 计算正方形的移动距离
                double d1 = (1 - a1) * squareArea; // d1 计算公式：d1 = (1 - a1) * 正方形边长
                                                   // 调整 y 坐标
                y1 -= d1;  // 移动第一个正方形
                y2 += d1;  // 移动第二个正方形
                           // 根据 a2 的值进行分类调整
                if (a2 >= 0)
                {
                    // 标记删除 a2
                    // 例如，可以从 xIntersecting 中删除 square2
                    xIntersecting.RemoveAt(i + 1);
                    intersectingAreas.RemoveAt(i + 1);
                }
                else if (a2 <= -0.8)
                {
                    // 标记不做更改
                }
                else if (-0.8 < a2 && a2 < 0)
                {
                    // 标记移动
                    // a1 向下移动 a2 / 2 , a2 向上移动 a2 / 2
                    double moveAmount = a2 / 2;
                    y1 -= moveAmount;
                    y2 += moveAmount;
                }
                // 更新正方形的位置
                square1.Centroid.Coordinate.Y = y1;
                square2.Centroid.Coordinate.Y = y2;
            }
        }
        //*************
        public static void GroupSquaresAndDrawLines(List<Polygon> xIntersecting)
        {
            // 第一步: 按质心X坐标分组，并统计每组的个数，x坐标相同的有可能有多组
            var groupedSquares = GroupSquaresByCentroidX(xIntersecting);
            // 第二步: 重新分组，每个list只有两个数据，为正方形对
            groupedSquares = groupedSquares.ReorganizeSquaresToPairs();
            int maxGroupSize = groupedSquares.Values.Max(group => group.Count);
            string layerName = $"00-hy-group";
            CreateLayerWithColor(layerName, Color.FromRgb((byte)(128), 0, (byte)(128)));
            foreach (var group in groupedSquares.Values)
            {
                DrawLinesForGroup(group, layerName);
            }
        }
        private static Dictionary<double, List<Polygon>> GroupSquaresByCentroidX(List<Polygon> xIntersecting)
        {
            // 创建一个字典，用于按 X 坐标分组，并统计每组的正方形个数
            Dictionary<double, List<Polygon>> groupedSquares = new Dictionary<double, List<Polygon>>();
            foreach (var square in xIntersecting)
            {
                // 获取正方形的质心坐标
                Coordinate centroid = square.Centroid.Coordinate;
                // 获取 X 坐标（可以根据需要四舍五入或调整精度）
                double xCoordinate = Math.Round(centroid.X, 2);  // 四舍五入到小数点后2位，避免浮动误差
                                                                 // 检查该 X 坐标是否已存在于字典中
                if (!groupedSquares.ContainsKey(xCoordinate))
                {
                    groupedSquares[xCoordinate] = new List<Polygon>();
                }
                // 将正方形添加到相应的质心分组中
                groupedSquares[xCoordinate].Add(square);
            }
            return groupedSquares;
        }
        private static void CreateLayerWithColor(string layerName, Color color)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                LayerTable layerTable = tr.GetObject(doc.Database.LayerTableId, OpenMode.ForWrite) as LayerTable;
                if (!layerTable.Has(layerName))
                {
                    LayerTableRecord layerRecord = new LayerTableRecord
                    {
                        Name = layerName,
                        Color = color
                    };
                    layerTable.Add(layerRecord);
                    tr.AddNewlyCreatedDBObject(layerRecord, true);
                }
                tr.Commit();
            }
        }
        private static void DrawLinesForGroup(List<Polygon> group, string layerName)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                BlockTable blockTable = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord modelSpace = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                // 根据正方形的个数进行分组
                if (group.Count % 2 == 0) // 双数
                {
                    for (int i = 0; i < group.Count; i += 2)
                    {
                        var square1 = group[i];
                        var square2 = group[i + 1];
                        // 获取正方形的质心坐标
                        Coordinate c1 = square1.Centroid.Coordinate;
                        Coordinate c2 = square2.Centroid.Coordinate;
                        // 创建直线
                        Line line = new Line(new Point3d(c1.X, c1.Y, 0), new Point3d(c2.X, c2.Y, 0))
                        {
                            Layer = layerName
                        };
                        modelSpace.AppendEntity(line);
                        tr.AddNewlyCreatedDBObject(line, true);
                    }
                }
                else // 单数
                {
                    var first = group.First();
                    var last = group.Last();
                    // 获取正方形的质心坐标
                    Coordinate c1 = first.Centroid.Coordinate;
                    Coordinate c2 = last.Centroid.Coordinate;
                    // 创建直线
                    Line line = new Line(new Point3d(c1.X, c1.Y, 0), new Point3d(c2.X, c2.Y, 0))
                    {
                        Layer = layerName
                    };
                    modelSpace.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                }
                tr.Commit();
            }
        }
        /// <summary>
        /// 重新分组，每个list只有两个数据，为正方形对
        /// </summary>
        /// <param name="groupedSquares"></param>
        /// <returns></returns>
        private static Dictionary<double, List<Polygon>> ReorganizeSquaresToPairs(this Dictionary<double, List<Polygon>> groupedSquares)
        {
            // 创建一个新的字典用于存储修改后的分组
            Dictionary<double, List<Polygon>> newGroupedSquares = new Dictionary<double, List<Polygon>>();
            foreach (var group in groupedSquares)
            {
                List<Polygon> groupSquares = group.Value; // 获取当前组的正方形列表
                List<Polygon> newGroup = new List<Polygon>(); // 创建新的分组列表
                                                              // 如果当前组的正方形数目是偶数
                if (groupSquares.Count % 2 == 0)
                {
                    // 每两个正方形分为一组
                    for (int i = 0; i < groupSquares.Count; i += 2)
                    {
                        // 将每对正方形添加到新组中
                        newGroup.Add(groupSquares[i]);
                        newGroup.Add(groupSquares[i + 1]);
                    }
                }
                else if (groupSquares.Count == 1)
                {
                    //如果只有一个正方形忽略
                }
                else
                {
                    // 如果是单数，将第一个和最后一个正方形组成一组
                    newGroup.Add(groupSquares.First());  // 添加第一个正方形
                    newGroup.Add(groupSquares.Last());   // 添加最后一个正方形
                                                         // 然后再按顺序处理剩下的正方形
                    for (int i = 1; i < groupSquares.Count - 1; i += 2)
                    {
                        // 将每对正方形添加到新组中
                        newGroup.Add(groupSquares[i]);
                        newGroup.Add(groupSquares[i + 1]);
                    }
                }
                // 将处理后的组存入新的字典
                newGroupedSquares[group.Key] = newGroup;
            }
            return newGroupedSquares;
        }
        #endregion
    }
}
