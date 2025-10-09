using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Services.GeometryAlgorithms;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Linq;
using Exception = Autodesk.AutoCAD.Runtime.Exception;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 阶段 2 测试命令集
    /// 用于验证底层几何组件迁移功能
    /// </summary>
    public static class Phase2TestCommand
    {
        #region 测试 1: 多边形偏移

        /// <summary>
        /// 测试多边形偏移（正偏移和负偏移）
        /// 命令: TestPolygonOffset
        /// </summary>
        public static void TestPolygonOffset()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 测试 1: 多边形偏移 ===");

            var geometryService = ServiceLocator.Resolve<IGeometryService>();

            try
            {
                // 创建正方形（边长 10）
                var square = new Polygon2D(new[]
                {
                    new Point2D(0, 0),
                    new Point2D(10, 0),
                    new Point2D(10, 10),
                    new Point2D(0, 10)
                });

                double originalArea = square.GetArea();
                ed.WriteMessage($"\n原多边形面积: {originalArea:F2}");

                // 测试正偏移（向外 5 单位）
                var offsetOutward = geometryService.Offset(square, 5.0).ToList();
                if (offsetOutward.Count > 0)
                {
                    double newArea = offsetOutward[0].GetArea();
                    ed.WriteMessage($"\n向外偏移 5 单位后面积: {newArea:F2}");
                    ed.WriteMessage($"\n预期面积: 400.00 (边长 20x20)");

                    bool passed = Math.Abs(newArea - 400.0) < 10.0;
                    ed.WriteMessage(passed ? "\n✅ 正偏移测试通过" : "\n❌ 正偏移测试失败");
                }

                // 测试负偏移（向内 2 单位）
                var offsetInward = geometryService.Offset(square, -2.0).ToList();
                if (offsetInward.Count > 0)
                {
                    double newArea = offsetInward[0].GetArea();
                    ed.WriteMessage($"\n向内偏移 2 单位后面积: {newArea:F2}");
                    ed.WriteMessage($"\n预期面积: 36.00 (边长 6x6)");

                    bool passed = Math.Abs(newArea - 36.0) < 5.0;
                    ed.WriteMessage(passed ? "\n✅ 负偏移测试通过" : "\n❌ 负偏移测试失败");
                }

                ed.WriteMessage("\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}\n");
            }
        }

        #endregion

        #region 测试 2: 线段重叠检测与合并

        /// <summary>
        /// 测试线段重叠检测与合并
        /// 命令: TestLineOverlap
        /// </summary>
        public static void TestLineOverlap()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 测试 2: 线段重叠检测与合并 ===");

            try
            {
                // 创建两条重叠的水平线段
                var line1 = new Line2D(new Point2D(0, 5), new Point2D(10, 5));
                var line2 = new Line2D(new Point2D(5, 5), new Point2D(15, 5));

                ed.WriteMessage($"\n线段1: {line1}");
                ed.WriteMessage($"\n线段2: {line2}");

                // 检测重叠
                var overlap = LineAlgorithms.CheckOverlap(line1, line2);
                if (overlap != default)
                {
                    ed.WriteMessage($"\n重叠部分: {overlap}");
                    double length = overlap.GetLength();
                    ed.WriteMessage($"\n重叠长度: {length:F2}");
                    ed.WriteMessage($"\n预期长度: 5.00");

                    bool passed = Math.Abs(length - 5.0) < 0.1;
                    ed.WriteMessage(passed ? "\n✅ 重叠检测测试通过" : "\n❌ 重叠检测测试失败");
                }
                else
                {
                    ed.WriteMessage("\n❌ 测试失败：未检测到重叠");
                }

                // 测试合并共线线段
                var lines = new[] { line1, line2 };
                var merged = LineAlgorithms.MergeCollinearLines(lines).ToList();

                ed.WriteMessage($"\n\n合并后线段数量: {merged.Count}");
                ed.WriteMessage($"\n预期数量: 1");

                if (merged.Count == 1)
                {
                    double mergedLength = merged[0].GetLength();
                    ed.WriteMessage($"\n合并后长度: {mergedLength:F2}");
                    ed.WriteMessage($"\n预期长度: 15.00");

                    bool passed = Math.Abs(mergedLength - 15.0) < 0.1;
                    ed.WriteMessage(passed ? "\n✅ 线段合并测试通过" : "\n❌ 线段合并测试失败");
                }
                else
                {
                    ed.WriteMessage("\n❌ 测试失败：合并后线段数量不正确");
                }

                ed.WriteMessage("\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}\n");
            }
        }

        #endregion

        #region 测试 3: 多边形交集

        /// <summary>
        /// 测试多边形交集运算
        /// 命令: TestPolygonIntersection
        /// </summary>
        public static void TestPolygonIntersection()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 测试 3: 多边形交集 ===");

            var geometryService = ServiceLocator.Resolve<IGeometryService>();

            try
            {
                // 创建两个矩形
                var rect1 = new Polygon2D(new[]
                {
                    new Point2D(0, 0),
                    new Point2D(10, 0),
                    new Point2D(10, 10),
                    new Point2D(0, 10)
                });

                var rect2 = new Polygon2D(new[]
                {
                    new Point2D(5, 5),
                    new Point2D(15, 5),
                    new Point2D(15, 15),
                    new Point2D(5, 15)
                });

                ed.WriteMessage($"\n矩形1面积: {rect1.GetArea():F2}");
                ed.WriteMessage($"\n矩形2面积: {rect2.GetArea():F2}");

                // 计算交集
                var intersection = geometryService.Intersection(rect1, rect2).ToList();

                ed.WriteMessage($"\n交集数量: {intersection.Count}");
                ed.WriteMessage($"\n预期: 1 个多边形");

                if (intersection.Count == 1)
                {
                    double area = intersection[0].GetArea();
                    ed.WriteMessage($"\n交集面积: {area:F2}");
                    ed.WriteMessage($"\n预期面积: 25.00 (5x5)");

                    bool passed = Math.Abs(area - 25.0) < 1.0;
                    ed.WriteMessage(passed ? "\n✅ 交集测试通过" : $"\n❌ 交集测试失败（误差: {Math.Abs(area - 25.0):F2}）");
                }
                else
                {
                    ed.WriteMessage("\n❌ 测试失败：交集数量不正确");
                }

                ed.WriteMessage("\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}\n");
            }
        }

        #endregion

        #region 测试 4: 凸包算法

        /// <summary>
        /// 测试凸包算法
        /// 命令: TestConvexHull
        /// </summary>
        public static void TestConvexHull()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 测试 4: 凸包算法 ===");

            var geometryService = ServiceLocator.Resolve<IGeometryService>();

            try
            {
                // 创建一组点（包括内部点）
                var points = new[]
                {
                    new Point2D(0, 0),
                    new Point2D(10, 0),
                    new Point2D(10, 10),
                    new Point2D(0, 10),
                    new Point2D(5, 5),  // 内部点
                    new Point2D(3, 3),  // 内部点
                    new Point2D(7, 7)   // 内部点
                };

                ed.WriteMessage($"\n输入点数: {points.Length}");

                // 计算凸包
                var convexHull = geometryService.ComputeConvexHull(points);

                ed.WriteMessage($"\n凸包顶点数: {convexHull.VertexCount}");
                ed.WriteMessage($"\n预期顶点数: 4 (正方形)");

                bool passed = convexHull.VertexCount == 4;
                ed.WriteMessage(passed ? "\n✅ 凸包顶点数测试通过" : "\n❌ 凸包顶点数测试失败");

                // 验证是否为凸多边形
                bool isConvex = ConvexHullAlgorithm.IsConvex(convexHull);
                ed.WriteMessage($"\n是否为凸多边形: {isConvex}");
                ed.WriteMessage(isConvex ? "\n✅ 凸性测试通过" : "\n❌ 凸性测试失败");

                // 验证面积
                double area = convexHull.GetArea();
                ed.WriteMessage($"\n凸包面积: {area:F2}");
                ed.WriteMessage($"\n预期面积: 100.00");

                passed = Math.Abs(area - 100.0) < 1.0;
                ed.WriteMessage(passed ? "\n✅ 凸包面积测试通过" : "\n❌ 凸包面积测试失败");

                ed.WriteMessage("\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}\n");
            }
        }

        #endregion

        #region 测试 5: 线段连通性排序

        /// <summary>
        /// 测试线段按连通性排序
        /// 命令: TestLineSorting
        /// </summary>
        public static void TestLineSorting()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 测试 5: 线段连通性排序 ===");

            try
            {
                // 创建一组无序的连接线段
                var lines = new[]
                {
                    new Line2D(new Point2D(10, 0), new Point2D(10, 10)),  // 第二段
                    new Line2D(new Point2D(10, 10), new Point2D(0, 10)),  // 第三段
                    new Line2D(new Point2D(0, 0), new Point2D(10, 0)),    // 第一段
                    new Line2D(new Point2D(0, 10), new Point2D(0, 0))     // 第四段（闭合）
                };

                ed.WriteMessage($"\n输入线段数: {lines.Length}");
                foreach (var line in lines)
                {
                    ed.WriteMessage($"\n  {line}");
                }

                // 排序
                var sorted = LineAlgorithms.SortLinesByConnectivity(lines);

                ed.WriteMessage($"\n\n排序后线段数: {sorted.Count}");

                // 验证连通性
                bool isConnected = true;
                for (int i = 0; i < sorted.Count - 1; i++)
                {
                    double dist = sorted[i].EndPoint.DistanceTo(sorted[i + 1].StartPoint);
                    if (dist > 1e-6)
                    {
                        isConnected = false;
                        ed.WriteMessage($"\n❌ 线段 {i} 和 {i + 1} 未连接（距离: {dist:F6}）");
                    }
                }

                if (isConnected)
                {
                    ed.WriteMessage("\n✅ 所有线段正确连接");
                }

                // 尝试构建多边形
                var polygon = LineAlgorithms.JoinLinesToPolygon(sorted);
                if (polygon != default)
                {
                    ed.WriteMessage($"\n构建的多边形顶点数: {polygon.VertexCount}");
                    ed.WriteMessage($"\n多边形面积: {polygon.GetArea():F2}");
                    ed.WriteMessage($"\n预期面积: 100.00");

                    bool passed = Math.Abs(polygon.GetArea() - 100.0) < 1.0;
                    ed.WriteMessage(passed ? "\n✅ 多边形构建测试通过" : "\n❌ 多边形构建测试失败");
                }
                else
                {
                    ed.WriteMessage("\n❌ 测试失败：无法构建多边形");
                }

                ed.WriteMessage("\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}\n");
            }
        }

        #endregion

        #region 综合测试

        /// <summary>
        /// 运行所有阶段 2 测试
        /// 命令: RunAllPhase2Tests
        /// </summary>
        public static void RunAllPhase2Tests()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;

            ed.WriteMessage("\n");
            ed.WriteMessage("\n========================================");
            ed.WriteMessage("\n  HyCADTool.Refactored 阶段 2 综合测试");
            ed.WriteMessage("\n========================================\n");

            TestPolygonOffset();
            TestLineOverlap();
            TestPolygonIntersection();
            TestConvexHull();
            TestLineSorting();

            ed.WriteMessage("\n========================================");
            ed.WriteMessage("\n  阶段 2 综合测试结束");
            ed.WriteMessage("\n========================================\n");
        }

        #endregion
    }
}

