using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Domain.Services.GeometryAlgorithms;
using HyCADTool.Refactored.Domain.Services.MathAlgorithms;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;
using System.Collections.Generic;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.TestDomainAlgorithmsCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// Domain 层算法测试命令
    /// Domain Layer Algorithm Test Commands
    /// </summary>
    public class TestDomainAlgorithmsCommand
    {
        /// <summary>
        /// 测试 PointAlgorithms
        /// Test PointAlgorithms
        /// </summary>
        [CommandMethod("HYTESTPOINT")]
        public void TestPointAlgorithms()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n========== 测试 PointAlgorithms ==========\n");

                // 创建测试点
                var p1 = new Point2D(0, 0);
                var p2 = new Point2D(10, 0);
                var p3 = new Point2D(5, 5);

                // 测试距离计算
                double distance = PointAlgorithms.Distance(p1, p2);
                ed.WriteMessage($"\n距离 (0,0) 到 (10,0): {distance:F3}"); // 应为 10

                // 测试中点
                var midpoint = PointAlgorithms.Midpoint(p1, p2);
                ed.WriteMessage($"\n中点: ({midpoint.X:F3}, {midpoint.Y:F3})"); // 应为 (5, 0)

                // 测试插值
                var lerp = PointAlgorithms.Lerp(p1, p2, 0.3);
                ed.WriteMessage($"\n插值 (t=0.3): ({lerp.X:F3}, {lerp.Y:F3})"); // 应为 (3, 0)

                // 测试旋转
                var rotated = PointAlgorithms.Rotate(p2, Math.PI / 2); // 旋转 90 度
                ed.WriteMessage($"\n旋转 90°: ({rotated.X:F3}, {rotated.Y:F3})"); // 应约为 (0, 10)

                // 测试投影
                var line = new Line2D(p1, p2);
                var projection = PointAlgorithms.ProjectToLine(p3, line);
                ed.WriteMessage($"\n点 (5,5) 到线段 (0,0)-(10,0) 的投影: ({projection.X:F3}, {projection.Y:F3})"); // 应为 (5, 0)

                // 测试点到直线距离
                double distToLine = PointAlgorithms.DistanceToLine(p3, line);
                ed.WriteMessage($"\n点 (5,5) 到线段的距离: {distToLine:F3}"); // 应为 5

                ed.WriteMessage("\n\n✓ PointAlgorithms 测试完成\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 测试 LineAlgorithms
        /// Test LineAlgorithms
        /// </summary>
        [CommandMethod("HYTESTLINE")]
        public void TestLineAlgorithms()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n========== 测试 LineAlgorithms ==========\n");

                // 创建测试线段
                var line1 = new Line2D(new Point2D(0, 0), new Point2D(10, 0)); // 水平线
                var line2 = new Line2D(new Point2D(0, 0), new Point2D(0, 10)); // 垂直线
                var line3 = new Line2D(new Point2D(5, -5), new Point2D(5, 5)); // 另一条垂直线

                // 测试平行
                bool isParallel = LineAlgorithms.AreParallel(line2, line3);
                ed.WriteMessage($"\n垂直线是否平行: {isParallel}"); // 应为 true

                // 测试垂直
                bool isPerpendicular = LineAlgorithms.ArePerpendicular(line1, line2);
                ed.WriteMessage($"\n水平线和垂直线是否垂直: {isPerpendicular}"); // 应为 true

                // 测试交点
                var intersection = LineAlgorithms.GetIntersection(line1, line3);
                if (intersection != null)
                {
                    ed.WriteMessage($"\n交点: ({intersection.Value.X:F3}, {intersection.Value.Y:F3})"); // 应为 (5, 0)
                }
                else
                {
                    ed.WriteMessage("\n没有交点");
                }

                // 测试长度
                double length = LineAlgorithms.GetLength(line1);
                ed.WriteMessage($"\n线段长度: {length:F3}"); // 应为 10

                // 测试中点
                var midpoint = LineAlgorithms.GetMidpoint(line1);
                ed.WriteMessage($"\n线段中点: ({midpoint.X:F3}, {midpoint.Y:F3})"); // 应为 (5, 0)

                // 测试角度
                double angle = LineAlgorithms.GetAngle(line1);
                ed.WriteMessage($"\n线段角度: {AngleCalculator.RadiansToDegrees(angle):F3}°"); // 应为 0°

                // 测试水平/垂直判断
                bool isHorizontal = LineAlgorithms.IsHorizontal(line1);
                bool isVertical = LineAlgorithms.IsVertical(line2);
                ed.WriteMessage($"\nline1 是否水平: {isHorizontal}, line2 是否垂直: {isVertical}"); // 都应为 true

                // 测试偏移
                var offset = LineAlgorithms.Offset(line1, 2); // 向左偏移 2
                ed.WriteMessage($"\n偏移后起点: ({offset.StartPoint.X:F3}, {offset.StartPoint.Y:F3})"); // 应为 (0, 2)

                ed.WriteMessage("\n\n✓ LineAlgorithms 测试完成\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 测试 PolygonAlgorithms
        /// Test PolygonAlgorithms
        /// </summary>
        [CommandMethod("HYTESTPOLYGON")]
        public void TestPolygonAlgorithms()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n========== 测试 PolygonAlgorithms ==========\n");

                // 创建正方形
                var square = new List<Point2D>
                {
                    new Point2D(0, 0),
                    new Point2D(10, 0),
                    new Point2D(10, 10),
                    new Point2D(0, 10)
                };

                // 测试面积
                double area = PolygonAlgorithms.CalculateArea(square);
                ed.WriteMessage($"\n正方形面积: {area:F3}"); // 应为 100

                // 测试周长
                double perimeter = PolygonAlgorithms.CalculatePerimeter(square);
                ed.WriteMessage($"\n正方形周长: {perimeter:F3}"); // 应为 40

                // 测试点在多边形内
                var insidePoint = new Point2D(5, 5);
                var outsidePoint = new Point2D(15, 15);
                bool isInside1 = PolygonAlgorithms.ContainsPoint(square, insidePoint);
                bool isInside2 = PolygonAlgorithms.ContainsPoint(square, outsidePoint);
                ed.WriteMessage($"\n点 (5,5) 在内部: {isInside1}"); // 应为 true
                ed.WriteMessage($"\n点 (15,15) 在内部: {isInside2}"); // 应为 false

                // 测试凸多边形判断
                bool isConvex = PolygonAlgorithms.IsConvex(square);
                ed.WriteMessage($"\n是否为凸多边形: {isConvex}"); // 应为 true

                // 测试质心
                var centroid = PolygonAlgorithms.CalculateCentroid(square);
                ed.WriteMessage($"\n质心: ({centroid.X:F3}, {centroid.Y:F3})"); // 应为 (5, 5)

                // 测试顺时针/逆时针
                bool isClockwise = PolygonAlgorithms.IsClockwise(square);
                ed.WriteMessage($"\n是否顺时针: {isClockwise}");

                ed.WriteMessage("\n\n✓ PolygonAlgorithms 测试完成\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 测试 AngleCalculator
        /// Test AngleCalculator
        /// </summary>
        [CommandMethod("HYTESTANGLE")]
        public void TestAngleCalculator()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n========== 测试 AngleCalculator ==========\n");

                // 测试弧度/角度转换
                double radians = Math.PI / 4; // 45 度
                double degrees = AngleCalculator.RadiansToDegrees(radians);
                ed.WriteMessage($"\nπ/4 弧度 = {degrees:F3}°"); // 应为 45°

                double radians2 = AngleCalculator.DegreesToRadians(90);
                ed.WriteMessage($"\n90° = {radians2:F6} 弧度"); // 应为 π/2 ≈ 1.5708

                // 测试归一化
                double normalized = AngleCalculator.NormalizeAngle(3 * Math.PI);
                ed.WriteMessage($"\n3π 归一化 = {normalized:F6} 弧度"); // 应为 π

                // 测试向量夹角
                var v1 = new Vector2D(1, 0);
                var v2 = new Vector2D(0, 1);
                double angleBetween = AngleCalculator.AngleBetweenVectors(v1, v2);
                ed.WriteMessage($"\n向量 (1,0) 和 (0,1) 夹角: {AngleCalculator.RadiansToDegrees(angleBetween):F3}°"); // 应为 90°

                // 测试三点夹角
                var p1 = new Point2D(1, 0);
                var p2 = new Point2D(0, 0); // 顶点
                var p3 = new Point2D(0, 1);
                double angleAtVertex = AngleCalculator.AngleAtVertex(p1, p2, p3);
                ed.WriteMessage($"\n三点夹角: {AngleCalculator.RadiansToDegrees(angleAtVertex):F3}°"); // 应为 90°

                // 测试角度类型判断
                bool isRightAngle = AngleCalculator.IsRightAngle(Math.PI / 2);
                bool isAcute = AngleCalculator.IsAcuteAngle(Math.PI / 6); // 30°
                bool isObtuse = AngleCalculator.IsObtuseAngle(2 * Math.PI / 3); // 120°
                ed.WriteMessage($"\nπ/2 是直角: {isRightAngle}"); // true
                ed.WriteMessage($"\nπ/6 是锐角: {isAcute}"); // true
                ed.WriteMessage($"\n2π/3 是钝角: {isObtuse}"); // true

                ed.WriteMessage("\n\n✓ AngleCalculator 测试完成\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 测试 DistanceCalculator
        /// Test DistanceCalculator
        /// </summary>
        [CommandMethod("HYTESTDISTANCE")]
        public void TestDistanceCalculator()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n========== 测试 DistanceCalculator ==========\n");

                var p1 = new Point2D(0, 0);
                var p2 = new Point2D(3, 4);

                // 测试欧几里得距离
                double euclidean = DistanceCalculator.EuclideanDistance(p1, p2);
                ed.WriteMessage($"\n欧几里得距离 (0,0) 到 (3,4): {euclidean:F3}"); // 应为 5

                // 测试曼哈顿距离
                double manhattan = DistanceCalculator.ManhattanDistance(p1, p2);
                ed.WriteMessage($"\n曼哈顿距离: {manhattan:F3}"); // 应为 7

                // 测试切比雪夫距离
                double chebyshev = DistanceCalculator.ChebyshevDistance(p1, p2);
                ed.WriteMessage($"\n切比雪夫距离: {chebyshev:F3}"); // 应为 4

                // 测试点到线段距离
                var lineStart = new Point2D(0, 0);
                var lineEnd = new Point2D(10, 0);
                var point = new Point2D(5, 3);
                double distToSegment = DistanceCalculator.PointToLineSegmentDistance(point, lineStart, lineEnd);
                ed.WriteMessage($"\n点 (5,3) 到线段 (0,0)-(10,0) 的距离: {distToSegment:F3}"); // 应为 3

                // 测试路径长度
                var path = new List<Point2D>
                {
                    new Point2D(0, 0),
                    new Point2D(3, 0),
                    new Point2D(3, 4),
                    new Point2D(0, 4)
                };
                double pathLength = DistanceCalculator.CalculatePathLength(path, closed: false);
                ed.WriteMessage($"\n路径长度: {pathLength:F3}"); // 应为 3 + 4 + 3 = 10

                double closedPathLength = DistanceCalculator.CalculatePathLength(path, closed: true);
                ed.WriteMessage($"\n闭合路径长度: {closedPathLength:F3}"); // 应为 10 + 4 = 14

                // 测试最近点
                var points = new List<Point2D>
                {
                    new Point2D(10, 10),
                    new Point2D(5, 5),
                    new Point2D(20, 20)
                };
                var (closestPoint, minDist) = DistanceCalculator.FindClosestPoint(p1, points);
                ed.WriteMessage($"\n最近点: ({closestPoint.X:F3}, {closestPoint.Y:F3}), 距离: {minDist:F3}");

                // 测试范围内判断
                bool isWithin = DistanceCalculator.IsWithinDistance(p1, new Point2D(3, 4), 6);
                ed.WriteMessage($"\n(0,0) 和 (3,4) 距离在 6 以内: {isWithin}"); // true

                ed.WriteMessage("\n\n✓ DistanceCalculator 测试完成\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 运行所有 Domain 算法测试
        /// Run all Domain algorithm tests
        /// </summary>
        [CommandMethod("HYTESTDOMAINALL")]
        public void TestAllDomainAlgorithms()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n╔════════════════════════════════════════════════════╗");
            ed.WriteMessage("\n║      HyCADTool - Domain 算法测试套件               ║");
            ed.WriteMessage("\n╚════════════════════════════════════════════════════╝\n");

            try
            {
                TestPointAlgorithms();
                ed.WriteMessage("\n");
                TestLineAlgorithms();
                ed.WriteMessage("\n");
                TestPolygonAlgorithms();
                ed.WriteMessage("\n");
                TestAngleCalculator();
                ed.WriteMessage("\n");
                TestDistanceCalculator();

                ed.WriteMessage("\n\n╔════════════════════════════════════════════════════╗");
                ed.WriteMessage("\n║          所有 Domain 算法测试完成！                ║");
                ed.WriteMessage("\n╚════════════════════════════════════════════════════╝\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n\n❌ 测试失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

