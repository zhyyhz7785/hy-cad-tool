using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Selection;
using HyCADTool.Refactored.Domain.Services.GeometryAlgorithms;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using System;
using System.Linq;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// Phase 2 综合测试命令
    /// Phase 2 Comprehensive Test Command
    /// </summary>
    public class TestPhase2Command
    {
        private readonly Document _doc;
        private readonly Editor _ed;

        public TestPhase2Command()
        {
            _doc = Application.DocumentManager.MdiActiveDocument;
            _ed = _doc.Editor;
        }

        /// <summary>
        /// 测试几何算法服务
        /// Test geometry algorithm services
        /// </summary>
        [CommandMethod("TestPhase2Geometry", CommandFlags.Session)]
        public void TestGeometryAlgorithms()
        {
            try
            {
                _ed.WriteMessage("\n========== Phase 2 几何算法服务测试 ==========\n");

                // 从容器获取服务
                var lineAlgorithms = ServiceLocator.Resolve<ILineAlgorithmService>();
                var polygonAlgorithms = ServiceLocator.Resolve<IPolygonAlgorithmService>();
                var pointAlgorithms = ServiceLocator.Resolve<IPointAlgorithmService>();
                var geometryConverter = ServiceLocator.Resolve<IGeometryConverterService>();

                // 测试 Line 算法
                _ed.WriteMessage("\n--- 测试 Line 算法 ---\n");
                var line1 = new Line2D(new Point2D(0, 0), new Point2D(10, 10));
                var line2 = new Line2D(new Point2D(0, 10), new Point2D(10, 0));

                var length = lineAlgorithms.CalculateLength(line1);
                _ed.WriteMessage($"Line1 长度: {length:F2}\n");

                var angle1 = lineAlgorithms.CalculateAngle(line1);
                var angle2 = lineAlgorithms.CalculateAngle(line2);
                var angleDiff = Math.Abs(angle1 - angle2) * 180.0 / Math.PI;
                _ed.WriteMessage($"Line1 角度: {angle1 * 180.0 / Math.PI:F2} 度\n");
                _ed.WriteMessage($"Line2 角度: {angle2 * 180.0 / Math.PI:F2} 度\n");
                _ed.WriteMessage($"Line1 与 Line2 夹角: {angleDiff:F2} 度\n");

                var isParallel = lineAlgorithms.AreParallel(line1, line2, new Tolerance(1e-6));
                _ed.WriteMessage($"Line1 与 Line2 是否平行: {isParallel}\n");

                // 测试 Polygon 算法
                _ed.WriteMessage("\n--- 测试 Polygon 算法 ---\n");
                var vertices = new System.Collections.Generic.List<Point2D>
                {
                    new Point2D(0, 0),
                    new Point2D(10, 0),
                    new Point2D(10, 10),
                    new Point2D(0, 10)
                };
                var polygon = new Polygon2D(vertices);

                var area = polygonAlgorithms.CalculateArea(polygon);
                _ed.WriteMessage($"Polygon 面积: {area:F2}\n");

                var centroid = polygonAlgorithms.CalculateCentroid(polygon);
                _ed.WriteMessage($"Polygon 质心: ({centroid.X:F2}, {centroid.Y:F2})\n");

                var isClockwise = polygonAlgorithms.IsClockwise(polygon);
                _ed.WriteMessage($"Polygon 是否顺时针: {isClockwise}\n");

                // 测试 Point 算法
                _ed.WriteMessage("\n--- 测试 Point 算法 ---\n");
                var point1 = new Point2D(0, 0);
                var point2 = new Point2D(3, 4);

                var distance = pointAlgorithms.CalculateDistance(point1, point2);
                _ed.WriteMessage($"Point1 与 Point2 距离: {distance:F2}\n");

                var midpoint = pointAlgorithms.CalculateMidpoint(point1, point2);
                if (midpoint.HasValue)
                {
                    _ed.WriteMessage($"Point1 与 Point2 中点: ({midpoint.Value.X:F2}, {midpoint.Value.Y:F2})\n");
                }
                else
                {
                    _ed.WriteMessage($"Point1 与 Point2 中点: 无法计算\n");
                }

                _ed.WriteMessage("\n========== 几何算法服务测试完成 ==========\n");
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n错误: {ex.Message}\n{ex.StackTrace}\n");
            }
        }

        /// <summary>
        /// 测试选择服务
        /// Test selection services
        /// </summary>
        [CommandMethod("TestPhase2Selection", CommandFlags.Session)]
        public void TestSelectionServices()
        {
            try
            {
                _ed.WriteMessage("\n========== Phase 2 选择服务测试 ==========\n");

                // 从容器获取服务
                var advancedSelection = ServiceLocator.Resolve<IAdvancedSelectionService>();
                var filterManager = ServiceLocator.Resolve<IFilterManagerService>();

                // 测试高级选择服务
                _ed.WriteMessage("\n--- 测试高级选择服务 ---\n");

                // 获取所有线
                var lines = advancedSelection.SelectWithoutUserAction(
                    null,
                    new[] { new TypedValue((int)DxfCode.Start, "LINE") }
                );

                if (lines != null && lines.Length > 0)
                {
                    _ed.WriteMessage($"找到 {lines.Length} 条线\n");
                }
                else
                {
                    _ed.WriteMessage("未找到线\n");
                }

                // 获取所有多段线
                var polylines = advancedSelection.SelectByType(CadEntityType.Polyline);

                if (polylines != null && polylines.Length > 0)
                {
                    _ed.WriteMessage($"找到 {polylines.Length} 个多段线\n");
                }
                else
                {
                    _ed.WriteMessage("未找到多段线\n");
                }

                // 测试过滤器管理器
                _ed.WriteMessage("\n--- 测试过滤器管理器 ---\n");

                var filterKeys = filterManager.GetRegisteredFilterKeys();
                _ed.WriteMessage($"已注册的过滤器: {string.Join(", ", filterKeys)}\n");

                _ed.WriteMessage("\n========== 选择服务测试完成 ==========\n");
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n错误: {ex.Message}\n{ex.StackTrace}\n");
            }
        }

        /// <summary>
        /// 测试扩展方法
        /// Test extension methods
        /// </summary>
        [CommandMethod("TestPhase2Extensions", CommandFlags.Session)]
        public void TestExtensionMethods()
        {
            try
            {
                _ed.WriteMessage("\n========== Phase 2 扩展方法测试 ==========\n");

                // 测试几何扩展方法
                _ed.WriteMessage("\n--- 测试几何扩展方法 ---\n");

                // 创建一个AutoCAD Line
                using (var line = new Line(new Autodesk.AutoCAD.Geometry.Point3d(0, 0, 0),
                                           new Autodesk.AutoCAD.Geometry.Point3d(10, 10, 0)))
                {
                    // 转换为Domain Line2D
                    var domainLine = line.ToDomainLine2D();
                    _ed.WriteMessage($"AutoCAD Line 转换为 Domain Line2D: 起点({domainLine.StartPoint.X}, {domainLine.StartPoint.Y}), 终点({domainLine.EndPoint.X}, {domainLine.EndPoint.Y})\n");

                    // 获取中点
                    var midpoint = line.GetMidpoint2D();
                    _ed.WriteMessage($"Line 中点: ({midpoint.X}, {midpoint.Y})\n");

                    // 获取方向向量
                    var direction = line.GetDirection2D();
                    _ed.WriteMessage($"Line 方向向量: ({direction.X:F2}, {direction.Y:F2})\n");
                }

                // 创建一个AutoCAD Polyline
                using (var polyline = new Polyline())
                {
                    polyline.AddVertexAt(0, new Autodesk.AutoCAD.Geometry.Point2d(0, 0), 0, 0, 0);
                    polyline.AddVertexAt(1, new Autodesk.AutoCAD.Geometry.Point2d(10, 0), 0, 0, 0);
                    polyline.AddVertexAt(2, new Autodesk.AutoCAD.Geometry.Point2d(10, 10), 0, 0, 0);
                    polyline.AddVertexAt(3, new Autodesk.AutoCAD.Geometry.Point2d(0, 10), 0, 0, 0);
                    polyline.Closed = true;

                    // 转换为Domain Polygon2D
                    var domainPolygon = polyline.ToDomainPolygon2D();
                    _ed.WriteMessage($"AutoCAD Polyline 转换为 Domain Polygon2D: 顶点数 = {domainPolygon.Vertices.Count}\n");

                    // 获取所有顶点
                    var vertices = polyline.GetVertices2D().ToList();
                    _ed.WriteMessage($"Polyline 顶点数: {vertices.Count}\n");

                    // 获取所有线段
                    var segments = polyline.GetSegments2D().ToList();
                    _ed.WriteMessage($"Polyline 线段数: {segments.Count}\n");

                    // 获取边界框
                    var bbox = polyline.GetBoundingBox2D();
                    _ed.WriteMessage($"Polyline 边界框: Min({bbox.MinPoint.X}, {bbox.MinPoint.Y}), Max({bbox.MaxPoint.X}, {bbox.MaxPoint.Y})\n");
                }

                // 测试选择扩展方法
                _ed.WriteMessage("\n--- 测试选择扩展方法 ---\n");

                var advancedSelection = ServiceLocator.Resolve<IAdvancedSelectionService>();
                var allIds = advancedSelection.SelectWithoutUserAction();

                if (allIds != null && allIds.Length > 0)
                {
                    _ed.WriteMessage($"总实体数: {allIds.Length}\n");

                    // 按类型过滤
                    var lineIds = allIds.FilterByType<Line>();
                    _ed.WriteMessage($"Line 实体数: {lineIds.Length}\n");

                    var polylineIds = allIds.FilterByType<Polyline>();
                    _ed.WriteMessage($"Polyline 实体数: {polylineIds.Length}\n");

                    // 检查是否包含特定类型
                    var hasCircles = allIds.ContainsType<Circle>();
                    _ed.WriteMessage($"是否包含 Circle: {hasCircles}\n");
                }
                else
                {
                    _ed.WriteMessage("模型空间中没有实体\n");
                }

                _ed.WriteMessage("\n========== 扩展方法测试完成 ==========\n");
            }
            catch (System.Exception ex)
            {
                _ed.WriteMessage($"\n错误: {ex.Message}\n{ex.StackTrace}\n");
            }
        }

        /// <summary>
        /// 运行所有Phase 2测试
        /// Run all Phase 2 tests
        /// </summary>
        [CommandMethod("TestPhase2All", CommandFlags.Session)]
        public void TestAll()
        {
            _ed.WriteMessage("\n\n");
            _ed.WriteMessage("╔════════════════════════════════════════════════════════════╗\n");
            _ed.WriteMessage("║     ZTools Phase 2 重构 - 综合测试套件                      ║\n");
            _ed.WriteMessage("║     Geometry & Selection Services Comprehensive Tests     ║\n");
            _ed.WriteMessage("╚════════════════════════════════════════════════════════════╝\n");
            _ed.WriteMessage("\n");

            TestGeometryAlgorithms();
            _ed.WriteMessage("\n");
            TestSelectionServices();
            _ed.WriteMessage("\n");
            TestExtensionMethods();

            _ed.WriteMessage("\n");
            _ed.WriteMessage("╔════════════════════════════════════════════════════════════╗\n");
            _ed.WriteMessage("║             所有测试已完成 All Tests Completed             ║\n");
            _ed.WriteMessage("╚════════════════════════════════════════════════════════════╝\n");
            _ed.WriteMessage("\n");
        }
    }
}

