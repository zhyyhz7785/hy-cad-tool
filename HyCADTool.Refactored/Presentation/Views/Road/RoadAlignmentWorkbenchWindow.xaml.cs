using System;
using System.Windows;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Refactored.Presentation.ViewModels.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// 路线工作台独立 WPF 窗口（非模态，<c>Application.ShowModelessWindow</c>）。
    /// 承载 <see cref="RoadAlignmentWorkbenchPanel"/>；ViewModel 生命周期由面板
    /// <see cref="RoadAlignmentWorkbenchPanel"/> 的 Unloaded 路径释放。
    ///
    /// 默认停靠策略（2026-04-21）：
    /// - <c>WindowStartupLocation = Manual</c>，首次显示时在 <see cref="SourceInitialized"/>
    ///   读 <see cref="AcApp.MainWindow"/> 的 DIP 位置 / 尺寸，把窗口贴到主窗口底部 —— 绘图区正下方、
    ///   AutoCAD 命令行上方，左右留出少量边距。
    /// - 用户拖动后不再强制吸附；只有第一次开窗才自动停靠。
    /// </summary>
    public partial class RoadAlignmentWorkbenchWindow : BlenderWindow
    {
        /// <summary>AutoCAD 底部命令行 + 状态栏预留像素（DIP）。经验值，覆盖命令行默认 3 行 + 状态栏。</summary>
        private const double CommandLineReservePx = 110;

        /// <summary>左右两侧的可视留白，避免紧贴主窗口边缘。</summary>
        private const double SideMarginPx = 24;

        public RoadAlignmentWorkbenchWindow(RoadAlignmentWorkbenchViewModel viewModel)
        {
            InitializeComponent();
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            WorkbenchHost.ViewModel = viewModel;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            TryDockToCadBottom();
        }

        /// <summary>
        /// 把窗口按 AutoCAD 主窗口的 DIP 坐标贴到底部（保持 XAML 里设置的 Height）。
        /// 任一步失败都静默跳过，不影响窗口本身打开。
        /// </summary>
        private void TryDockToCadBottom()
        {
            try
            {
                var main = AcApp.MainWindow;
                if (main == null) return;

                var loc = main.DeviceIndependentLocation;   // DIP 左上角
                var size = main.DeviceIndependentSize;      // DIP 尺寸
                if (size.Width <= 0 || size.Height <= 0) return;

                double desiredWidth = Math.Max(MinWidth, size.Width - SideMarginPx * 2);
                double desiredHeight = Math.Min(Height, Math.Max(MinHeight, size.Height * 0.5));

                Width = desiredWidth;
                Height = desiredHeight;
                Left = loc.X + SideMarginPx;
                Top = loc.Y + size.Height - CommandLineReservePx - desiredHeight;

                // 防止主窗口非常矮时算出负坐标
                if (Top < loc.Y + 40) Top = loc.Y + 40;
            }
            catch
            {
                // 在极少数 AcApp.MainWindow 不可用的时序下静默跳过
            }
        }
    }
}
