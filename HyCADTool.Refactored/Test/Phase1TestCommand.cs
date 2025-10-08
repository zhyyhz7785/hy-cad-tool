using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Linq;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 阶段 1 测试命令集
    /// 类似 TestCommand.cs，用于动态加载测试
    /// </summary>
    public class Phase1TestCommand
    {
        #region 测试 1: 插件加载测试

        /// <summary>
        /// 测试插件加载状态和依赖注入容器
        /// </summary>
        public static void TestPluginLoad()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 测试 1: 插件加载状态 ===");
            
            try
            {
                var container = ServiceLocator.Container;
                ed.WriteMessage("\n✅ Autofac 容器已初始化");
                
                var geometryService = ServiceLocator.Resolve<IGeometryService>();
                ed.WriteMessage("\n✅ IGeometryService 已解析");
                
                var layerService = ServiceLocator.Resolve<ILayerService>();
                ed.WriteMessage("\n✅ ILayerService 已解析");
                
                var styleService = ServiceLocator.Resolve<IStyleService>();
                ed.WriteMessage("\n✅ IStyleService 已解析");
                
                var converter = ServiceLocator.Resolve<Infrastructure.AutoCAD.Converters.IGeometryConverter>();
                ed.WriteMessage("\n✅ IGeometryConverter 已解析");
                
                ed.WriteMessage("\n\n✅ 所有服务解析成功！插件加载正常。\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 插件加载失败: {ex.Message}");
                ed.WriteMessage($"\n堆栈跟踪: {ex.StackTrace}\n");
            }
        }

        #endregion

        #region 测试 2: 几何计算测试

        /// <summary>
        /// 测试多边形面积计算
        /// </summary>
        public static void TestPolygonArea()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 测试 2.1: 多边形面积计算 ===");
            
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
                
                double area = square.GetArea();
                
                ed.WriteMessage($"\n正方形面积: {area:F6}");
                ed.WriteMessage($"\n预期值: 100.000000");
                
                bool passed = Math.Abs(area - 100.0) < 1e-6;
                ed.WriteMessage(passed ? "\n✅ 测试通过\n" : "\n❌ 测试失败\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}\n");
            }
        }

        /// <summary>
        /// 测试多边形质心计算
        /// </summary>
        public static void TestPolygonCentroid()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 测试 2.2: 多边形质心计算 ===");
            
            try
            {
                var square = new Polygon2D(new[]
                {
                    new Point2D(0, 0),
                    new Point2D(10, 0),
                    new Point2D(10, 10),
                    new Point2D(0, 10)
                });
                
                var centroid = square.GetCentroid();
                
                ed.WriteMessage($"\n质心坐标: ({centroid.X:F6}, {centroid.Y:F6})");
                ed.WriteMessage($"\n预期值: (5.000000, 5.000000)");
                
                bool passed = Math.Abs(centroid.X - 5.0) < 1e-6 && 
                              Math.Abs(centroid.Y - 5.0) < 1e-6;
                ed.WriteMessage(passed ? "\n✅ 测试通过\n" : "\n❌ 测试失败\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}\n");
            }
        }

        /// <summary>
        /// 测试点在多边形内判断
        /// </summary>
        public static void TestPointInPolygon()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 测试 2.3: 点在多边形内判断 ===");
            
            try
            {
                var polygon = new Polygon2D(new[]
                {
                    new Point2D(0, 0),
                    new Point2D(10, 0),
                    new Point2D(10, 10),
                    new Point2D(0, 10)
                });
                
                var pointInside = new Point2D(5, 5);
                var pointOutside = new Point2D(15, 15);
                var pointOnEdge = new Point2D(0, 5);
                
                bool test1 = polygon.ContainsPoint(pointInside);
                bool test2 = !polygon.ContainsPoint(pointOutside);
                bool test3 = polygon.ContainsPoint(pointOnEdge); // 边界点
                
                ed.WriteMessage($"\n点 (5, 5) 在多边形内: {test1}");
                ed.WriteMessage($"\n点 (15, 15) 在多边形外: {!test2}");
                ed.WriteMessage($"\n点 (0, 5) 在边界上: {test3}");
                
                bool passed = test1 && test2;
                ed.WriteMessage(passed ? "\n✅ 测试通过\n" : "\n❌ 测试失败\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}\n");
            }
        }

        #endregion

        #region 测试 3: 图层和样式服务测试

        /// <summary>
        /// 测试图层和样式服务功能
        /// </summary>
        public static void TestLayerAndStyleServices()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 测试 3: 图层和样式服务 ===");
            
            var layerService = ServiceLocator.Resolve<ILayerService>();
            var styleService = ServiceLocator.Resolve<IStyleService>();
            
            try
            {
                // 测试 1: 创建图层
                string testLayerName = $"测试图层_{DateTime.Now.Ticks}";
                layerService.CreateLayer(testLayerName, 1); // 红色
                bool layerExists = layerService.LayerExists(testLayerName);
                ed.WriteMessage($"\n图层创建测试 ({testLayerName}): {(layerExists ? "✅ 通过" : "❌ 失败")}");
                
                // 测试 2: 创建文本样式
                string testTextStyleName = $"测试文本样式_{DateTime.Now.Ticks}";
                var textConfig = TextStyleConfig.CreateDefault(testTextStyleName, 1.0);
                styleService.CreateOrUpdateTextStyle(textConfig);
                bool styleExists = styleService.TextStyleExists(testTextStyleName);
                ed.WriteMessage($"\n文本样式创建测试 ({testTextStyleName}): {(styleExists ? "✅ 通过" : "❌ 失败")}");
                
                // 测试 3: 创建标注样式
                string testDimStyleName = $"测试标注样式_{DateTime.Now.Ticks}";
                var dimConfig = DimensionStyleConfig.CreateDefault(testDimStyleName, testTextStyleName, 1.0);
                styleService.CreateOrUpdateDimensionStyle(dimConfig);
                bool dimStyleExists = styleService.DimensionStyleExists(testDimStyleName);
                ed.WriteMessage($"\n标注样式创建测试 ({testDimStyleName}): {(dimStyleExists ? "✅ 通过" : "❌ 失败")}");
                
                bool allPassed = layerExists && styleExists && dimStyleExists;
                ed.WriteMessage(allPassed ? "\n\n✅ 所有测试通过\n" : "\n\n❌ 部分测试失败\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}");
                ed.WriteMessage($"\n堆栈跟踪: {ex.StackTrace}\n");
            }
        }

        #endregion

        #region 测试 4: 几何服务布尔运算测试

        /// <summary>
        /// 测试几何服务并集运算
        /// </summary>
        public static void TestGeometryServiceUnion()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            ed.WriteMessage("\n=== 测试 4: 几何服务布尔运算（并集）===");
            
            var geometryService = ServiceLocator.Resolve<IGeometryService>();
            
            try
            {
                // 创建两个重叠的正方形
                var square1 = new Polygon2D(new[]
                {
                    new Point2D(0, 0),
                    new Point2D(10, 0),
                    new Point2D(10, 10),
                    new Point2D(0, 10)
                });
                
                var square2 = new Polygon2D(new[]
                {
                    new Point2D(5, 5),
                    new Point2D(15, 5),
                    new Point2D(15, 15),
                    new Point2D(5, 15)
                });
                
                ed.WriteMessage($"\n正方形1面积: {square1.GetArea():F2}");
                ed.WriteMessage($"\n正方形2面积: {square2.GetArea():F2}");
                
                // 并集运算
                var unionResult = geometryService.Union(new[] { square1, square2 }).ToList();
                
                ed.WriteMessage($"\n并集结果数量: {unionResult.Count}");
                ed.WriteMessage($"\n预期: 1 个多边形");
                
                if (unionResult.Count == 1)
                {
                    double area = unionResult[0].GetArea();
                    ed.WriteMessage($"\n并集面积: {area:F2}");
                    ed.WriteMessage($"\n预期面积: 150.00 (两个100平方的正方形，重叠50平方)");
                    
                    bool passed = Math.Abs(area - 150.0) < 1.0;
                    ed.WriteMessage(passed ? "\n✅ 测试通过\n" : $"\n❌ 测试失败（误差: {Math.Abs(area - 150.0):F2}）\n");
                }
                else
                {
                    ed.WriteMessage("\n❌ 测试失败：并集结果数量不正确\n");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}");
                ed.WriteMessage($"\n堆栈跟踪: {ex.StackTrace}\n");
            }
        }

        #endregion

        #region 综合测试

        /// <summary>
        /// 运行所有阶段 1 测试
        /// </summary>
        public static void RunAllPhase1Tests()
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            
            ed.WriteMessage("\n");
            ed.WriteMessage("\n========================================");
            ed.WriteMessage("\n  HyCADTool.Refactored 阶段 1 综合测试");
            ed.WriteMessage("\n========================================\n");
            
            TestPluginLoad();
            TestPolygonArea();
            TestPolygonCentroid();
            TestPointInPolygon();
            TestLayerAndStyleServices();
            TestGeometryServiceUnion();
            
            ed.WriteMessage("\n========================================");
            ed.WriteMessage("\n  阶段 1 综合测试结束");
            ed.WriteMessage("\n========================================\n");
        }

        #endregion
    }
}

