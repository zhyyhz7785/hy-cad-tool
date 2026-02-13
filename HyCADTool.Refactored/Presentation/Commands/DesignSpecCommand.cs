using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Models.Text;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using HyCADTool.Refactored.Presentation.Views;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// Markdown 设计说明排版命令 (hymd)
    /// 优先使用 WebView2 编辑器（net8.0），失败回退到旧版 TextBox 编辑器
    /// </summary>
    public class DesignSpecCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            string[] columnContents = null;
            string markdownSource = null;
            DesignSpecConfig config = null;

            // 尝试 WebView2 编辑器（net8.0 项目）
            long ownerHandle = 0;
            try { ownerHandle = AcApp.MainWindow.Handle.ToInt64(); } catch { }

            bool useModernEditor = EditorLoader.TryShowEditor(
                null, null, ownerHandle,
                out columnContents, out markdownSource, out config);

            if (!useModernEditor)
            {
                // 回退到旧版编辑器
                ed.WriteMessage("\n[提示] WebView2 编辑器不可用，使用回退编辑器");
                try
                {
                    var dialog = new MarkdownEditorDialog();
                    dialog.ViewModel.InsertRequested += (colContents, mdSource, cfg) =>
                    {
                        columnContents = colContents;
                        markdownSource = mdSource;
                        config = cfg;
                    };

                    AcApp.ShowModalWindow(dialog);

                    if (dialog.DialogResult != true || columnContents == null || columnContents.Length == 0)
                    {
                        ed.WriteMessage("\n已取消。");
                        return;
                    }
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n打开编辑器失败: {ex.Message}");
                    return;
                }
            }

            if (columnContents == null || columnContents.Length == 0)
            {
                ed.WriteMessage("\n已取消。");
                return;
            }

            // 选择插入点
            var ptRes = ed.GetPoint("\n请选择设计说明左上角插入点：");
            if (ptRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n已取消。");
                return;
            }

            try
            {
                string cpcStr = config.CharsPerColumn != null
                    ? string.Join(",", config.CharsPerColumn)
                    : "null";
                ed.WriteMessage($"\n[诊断] CharsPerColumn=[{cpcStr}], TextSize={config.TextSize}, Scale={config.Scale}");
                ed.WriteMessage($"\n[诊断] ColumnCount={config.ColumnCount}, ColumnGutter={config.ColumnGutter}");
                ed.WriteMessage($"\n[诊断] 各栏内容长度=[{string.Join(",", columnContents.Select(c => c?.Length ?? 0))}]");

                var service = new DesignSpecService();
                service.Insert(columnContents, markdownSource, config, ptRes.Value);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n插入失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
