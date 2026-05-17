using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using HyCADTool.Features.TextEdit.Services;
using HyCADTool.Features.TextEdit.ViewModels;

namespace HyCADTool.Features.TextEdit.Views
{
    /// <summary>
    /// 覆盖在绘图区上的透明原位编辑层（无 IPE）。
    /// 非模态 <c>Show()</c>——左键点 AutoCAD → 失焦 → 提交。
    /// <para>
    /// 焦点博弈：<c>_HYED_INTERNAL</c> 命令一收尾，AutoCAD 命令行会反复抢焦点，
    /// WPF <see cref="Window.Deactivated"/> 会被这股"伪失焦"立刻触发。
    /// 解法：开 400ms 延迟门，期间任何 <c>Deactivated</c> 都不提交、并把焦点抢回；
    /// 过门后才允许"真失焦=提交"（用户左键点了 AutoCAD 画布的语义）。
    /// </para>
    /// </summary>
    public partial class HyEdInPlaceWindow
    {
        private readonly bool _singleLine;
        private bool _closed;
        private bool _canAcceptDeactivate;
        private bool _cancelRequested;
        private DispatcherTimer _activateGuardTimer;

        public bool? Result { get; private set; }

        public HyEdInPlaceWindow(HyEdDialogViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            _singleLine = vm.Kind == HyEdEntityKind.DBText;
            if (_singleLine)
            {
                BodyBox.AcceptsReturn = false;
                BodyBox.TextWrapping = TextWrapping.NoWrap;
            }

            Loaded += OnLoaded;
            Deactivated += OnDeactivated;
            PreviewKeyDown += OnPreviewKeyDown;
            Closed += OnClosedCleanup;
            ResizeThumb.DragDelta += OnResizeThumbDragDelta;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            BodyBox.Focus();
            BodyBox.SelectAll();

            // AutoCAD _HYED_INTERNAL 命令收尾会把焦点拽回主窗口/命令行；
            // 必须在命令收尾的下一个 dispatcher 帧再 Activate + Focus 一次。
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    Activate();
                    BodyBox.Focus();
                    BodyBox.SelectAll();
                }
                catch { /* 偶发 */ }
            }), DispatcherPriority.Background);

            // 延迟 400ms 才允许"失焦=提交"。期间命令收尾的伪失焦由 OnDeactivated 抢回焦点处理。
            _activateGuardTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _activateGuardTimer.Tick += (s, ev) =>
            {
                _activateGuardTimer?.Stop();
                _canAcceptDeactivate = true;
            };
            _activateGuardTimer.Start();
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            if (_closed || _cancelRequested)
                return;

            if (!_canAcceptDeactivate)
            {
                // 命令收尾的伪失焦：强制把焦点拽回 TextBox，并保留延迟门继续生效。
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_closed) return;
                    try
                    {
                        Activate();
                        BodyBox.Focus();
                    }
                    catch { /* 偶发 */ }
                }), DispatcherPriority.Input);
                return;
            }

            // 真失焦：用户在 AutoCAD 画布或其它窗口按下了左键。
            CloseSafe(true);
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_closed)
                return;

            // 用户开始操作时，立即开放"失焦=提交"门（避免延迟内点外部失败）。
            _canAcceptDeactivate = true;

            if (e.Key == Key.Escape)
            {
                _cancelRequested = true;
                CloseSafe(false);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab)
            {
                CloseSafe(true);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CloseSafe(true);
                e.Handled = true;
                return;
            }

            if (_singleLine && e.Key == Key.Enter)
            {
                CloseSafe(true);
                e.Handled = true;
            }
        }

        private void CloseSafe(bool? result)
        {
            if (_closed)
                return;
            _closed = true;
            Result = result;
            try { Close(); } catch { /* 关闭异常忽略 */ }
        }

        private void OnClosedCleanup(object sender, EventArgs e)
        {
            try { _activateGuardTimer?.Stop(); } catch { }
            _activateGuardTimer = null;
        }

        /// <summary>
        /// 右下角 Thumb 拖拽：调整窗口宽高。WindowStyle=None+AllowsTransparency 下系统边框不可用，
        /// 必须自己实现拖拉调整。
        /// </summary>
        private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
        {
            double w = Width + e.HorizontalChange;
            double h = Height + e.VerticalChange;
            if (w < MinWidth) w = MinWidth;
            if (h < MinHeight) h = MinHeight;
            Width = w;
            Height = h;
        }
    }
}
