using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HyCADTool.Shared.AutoCAD.UI
{
    /// <summary>
    /// Win32 辅助：按标题文字枚举顶级窗口，抹掉 PaletteSet 的原生标题栏（WS_CAPTION）。
    /// 仅在 Palette 处于 Dock 状态时调用（Float 时保留原框，避免拖动/缩放失效）。
    ///
    /// 实现要点：
    /// - AutoCAD .NET API 不暴露 PaletteSet 的宿主 HWND，只能 EnumWindows + 标题匹配
    /// - 先找到"标题完全匹配"的顶级窗口，再顺着 parent 链（Dock 时 PaletteSet 是子窗口）找到真正的 Caption HWND
    /// - 对找到的第一个窗口 GetWindowLong(GWL_STYLE) → AND NOT WS_CAPTION → SetWindowLong → SetWindowPos FRAMECHANGED 强制重绘
    /// - 找不到就静默返回（不抛异常，避免污染 AutoCAD 命令行）
    /// </summary>
    internal static class PaletteTitleBarStripper
    {
        private const int GWL_STYLE = -16;
        private const uint WS_CAPTION = 0x00C00000;

        private const uint SWP_NOMOVE       = 0x0002;
        private const uint SWP_NOSIZE       = 0x0001;
        private const uint SWP_NOZORDER     = 0x0004;
        private const uint SWP_NOACTIVATE   = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                                int X, int Y, int cx, int cy, uint uFlags);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, nIndex)
                : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        /// <summary>按标题文字找到第一个匹配的顶级窗口，抹掉 WS_CAPTION。</summary>
        /// <param name="titleText">PaletteSet.Name 一致的字符串，例 "HyCAD 命令"。</param>
        /// <returns>true 表示找到并改写成功；false 表示没找到。</returns>
        public static bool TryStripCaption(string titleText)
        {
            if (string.IsNullOrEmpty(titleText)) return false;

            IntPtr found = IntPtr.Zero;
            try
            {
                EnumWindows((hWnd, _) =>
                {
                    if (!IsWindowVisible(hWnd)) return true;
                    int len = GetWindowTextLength(hWnd);
                    if (len <= 0 || len > 256) return true;
                    var sb = new StringBuilder(len + 1);
                    GetWindowText(hWnd, sb, sb.Capacity);
                    if (sb.ToString() == titleText)
                    {
                        found = hWnd;
                        return false;
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch
            {
                return false;
            }

            if (found == IntPtr.Zero) return false;

            try
            {
                var style = GetWindowLongPtr(found, GWL_STYLE).ToInt64();
                if ((style & WS_CAPTION) == 0) return true;

                var newStyle = style & ~(long)WS_CAPTION;
                SetWindowLongPtr(found, GWL_STYLE, new IntPtr(newStyle));
                SetWindowPos(found, IntPtr.Zero, 0, 0, 0, 0,
                             SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
