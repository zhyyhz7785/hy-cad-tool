using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate;
using System;
using System.Collections.Generic;
using NtsGeometry = NetTopologySuite.Geometries.Geometry;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// Voronoi 图与 Lloyd 优化服务（纯 Domain，平台无关）
    /// 负责：Voronoi 图生成、Lloyd 算法优化桩位
    /// </summary>
    public class VoronoiOptimizationService
    {
        /// <summary>
        /// 创建 Voronoi 图
        /// </summary>
        public GeometryCollection CreateVoronoiDiagram(Polygon polygon, List<Coordinate> points)
        {
            var voronoiBuilder = new VoronoiDiagramBuilder();
            voronoiBuilder.SetSites(points);
            voronoiBuilder.ClipEnvelope = polygon.EnvelopeInternal;

            var geometryFactory = new GeometryFactory();
            var voronoiDiagram = voronoiBuilder.GetDiagram(geometryFactory);

            return voronoiDiagram;
        }

        /// <summary>
        /// 应用 Lloyd 算法优化桩位
        /// </summary>
        /// <param name="polygon">约束多边形</param>
        /// <param name="points">初始桩位</param>
        /// <param name="iterations">迭代次数</param>
        /// <param name="pileDiameter">桩直径（用于判断最小单元面积）</param>
        public List<Coordinate> ApplyLloydOptimization(Polygon polygon, List<Coordinate> points, int iterations, double pileDiameter)
        {
            List<Coordinate> optimizedPoints = new List<Coordinate>(points);
            Coordinate polygonCentroid = polygon.Centroid.Coordinate;

            for (int iter = 0; iter < iterations; iter++)
            {
                var voronoiDiagram = CreateVoronoiDiagram(polygon, optimizedPoints);
                List<Coordinate> newPoints = new List<Coordinate>();

                foreach (NtsGeometry cell in voronoiDiagram)
                {
                    if (cell is Polygon voronoiCell)
                    {
                        NtsGeometry clippedCell = voronoiCell.Intersection(polygon);
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
                    newPoints.AddRange(GenerateRandomPointsInsidePolygon(polygon, missingCount));
                }

                optimizedPoints = new List<Coordinate>(newPoints);
            }

            return optimizedPoints;
        }

        /// <summary>
        /// 将点移动到多边形内部
        /// </summary>
        private Coordinate MovePointInsidePolygon(Coordinate point, Coordinate polygonCentroid, Polygon polygon, double initialDistance)
        {
            const double maxDistanceFactor = 10;
            double distance = initialDistance;

            while (true)
            {
                double directionX = polygonCentroid.X - point.X;
                double directionY = polygonCentroid.Y - point.Y;
                double length = Math.Sqrt(directionX * directionX + directionY * directionY);

                if (length < 1e-6)
                {
                    directionX = 1.0;
                    directionY = 0.0;
                    length = 1.0;
                }

                double unitX = directionX / length;
                double unitY = directionY / length;

                Coordinate movedPoint = new Coordinate(
                    point.X + unitX * distance,
                    point.Y + unitY * distance
                );

                if (polygon.Contains(new Point(movedPoint)))
                {
                    return movedPoint;
                }

                distance += initialDistance;

                if (distance > initialDistance * maxDistanceFactor)
                {
                    throw new InvalidOperationException("无法将点移动到多边形内部，可能是多边形形状过于复杂或输入错误。");
                }
            }
        }

        /// <summary>
        /// 在多边形内生成随机点
        /// </summary>
        public List<Coordinate> GenerateRandomPointsInsidePolygon(Polygon polygon, int numberOfPoints)
        {
            List<Coordinate> points = new List<Coordinate>();
            Random rand = new Random();
            Envelope envelope = polygon.EnvelopeInternal;

            while (points.Count < numberOfPoints)
            {
                double x = rand.NextDouble() * (envelope.MaxX - envelope.MinX) + envelope.MinX;
                double y = rand.NextDouble() * (envelope.MaxY - envelope.MinY) + envelope.MinY;
                Coordinate newPoint = new Coordinate(x, y);

                if (polygon.Contains(new Point(newPoint)))
                {
                    points.Add(newPoint);
                }
            }

            return points;
        }

        /// <summary>
        /// 过滤多边形内的点
        /// </summary>
        public List<Coordinate> GetPointsInsidePolygon(Polygon polygon, List<Coordinate> points)
        {
            List<Coordinate> pointsInsidePolygon = new List<Coordinate>();

            foreach (var point in points)
            {
                if (polygon.Contains(new Point(point)))
                {
                    pointsInsidePolygon.Add(point);
                }
            }

            return pointsInsidePolygon;
        }
    }
}
