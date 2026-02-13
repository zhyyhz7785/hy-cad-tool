using System;
using System.Windows.Interop;
using HyCADTool.MarkdownEditor.Models;
using HyCADTool.MarkdownEditor.Views;
using Newtonsoft.Json;

namespace HyCADTool.MarkdownEditor
{
    /// <summary>
    /// 编辑器静态入口（供主项目反射调用）
    /// 签名: public static string ShowDialog(string inputJson, long ownerHandle)
    /// </summary>
    public static class EditorLauncher
    {
        /// <summary>
        /// 显示 Markdown WYSIWYG 编辑器
        /// </summary>
        /// <param name="inputJson">EditorInput 的 JSON 序列化</param>
        /// <param name="ownerHandle">父窗口句柄（0 = 无父窗口）</param>
        /// <returns>EditorResult 的 JSON 序列化</returns>
        public static string ShowDialog(string inputJson, long ownerHandle = 0)
        {
            EditorInput input;
            try
            {
                input = JsonConvert.DeserializeObject<EditorInput>(inputJson) ?? new EditorInput();
            }
            catch
            {
                input = new EditorInput();
            }

            var window = new EditorWindow(input);

            // 设置父窗口（AutoCAD 主窗口）
            if (ownerHandle != 0)
            {
                try
                {
                    var helper = new WindowInteropHelper(window);
                    helper.Owner = new IntPtr(ownerHandle);
                }
                catch { /* 忽略设置父窗口失败 */ }
            }

            window.ShowDialog();

            var result = window.Result ?? new EditorResult { Confirmed = false };
            return JsonConvert.SerializeObject(result);
        }
    }
}
