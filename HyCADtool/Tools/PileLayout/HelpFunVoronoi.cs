using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate;
using System;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class Et
    {
        /// <summary>
        /// 选择一个闭合多段线，并将其转换为 NetTopologySuite 的 Polygon
        /// </summary>
        /// <returns>返回一个 NetTopologySuite 的 Polygon 或 null</returns>
        public static Polygon PromptAndGetPolygon()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            Editor ed = doc.Editor;
            // 提示用户选择闭合多段线
            var peo = new PromptEntityOptions("\n请选择一个闭合的多段线:");
            peo.SetRejectMessage("\n所选对象必须是闭合的多段线！\n");
            peo.AddAllowedClass(typeof(Polyline), exactMatch: false);
            // 获取用户选择的实体
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择有效的多段线，命令结束。");
                return null;
            }
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                // 打开选中的对象
                var polyline = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                if (polyline == null || !polyline.Closed)
                {
                    ed.WriteMessage("\n所选对象不是闭合多段线！");
                    return null;
                }
                // 将多段线的顶点转换为 NTS 的 Coordinate 列表
                var coordinates = new List<Coordinate>();
                int vertexCount = polyline.NumberOfVertices;
                for (int i = 0; i < vertexCount; i++)
                {
                    Point2d vertex = polyline.GetPoint2dAt(i);
                    coordinates.Add(new Coordinate(vertex.X, vertex.Y));
                }
                coordinates.Add(new Coordinate(coordinates[0].X, coordinates[0].Y));
                // 构造 NetTopologySuite 的 Polygon
                var geometryFactory = new GeometryFactory();
                var linearRing = geometryFactory.CreateLinearRing(coordinates.ToArray());
                var polygon = geometryFactory.CreatePolygon(linearRing);
                tr.Commit();
                return polygon;
            }
        }
        private static (double diameter, double replacementRate, int numberOfPiles) GetPileParametersAndCount(Polygon polygon)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 默认值
            double diameter = 400.0; // 默认桩直径400mm
            double replacementRate = 0.022734275; // 默认置换率5%
                                                  // 获取桩直径
            PromptDoubleOptions pdoD = new PromptDoubleOptions("\n请输入桩直径(单位mm):");
            pdoD.DefaultValue = 400.0;
            var pdrD = ed.GetDouble(pdoD);
            if (pdrD.Status == PromptStatus.OK)
            {
                diameter = pdrD.Value;
            }
            // 获取目标置换率
            PromptDoubleOptions pdoRate = new PromptDoubleOptions("\n请输入目标置换率 (0-1):");
            pdoRate.DefaultValue = 0.022734275;
            var pdrRate = ed.GetDouble(pdoRate);
            if (pdrRate.Status == PromptStatus.OK)
            {
                replacementRate = pdrRate.Value;
            }
            // 计算桩的数量
            double pileArea = Math.PI * Math.Pow(diameter / 2.0, 2); // 单个桩的面积，单位mm²
            double totalArea = polygon.Area; // 多边形面积，单位mm²
            int numberOfPiles = (int)Math.Ceiling((totalArea * replacementRate) / pileArea); // 计算桩的数量
                                                                                             // 返回桩直径、置换率和桩数
            return (diameter, replacementRate, numberOfPiles);
        }
        /// <summary>
        /// 对比上面，添加了用户输入桩数量。
        /// </summary>
        /// <param name="polygon"></param>
        /// <returns></returns>
        private static (double diameter, double replacementRate, int numberOfPiles) GetPileParametersAndCount1(Polygon polygon)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 默认值
            double diameter = 400.0; // 默认桩直径400mm
            double replacementRate = 0.022734275; // 默认置换率5%           
            int usernumberOfPiles = 0; // 默认桩数为0
                                       // 获取桩的数量 (如果根数不为0，则使用用户输入的根数)
            PromptIntegerOptions pdoPiles = new PromptIntegerOptions("\n请输入桩的根数 (0表示自动计算):");
            pdoPiles.DefaultValue = 0; // 默认值为0，表示自动计算
            var pdrPiles = ed.GetInteger(pdoPiles);
            if (pdrPiles.Status == PromptStatus.OK && pdrPiles.Value != 0)
            {
                usernumberOfPiles = pdrPiles.Value;
            }
            else
            {
                // 计算桩的数量
                double pileArea = Math.PI * Math.Pow(diameter / 2.0, 2); // 单个桩的面积，单位mm²
                double totalArea = polygon.Area; // 多边形面积，单位mm²
                usernumberOfPiles = (int)Math.Ceiling((totalArea * replacementRate) / pileArea); // 计算桩的数量
                ed.WriteMessage($"\n对应置换率，需要的桩为{usernumberOfPiles}棵");
            }
            // 获取桩直径
            PromptDoubleOptions pdoD = new PromptDoubleOptions("\n请输入桩直径(单位mm):");
            pdoD.DefaultValue = 400.0;
            var pdrD = ed.GetDouble(pdoD);
            if (pdrD.Status == PromptStatus.OK)
            {
                diameter = pdrD.Value;
            }
            // 获取目标置换率
            PromptDoubleOptions pdoRate = new PromptDoubleOptions("\n请输入目标置换率 (0-100)%:");
            pdoRate.DefaultValue = 2.2;
            var pdrRate = ed.GetDouble(pdoRate);
            if (pdrRate.Status == PromptStatus.OK)
            {
                replacementRate = pdrRate.Value / 100;
            }
            // 返回桩直径、置换率和桩数
            return (diameter, replacementRate, usernumberOfPiles);
        }
        private static GeometryCollection CreateVoronoiDiagram(Polygon polygon, List<Coordinate> points)
        {
            // 使用 VoronoiDiagramBuilder 创建 Voronoi 图
            var voronoiBuilder = new VoronoiDiagramBuilder();
            voronoiBuilder.SetSites(points);  // 设置 Voronoi 图的点集
            voronoiBuilder.ClipEnvelope = polygon.EnvelopeInternal;  // 限制Voronoi图的生成区域
            var geometryFactory = new GeometryFactory();
            var voronoiDiagram = voronoiBuilder.GetDiagram(geometryFactory);  // 获取Voronoi图            
            return voronoiDiagram;  // 返回 GeometryCollection 类型        
        }
        private static Coordinate GenerateRandomPointInsidePolygon(Polygon polygon)
        {
            Random rand = new Random();
            Envelope envelope = polygon.EnvelopeInternal;
            // 不断生成随机点直到其位于多边形内部
            while (true)
            {
                double x = rand.NextDouble() * (envelope.MaxX - envelope.MinX) + envelope.MinX;
                double y = rand.NextDouble() * (envelope.MaxY - envelope.MinY) + envelope.MinY;
                Coordinate newPoint = new Coordinate(x, y);
                // 确保点在多边形内部
                if (polygon.Contains(new Point(newPoint)))
                {
                    return newPoint;
                }
            }
        }
        /// <summary>
        /// 生成多边形内部的随机点
        /// </summary>     
        private static List<Coordinate> GeneratePointsInsidePolygon(Polygon polygon, int numberOfPoints)
        {
            List<Coordinate> points = new List<Coordinate>();
            Random rand = new Random();
            // 使用多边形的包围盒来避免生成超出多边形范围的点
            Envelope envelope = polygon.EnvelopeInternal;
            while (points.Count < numberOfPoints)
            {
                double x = rand.NextDouble() * (envelope.MaxX - envelope.MinX) + envelope.MinX;
                double y = rand.NextDouble() * (envelope.MaxY - envelope.MinY) + envelope.MinY;
                Coordinate newPoint = new Coordinate(x, y);
                // 确保点在多边形内部（不仅仅是位于包围盒内）
                if (polygon.Contains(new Point(newPoint)))
                {
                    points.Add(newPoint);
                }
            }
            return points;
        }
        /// <summary>
        /// 绘制多边形
        /// </summary>
        private static void DrawPolygon(BlockTableRecord btr, Transaction tr, List<Point2d> points, string layer)
        {
            using (Polyline pl = new Polyline())
            {
                for (int i = 0; i < points.Count; i++)
                {
                    pl.AddVertexAt(i, points[i], 0, 0, 0);
                }
                pl.Closed = true;
                pl.Layer = layer;
                btr.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);
            }
        }
        private static void DrawVoronoiRegion(Transaction tr, BlockTableRecord btr, GeometryCollection voronoiDiagram, Polygon polygon)
        {
            foreach (Geometry cell in voronoiDiagram)
            {
                // 裁剪 Voronoi 区域到基础多边形
                Geometry intersection = cell.Intersection(polygon);
                if (intersection is Polygon clippedPolygon)
                {
                    // 将 Voronoi 区域绘制为多边形
                    List<Point2d> points = new List<Point2d>();
                    foreach (Coordinate coord in clippedPolygon.Coordinates)
                    {
                        points.Add(new Point2d(coord.X, coord.Y));
                    }
                    DrawPolygon(btr, tr, points, "00-HY-Voronoi");  // 绘制 Voronoi 区域
                }
            }
        }
        private static void DrawVoronoiRegion(Transaction tr, BlockTableRecord btr, GeometryCollection voronoiDiagram)
        {
            foreach (Geometry cell in voronoiDiagram)
            {
                if (cell is Polygon clippedPolygon)
                {
                    // 将 Voronoi 区域绘制为多边形
                    List<Point2d> points = new List<Point2d>();
                    foreach (Coordinate coord in clippedPolygon.Coordinates)
                    {
                        points.Add(new Point2d(coord.X, coord.Y));
                    }
                    DrawPolygon(btr, tr, points, "00-HY-Voronoi");  // 绘制 Voronoi 区域
                }
            }
        }
        private static void DrawPiles(Transaction tr, BlockTableRecord btr, List<Coordinate> pileCenters, double diameter)
        {
            double radius = (diameter / 2.0); // 半径，单位mm
            foreach (Coordinate center in pileCenters)
            {
                // 在每个生成点的位置绘制桩
                using (Circle circle = new Circle(new Point3d(center.X, center.Y, 0), Vector3d.ZAxis, radius))
                {
                    circle.Layer = "00-HY-桩";
                    btr.AppendEntity(circle);
                    tr.AddNewlyCreatedDBObject(circle, true);
                }
            }
        }
        //private static List<Coordinate> ApplyLloydOptimization(Polygon polygon, List<Coordinate> points, int iterations)
        //{
        //    List<Coordinate> optimizedPoints = new List<Coordinate>(points);
        //    for (int iter = 0; iter < iterations; iter++)
        //    {
        //        // 使用所有点共同生成 Voronoi 图
        //        var voronoiDiagram = CreateVoronoiDiagram(polygon, optimizedPoints);
        //        List<Coordinate> newPoints = new List<Coordinate>();
        //        // 遍历 Voronoi 图中的所有几何对象
        //        foreach (Geometry cell in voronoiDiagram)
        //        {
        //            if (cell is Polygon clippedPolygon)
        //            {
        //                // 计算 Voronoi 区域的质心
        //                var centroid = clippedPolygon.Centroid.Coordinate;
        //                if (centroid != null)
        //                {
        //                    newPoints.Add(centroid);
        //                }
        //            }
        //        }
        //        //ed.WriteMessage($"\n第 {iter + 1} 轮优化后，点集数量: {newPoints.Count}");
        //        // 输出每个点的新位置
        //        foreach (var p in newPoints)
        //        {
        //            //ed.WriteMessage($"\nX坐标: {p.X}, Y坐标: {p.Y}");
        //        }
        //        // 更新优化后的点集
        //        optimizedPoints = new List<Coordinate>(newPoints);
        //    }
        //    return optimizedPoints;
        //}
        //private static List<Coordinate> ApplyLloydOptimization(Polygon polygon, List<Coordinate> points, int iterations)
        //{
        //    // 初始化优化后的点集
        //    List<Coordinate> optimizedPoints = new List<Coordinate>(points);
        //    for (int iter = 0; iter < iterations; iter++)
        //    {
        //        // 生成 Voronoi 图
        //        var voronoiDiagram = CreateVoronoiDiagram(polygon, optimizedPoints);
        //        List<Coordinate> newPoints = new List<Coordinate>();
        //        // 遍历 Voronoi 图中的每个单元
        //        foreach (Geometry cell in voronoiDiagram)
        //        {
        //            // 检查当前单元是否为多边形
        //            if (cell is Polygon voronoiCell)
        //            {
        //                // 裁剪 Voronoi 单元到多边形范围内
        //                Geometry clippedCell = voronoiCell.Intersection(polygon);
        //                // 如果裁剪结果是多边形，则计算质心
        //                if (clippedCell is Polygon clippedPolygon && !clippedPolygon.IsEmpty)
        //                {
        //                    var centroid = clippedPolygon.Centroid.Coordinate;
        //                    if (centroid != null && polygon.Contains(new Point(centroid)))
        //                    {
        //                        newPoints.Add(centroid); // 仅添加在多边形内部的质心
        //                    }
        //                }
        //            }
        //        }
        //        // 更新优化点集
        //        optimizedPoints = new List<Coordinate>(newPoints);
        //    }
        //    return optimizedPoints;
        //}
        //private static List<Coordinate> ApplyLloydOptimization(Polygon polygon, List<Coordinate> points, int iterations, double pileDiameter)
        //{
        //    // 初始化优化后的点集
        //    List<Coordinate> optimizedPoints = new List<Coordinate>(points);
        //    // 计算多边形的质心
        //    Coordinate polygonCentroid = polygon.Centroid.Coordinate;
        //    for (int iter = 0; iter < iterations; iter++)
        //    {
        //        // 生成 Voronoi 图
        //        var voronoiDiagram = CreateVoronoiDiagram(polygon, optimizedPoints);
        //        List<Coordinate> newPoints = new List<Coordinate>();
        //        // 遍历 Voronoi 图中的每个单元
        //        foreach (Geometry cell in voronoiDiagram)
        //        {
        //            // 检查当前单元是否为多边形
        //            if (cell is Polygon voronoiCell)
        //            {
        //                // 裁剪 Voronoi 单元到多边形范围内
        //                Geometry clippedCell = voronoiCell.Intersection(polygon);
        //                // 如果裁剪结果是多边形，则计算质心
        //                if (clippedCell is Polygon clippedPolygon && !clippedPolygon.IsEmpty)
        //                {
        //                    Coordinate centroid = clippedPolygon.Centroid.Coordinate;
        //                    // 如果质心在多边形外部，移动到多边形内部
        //                    if (!polygon.Contains(new Point(centroid)))
        //                    {
        //                        centroid = MovePointInsidePolygon(centroid, polygonCentroid, polygon, 10 * pileDiameter);
        //                    }
        //                    if (centroid != null)
        //                    {
        //                        newPoints.Add(centroid); // 添加修正后的质心
        //                    }
        //                }
        //            }
        //        }
        //        // 更新优化点集
        //        optimizedPoints = new List<Coordinate>(newPoints);
        //    }
        //    return optimizedPoints;
        //}
        private static List<Coordinate> ApplyLloydOptimization(Polygon polygon, List<Coordinate> points, int iterations, double pileDiameter)
        {
            List<Coordinate> optimizedPoints = new List<Coordinate>(points);
            Coordinate polygonCentroid = polygon.Centroid.Coordinate;
            for (int iter = 0; iter < iterations; iter++)
            {
                var voronoiDiagram = CreateVoronoiDiagram(polygon, optimizedPoints);
                List<Coordinate> newPoints = new List<Coordinate>();
                foreach (Geometry cell in voronoiDiagram)
                {
                    if (cell is Polygon voronoiCell)
                    {
                        Geometry clippedCell = voronoiCell.Intersection(polygon);
                        if (clippedCell is Polygon clippedPolygon && !clippedPolygon.IsEmpty)
                        {
                            // 如果裁剪后的单元面积过小，跳过
                            if (clippedPolygon.Area < Math.Pow(pileDiameter / 10, 2))
                            {
                                continue;
                            }
                            Coordinate centroid = clippedPolygon.Centroid.Coordinate;
                            if (centroid != null && !polygon.Contains(new Point(centroid)))
                            {
                                centroid = MovePointInsidePolygon(centroid, polygonCentroid, polygon, 2 * pileDiameter);
                            }
                            if (centroid != null)
                            {
                                newPoints.Add(centroid);
                            }
                        }
                    }
                }
                // 补充缺失点，保持桩数不变
                if (newPoints.Count < points.Count)
                {
                    int missingCount = points.Count - newPoints.Count;
                    newPoints.AddRange(GeneratePointsInsidePolygon(polygon, missingCount));
                }
                optimizedPoints = new List<Coordinate>(newPoints);
            }
            return optimizedPoints;
        }
        //private static Coordinate MovePointInsidePolygon(Coordinate point, Coordinate polygonCentroid, Polygon polygon, double distance)
        //{
        //    // 计算从点到多边形质心的向量方向
        //    double directionX = polygonCentroid.X - point.X;
        //    double directionY = polygonCentroid.Y - point.Y;
        //    double length = Math.Sqrt(directionX * directionX + directionY * directionY);
        //    // 归一化向量
        //    double unitX = directionX / length;
        //    double unitY = directionY / length;
        //    // 沿方向移动距离
        //    Coordinate movedPoint = new Coordinate(
        //        point.X + unitX * distance,
        //        point.Y + unitY * distance
        //    );
        //    // 如果仍然在多边形外部，递归尝试更小的移动距离
        //    if (!polygon.Contains(new Point(movedPoint)))
        //    {
        //        return MovePointInsidePolygon(movedPoint, polygonCentroid, polygon, distance / 2);
        //    }
        //    return movedPoint;
        //}
        private static Coordinate MovePointInsidePolygon(Coordinate point, Coordinate polygonCentroid, Polygon polygon, double initialDistance)
        {
            const double maxDistanceFactor = 10; // 最大移动距离因子（初始距离的倍数）
            double distance = initialDistance;
            while (true)
            {
                // 计算从点到多边形质心的向量方向
                double directionX = polygonCentroid.X - point.X;
                double directionY = polygonCentroid.Y - point.Y;
                double length = Math.Sqrt(directionX * directionX + directionY * directionY);
                // 如果点与质心重合，则略微调整方向
                if (length < 1e-6)
                {
                    directionX = 1.0;
                    directionY = 0.0;
                    length = 1.0;
                }
                // 归一化向量
                double unitX = directionX / length;
                double unitY = directionY / length;
                // 沿方向移动距离
                Coordinate movedPoint = new Coordinate(
                    point.X + unitX * distance,
                    point.Y + unitY * distance
                );
                // 如果点在多边形内，则返回结果
                if (polygon.Contains(new Point(movedPoint)))
                {
                    return movedPoint;
                }
                // 增加移动距离
                distance += initialDistance;
                // 防止无限循环，设置一个合理的最大移动距离
                if (distance > initialDistance * maxDistanceFactor)
                {
                    throw new InvalidOperationException("无法将点移动到多边形内部，可能是多边形形状过于复杂或输入错误。");
                }
            }
        }
        private static void CreateLayerIfNotExists(Transaction tr, Database db, string layerName, Color color)
        {
            LayerTable layerTable = tr.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
            if (!layerTable.Has(layerName))
            {
                LayerTableRecord layerTableRecord = new LayerTableRecord();
                layerTableRecord.Name = layerName;
                layerTableRecord.Color = color;
                layerTable.UpgradeOpen();
                layerTable.Add(layerTableRecord);
                tr.AddNewlyCreatedDBObject(layerTableRecord, true);
            }
        }
        public static List<Coordinate> GetSelectedCircleCentersAsCoordinates()
        {
            // 获取当前文档和编辑器
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 创建一个选择集的过滤条件，只允许选择圆
            TypedValue[] filter = new TypedValue[]
            {
        new TypedValue((int)DxfCode.Start, "CIRCLE")
            };
            SelectionFilter selectionFilter = new SelectionFilter(filter);
            // 提示用户选择对象
            PromptSelectionOptions selectionOptions = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择一些圆:"
            };
            PromptSelectionResult selectionResult = ed.GetSelection(selectionOptions, selectionFilter);
            // 检查选择结果
            if (selectionResult.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择任何圆。");
                return new List<Coordinate>(); // 返回空列表
            }
            // 获取选择集中的圆，并提取圆心
            List<Coordinate> circleCenters = new List<Coordinate>();
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                SelectionSet selectionSet = selectionResult.Value;
                foreach (SelectedObject selectedObj in selectionSet)
                {
                    if (selectedObj != null)
                    {
                        Circle circle = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Circle;
                        if (circle != null)
                        {
                            // 提取圆心并转换为 NetTopologySuite 的 Coordinate 类型
                            Coordinate center = new Coordinate(circle.Center.X, circle.Center.Y);
                            circleCenters.Add(center);
                        }
                    }
                }
                tr.Commit();
            }
            return circleCenters;
        }
        public static List<Coordinate> GetPointsInsidePolygon(Polygon polygon, List<Coordinate> points)
        {
            List<Coordinate> pointsInsidePolygon = new List<Coordinate>();
            // 通过多边形的包含方法来检查每个点
            foreach (var point in points)
            {
                if (polygon.Contains(new Point(point))) // 使用 NetTopologySuite 判断点是否在多边形内
                {
                    pointsInsidePolygon.Add(point); // 将在多边形内的点添加到结果列表中
                }
            }
            return pointsInsidePolygon;
        }
    }
}
