using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.TextEdit.Services
{
    /// <summary>
    /// 把实体 WCS 包络（按 <paramref name="aboveOffsetFactor"/> 上移）映射到 WPF DIP **屏幕** 矩形。
    /// <para>
    /// 坐标链：<see cref="Autodesk.AutoCAD.EditorInput.Editor.PointToScreen"/> 返回 DIP CLIENT
    /// （绘图视图客户区原点）→ × DPI 转像素 → <c>ClientToScreen(HyEdDrawingViewHwnd.Resolve(doc), ...)</c>
    /// 转像素屏幕 → ÷ DPI 转 DIP 屏幕。<see cref="HyEdDrawingViewHwnd"/> 是关键：用 doc.Window.Handle
    /// 会把文件标签栏/视图导航条厚度算进客户区原点，Y 向偏低 ~40 DIP。
    /// </para>
    /// </summary>
    public static class HyEdScreenLocator
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref System.Drawing.Point lpPoint);

        /// <summary>
        /// 计算编辑框屏幕矩形与估算 FontSize。
        /// </summary>
        /// <param name="aboveOffsetFactor">编辑框在 WCS 中相对原文字向上偏移的"文字高度倍数"。0=原位重叠。</param>
        /// <param name="fontSizeDip">建议的编辑框字号（DIP）= clamp(行高 × 0.85, 10, 200)。</param>
        public static bool TryLocate(Document doc, ObjectId id, double aboveOffsetFactor,
            out double leftDip, out double topDip, out double widthDip, out double heightDip,
            out double fontSizeDip)
        {
            leftDip = topDip = widthDip = heightDip = 0;
            fontSizeDip = 14;

            if (doc == null || id.IsNull || !id.IsValid)
                return false;

            Extents3d ext;
            double textHeightWcs;
            int lineCount;
            try
            {
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead, false) is Entity ent))
                    {
                        tr.Commit();
                        return false;
                    }

                    try
                    {
                        ext = ent.GeometricExtents;
                    }
                    catch
                    {
                        tr.Commit();
                        return false;
                    }

                    textHeightWcs = TryReadTextHeight(ent, ext);
                    lineCount = EstimateLineCount(ent);
                    tr.Commit();
                }
            }
            catch
            {
                return false;
            }

            int vpNumber;
            IntPtr viewHwnd;
            try
            {
                vpNumber = Convert.ToInt32(AcApp.GetSystemVariable("CVPORT"));
                viewHwnd = HyEdDrawingViewHwnd.Resolve(doc);
                if (viewHwnd == IntPtr.Zero)
                    return false;
            }
            catch
            {
                return false;
            }

            double dpiX = 1.0, dpiY = 1.0;
            try
            {
                var src = HwndSource.FromHwnd(AcApp.MainWindow.Handle);
                if (src?.CompositionTarget != null)
                {
                    var m = src.CompositionTarget.TransformToDevice;
                    if (m.M11 > 0) dpiX = m.M11;
                    if (m.M22 > 0) dpiY = m.M22;
                }
            }
            catch
            {
                /* DPI 取不到时退 1:1 */
            }

            double offsetWcs = textHeightWcs * Math.Max(0.0, aboveOffsetFactor);
            double z = (ext.MinPoint.Z + ext.MaxPoint.Z) * 0.5;
            var corners = new[]
            {
                new Point3d(ext.MinPoint.X, ext.MinPoint.Y + offsetWcs, z),
                new Point3d(ext.MaxPoint.X, ext.MinPoint.Y + offsetWcs, z),
                new Point3d(ext.MinPoint.X, ext.MaxPoint.Y + offsetWcs, z),
                new Point3d(ext.MaxPoint.X, ext.MaxPoint.Y + offsetWcs, z),
            };

            double minSx = double.MaxValue;
            double minSy = double.MaxValue;
            double maxSx = double.MinValue;
            double maxSy = double.MinValue;
            int okCount = 0;

            var ed = doc.Editor;
            foreach (var p in corners)
            {
                System.Windows.Point clientDip;
                try
                {
                    clientDip = ed.PointToScreen(p, vpNumber);
                }
                catch
                {
                    continue;
                }

                if (double.IsNaN(clientDip.X) || double.IsNaN(clientDip.Y) ||
                    double.IsInfinity(clientDip.X) || double.IsInfinity(clientDip.Y))
                    continue;

                var clientPx = new System.Drawing.Point(
                    (int)Math.Round(clientDip.X * dpiX),
                    (int)Math.Round(clientDip.Y * dpiY));

                if (!ClientToScreen(viewHwnd, ref clientPx))
                    continue;

                double sx = clientPx.X / dpiX;
                double sy = clientPx.Y / dpiY;

                okCount++;
                if (sx < minSx) minSx = sx;
                if (sy < minSy) minSy = sy;
                if (sx > maxSx) maxSx = sx;
                if (sy > maxSy) maxSy = sy;
            }

            if (okCount < 2 || maxSx <= minSx || maxSy <= minSy)
                return false;

            leftDip = minSx;
            topDip = minSy;
            widthDip = maxSx - minSx;
            heightDip = maxSy - minSy;

            // 行高 = 总屏幕高 / 行数；FontSize ≈ 行高 × 0.85（经验值，留行间距）
            double lineHeightDip = heightDip / Math.Max(1, lineCount);
            fontSizeDip = Math.Max(10.0, Math.Min(200.0, lineHeightDip * 0.85));

            return widthDip > 2 && heightDip > 2;
        }

        /// <summary>读"文字本身"的 WCS 高度。</summary>
        private static double TryReadTextHeight(Entity ent, Extents3d ext)
        {
            double fallback = Math.Max(Math.Abs(ext.MaxPoint.Y - ext.MinPoint.Y), 1.0);

            try
            {
                switch (ent)
                {
                    case MText mt:
                        return mt.TextHeight > 0 ? mt.TextHeight : fallback;
                    case DBText db:
                        return db.Height > 0 ? db.Height : fallback;
                    case MLeader ml:
                        return ml.TextHeight > 0 ? ml.TextHeight : fallback;
                }

                if (ent is Dimension dim)
                {
                    double h = 0;
                    double s = 1;
                    try { h = dim.Dimtxt; } catch { }
                    try { s = dim.Dimscale; } catch { }
                    if (h <= 0) h = 2.5;
                    if (s <= 0) s = 1.0;
                    return h * s;
                }
            }
            catch
            {
                /* 属性访问异常退 fallback */
            }

            return fallback;
        }

        /// <summary>估算行数：MText 用 Contents 中 \P 计数；MLeader 同理；其余视为 1 行。</summary>
        private static int EstimateLineCount(Entity ent)
        {
            try
            {
                string text = null;
                switch (ent)
                {
                    case MText mt: text = mt.Contents; break;
                    case MLeader ml: text = ml.MText?.Contents; break;
                }

                if (string.IsNullOrEmpty(text))
                    return 1;

                int n = 1;
                int idx = 0;
                while ((idx = text.IndexOf("\\P", idx, StringComparison.Ordinal)) >= 0)
                {
                    n++;
                    idx += 2;
                }
                return n;
            }
            catch
            {
                return 1;
            }
        }
    }
}
