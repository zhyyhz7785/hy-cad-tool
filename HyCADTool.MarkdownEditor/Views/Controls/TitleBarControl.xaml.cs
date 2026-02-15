using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HyCADTool.MarkdownEditor.Views.Controls
{
    public enum WorkspaceMode
    {
        Writing,
        Layout,
        Proofread
    }

    public class WorkspaceModeEventArgs : EventArgs
    {
        public WorkspaceMode Mode { get; }
        public WorkspaceModeEventArgs(WorkspaceMode mode) => Mode = mode;
    }

    public class ScreenPointEventArgs : EventArgs
    {
        public Point ScreenPoint { get; }
        public ScreenPointEventArgs(Point screenPoint) => ScreenPoint = screenPoint;
    }

    public partial class TitleBarControl : UserControl
    {
        public event EventHandler ThemeDarkRequested;
        public event EventHandler ThemeLightRequested;
        public event EventHandler ToggleRulerRequested;
        public event EventHandler ToggleOutlineRequested;
        public event EventHandler ToggleBottomPanelRequested;
        public event EventHandler TogglePreviewRequested;
        public event EventHandler ConfirmRequested;
        public event EventHandler MinimizeRequested;
        public event EventHandler MaxRestoreRequested;
        public event EventHandler CloseRequested;
        public event EventHandler DragMoveRequested;
        public event EventHandler<ScreenPointEventArgs> SystemMenuRequested;
        public event EventHandler<WorkspaceModeEventArgs> WorkspaceModeRequested;

        public TitleBarControl()
        {
            InitializeComponent();
        }

        public void UpdateToggleState(bool rulerVisible, bool outlineVisible, bool bottomPanelVisible, bool previewVisible, Brush primaryBrush, Brush secondaryBrush)
        {
            if (primaryBrush == null || secondaryBrush == null) return;
            RulerToggleBtn.Foreground = rulerVisible ? primaryBrush : secondaryBrush;
            OutlineToggleBtn.Foreground = outlineVisible ? primaryBrush : secondaryBrush;
            BottomPanelToggleBtn.Foreground = bottomPanelVisible ? primaryBrush : secondaryBrush;
            PreviewToggleBtn.Foreground = previewVisible ? primaryBrush : secondaryBrush;
        }

        public void UpdateWindowState(bool isMaximized)
        {
            MaxRestoreGlyph.Text = isMaximized ? "\uE923" : "\uE922";
            MaxRestoreWindowBtn.ToolTip = isMaximized ? "还原" : "最大化";
        }

        private void OnThemeDarkClick(object sender, RoutedEventArgs e) => ThemeDarkRequested?.Invoke(this, EventArgs.Empty);
        private void OnThemeLightClick(object sender, RoutedEventArgs e) => ThemeLightRequested?.Invoke(this, EventArgs.Empty);
        private void OnToggleRulerClick(object sender, RoutedEventArgs e) => ToggleRulerRequested?.Invoke(this, EventArgs.Empty);
        private void OnToggleOutlineClick(object sender, RoutedEventArgs e) => ToggleOutlineRequested?.Invoke(this, EventArgs.Empty);
        private void OnToggleBottomPanelClick(object sender, RoutedEventArgs e) => ToggleBottomPanelRequested?.Invoke(this, EventArgs.Empty);
        private void OnTogglePreviewClick(object sender, RoutedEventArgs e) => TogglePreviewRequested?.Invoke(this, EventArgs.Empty);
        private void OnConfirmClick(object sender, RoutedEventArgs e) => ConfirmRequested?.Invoke(this, EventArgs.Empty);
        private void OnMinimizeWindowClick(object sender, RoutedEventArgs e) => MinimizeRequested?.Invoke(this, EventArgs.Empty);
        private void OnMaxRestoreWindowClick(object sender, RoutedEventArgs e) => MaxRestoreRequested?.Invoke(this, EventArgs.Empty);
        private void OnCloseClick(object sender, RoutedEventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
        private void OnWorkspaceWritingClick(object sender, RoutedEventArgs e) => WorkspaceModeRequested?.Invoke(this, new WorkspaceModeEventArgs(WorkspaceMode.Writing));
        private void OnWorkspaceLayoutClick(object sender, RoutedEventArgs e) => WorkspaceModeRequested?.Invoke(this, new WorkspaceModeEventArgs(WorkspaceMode.Layout));
        private void OnWorkspaceProofreadClick(object sender, RoutedEventArgs e) => WorkspaceModeRequested?.Invoke(this, new WorkspaceModeEventArgs(WorkspaceMode.Proofread));

        private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount == 2)
            {
                MaxRestoreRequested?.Invoke(this, EventArgs.Empty);
                return;
            }

            DragMoveRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnTitleBarMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Right) return;
            Point screenPoint = PointToScreen(e.GetPosition(this));
            SystemMenuRequested?.Invoke(this, new ScreenPointEventArgs(screenPoint));
        }
    }
}
