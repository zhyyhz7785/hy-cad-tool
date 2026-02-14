using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Refactored.Presentation.Commands;
using HyCADTool.Refactored.Presentation.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// C1 入口：
    ///   1. 有命令（新命令或重复上次） → 执行（带计时）
    ///   2. 无命令 → 打开 HY 统一工具面板
    /// </summary>
    public static class TestCommand
    {
        public static void Run()
        {
            // 获取命令（新命令或重复上次）
            var command = SettingsPanelViewModel.ConsumePendingCommand();
            if (command != null)
            {
                // 执行命令（面板按钮或重复上次）
                SimpleLogger.LogElapsedTime("命令执行", () =>
                {
                    try
                    {
                        command();
                    }
                    catch (System.Exception ex)
                    {
                        Application.DocumentManager.MdiActiveDocument?.Editor
                            ?.WriteMessage($"\n错误：{ex.Message}");
                    }
                });
            }
            else
            {
                // ★ 测试入口：Markdown 导入命令（hymd）
                new DesignSpecCommand().Execute();
            }
        }

        /// <summary>
        /// 直接打开 MarkdownEditor 编辑器窗口，测试编辑器 UI。
        /// 不执行 AutoCAD 选点/插入流程，专注测试编辑器本身。
        /// </summary>
        private static void TestMarkdownEditor()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;

            long ownerHandle = 0;
            try { ownerHandle = AcApp.MainWindow.Handle.ToInt64(); } catch { }

            SimpleLogger.LogElapsedTime("MarkdownEditor 测试", () =>
            {
                bool confirmed = EditorLoader.TryShowEditor(
                    null, null, ownerHandle,
                    out var columnContents, out var columnMarkdowns, out var markdownSource, out var config);

                if (confirmed && columnContents != null)
                {
                    ed?.WriteMessage($"\n[测试结果] 确认插入，栏数={columnContents.Length}，Markdown长度={markdownSource?.Length ?? 0}");
                    for (int i = 0; i < columnContents.Length; i++)
                        ed?.WriteMessage($"\n  栏{i + 1}: {columnContents[i]?.Length ?? 0} 字符");
                }
                else
                {
                    ed?.WriteMessage("\n[测试结果] 用户取消或编辑器加载失败");
                }
            });
        }
    }
}
