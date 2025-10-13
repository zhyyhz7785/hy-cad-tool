using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.Modules;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

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

                // ===== 阶段 3: ZTools 工具层测试（待添加） =====
                // _editor.WriteMessage("\n\n【阶段 3】ZTools 工具层测试");
                // _editor.WriteMessage("\n" + new string('═', 50));
                // ... 未来在这里添加 ZTools 相关测试 ...

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
            if (Math.Abs(configService.Global.Scale.Default - newScale) > 0.001)
            {
                throw new System.Exception("配置更新失败：比例值未正确更新");
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
    }
}

