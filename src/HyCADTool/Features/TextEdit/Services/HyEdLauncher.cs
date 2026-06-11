using System;
using System.Windows.Interop;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.TextEdit.ViewModels;
using HyCADTool.Features.TextEdit.Views;
using HyCADTool.Shell.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.TextEdit.Services
{
    /// <summary>
    /// 打开 hyed 原位透明编辑层并把改动写回数据库。
    /// <para>
    /// 设计取舍：**不**修改 <c>entity.Visible</c> 来隐藏原字。理由：
    /// <list type="bullet">
    ///   <item>AutoCAD .NET 托管层未公开 <c>StartUndoMark/EndUndoMark</c>，多次 Commit 会污染 Undo 栈</item>
    ///   <item>AutoCAD 原生 IPE 也不动数据库，是渲染层 ghost 擦除（托管 API 不可达）</item>
    ///   <item>编辑框背景 <c>#F2000000</c>（95% 不透明深色）+ 屏幕字号匹配，足够完全遮挡原文字</item>
    /// </list>
    /// </para>
    /// <para>
    /// 流程：
    /// <list type="number">
    ///   <item>读 text/style/heightDip/fontSizeDip，定位 WPF 编辑层至屏幕 DIP 坐标</item>
    ///   <item>非模态 <c>Show()</c>：命令线程立即返回，AutoCAD 回到 <c>Command:</c>; 焦点由编辑层接管</item>
    ///   <item><c>win.Closed</c> 回调（Result==true）：LockDocument + Transaction 写回新文字</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class HyEdLauncher
    {
        public static void LaunchEditor(Document doc, ObjectId targetId)
        {
            if (doc == null || targetId.IsNull)
                return;

            var ed = doc.Editor;

            if (!HyEdTextAccessor.TryRead(targetId, out string text, out var kind, out string handleHex))
            {
                ed.WriteMessage("\n[hyed] 无法读取所选实体或未支持的类型。\n");
                return;
            }

            double offsetFactor = 0;
            try { offsetFactor = SettingsPanelViewModel.Current?.HyEdAboveOffsetFactor ?? 0; }
            catch { /* ViewModel 未就绪退默认 0 */ }

            // MText/MLeader 走 codec：把 \T1.137;{\L...} 这类控制码剥成纯文本进编辑框，
            // 提交时由 Encode 拼回；DBText/Dimension 不需要 codec。
            string editableText = text;
            MTextWrap wrap = null;
            if (kind == HyEdEntityKind.MText || kind == HyEdEntityKind.MLeader)
            {
                MTextCodec.Decode(text, out editableText, out wrap);
            }

            var vm = new HyEdDialogViewModel(kind, handleHex, editableText)
            {
                MTextWrap = wrap
            };
            var win = new HyEdInPlaceWindow(vm);

            try
            {
                new WindowInteropHelper(win) { Owner = AcApp.MainWindow.Handle };
            }
            catch { /* 无宿主仍允许 Show */ }

            if (HyEdScreenLocator.TryLocate(doc, targetId, offsetFactor,
                    out double l, out double t, out double w, out double h, out _))
            {
                win.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
                win.Left = l;
                win.Top = t;
                win.Width = Math.Max(120, w);
                win.Height = Math.Max(28, h);
            }
            else
            {
                win.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                win.Width = 320;
                win.Height = 120;
            }
            // 字号固定 14 DIP：编辑框尺寸贴近原文字屏幕大小、但字号便于编辑（用户可拖右下角调框大小）。
            vm.FontSizeDip = 14;

            // Closed 回调里写回：非模态 Show() 后命令线程立刻结束，写库在文档锁下进行。
            win.Closed += (s, e) =>
            {
                try
                {
                    if (win.Result != true)
                        return;

                    // MText/MLeader 把纯文本 + wrap 拼回原 Contents 串；其余类型原样写回。
                    string finalText = vm.EditableText ?? string.Empty;
                    if (vm.MTextWrap != null)
                        finalText = MTextCodec.Encode(finalText, vm.MTextWrap);

                    using (doc.LockDocument())
                    {
                        bool ok = HyEdTextAccessor.Write(targetId, finalText, kind);
                        if (ok)
                            ed.WriteMessage("\n[hyed] 已更新。\n");
                        else
                            ed.WriteMessage("\n[hyed] 写回失败。\n");
                    }
                }
                catch (System.Exception ex)
                {
                    try { ed.WriteMessage($"\n[hyed] 写回异常：{ex.Message}\n"); }
                    catch { /* 命令行写入失败时静默 */ }
                }
            };

            win.Show();
        }
    }
}
