using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.Modules;
using HyCADTool.Refactored.Domain.Services.GeometryAlgorithms;
using HyCADTool.Refactored.Domain.Services.MathAlgorithms;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using SysException = System.Exception;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 通用测试运行器
    /// 负责管理和执行所有重构阶段的测试
    /// </summary>
    public class TestRunner
    {
        private readonly Editor _editor;

        public TestRunner()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            _editor = doc?.Editor;
        }

        /// <summary>
        /// 运行所有测试
        /// 此方法会根据当前重构进度自动执行相应的测试
        /// </summary>
        public void RunAllTests()
        {
            if (_editor == null)
            {
                return;
            }

            _editor.WriteMessage("\n");
            _editor.WriteMessage("\n╔════════════════════════════════════════════════════╗");
            _editor.WriteMessage("\n║      HyCADTool.Refactored - 完整测试套件          ║");
            _editor.WriteMessage("\n╚════════════════════════════════════════════════════╝");
            _editor.WriteMessage("\n");

            try
            {
                int totalTests = 0;
                int passedTests = 0;

                // ===== 阶段 1: 配置层测试 =====
                _editor.WriteMessage("\n【阶段 1】配置层测试");
                _editor.WriteMessage("\n" + new string('═', 50));
                
                if (RunTest("全局配置", TestGlobalConfiguration))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("模块配置", TestModuleConfiguration))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("配置更新", TestConfigurationUpdate))
                {
                    passedTests++;
                }
                totalTests++;

                // ===== 阶段 2: 服务层测试 =====
                _editor.WriteMessage("\n\n【阶段 2】服务层测试");
                _editor.WriteMessage("\n" + new string('═', 50));
                
                if (RunTest("绘制服务", TestDrawingService))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("编辑器服务", TestEditorService))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("数据库服务", TestDatabaseService))
                {
                    passedTests++;
                }
                totalTests++;

                // ===== 阶段 3: Domain 算法层测试 =====
                _editor.WriteMessage("\n\n【阶段 3】Domain 算法层测试");
                _editor.WriteMessage("\n" + new string('═', 50));
                
                if (RunTest("点算法", TestPointAlgorithms))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("线算法", TestLineAlgorithms))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("多边形算法", TestPolygonAlgorithms))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("角度计算", TestAngleCalculator))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("距离计算", TestDistanceCalculator))
                {
                    passedTests++;
                }
                totalTests++;

                // ===== 测试结果汇总 =====
                _editor.WriteMessage("\n");
                _editor.WriteMessage("\n" + new string('═', 50));
                _editor.WriteMessage($"\n测试完成: {passedTests}/{totalTests} 通过");
                
                if (passedTests == totalTests)
                {
                    _editor.WriteMessage("\n✓ 所有测试通过！");
                    _editor.WriteMessage("\n╔════════════════════════════════════════════════════╗");
                    _editor.WriteMessage("\n║              测试执行成功！                        ║");
                    _editor.WriteMessage("\n╚════════════════════════════════════════════════════╝");
                }
                else
                {
                    _editor.WriteMessage($"\n✗ {totalTests - passedTests} 个测试失败");
                    _editor.WriteMessage("\n请检查失败的测试项");
                }
                _editor.WriteMessage("\n");
            }
            catch (System.Exception ex)
            {
                _editor.WriteMessage($"\n✗ 测试执行失败: {ex.Message}");
                _editor.WriteMessage($"\n堆栈跟踪: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 运行单个测试并捕获异常
        /// </summary>
        private bool RunTest(string testName, Action testAction)
        {
            try
            {
                _editor.WriteMessage($"\n▶ {testName}...");
                testAction();
                _editor.WriteMessage($" ✓");
                return true;
            }
            catch (System.Exception ex)
            {
                _editor.WriteMessage($" ✗");
                _editor.WriteMessage($"\n  错误: {ex.Message}");
                return false;
            }
        }

        #region 阶段 1: 配置层测试方法

        /// <summary>
        /// 测试全局配置
        /// </summary>
        private void TestGlobalConfiguration()
        {
            var configService = ServiceLocator.Resolve<IConfigurationService>();
            
            // 验证比例配置
            if (configService.Global.Scale == null || configService.Global.Scale.Default <= 0)
            {
                throw new System.Exception("全局配置加载失败：比例配置无效");
            }

            // 验证样式配置
            if (configService.Global.Styles == null || configService.Global.Styles.TextStyle == null)
            {
                throw new System.Exception("全局配置加载失败：样式配置无效");
            }

            // 测试通过
        }

        /// <summary>
        /// 测试模块配置
        /// </summary>
        private void TestModuleConfiguration()
        {
            var configService = ServiceLocator.Resolve<IConfigurationService>();

            // 验证桩基配置
            var pileConfig = configService.GetModuleConfig<PileConfiguration>("Pile");
            if (pileConfig == null || pileConfig.DiameterOrEdge <= 0)
            {
                throw new System.Exception("模块配置加载失败：桩基配置无效");
            }

            // 验证基础配置
            var foundationConfig = configService.GetModuleConfig<FoundationConfiguration>("Foundation");
            if (foundationConfig == null || foundationConfig.DefaultThickness <= 0)
            {
                throw new System.Exception("模块配置加载失败：基础配置无效");
            }

            // 测试通过
        }

        /// <summary>
        /// 测试配置更新
        /// </summary>
        private void TestConfigurationUpdate()
        {
            var configService = ServiceLocator.Resolve<IConfigurationService>();
            
            // 保存原始值
            double originalScale = configService.Global.Scale.Default;

            // 更新配置
            double newScale = originalScale + 10;
            configService.Global.Scale.Default = newScale;

            // 验证更新
            if (System.Math.Abs(configService.Global.Scale.Default - newScale) > 0.001)
            {
                throw new SysException("配置更新失败：比例值未正确更新");
            }

            // 恢复原始值
            configService.Global.Scale.Default = originalScale;

            // 测试通过
        }

        #endregion

        #region 阶段 2: 服务层测试方法

        /// <summary>
        /// 测试绘制服务 (Test Drawing Service)
        /// </summary>
        private void TestDrawingService()
        {
            var drawingService = ServiceLocator.Resolve<IDrawingService>();
            
            if (drawingService == null)
            {
                throw new System.Exception("绘制服务未注册 (Drawing service not registered)");
            }

            // 测试绘制线段
            var lineId = drawingService.DrawLine(
                new Point3d(0, 0, 0),
                new Point3d(100, 0, 0)
            );

            if (lineId == ObjectId.Null)
            {
                throw new System.Exception("线段绘制失败 (Line drawing failed)");
            }

            // 测试绘制圆
            var circleId = drawingService.DrawCircle(
                new Point3d(50, 50, 0),
                20
            );

            if (circleId == ObjectId.Null)
            {
                throw new System.Exception("圆绘制失败 (Circle drawing failed)");
            }

            // 测试通过
        }

        /// <summary>
        /// 测试编辑器服务 (Test Editor Service)
        /// </summary>
        private void TestEditorService()
        {
            var editorService = ServiceLocator.Resolve<IEditorService>();
            
            if (editorService == null)
            {
                throw new System.Exception("编辑器服务未注册 (Editor service not registered)");
            }

            // 测试消息输出（不需要用户交互）
            editorService.WriteMessage("  (测试消息输出 - Test message output)");
            editorService.WriteWarning("  (测试警告输出 - Test warning output)");

            // 测试通过 - 其他方法需要用户交互，在交互式测试中验证
        }

        /// <summary>
        /// 测试数据库服务 (Test Database Service)
        /// </summary>
        private void TestDatabaseService()
        {
            var databaseService = ServiceLocator.Resolve<IDatabaseService>();
            
            if (databaseService == null)
            {
                throw new System.Exception("数据库服务未注册 (Database service not registered)");
            }

            // 测试获取数据库
            var db = databaseService.GetCurrentDatabase();
            if (db == null)
            {
                throw new System.Exception("无法获取当前数据库 (Cannot get current database)");
            }

            // 测试获取所有实体
            var allEntities = databaseService.GetAllEntitiesInModelSpace();
            if (allEntities == null)
            {
                throw new System.Exception("无法获取 ModelSpace 实体 (Cannot get ModelSpace entities)");
            }

            // 测试获取所有图层
            var layerNames = databaseService.GetAllLayerNames();
            if (layerNames == null || layerNames.Count == 0)
            {
                throw new System.Exception("无法获取图层列表 (Cannot get layer list)");
            }

            // 测试通过
        }

        #endregion

        #region 阶段 3: ZTools 工具层测试方法（待添加）

        // 未来在这里添加 ZTools 相关的测试方法
        // private void TestGeometryUtils() { ... }
        // private void TestMathUtils() { ... }

        #endregion

        #region 阶段 3: Domain 算法层测试方法

        /// <summary>
        /// 测试点算法 (Test Point Algorithms)
        /// </summary>
        private void TestPointAlgorithms()
        {
            var p1 = new Point2D(0, 0);
            var p2 = new Point2D(10, 0);
            var p3 = new Point2D(5, 5);

            // 测试距离计算
            double distance = PointAlgorithms.Distance(p1, p2);
            if (System.Math.Abs(distance - 10) > 0.001)
            {
                throw new SysException($"距离计算错误：期望 10，实际 {distance}");
            }

            // 测试中点
            var midpoint = PointAlgorithms.Midpoint(p1, p2);
            if (System.Math.Abs(midpoint.X - 5) > 0.001 || System.Math.Abs(midpoint.Y - 0) > 0.001)
            {
                throw new SysException($"中点计算错误：期望 (5, 0)，实际 ({midpoint.X}, {midpoint.Y})");
            }

            // 测试投影
            var line = new Line2D(p1, p2);
            var projection = PointAlgorithms.ProjectToLine(p3, line);
            if (System.Math.Abs(projection.X - 5) > 0.001 || System.Math.Abs(projection.Y - 0) > 0.001)
            {
                throw new SysException($"投影计算错误：期望 (5, 0)，实际 ({projection.X}, {projection.Y})");
            }

            // 测试点到直线距离
            double distToLine = PointAlgorithms.DistanceToLine(p3, line);
            if (System.Math.Abs(distToLine - 5) > 0.001)
            {
                throw new SysException($"点到直线距离错误：期望 5，实际 {distToLine}");
            }
        }

        /// <summary>
        /// 测试线算法 (Test Line Algorithms)
        /// </summary>
        private void TestLineAlgorithms()
        {
            var line1 = new Line2D(new Point2D(0, 0), new Point2D(10, 0)); // 水平线
            var line2 = new Line2D(new Point2D(0, 0), new Point2D(0, 10)); // 垂直线
            var line3 = new Line2D(new Point2D(5, -5), new Point2D(5, 5)); // 另一条垂直线

            // 测试平行
            bool isParallel = LineAlgorithms.AreParallel(line2, line3);
            if (!isParallel)
            {
                throw new SysException("平行判断错误：两条垂直线应该平行");
            }

            // 测试垂直
            bool isPerpendicular = LineAlgorithms.ArePerpendicular(line1, line2);
            if (!isPerpendicular)
            {
                throw new SysException("垂直判断错误：水平线和垂直线应该垂直");
            }

            // 测试交点
            var intersection = LineAlgorithms.GetIntersection(line1, line3);
            if (intersection == null || System.Math.Abs(intersection.Value.X - 5) > 0.001 || System.Math.Abs(intersection.Value.Y - 0) > 0.001)
            {
                throw new SysException("交点计算错误：应该在 (5, 0)");
            }

            // 测试长度
            double length = LineAlgorithms.GetLength(line1);
            if (System.Math.Abs(length - 10) > 0.001)
            {
                throw new SysException($"长度计算错误：期望 10，实际 {length}");
            }

            // 测试水平/垂直判断
            bool isHorizontal = LineAlgorithms.IsHorizontal(line1);
            bool isVertical = LineAlgorithms.IsVertical(line2);
            if (!isHorizontal || !isVertical)
            {
                throw new SysException("水平/垂直判断错误");
            }
        }

        /// <summary>
        /// 测试多边形算法 (Test Polygon Algorithms)
        /// </summary>
        private void TestPolygonAlgorithms()
        {
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
            if (System.Math.Abs(area - 100) > 0.001)
            {
                throw new SysException($"面积计算错误：期望 100，实际 {area}");
            }

            // 测试周长
            double perimeter = PolygonAlgorithms.CalculatePerimeter(square);
            if (System.Math.Abs(perimeter - 40) > 0.001)
            {
                throw new SysException($"周长计算错误：期望 40，实际 {perimeter}");
            }

            // 测试点在多边形内
            var insidePoint = new Point2D(5, 5);
            var outsidePoint = new Point2D(15, 15);
            bool isInside1 = PolygonAlgorithms.ContainsPoint(square, insidePoint);
            bool isInside2 = PolygonAlgorithms.ContainsPoint(square, outsidePoint);
            if (!isInside1 || isInside2)
            {
                throw new SysException("点在多边形内判断错误");
            }

            // 测试凸多边形判断
            bool isConvex = PolygonAlgorithms.IsConvex(square);
            if (!isConvex)
            {
                throw new SysException("凸多边形判断错误：正方形应该是凸多边形");
            }

            // 测试质心
            var centroid = PolygonAlgorithms.CalculateCentroid(square);
            if (System.Math.Abs(centroid.X - 5) > 0.001 || System.Math.Abs(centroid.Y - 5) > 0.001)
            {
                throw new SysException($"质心计算错误：期望 (5, 5)，实际 ({centroid.X}, {centroid.Y})");
            }
        }

        /// <summary>
        /// 测试角度计算 (Test Angle Calculator)
        /// </summary>
        private void TestAngleCalculator()
        {
            // 测试弧度/角度转换
            double radians = System.Math.PI / 4; // 45 度
            double degrees = AngleCalculator.RadiansToDegrees(radians);
            if (System.Math.Abs(degrees - 45) > 0.001)
            {
                throw new SysException($"弧度转角度错误：期望 45，实际 {degrees}");
            }

            double radians2 = AngleCalculator.DegreesToRadians(90);
            if (System.Math.Abs(radians2 - System.Math.PI / 2) > 0.001)
            {
                throw new SysException($"角度转弧度错误：期望 π/2，实际 {radians2}");
            }

            // 测试向量夹角
            var v1 = new Vector2D(1, 0);
            var v2 = new Vector2D(0, 1);
            double angleBetween = AngleCalculator.AngleBetweenVectors(v1, v2);
            if (System.Math.Abs(angleBetween - System.Math.PI / 2) > 0.001)
            {
                throw new SysException($"向量夹角错误：期望 π/2，实际 {angleBetween}");
            }

            // 测试角度类型判断
            bool isRightAngle = AngleCalculator.IsRightAngle(System.Math.PI / 2);
            if (!isRightAngle)
            {
                throw new SysException("直角判断错误");
            }

            bool isAcute = AngleCalculator.IsAcuteAngle(System.Math.PI / 6); // 30°
            if (!isAcute)
            {
                throw new SysException("锐角判断错误");
            }
        }

        /// <summary>
        /// 测试距离计算 (Test Distance Calculator)
        /// </summary>
        private void TestDistanceCalculator()
        {
            var p1 = new Point2D(0, 0);
            var p2 = new Point2D(3, 4);

            // 测试欧几里得距离
            double euclidean = DistanceCalculator.EuclideanDistance(p1, p2);
            if (System.Math.Abs(euclidean - 5) > 0.001)
            {
                throw new SysException($"欧几里得距离错误：期望 5，实际 {euclidean}");
            }

            // 测试曼哈顿距离
            double manhattan = DistanceCalculator.ManhattanDistance(p1, p2);
            if (System.Math.Abs(manhattan - 7) > 0.001)
            {
                throw new SysException($"曼哈顿距离错误：期望 7，实际 {manhattan}");
            }

            // 测试切比雪夫距离
            double chebyshev = DistanceCalculator.ChebyshevDistance(p1, p2);
            if (System.Math.Abs(chebyshev - 4) > 0.001)
            {
                throw new SysException($"切比雪夫距离错误：期望 4，实际 {chebyshev}");
            }

            // 测试点到线段距离
            var lineStart = new Point2D(0, 0);
            var lineEnd = new Point2D(10, 0);
            var point = new Point2D(5, 3);
            double distToSegment = DistanceCalculator.PointToLineSegmentDistance(point, lineStart, lineEnd);
            if (System.Math.Abs(distToSegment - 3) > 0.001)
            {
                throw new SysException($"点到线段距离错误：期望 3，实际 {distToSegment}");
            }

            // 测试路径长度
            var path = new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(3, 0),
                new Point2D(3, 4),
                new Point2D(0, 4)
            };
            double pathLength = DistanceCalculator.CalculatePathLength(path, closed: false);
            if (System.Math.Abs(pathLength - 10) > 0.001)
            {
                throw new SysException($"路径长度错误：期望 10，实际 {pathLength}");
            }
        }

        #endregion
    }
}

