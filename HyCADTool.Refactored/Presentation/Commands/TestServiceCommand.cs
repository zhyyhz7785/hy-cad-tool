using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;
using System.Linq;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 测试服务命令类 (Test Service Commands)
    /// 用于测试阶段 2 的服务层功能
    /// </summary>
    public class TestServiceCommand
    {
        /// <summary>
        /// 测试 DrawingService - 绘制基础图形
        /// </summary>
        [CommandMethod("HYTESTDRAW")]
        public void TestDrawing()
        {
            var drawingService = ServiceLocator.Resolve<IDrawingService>();
            var editorService = ServiceLocator.Resolve<IEditorService>();

            try
            {
                editorService.WriteMessage("=================================================");
                editorService.WriteMessage("开始测试 DrawingService (Drawing Service Test)...");
                editorService.WriteMessage("=================================================");

                // 测试 1: 绘制线段
                editorService.WriteMessage("\n[测试 1] 绘制线段 (Draw Line)...");
                var lineId = drawingService.DrawLine(
                    new Point3d(0, 0, 0),
                    new Point3d(100, 100, 0)
                );
                if (lineId != Autodesk.AutoCAD.DatabaseServices.ObjectId.Null)
                {
                    editorService.WriteMessage($"✓ 线段已绘制 (Line drawn): {lineId}");
                }
                else
                {
                    editorService.WriteError("✗ 线段绘制失败 (Line drawing failed)");
                }

                // 测试 2: 绘制多段线
                editorService.WriteMessage("\n[测试 2] 绘制多段线 (Draw Polyline)...");
                var points = new[]
                {
                    new Point2d(0, 0),
                    new Point2d(100, 0),
                    new Point2d(100, 100),
                    new Point2d(0, 100)
                };
                var polyId = drawingService.DrawPolyline(points, isClosed: true);
                if (polyId != Autodesk.AutoCAD.DatabaseServices.ObjectId.Null)
                {
                    editorService.WriteMessage($"✓ 多段线已绘制 (Polyline drawn): {polyId}");
                }
                else
                {
                    editorService.WriteError("✗ 多段线绘制失败 (Polyline drawing failed)");
                }

                // 测试 3: 绘制圆
                editorService.WriteMessage("\n[测试 3] 绘制圆 (Draw Circle)...");
                var circleId = drawingService.DrawCircle(
                    new Point3d(50, 50, 0),
                    30
                );
                if (circleId != Autodesk.AutoCAD.DatabaseServices.ObjectId.Null)
                {
                    editorService.WriteMessage($"✓ 圆已绘制 (Circle drawn): {circleId}");
                }
                else
                {
                    editorService.WriteError("✗ 圆绘制失败 (Circle drawing failed)");
                }

                // 测试 4: 绘制文本
                editorService.WriteMessage("\n[测试 4] 绘制文本 (Draw Text)...");
                var textId = drawingService.DrawText(
                    new Point3d(200, 200, 0),
                    "测试文本 (Test Text)",
                    5.0
                );
                if (textId != Autodesk.AutoCAD.DatabaseServices.ObjectId.Null)
                {
                    editorService.WriteMessage($"✓ 文本已绘制 (Text drawn): {textId}");
                }
                else
                {
                    editorService.WriteError("✗ 文本绘制失败 (Text drawing failed)");
                }

                editorService.WriteMessage("\n=================================================");
                editorService.WriteMessage("✓ DrawingService 测试通过！(Test Passed!)");
                editorService.WriteMessage("=================================================");
            }
            catch (System.Exception ex)
            {
                editorService.WriteError($"测试失败 (Test Failed): {ex.Message}");
            }
        }

        /// <summary>
        /// 测试 EditorService - 用户交互
        /// </summary>
        [CommandMethod("HYTESTEDITOR")]
        public void TestEditor()
        {
            var editorService = ServiceLocator.Resolve<IEditorService>();

            try
            {
                editorService.WriteMessage("=================================================");
                editorService.WriteMessage("开始测试 EditorService (Editor Service Test)...");
                editorService.WriteMessage("=================================================");

                // 测试 1: 获取点
                editorService.WriteMessage("\n[测试 1] 获取点 (Get Point)...");
                var point = editorService.GetPoint("请选择一个点");
                if (point.HasValue)
                {
                    editorService.WriteMessage($"✓ 已选择点 (Point selected): ({point.Value.X:F2}, {point.Value.Y:F2}, {point.Value.Z:F2})");
                }
                else
                {
                    editorService.WriteWarning("用户取消了点选择 (User cancelled point selection)");
                }

                // 测试 2: 获取距离
                editorService.WriteMessage("\n[测试 2] 获取距离 (Get Distance)...");
                var distance = editorService.GetDistance("请输入距离");
                if (distance.HasValue)
                {
                    editorService.WriteMessage($"✓ 已输入距离 (Distance entered): {distance.Value:F2}");
                }
                else
                {
                    editorService.WriteWarning("用户取消了距离输入 (User cancelled distance input)");
                }

                // 测试 3: 关键字选择
                editorService.WriteMessage("\n[测试 3] 关键字选择 (Keyword Selection)...");
                var keyword = editorService.GetKeyword(
                    "请选择选项 (Please select option)",
                    "选项1(Option1)", "选项2(Option2)", "选项3(Option3)"
                );
                if (keyword != null)
                {
                    editorService.WriteMessage($"✓ 已选择 (Selected): {keyword}");
                }
                else
                {
                    editorService.WriteWarning("用户取消了关键字选择 (User cancelled keyword selection)");
                }

                // 测试 4: 是/否确认
                editorService.WriteMessage("\n[测试 4] 是/否确认 (Yes/No Confirmation)...");
                var confirmed = editorService.GetYesNo("是否继续测试？ (Continue testing?)", defaultValue: true);
                editorService.WriteMessage($"✓ 用户选择 (User choice): {(confirmed ? "是 (Yes)" : "否 (No)")}");

                editorService.WriteMessage("\n=================================================");
                editorService.WriteMessage("✓ EditorService 测试通过！(Test Passed!)");
                editorService.WriteMessage("=================================================");
            }
            catch (System.Exception ex)
            {
                editorService.WriteError($"测试失败 (Test Failed): {ex.Message}");
            }
        }

        /// <summary>
        /// 测试 DatabaseService - 数据库查询
        /// </summary>
        [CommandMethod("HYTESTDB")]
        public void TestDatabase()
        {
            var databaseService = ServiceLocator.Resolve<IDatabaseService>();
            var editorService = ServiceLocator.Resolve<IEditorService>();

            try
            {
                editorService.WriteMessage("=================================================");
                editorService.WriteMessage("开始测试 DatabaseService (Database Service Test)...");
                editorService.WriteMessage("=================================================");

                // 测试 1: 获取所有实体
                editorService.WriteMessage("\n[测试 1] 获取所有实体 (Get All Entities)...");
                var allEntities = databaseService.GetAllEntitiesInModelSpace();
                editorService.WriteMessage($"✓ ModelSpace 中共有 {allEntities.Length} 个实体 (Total entities in ModelSpace)");

                // 测试 2: 获取特定类型实体
                editorService.WriteMessage("\n[测试 2] 获取线段 (Get Lines)...");
                var lines = databaseService.GetAllEntitiesByType<Autodesk.AutoCAD.DatabaseServices.Line>();
                editorService.WriteMessage($"✓ 共有 {lines.Length} 条线段 (Total lines)");

                // 测试 3: 获取所有图层
                editorService.WriteMessage("\n[测试 3] 获取所有图层 (Get All Layers)...");
                var layerNames = databaseService.GetAllLayerNames();
                editorService.WriteMessage($"✓ 共有 {layerNames.Count} 个图层 (Total layers)");
                
                // 显示前5个图层
                if (layerNames.Count > 0)
                {
                    editorService.WriteMessage("  前 5 个图层 (First 5 layers):");
                    for (int i = 0; i < System.Math.Min(5, layerNames.Count); i++)
                    {
                        editorService.WriteMessage($"    - {layerNames[i]}");
                    }
                }

                // 测试 4: 检查图层存在性
                editorService.WriteMessage("\n[测试 4] 检查图层存在性 (Check Layer Exists)...");
                bool layer0Exists = databaseService.LayerExists("0");
                editorService.WriteMessage($"✓ 图层 '0' 存在 (Layer '0' exists): {layer0Exists}");

                // 测试 5: 获取数据库对象
                editorService.WriteMessage("\n[测试 5] 获取数据库对象 (Get Database)...");
                var db = databaseService.GetCurrentDatabase();
                editorService.WriteMessage($"✓ 当前数据库 (Current database): {db.Filename ?? "未保存 (Unsaved)"}");

                editorService.WriteMessage("\n=================================================");
                editorService.WriteMessage("✓ DatabaseService 测试通过！(Test Passed!)");
                editorService.WriteMessage("=================================================");
            }
            catch (System.Exception ex)
            {
                editorService.WriteError($"测试失败 (Test Failed): {ex.Message}");
            }
        }

        /// <summary>
        /// 综合测试 - 测试所有阶段2服务
        /// </summary>
        [CommandMethod("HYTESTSERVICES")]
        public void TestAllServices()
        {
            var editorService = ServiceLocator.Resolve<IEditorService>();

            try
            {
                editorService.WriteMessage("\n");
                editorService.WriteMessage("╔════════════════════════════════════════════════╗");
                editorService.WriteMessage("║   阶段 2 服务层综合测试                       ║");
                editorService.WriteMessage("║   Stage 2 Service Layer Comprehensive Test    ║");
                editorService.WriteMessage("╚════════════════════════════════════════════════╝");
                editorService.WriteMessage("\n");

                // 测试 1: DrawingService
                editorService.WriteMessage("▶ 测试绘制服务 (Testing DrawingService)...");
                TestDrawingServiceInternal();

                // 测试 2: DatabaseService
                editorService.WriteMessage("\n▶ 测试数据库服务 (Testing DatabaseService)...");
                TestDatabaseServiceInternal();

                editorService.WriteMessage("\n");
                editorService.WriteMessage("╔════════════════════════════════════════════════╗");
                editorService.WriteMessage("║        所有测试通过！ All Tests Passed!       ║");
                editorService.WriteMessage("╚════════════════════════════════════════════════╝");
                editorService.WriteMessage("\n");
            }
            catch (System.Exception ex)
            {
                editorService.WriteError($"综合测试失败 (Comprehensive test failed): {ex.Message}");
            }
        }

        #region 内部测试方法 (Internal Test Methods)

        private void TestDrawingServiceInternal()
        {
            var drawingService = ServiceLocator.Resolve<IDrawingService>();
            var editorService = ServiceLocator.Resolve<IEditorService>();

            // 测试绘制
            var lineId = drawingService.DrawLine(
                new Point3d(0, 0, 0),
                new Point3d(100, 0, 0)
            );

            if (lineId != Autodesk.AutoCAD.DatabaseServices.ObjectId.Null)
            {
                editorService.WriteMessage("  ✓ 绘制服务正常 (Drawing service OK)");
            }
            else
            {
                throw new System.Exception("绘制服务测试失败 (Drawing service test failed)");
            }
        }

        private void TestDatabaseServiceInternal()
        {
            var databaseService = ServiceLocator.Resolve<IDatabaseService>();
            var editorService = ServiceLocator.Resolve<IEditorService>();

            // 测试数据库查询
            var allEntities = databaseService.GetAllEntitiesInModelSpace();
            editorService.WriteMessage($"  ✓ 数据库服务正常，共有 {allEntities.Length} 个实体 (Database service OK, {allEntities.Length} entities)");
        }

        #endregion
    }
}

