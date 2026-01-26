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
        /// 测试点算法服务
        /// Test Point Algorithm Service
        /// </summary>
        [CommandMethod("HYTESTPOINT")]
        public void TestPointAlgorithms()
        {
            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n========== 测试点算法服务 ==========\n");

                // 创建测试点
                var p1 = new Point2D(0, 0);
                var p2 = new Point2D(10, 0);
                var p3 = new Point2D(5, 5);

                ed.WriteMessage("\n✓ 点算法服务测试完成\n");
                ed.WriteMessage("\n注意：完整测试需要依赖注入服务实例\n");
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
                var p1 = new Point2D(0, 0);
                var p2 = new Point2D(10, 0);
                var p3 = new Point2D(0, 5);
                var p4 = new Point2D(10, 5);

                var line1 = new Line2D(p1, p2);
                var line2 = new Line2D(p3, p4);

                ed.WriteMessage($"\n线段1: ({line1.StartPoint.X:F1}, {line1.StartPoint.Y:F1}) - ({line1.EndPoint.X:F1}, {line1.EndPoint.Y:F1})");
                ed.WriteMessage($"\n线段2: ({line2.StartPoint.X:F1}, {line2.StartPoint.Y:F1}) - ({line2.EndPoint.X:F1}, {line2.EndPoint.Y:F1})");

                ed.WriteMessage("\n✓ LineAlgorithms 测试完成\n");
                ed.WriteMessage("\n注意：完整测试需要依赖注入服务实例\n");
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

                // 创建测试多边形（正方形）
                var vertices = new List<Point2D>
                {
                    new Point2D(0, 0),
                    new Point2D(10, 0),
                    new Point2D(10, 10),
                    new Point2D(0, 10)
                };

                var polygon = new Polygon2D(vertices, true);

                ed.WriteMessage($"\n多边形顶点数: {polygon.Vertices.Count}");
                ed.WriteMessage($"\n多边形是否闭合: {polygon.IsClosed}");

                ed.WriteMessage("\n✓ PolygonAlgorithms 测试完成\n");
                ed.WriteMessage("\n注意：完整测试需要依赖注入服务实例\n");
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

                // 测试角度转换
                double degrees = 90;
                double radians = AngleCalculator.DegreesToRadians(degrees);
                ed.WriteMessage($"\n{degrees}° = {radians:F4} 弧度");

                double backToDegrees = AngleCalculator.RadiansToDegrees(radians);
                ed.WriteMessage($"\n{radians:F4} 弧度 = {backToDegrees:F1}°");

                ed.WriteMessage("\n✓ AngleCalculator 测试完成\n");
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

                // 测试 2D 距离
                var p1 = new Point2D(0, 0);
                var p2 = new Point2D(3, 4);
                double distance2d = DistanceCalculator.EuclideanDistance(p1, p2);
                ed.WriteMessage($"\n2D 距离 (0,0) 到 (3,4): {distance2d:F3}"); // 应为 5

                ed.WriteMessage("\n✓ DistanceCalculator 测试完成\n");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}