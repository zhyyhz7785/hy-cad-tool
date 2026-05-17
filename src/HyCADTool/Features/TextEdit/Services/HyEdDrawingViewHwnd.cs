using System;
using System.Runtime.InteropServices;
using Autodesk.AutoCAD.ApplicationServices;

namespace HyCADTool.Features.TextEdit.Services
{
    /// <summary>
    /// 解析"绘图视图"窗口句柄 —— 即 AutoCAD 文档窗口中**实际显示几何**的内层子窗口。
    /// <para>
    /// 用途：把 <see cref="Autodesk.AutoCAD.EditorInput.Editor.PointToScreen"/> 返回的 DIP CLIENT 坐标
    /// 通过 <c>ClientToScreen</c> 转换为屏幕坐标时，必须用绘图视图 HWND；用 <c>doc.Window.Handle</c>
    /// 会把"文件标签栏 + 视图导航条 + 命令行 dock"的厚度算进客户区原点，导致编辑层 Y 向偏低 ~40 DIP
    /// （[Autodesk 论坛 7504089](https://forums.autodesk.com/t5/net-forum/wcs-to-pixel/td-p/7504089) 实测）。
    /// </para>
    /// <para>
    /// 解析策略（自动 fallback）：
    /// <list type="number">
    ///   <item>试 P/Invoke <c>AcadGetCurrentDwgView()</c>（accore.dll，C++ mangled name）</item>
    ///   <item>若该版本 AutoCAD 不导出 mangled 名（抛 EntryPointNotFoundException）：
    ///         <c>EnumChildWindows(doc.Window.Handle, ...)</c> 按 <c>GetClientRect</c> 面积选最大子窗（默认布局下=绘图画布）</item>
    ///   <item>仍取不到：退回 <c>doc.Window.Handle</c>（至少不崩、保留当前行为）</item>
    /// </list>
    /// </para>
    /// </summary>
    internal static class HyEdDrawingViewHwnd
    {
        [DllImport("accore.dll", CallingConvention = CallingConvention.Cdecl,
            EntryPoint = "?AcadGetCurrentDwgView@@YAPEAUHWND__@@XZ")]
        private static extern IntPtr AcadGetCurrentDwgView();

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumChildProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hwndParent, EnumChildProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        /// <summary>解析当前文档的绘图视图 HWND。失败时退回 <paramref name="doc"/>.Window.Handle。</summary>
        public static IntPtr Resolve(Document doc)
        {
            if (doc == null)
                return IntPtr.Zero;

            try
            {
                IntPtr h = AcadGetCurrentDwgView();
                if (h != IntPtr.Zero)
                    return h;
            }
            catch (EntryPointNotFoundException)
            {
                /* 不同版本 AutoCAD 的 C++ mangling 名可能变；进入兜底路径 */
            }
            catch (DllNotFoundException)
            {
                /* accore.dll 不可达（极少）；进入兜底路径 */
            }

            try
            {
                IntPtr largest = FindLargestVisibleChild(doc.Window.Handle);
                if (largest != IntPtr.Zero)
                    return largest;
            }
            catch
            {
                /* EnumChildWindows 异常退回 doc.Window.Handle */
            }

            try { return doc.Window.Handle; }
            catch { return IntPtr.Zero; }
        }

        private static IntPtr FindLargestVisibleChild(IntPtr parent)
        {
            if (parent == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr bestHwnd = IntPtr.Zero;
            long bestArea = 0;

            EnumChildProc callback = (hwnd, _) =>
            {
                if (!IsWindowVisible(hwnd))
                    return true;

                if (!GetClientRect(hwnd, out RECT rc))
                    return true;

                long w = rc.Right - rc.Left;
                long h = rc.Bottom - rc.Top;
                long area = w * h;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestHwnd = hwnd;
                }
                return true;
            };

            EnumChildWindows(parent, callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            return bestHwnd;
        }
    }
}
