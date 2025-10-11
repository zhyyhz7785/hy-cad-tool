using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.ValueObjects;
using System;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Test.Phase5_0_6TestCommand))]

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 阶段 5.0.6 测试命令 - ReinPanel 钢筋面板功能测试
    /// </summary>
    public class Phase5_0_6TestCommand
    {
        private static PanelManager GetPanelManager()
        {
            return ServiceLocator.Resolve<PanelManager>();
        }

        private static T Resolve<T>()
        {
            return ServiceLocator.Resolve<T>();
        }

        #region 面板测试命令

        /// <summary>
        /// 测试命令：显示钢筋面板
        /// </summary>
        [CommandMethod("TESTREINPANEL", CommandFlags.Session)]
        public void TestReinPanel()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;

            try
            {
                var panelManager = GetPanelManager();
                if (panelManager == null)
                {
                    ed?.WriteMessage("\n错误：PanelManager 未初始化。\n");
                    return;
                }

                // 显示面板
                panelManager.ShowPanel<HyCADTool.Refactored.Presentation.Views.ReinPanel>("钢筋配置", Guid.NewGuid());

                ed?.WriteMessage("\n钢筋面板已显示。\n");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n显示钢筋面板时发生错误: {ex.Message}\n");
                ed?.WriteMessage($"堆栈跟踪: {ex.StackTrace}\n");
            }
        }

        #endregion

        #region 服务测试命令

        /// <summary>
        /// 测试命令：测试钢筋绘制服务（使用默认参数）
        /// 对应原命令：gj
        /// </summary>
        [CommandMethod("TESTGJ", CommandFlags.Modal)]
        public void TestReinDraw()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;

            try
            {
                // 解析服务
                var reinService = Resolve<IReinService>();

                // 创建默认参数
                var parameters = ReinParameters.CreateDefault();

                ed?.WriteMessage("\n开始绘制钢筋（使用默认参数）...\n");
                ed?.WriteMessage($"比例: {parameters.Scale}\n");
                ed?.WriteMessage($"钢筋直径: {parameters.RebarDiameter}\n");
                ed?.WriteMessage($"钢筋间距: {parameters.RebarSpacing}\n");

                // 调用绘制服务
                reinService.DrawReinforcement(parameters);

                ed?.WriteMessage("\n钢筋绘制完成。\n");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n绘制钢筋时发生错误: {ex.Message}\n");
                ed?.WriteMessage($"堆栈跟踪: {ex.StackTrace}\n");
            }
        }

        /// <summary>
        /// 测试命令：测试样式设置服务
        /// </summary>
        [CommandMethod("TESTREIN_STYLE", CommandFlags.Modal)]
        public void TestReinStyle()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;

            try
            {
                // 解析服务
                var reinService = Resolve<IReinService>();

                // 创建默认参数
                var parameters = ReinParameters.CreateDefault();

                ed?.WriteMessage("\n应用钢筋样式设置...\n");

                // 调用样式设置服务
                reinService.ApplyStyle(parameters);

                ed?.WriteMessage("\n样式设置完成。\n");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n设置样式时发生错误: {ex.Message}\n");
                ed?.WriteMessage($"堆栈跟踪: {ex.StackTrace}\n");
            }
        }

        /// <summary>
        /// 测试命令：测试标注服务（模式1 - 三点标注）
        /// 对应原命令：gb
        /// </summary>
        [CommandMethod("TESTGB", CommandFlags.Modal)]
        public void TestReinDim1()
        {
            TestReinDimension(1, "三点标注");
        }

        /// <summary>
        /// 测试命令：测试标注服务（模式2 - 单点标注）
        /// 对应原命令：gb1
        /// </summary>
        [CommandMethod("TESTGB1", CommandFlags.Modal)]
        public void TestReinDim2()
        {
            TestReinDimension(2, "单点标注");
        }

        /// <summary>
        /// 测试命令：测试标注服务（模式3 - 六点标注）
        /// 对应原命令：gb2
        /// </summary>
        [CommandMethod("TESTGB2", CommandFlags.Modal)]
        public void TestReinDim3()
        {
            TestReinDimension(3, "六点标注");
        }

        /// <summary>
        /// 测试标注功能的通用方法
        /// </summary>
        private void TestReinDimension(int mode, string modeName)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;

            try
            {
                // 解析服务
                var reinService = Resolve<IReinService>();

                // 创建默认参数
                var parameters = ReinParameters.CreateDefault();

                ed?.WriteMessage($"\n开始钢筋标注（模式{mode} - {modeName}）...\n");

                // 调用标注服务
                reinService.DimensionRein(mode, parameters);

                ed?.WriteMessage($"\n钢筋标注（模式{mode}）完成。\n");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n标注钢筋时发生错误: {ex.Message}\n");
                ed?.WriteMessage($"堆栈跟踪: {ex.StackTrace}\n");
            }
        }

        #endregion

        #region 参数验证测试

        /// <summary>
        /// 测试命令：验证参数有效性
        /// </summary>
        [CommandMethod("TESTREIN_PARAMS", CommandFlags.Session)]
        public void TestReinParameters()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;

            try
            {
                ed?.WriteMessage("\n========== 钢筋参数验证测试 ==========\n");

                // 测试默认参数
                var defaultParams = ReinParameters.CreateDefault();
                ed?.WriteMessage("\n【默认参数】\n");
                ed?.WriteMessage($"比例: {defaultParams.Scale}\n");
                ed?.WriteMessage($"钢筋直径: {defaultParams.RebarDiameter}\n");
                ed?.WriteMessage($"钢筋间距: {defaultParams.RebarSpacing}\n");
                ed?.WriteMessage($"锚固长度: {defaultParams.AnchorageLength}\n");
                ed?.WriteMessage($"点钢筋间距: {defaultParams.DotSeparation}\n");
                
                bool isValid = defaultParams.IsValid(out string errorMessage);
                ed?.WriteMessage($"参数有效性: {(isValid ? "有效" : $"无效 - {errorMessage}")}\n");

                // 测试无效参数（比例为负数）
                ed?.WriteMessage("\n【测试无效参数：比例 = -10】\n");
                var invalidParams = defaultParams.Clone();
                invalidParams.Scale = -10;
                isValid = invalidParams.IsValid(out errorMessage);
                ed?.WriteMessage($"参数有效性: {(isValid ? "有效" : $"无效 - {errorMessage}")}\n");

                // 测试克隆功能
                ed?.WriteMessage("\n【测试参数克隆】\n");
                var clonedParams = defaultParams.Clone();
                clonedParams.Scale = 50;
                ed?.WriteMessage($"原参数比例: {defaultParams.Scale}\n");
                ed?.WriteMessage($"克隆参数比例: {clonedParams.Scale}\n");
                ed?.WriteMessage($"克隆功能: {(defaultParams.Scale != clonedParams.Scale ? "正常" : "失败")}\n");

                ed?.WriteMessage("\n========== 测试完成 ==========\n");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n参数验证测试时发生错误: {ex.Message}\n");
            }
        }

        #endregion

        #region 集成测试命令

        /// <summary>
        /// 测试命令：完整功能测试（显示面板 + 测试所有功能）
        /// </summary>
        [CommandMethod("TESTREINALL", CommandFlags.Session)]
        public void TestReinAll()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;

            try
            {
                ed?.WriteMessage("\n========== ReinPanel 完整功能测试 ==========\n");

                // 1. 测试参数
                ed?.WriteMessage("\n[1/5] 测试参数验证...\n");
                TestReinParameters();

                // 2. 测试样式设置
                ed?.WriteMessage("\n[2/5] 测试样式设置...\n");
                TestReinStyle();

                // 3. 测试面板显示
                ed?.WriteMessage("\n[3/5] 显示钢筋面板...\n");
                TestReinPanel();

                // 提示用户进行手动测试
                ed?.WriteMessage("\n[4/5] 请在面板中手动测试以下功能：\n");
                ed?.WriteMessage("  - 修改参数值\n");
                ed?.WriteMessage("  - 点击\"设置样式\"按钮\n");
                ed?.WriteMessage("  - 点击\"恢复默认值\"按钮\n");
                ed?.WriteMessage("  - 点击\"绘制\"按钮（需选择多段线）\n");
                ed?.WriteMessage("  - 点击三个\"标注钢筋\"按钮\n");

                ed?.WriteMessage("\n[5/5] 自动测试完成。请继续手动测试。\n");
                ed?.WriteMessage("\n========== 测试完成 ==========\n");
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n完整功能测试时发生错误: {ex.Message}\n");
            }
        }

        #endregion
    }
}

