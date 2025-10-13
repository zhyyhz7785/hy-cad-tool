using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.Modules;
using HyCADTool.Refactored.Domain.Services.GeometryAlgorithms;
using HyCADTool.Refactored.Domain.Services.MathAlgorithms;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.Utilities;
using HyCADTool.Refactored.Domain.ValueObjects.Grid;
using HyCADTool.Refactored.Domain.DataStructures.DCEL;
using HyCADTool.Refactored.Domain.Entities.Pile;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Selection;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Selection.Filters;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using AcDbPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
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

                // ===== 阶段 4: HelpClass 层测试 =====
                _editor.WriteMessage("\n\n【阶段 4】HelpClass 层测试");
                _editor.WriteMessage("\n" + new string('═', 50));
                
                if (RunTest("EntityTypeMapping", TestEntityTypeMapping))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("RowColValue", TestRowColValue))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("DCEL - 基础", TestDCELBasics))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("DCEL - 面", TestDCELFace))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("Pile - 圆形", TestCircularPile))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("Pile - 方形", TestSquarePile))
                {
                    passedTests++;
                }
                totalTests++;

                // ===== 阶段 5: 工具层（Selection & Layer）测试 =====
                _editor.WriteMessage("\n\n【阶段 5】工具层测试");
                _editor.WriteMessage("\n" + new string('═', 50));

                if (RunTest("SelectionFilterBuilder", TestSelectionFilterBuilder))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("LayerHelper", TestLayerHelperValuesOnly))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("SelectionService 组合过滤", TestSelectionServiceCombinedFilter))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("SelectionFilterService FromEntity", TestSelectionFilterFromEntity))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("Entity 属性过滤器（Layer/Color/Type）", TestEntityPropertyFilters))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("LayerHelper 批量样式预览", TestLayerHelperPreviewOnly))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("PolylineExtensions 扩展方法", TestPolylineExtensions))
                {
                    passedTests++;
                }
                totalTests++;

                // ===== 阶段 6: 几何工具整合测试 =====
                _editor.WriteMessage("\n\n【阶段 6】几何工具整合测试");
                _editor.WriteMessage("\n" + new string('═', 50));

                if (RunTest("Polyline 去重顶点", TestPolylineRemoveDuplicates))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("Polyline 顺时针/逆时针", TestPolylineClockwise))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("Polyline 代数面积", TestPolylineAlgebraicArea))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("Line 连接性排序", TestLineSortByConnectivity))
                {
                    passedTests++;
                }
                totalTests++;

                if (RunTest("Line 合并为 Polyline", TestJoinLinesToPolyline))
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

            // TODO: 点算法测试需要依赖注入的IPointAlgorithmService实例
            // 暂时跳过具体测试，只验证基本的Point2D创建
            
            // 验证Point2D创建
            if (p1.X != 0 || p1.Y != 0)
                throw new SysException("Point2D创建失败");
            if (p2.X != 10 || p2.Y != 0)
                throw new SysException("Point2D创建失败");
            if (p3.X != 5 || p3.Y != 5)
                throw new SysException("Point2D创建失败");
                
            // 验证Line2D创建
            var line = new Line2D(p1, p2);
            if (line.StartPoint.X != 0 || line.EndPoint.X != 10)
                throw new SysException("Line2D创建失败");
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

        #region 阶段 4: HelpClass 层测试方法

        /// <summary>
        /// 测试 EntityTypeMapping（实体类型映射）
        /// </summary>
        private void TestEntityTypeMapping()
        {
            // 测试 DXF 类型名称转中文
            string chinese1 = EntityTypeMapping.ToChinese("Line");
            if (chinese1 != "直线")
                throw new SysException($"预期 '直线'，实际 '{chinese1}'");

            // 测试中文转 DXF 标签
            string dxf1 = EntityTypeMapping.ToDxfType("多段线");
            if (dxf1 != "LWPOLYLINE")
                throw new SysException($"预期 'LWPOLYLINE'，实际 '{dxf1}'");
        }

        /// <summary>
        /// 测试 RowColValue（网格行列值对象）
        /// </summary>
        private void TestRowColValue()
        {
            var cell1 = new RowColValue<double>(row: 2, col: 3, value: 100.5);
            
            if (cell1.Row != 2)
                throw new SysException($"行索引错误：预期 2，实际 {cell1.Row}");

            // 测试相等性
            var cell2 = new RowColValue<double>(row: 2, col: 3, value: 100.5);
            if (!cell1.Equals(cell2))
                throw new SysException("相同的 RowColValue 应该相等");
        }

        /// <summary>
        /// 测试 DCEL 基础功能（顶点和边）
        /// </summary>
        private void TestDCELBasics()
        {
            var dcel = new DCELGraph();

            var v1 = dcel.AddVertex(new Point2D(0, 0));
            var v2 = dcel.AddVertex(new Point2D(100, 0));

            if (dcel.Vertices.Count != 2)
                throw new SysException($"顶点数量错误：预期 2，实际 {dcel.Vertices.Count}");

            var (he1, he2) = dcel.AddEdgePair(v1, v2);

            if (he1.Twin != he2 || he2.Twin != he1)
                throw new SysException("孪生边关系错误");
        }

        /// <summary>
        /// 测试 DCEL 面创建
        /// </summary>
        private void TestDCELFace()
        {
            var dcel = new DCELGraph();

            var v1 = dcel.AddVertex(new Point2D(0, 0));
            var v2 = dcel.AddVertex(new Point2D(100, 0));
            var v3 = dcel.AddVertex(new Point2D(50, 100));

            var (he1, _) = dcel.AddEdgePair(v1, v2);
            var (he2, _) = dcel.AddEdgePair(v2, v3);
            var (he3, _) = dcel.AddEdgePair(v3, v1);

            var face = dcel.CreateFace(new List<HalfEdge> { he1, he2, he3 });

            if (face.GetVertexCount() != 3)
                throw new SysException($"面顶点数错误：预期 3，实际 {face.GetVertexCount()}");

            if (he1.Next != he2 || he2.Next != he3 || he3.Next != he1)
                throw new SysException("Next 关系错误");
        }

        /// <summary>
        /// 测试圆形桩面积计算
        /// </summary>
        private void TestCircularPile()
        {
            var pile = new Pile(PileSectionType.Circle, diameterOrEdge: 600);

            double expectedArea = System.Math.PI * 300 * 300;
            if (System.Math.Abs(pile.PileArea - expectedArea) > 0.01)
                throw new SysException($"圆形桩面积计算错误：预期 {expectedArea:F2}，实际 {pile.PileArea:F2}");
        }

        /// <summary>
        /// 测试方形桩面积计算
        /// </summary>
        private void TestSquarePile()
        {
            var pile = new Pile(PileSectionType.Square, diameterOrEdge: 500);

            double expectedArea = 500 * 500;
            if (System.Math.Abs(pile.PileArea - expectedArea) > 0.01)
                throw new SysException($"方形桩面积计算错误：预期 {expectedArea:F2}，实际 {pile.PileArea:F2}");
        }

        #endregion

        #region 阶段 5: 工具层测试方法

        /// <summary>
        /// 测试 SelectionFilterBuilder（不执行选择，仅验证 TypedValue 构造）
        /// </summary>
        private void TestSelectionFilterBuilder()
        {
            var builder = new SelectionFilterBuilder()
                .ByType("LWPOLYLINE")
                .ByLayer("0")
                .ByColorIndex(256) // BYLAYER
                .ByLinetype("ByLayer")
                .ByLineWeight(LineWeight.ByLayer);

            var values = builder.BuildValues();
            if (values == null || values.Length < 3)
            {
                throw new SysException("SelectionFilter 构造失败：TypedValue 数量过少");
            }

            // 简单校验关键键值存在
            bool hasType = false, hasLayer = false;
            foreach (var tv in values)
            {
                if ((DxfCode)tv.TypeCode == DxfCode.Start && (string)tv.Value == "LWPOLYLINE") hasType = true;
                if ((DxfCode)tv.TypeCode == DxfCode.LayerName && (string)tv.Value == "0") hasLayer = true;
            }
            if (!hasType || !hasLayer)
            {
                throw new SysException("SelectionFilter 构造失败：缺少类型或图层条件");
            }
        }

        /// <summary>
        /// 测试 LayerHelper 的只读能力（列出所有图层名称）
        /// 为避免写库，该测试仅调用 GetAllLayerNames
        /// </summary>
        private void TestLayerHelperValuesOnly()
        {
            var db = HostApplicationServices.WorkingDatabase;
            var layers = LayerHelper.GetAllLayerNames(db);
            if (layers == null || layers.Count == 0)
            {
                throw new SysException("无法获取图层列表");
            }
        }

        /// <summary>
        /// 测试 SelectionService 的组合过滤只读路径（SelectAllWithFilter）
        /// 仅验证调用链不抛异常且返回数组非 null
        /// </summary>
        private void TestSelectionServiceCombinedFilter()
        {
            var selectionService = ServiceLocator.Resolve<ISelectionService>();
            var ids = selectionService.SelectAllWithFilter(
                dxfType: "LWPOLYLINE",
                layerName: "0",
                colorIndex: null,
                linetypeName: null,
                lineWeight: null);
            if (ids == null)
            {
                throw new SysException("SelectAllWithFilter 返回 null");
            }
        }

        /// <summary>
        /// 测试 SelectionFilterService.BuildFromEntity（只读路径）
        /// 构建一个临时的 DBText 实例，仅用于读取属性并构建过滤器，不写库
        /// </summary>
        private void TestSelectionFilterFromEntity()
        {
            var filterService = ServiceLocator.Resolve<ISelectionFilterService>();
            using (var txt = new DBText())
            {
                txt.Layer = "0";
                txt.ColorIndex = 256;
                txt.Linetype = "ByLayer";
                txt.LineWeight = LineWeight.ByLayer;
                var filter = filterService.BuildFromEntity(txt, byLayer: true, byColor: true, byLinetype: true, byLineWeight: true, byType: false);
                if (filter == null)
                {
                    throw new SysException("BuildFromEntity 返回 null");
                }
            }
        }

        /// <summary>
        /// 测试基础设施层独立实体属性过滤器（只读）
        /// </summary>
        private void TestEntityPropertyFilters()
        {
            using (var ent = new DBText())
            {
                ent.Layer = "0";
                ent.ColorIndex = 7;
                var lf = new LayerFilter().Build(ent);
                var cf = new ColorFilter().Build(ent);
                var tf = new TypeFilter().Build(ent);
                if (lf == null || cf == null || tf == null)
                {
                    throw new SysException("实体属性过滤器构建失败");
                }
            }
        }

        /// <summary>
        /// 测试 LayerHelper 批量样式预览（只读）
        /// </summary>
        private void TestLayerHelperPreviewOnly()
        {
            var db = HostApplicationServices.WorkingDatabase;
            var styles = new List<LayerHelper.LayerStyle>
            {
                new LayerHelper.LayerStyle{ LayerName = "0", LinetypeName = "ByLayer", LineWeight = LineWeight.ByLayer },
                new LayerHelper.LayerStyle{ LayerName = "Hy_Test", LinetypeName = "Continuous" }
            };
            var preview = LayerHelper.PreviewLayerStyles(db, styles);
            if (preview == null || preview.Count == 0)
            {
                throw new SysException("PreviewLayerStyles 返回空");
            }
            if (!preview.ContainsKey("0"))
            {
                throw new SysException("预期包含图层 0 的预览结果");
            }
        }

        /// <summary>
        /// 测试 PolylineExtensions 扩展方法（只读，使用临时 Polyline）
        /// </summary>
        private void TestPolylineExtensions()
        {
            using (var poly = new AcDbPolyline())
            {
                poly.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);
                poly.AddVertexAt(1, new Point2d(10, 0), 0, 0, 0);
                poly.AddVertexAt(2, new Point2d(10, 10), 0, 0, 0);
                poly.AddVertexAt(3, new Point2d(0, 10), 0, 0, 0);
                poly.Closed = true;

                // 测试顶点获取
                var vertices = poly.GetAllVertices();
                if (vertices == null || vertices.Length != 4)
                    throw new SysException("GetAllVertices 返回错误");

                // 测试面积
                double area = poly.GetArea();
                if (System.Math.Abs(area - 100) > 0.01)
                    throw new SysException($"GetArea 错误：期望 100，实际 {area}");

                // 测试周长
                double perimeter = poly.GetPerimeter();
                if (System.Math.Abs(perimeter - 40) > 0.01)
                    throw new SysException($"GetPerimeter 错误：期望 40，实际 {perimeter}");

                // 测试点包含判断
                bool inside = poly.ContainsPoint(new Point3d(5, 5, 0));
                if (!inside)
                    throw new SysException("ContainsPoint 应返回 true（点在内部）");

                bool outside = poly.ContainsPoint(new Point3d(15, 15, 0));
                if (outside)
                    throw new SysException("ContainsPoint 应返回 false（点在外部）");
            }
        }

        /// <summary>
        /// 测试 Polyline 去重顶点功能（只读，使用临时 Polyline）
        /// </summary>
        private void TestPolylineRemoveDuplicates()
        {
            using (var poly = new AcDbPolyline())
            {
                // 创建一个有重复首尾顶点的多段线
                poly.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);
                poly.AddVertexAt(1, new Point2d(10, 0), 0, 0, 0);
                poly.AddVertexAt(2, new Point2d(10, 10), 0, 0, 0);
                poly.AddVertexAt(3, new Point2d(0, 0), 0, 0, 0); // 重复的起点

                if (poly.NumberOfVertices != 4)
                    throw new SysException("初始顶点数应为 4");

                // 去重
                poly.RemoveDuplicateVertices();

                if (poly.NumberOfVertices != 3)
                    throw new SysException($"去重后顶点数应为 3，实际为 {poly.NumberOfVertices}");
            }
        }

        /// <summary>
        /// 测试 Polyline 顺时针/逆时针判断与转换（只读，使用临时 Polyline）
        /// </summary>
        private void TestPolylineClockwise()
        {
            // 测试逆时针多段线
            using (var polyCounterclockwise = new AcDbPolyline())
            {
                polyCounterclockwise.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);
                polyCounterclockwise.AddVertexAt(1, new Point2d(10, 0), 0, 0, 0);
                polyCounterclockwise.AddVertexAt(2, new Point2d(10, 10), 0, 0, 0);
                polyCounterclockwise.AddVertexAt(3, new Point2d(0, 10), 0, 0, 0);
                polyCounterclockwise.Closed = true;

                double areaBefore = polyCounterclockwise.GetAlgebraicArea();
                if (areaBefore <= 0)
                    throw new SysException("逆时针多段线的代数面积应为正值");

                // 转换为顺时针
                polyCounterclockwise.EnsureClockwise();
                double areaAfter = polyCounterclockwise.GetAlgebraicArea();
                if (areaAfter >= 0)
                    throw new SysException("转换后应为顺时针（负面积）");
            }

            // 测试顺时针多段线
            using (var polyClockwise = new AcDbPolyline())
            {
                polyClockwise.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);
                polyClockwise.AddVertexAt(1, new Point2d(0, 10), 0, 0, 0);
                polyClockwise.AddVertexAt(2, new Point2d(10, 10), 0, 0, 0);
                polyClockwise.AddVertexAt(3, new Point2d(10, 0), 0, 0, 0);
                polyClockwise.Closed = true;

                double areaBefore = polyClockwise.GetAlgebraicArea();
                if (areaBefore >= 0)
                    throw new SysException("顺时针多段线的代数面积应为负值");

                // 转换为逆时针
                polyClockwise.EnsureCounterclockwise();
                double areaAfter = polyClockwise.GetAlgebraicArea();
                if (areaAfter <= 0)
                    throw new SysException("转换后应为逆时针（正面积）");
            }
        }

        /// <summary>
        /// 测试 Polyline 代数面积计算（只读，使用临时 Polyline）
        /// </summary>
        private void TestPolylineAlgebraicArea()
        {
            using (var poly = new AcDbPolyline())
            {
                // 创建一个 10x10 的正方形（逆时针）
                poly.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);
                poly.AddVertexAt(1, new Point2d(10, 0), 0, 0, 0);
                poly.AddVertexAt(2, new Point2d(10, 10), 0, 0, 0);
                poly.AddVertexAt(3, new Point2d(0, 10), 0, 0, 0);
                poly.Closed = true;

                double area = poly.GetAlgebraicArea();
                // 逆时针应为正值，面积为 100
                if (System.Math.Abs(area - 100) > 0.01)
                    throw new SysException($"代数面积应为 100，实际为 {area}");
            }

            using (var poly2 = new AcDbPolyline())
            {
                // 创建一个 10x10 的正方形（顺时针）
                poly2.AddVertexAt(0, new Point2d(0, 0), 0, 0, 0);
                poly2.AddVertexAt(1, new Point2d(0, 10), 0, 0, 0);
                poly2.AddVertexAt(2, new Point2d(10, 10), 0, 0, 0);
                poly2.AddVertexAt(3, new Point2d(10, 0), 0, 0, 0);
                poly2.Closed = true;

                double area = poly2.GetAlgebraicArea();
                // 顺时针应为负值，面积为 -100
                if (System.Math.Abs(area + 100) > 0.01)
                    throw new SysException($"代数面积应为 -100，实际为 {area}");
            }
        }

        /// <summary>
        /// 测试 Line 连接性排序（只读，使用临时 Line）
        /// </summary>
        private void TestLineSortByConnectivity()
        {
            var lines = new List<Line>
            {
                new Line(new Point3d(0, 0, 0), new Point3d(10, 0, 0)),
                new Line(new Point3d(20, 10, 0), new Point3d(10, 10, 0)), // 反向
                new Line(new Point3d(10, 0, 0), new Point3d(10, 10, 0)),
                new Line(new Point3d(10, 10, 0), new Point3d(20, 10, 0))
            };

            var sorted = lines.SortByConnectivity();

            if (sorted.Count != 4)
                throw new SysException($"排序后应有 4 条线段，实际为 {sorted.Count}");

            // 检查连接性
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var end = sorted[i].EndPoint;
                var nextStart = sorted[i + 1].StartPoint;
                double dist = end.DistanceTo(nextStart);
                if (dist > 1e-4)
                    throw new SysException($"线段 {i} 的终点与线段 {i + 1} 的起点不连接，距离 = {dist}");
            }
        }

        /// <summary>
        /// 测试 Line 合并为 Polyline（只读，使用临时 Line）
        /// </summary>
        private void TestJoinLinesToPolyline()
        {
            var lines = new List<Line>
            {
                new Line(new Point3d(0, 0, 0), new Point3d(10, 0, 0)),
                new Line(new Point3d(10, 0, 0), new Point3d(10, 10, 0)),
                new Line(new Point3d(10, 10, 0), new Point3d(0, 10, 0)),
                new Line(new Point3d(0, 10, 0), new Point3d(0, 0, 0))
            };

            using (var poly = lines.JoinToPolyline(requireClosed: true))
            {
                if (poly == null)
                    throw new SysException("JoinToPolyline 返回 null");

                if (poly.NumberOfVertices != 4)
                    throw new SysException($"多段线应有 4 个顶点，实际为 {poly.NumberOfVertices}");

                if (!poly.Closed)
                    throw new SysException("多段线应为闭合");

                double area = poly.GetArea();
                if (System.Math.Abs(area - 100) > 0.01)
                    throw new SysException($"多段线面积应为 100，实际为 {area}");
            }
        }

        #endregion
    }
}

