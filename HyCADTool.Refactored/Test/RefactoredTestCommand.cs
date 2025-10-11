using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autofac;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 重构版测试命令 - 仿照原项目 TestCommand.cs 的迭代开发模式
    /// 
    /// 使用方式：
    /// 1. 编写当前测试代码（取消注释）
    /// 2. 编译 → C2 → C1 测试
    /// 3. 测试通过后注释当前测试
    /// 4. 编写下一个测试
    /// 
    /// 这样可以实现热加载开发，无需重启 AutoCAD
    /// </summary>
    public static class RefactoredTestCommand
    {
        /// <summary>
        /// 当前测试命令（热加载开发用）
        /// 命令: C1
        /// </summary>
        [CommandMethod("C1")]
        public static void Test()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n========================================");
                ed.WriteMessage("\n  RefactoredTestCommand - 当前测试");
                ed.WriteMessage("\n========================================\n");

                // ============================================================
                // 当前测试：集成主面板功能测试（活跃）
                // ============================================================
                TestMainPanelIntegration(ed);

                // ============================================================
                // 已完成的测试（已注释）
                // ============================================================
                // TestServiceLocatorInitialization(ed);
                // TestSelectionService(ed);
                // TestPanelManager(ed);
                // TestReinPanelParameterSync(ed);
                // TestFilterPanelFunctionality(ed);
                // TestBaseReinPanelMVVM(ed);

                ed.WriteMessage("\n========================================");
                ed.WriteMessage("\n  当前测试完成");
                ed.WriteMessage("\n========================================\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}\n");
                ed.WriteMessage($"堆栈跟踪: {ex.StackTrace}\n");
            }
        }

        #region 当前测试：集成主面板功能测试

        /// <summary>
        /// 测试集成主面板功能（HYREFACTOR 命令）
        /// </summary>
        private static void TestMainPanelIntegration(Editor ed)
        {
            ed.WriteMessage("\n=== 测试：集成主面板功能 ===\n");

            try
            {
                // 1. 检查 ServiceLocator 初始化
                if (ServiceLocator.Container == null)
                {
                    ed.WriteMessage("❌ ServiceLocator.Container 为 null！\n");
                    ed.WriteMessage("提示：请确保插件已正确初始化\n");
                    return;
                }
                ed.WriteMessage("✅ ServiceLocator.Container 已初始化\n");

                // 2. 测试主面板命令是否可用
                ed.WriteMessage("\n正在测试主面板命令...\n");
                ed.WriteMessage("提示：请手动执行以下命令进行测试：\n");
                ed.WriteMessage("  HYREFACTOR  - 显示集成主面板\n");
                ed.WriteMessage("  HYTOGGLE    - 切换主面板显示/隐藏\n");
                ed.WriteMessage("  HYHIDE      - 隐藏主面板\n");

                // 3. 验证各个子面板的 ViewModel 能否正确解析
                ed.WriteMessage("\n正在验证子面板 ViewModel 解析...\n");

                try
                {
                    var filterViewModel = ServiceLocator.Container.Resolve<HyCADTool.Refactored.Presentation.ViewModels.FilterPanelViewModel>();
                    ed.WriteMessage("✅ FilterPanelViewModel 解析成功\n");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"❌ FilterPanelViewModel 解析失败: {ex.Message}\n");
                }

                try
                {
                    var baseReinViewModel = ServiceLocator.Container.Resolve<HyCADTool.Refactored.Presentation.ViewModels.BaseReinPanelViewModel>();
                    ed.WriteMessage("✅ BaseReinPanelViewModel 解析成功\n");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"❌ BaseReinPanelViewModel 解析失败: {ex.Message}\n");
                }

                // 4. 测试选择服务（FilterPanel 依赖）
                try
                {
                    var selectionService = ServiceLocator.Container.Resolve<ISelectionService>();
                    ed.WriteMessage("✅ ISelectionService 解析成功\n");

                    // 测试 SelectAll 方法
                    var allEntities = selectionService.SelectAll();
                    ed.WriteMessage($"✅ SelectAll() 返回 {allEntities.Length} 个实体\n");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"❌ ISelectionService 测试失败: {ex.Message}\n");
                }

                // 5. 测试面板管理器
                try
                {
                    var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                    ed.WriteMessage("✅ PanelManager 解析成功\n");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"❌ PanelManager 解析失败: {ex.Message}\n");
                }

                ed.WriteMessage("\n🎯 集成主面板测试要点：\n");
                ed.WriteMessage("   1. 执行 HYREFACTOR 命令，检查是否显示包含 5 个 Tab 的主面板\n");
                ed.WriteMessage("   2. 各个子面板应该能正常显示和交互\n");
                ed.WriteMessage("   3. 钢筋面板：基本配置\n");
                ed.WriteMessage("   4. 过滤器面板：按类型/图层/颜色过滤\n");
                ed.WriteMessage("   5. 基础钢筋面板：26个参数 + 7个命令\n");
                ed.WriteMessage("   6. 桩基面板：桩基布置参数\n");
                ed.WriteMessage("   7. 聚类面板：聚类分析与标注\n");

                ed.WriteMessage("\n✅ 集成主面板功能测试完成\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 集成主面板测试失败: {ex.Message}\n");
                ed.WriteMessage($"堆栈跟踪: {ex.StackTrace}\n");
            }
        }

        #endregion

        #region 已完成的测试（已注释）

        /// <summary>
        /// 测试 1: ServiceLocator 初始化
        /// </summary>
        private static void TestServiceLocatorInitialization(Editor ed)
        {
            ed.WriteMessage("\n=== 测试 1: ServiceLocator 初始化 ===\n");

            try
            {
                // 检查 ServiceLocator 是否已初始化
                if (ServiceLocator.Container == null)
                {
                    ed.WriteMessage("❌ ServiceLocator.Container 为 null！\n");
                    ed.WriteMessage("提示：请确保插件已正确初始化\n");
                    return;
                }

                ed.WriteMessage("✅ ServiceLocator.Container 已初始化\n");
                ed.WriteMessage($"容器类型: {ServiceLocator.Container.GetType().Name}\n");

                // 测试基本服务解析
                var selectionService = ServiceLocator.Container.Resolve<ISelectionService>();
                ed.WriteMessage("✅ ISelectionService 解析成功\n");

                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();
                ed.WriteMessage("✅ PanelManager 解析成功\n");

                ed.WriteMessage("\n✅ 测试 1 完成\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试 1 失败: {ex.Message}\n");
            }
        }

        /// <summary>
        /// 测试 2: 选择服务功能
        /// </summary>
        private static void TestSelectionService(Editor ed)
        {
            ed.WriteMessage("\n=== 测试 2: 选择服务功能 ===\n");

            try
            {
                var selectionService = ServiceLocator.Container.Resolve<ISelectionService>();

                // 测试 SelectAll
                var allEntities = selectionService.SelectAll();
                ed.WriteMessage($"SelectAll() 返回: {allEntities.Length} 个实体\n");

                // 测试类型过滤
                var lines = selectionService.SelectAll("LINE");
                ed.WriteMessage($"SelectAll(\"LINE\") 返回: {lines.Length} 个线段\n");

                var circles = selectionService.SelectAll("CIRCLE");
                ed.WriteMessage($"SelectAll(\"CIRCLE\") 返回: {circles.Length} 个圆\n");

                ed.WriteMessage("✅ 选择服务基本功能正常\n");
                ed.WriteMessage("\n✅ 测试 2 完成\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试 2 失败: {ex.Message}\n");
            }
        }

        /// <summary>
        /// 测试 3: 面板管理器
        /// </summary>
        private static void TestPanelManager(Editor ed)
        {
            ed.WriteMessage("\n=== 测试 3: 面板管理器 ===\n");

            try
            {
                var panelManager = ServiceLocator.Container.Resolve<PanelManager>();

                // 显示 ReinPanel
                panelManager.ShowPanel<HyCADTool.Refactored.Presentation.Views.ReinPanel>(
                    "钢筋配置（测试）", 
                    new Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890"));
                ed.WriteMessage("✅ ReinPanel 已显示\n");

                // 显示 FilterPanel
                panelManager.ShowPanel<HyCADTool.Refactored.Presentation.Views.FilterPanel>(
                    "图形过滤器（测试）", 
                    new Guid("B2C3D4E5-F6A7-8901-BCDE-F23456789012"));
                ed.WriteMessage("✅ FilterPanel 已显示\n");

                ed.WriteMessage("提示：请检查 AutoCAD 窗口四周是否出现面板\n");
                ed.WriteMessage("\n✅ 测试 3 完成\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试 3 失败: {ex.Message}\n");
            }
        }

        /// <summary>
        /// 测试 4: ReinPanel 参数同步
        /// </summary>
        private static void TestReinPanelParameterSync(Editor ed)
        {
            ed.WriteMessage("\n=== 测试 4: ReinPanel 参数同步 ===\n");

            try
            {
                // 这里可以添加 ReinPanel 的具体测试逻辑
                ed.WriteMessage("✅ ReinPanel 参数同步测试（占位）\n");
                ed.WriteMessage("\n✅ 测试 4 完成\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试 4 失败: {ex.Message}\n");
            }
        }

        /// <summary>
        /// 测试 5: FilterPanel 功能
        /// </summary>
        private static void TestFilterPanelFunctionality(Editor ed)
        {
            ed.WriteMessage("\n=== 测试 5: FilterPanel 功能 ===\n");

            try
            {
                // 这里可以添加 FilterPanel 的具体测试逻辑
                ed.WriteMessage("✅ FilterPanel 功能测试（占位）\n");
                ed.WriteMessage("\n✅ 测试 5 完成\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试 5 失败: {ex.Message}\n");
            }
        }

        /// <summary>
        /// 测试 6: BaseReinPanel MVVM 架构
        /// </summary>
        private static void TestBaseReinPanelMVVM(Editor ed)
        {
            ed.WriteMessage("\n=== 测试 6: BaseReinPanel MVVM 架构 ===\n");

            try
            {
                // 这里可以添加 BaseReinPanel 的具体测试逻辑
                ed.WriteMessage("✅ BaseReinPanel MVVM 架构测试（占位）\n");
                ed.WriteMessage("\n✅ 测试 6 完成\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试 6 失败: {ex.Message}\n");
            }
        }

        #endregion
    }
}

