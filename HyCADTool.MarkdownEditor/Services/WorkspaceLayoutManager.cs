using System;
using System.Windows;
using System.Windows.Controls;
using HyCADTool.MarkdownEditor.Views.Controls;

namespace HyCADTool.MarkdownEditor.Services
{
    internal sealed record LayoutTargets(
        ColumnDefinition OutlineCol,
        ColumnDefinition OutlineSplitterCol,
        ColumnDefinition EditorCol,
        ColumnDefinition SplitterCol,
        ColumnDefinition PreviewCol,
        RowDefinition BottomPanelSplitterRow,
        RowDefinition BottomPanelRow,
        GridSplitter OutlineSplitter,
        GridSplitter PreviewSplitter,
        GridSplitter BottomPanelSplitter,
        FrameworkElement EditorWebView,
        FrameworkElement BottomPanelHost,
        PreviewPanelControl PreviewPanel);

    internal readonly record struct WorkspaceModeApplyResult(
        bool EditorBecameVisible,
        bool ShouldRefreshPreview,
        string StatusText);

    internal sealed class WorkspaceLayoutManager
    {
        private const bool DefaultPaperPrimaryMode = false;
        private const double MinPreviewVisibleWidth = 280;
        private const double DefaultPreviewPanelWidth = 420;
        private const double MinOutlinePanelWidth = 120;
        private const double DefaultOutlinePanelWidth = 220;
        private const double OutlineSplitterWidth = 4;
        private const double RulerThickness = 24;
        private const double DefaultEditorMinWidth = 180;
        private const double DefaultRatioLeft = 1.0;
        private const double DefaultRatioEditor = 2.0;
        private const double DefaultRatioPreview = 4.0;

        private readonly LayoutTargets _targets;
        private readonly Action _updateTitleBarToggleState;
        private readonly Action _updatePreviewPageState;
        private readonly Action<bool> _setRulerSyncRunning;

        private bool _previewVisible = true;
        private bool _outlineVisible = true;
        private bool _bottomPanelVisible = true;
        private bool _rulerVisible = true;
        private bool _editorVisible = true;
        private bool _paperPrimaryEditMode = DefaultPaperPrimaryMode;
        private bool _defaultWorkspaceLayoutApplied;
        private double _bottomPanelHeight = 160;
        private double _outlinePanelWidth = DefaultOutlinePanelWidth;
        private double _previewPanelWidth = DefaultPreviewPanelWidth;
        private double _editorPanelWidth;

        public WorkspaceLayoutManager(
            LayoutTargets targets,
            Action updateTitleBarToggleState,
            Action updatePreviewPageState,
            Action<bool> setRulerSyncRunning)
        {
            _targets = targets ?? throw new ArgumentNullException(nameof(targets));
            _updateTitleBarToggleState = updateTitleBarToggleState ?? throw new ArgumentNullException(nameof(updateTitleBarToggleState));
            _updatePreviewPageState = updatePreviewPageState ?? throw new ArgumentNullException(nameof(updatePreviewPageState));
            _setRulerSyncRunning = setRulerSyncRunning ?? throw new ArgumentNullException(nameof(setRulerSyncRunning));
        }

        public bool IsPreviewVisible => _previewVisible;
        public bool IsOutlineVisible => _outlineVisible;
        public bool IsBottomPanelVisible => _bottomPanelVisible;
        public bool IsRulerVisible => _rulerVisible;
        public bool IsEditorVisible => _editorVisible;
        public bool IsPaperPrimaryEditMode => _paperPrimaryEditMode;

        public void ToggleOutline()
        {
            _outlineVisible = !_outlineVisible;
            ApplyOutlineLayout();
        }

        public void ToggleBottomPanel()
        {
            _bottomPanelVisible = !_bottomPanelVisible;
            ApplyBottomPanelLayout();
            _updateTitleBarToggleState();
        }

        public void ShowSettingsOutline()
        {
            if (_outlineVisible)
                return;

            _outlineVisible = true;
            ApplyOutlineLayout();
        }

        public void TogglePreview()
        {
            _previewVisible = !_previewVisible;
            if (!_previewVisible && !_editorVisible)
            {
                _paperPrimaryEditMode = false;
                _editorVisible = true;
            }

            ApplyPreviewLayout();
            ApplyEditorLayout();
        }

        public void ToggleRuler()
        {
            _rulerVisible = !_rulerVisible;
            ApplyRulerLayout();
            _updateTitleBarToggleState();
        }

        public WorkspaceModeApplyResult ApplyWorkspaceMode(WorkspaceMode mode, bool editorReady)
        {
            bool wasEditorVisible = _editorVisible;
            string statusText;

            switch (mode)
            {
                case WorkspaceMode.Writing:
                    _outlineVisible = true;
                    _previewVisible = false;
                    _bottomPanelVisible = false;
                    _rulerVisible = false;
                    _paperPrimaryEditMode = false;
                    _editorVisible = true;
                    statusText = "视图：写作模式（编辑主导）";
                    break;

                case WorkspaceMode.Layout:
                    _outlineVisible = false;
                    _previewVisible = true;
                    _bottomPanelVisible = true;
                    _rulerVisible = true;
                    _paperPrimaryEditMode = true;
                    _editorVisible = false;
                    statusText = "视图：排版模式（图纸主编辑）";
                    break;

                case WorkspaceMode.Proofread:
                default:
                    _outlineVisible = true;
                    _previewVisible = true;
                    _rulerVisible = false;
                    _paperPrimaryEditMode = false;
                    _editorVisible = true;
                    statusText = "视图：校对模式（双面板）";
                    break;
            }

            if (!_previewVisible && !_editorVisible)
                _editorVisible = true;

            ApplyOutlineLayout();
            ApplyPreviewLayout();
            ApplyEditorLayout();
            ApplyBottomPanelLayout();
            ApplyRulerLayout();

            bool editorBecameVisible = _editorVisible && !wasEditorVisible && editorReady;
            return new WorkspaceModeApplyResult(editorBecameVisible, _previewVisible, statusText);
        }

        public void EnsurePreviewColumnVisible()
        {
            if (!_previewVisible || _targets.PreviewCol == null)
                return;

            bool tooNarrowByActual = _targets.PreviewCol.ActualWidth > 0 && _targets.PreviewCol.ActualWidth < MinPreviewVisibleWidth;
            bool collapsed = _targets.PreviewCol.Width.Value <= 0;
            if (tooNarrowByActual || collapsed)
            {
                double target = Math.Max(MinPreviewVisibleWidth, _previewPanelWidth);
                _targets.PreviewCol.Width = new GridLength(target, GridUnitType.Pixel);
            }
        }

        public void ApplyOutlineLayout()
        {
            if (_targets.OutlineCol == null || _targets.OutlineSplitterCol == null || _targets.OutlineSplitter == null)
                return;

            if (_outlineVisible)
            {
                _targets.OutlineCol.Width = new GridLength(Math.Max(MinOutlinePanelWidth, _outlinePanelWidth), GridUnitType.Pixel);
                _targets.OutlineSplitterCol.Width = new GridLength(OutlineSplitterWidth, GridUnitType.Pixel);
                _targets.OutlineSplitter.Visibility = Visibility.Visible;
            }
            else
            {
                if (_targets.OutlineCol.ActualWidth > 0)
                    _outlinePanelWidth = Math.Max(MinOutlinePanelWidth, _targets.OutlineCol.ActualWidth);

                _targets.OutlineCol.Width = new GridLength(0, GridUnitType.Pixel);
                _targets.OutlineSplitterCol.Width = new GridLength(0, GridUnitType.Pixel);
                _targets.OutlineSplitter.Visibility = Visibility.Collapsed;
            }

            _updateTitleBarToggleState();
            _updatePreviewPageState();
        }

        public void ApplyPreviewLayout()
        {
            if (_targets.SplitterCol == null || _targets.PreviewCol == null || _targets.PreviewSplitter == null || _targets.PreviewPanel == null)
                return;

            if (_previewVisible)
            {
                _targets.SplitterCol.Width = new GridLength(4, GridUnitType.Pixel);
                _targets.PreviewCol.MinWidth = MinPreviewVisibleWidth;
                _targets.PreviewCol.Width = new GridLength(Math.Max(MinPreviewVisibleWidth, _previewPanelWidth), GridUnitType.Pixel);
                _targets.PreviewSplitter.Visibility = Visibility.Visible;
                _targets.PreviewPanel.Visibility = Visibility.Visible;
                _setRulerSyncRunning(true);
                EnsurePreviewColumnVisible();
                ApplyRulerLayout();
                ApplyBottomPanelLayout();
            }
            else
            {
                if (_targets.PreviewCol.ActualWidth > 0)
                    _previewPanelWidth = Math.Max(MinPreviewVisibleWidth, _targets.PreviewCol.ActualWidth);

                _targets.SplitterCol.Width = new GridLength(0);
                _targets.PreviewCol.MinWidth = 0;
                _targets.PreviewCol.Width = new GridLength(0);
                _targets.PreviewSplitter.Visibility = Visibility.Collapsed;
                _targets.PreviewPanel.Visibility = Visibility.Collapsed;
                _setRulerSyncRunning(false);
                ApplyRulerLayout();
                ApplyBottomPanelLayout();
            }

            _updateTitleBarToggleState();
        }

        public void ApplyEditorLayout()
        {
            if (_targets.EditorCol == null || _targets.EditorWebView == null)
                return;

            if (_editorVisible)
            {
                _targets.EditorCol.MinWidth = DefaultEditorMinWidth;
                if (_targets.EditorCol.Width.Value <= 0)
                {
                    double target = _editorPanelWidth > 0 ? _editorPanelWidth : DefaultEditorMinWidth * 2.0;
                    _targets.EditorCol.Width = new GridLength(target, GridUnitType.Pixel);
                }

                _targets.EditorWebView.Visibility = Visibility.Visible;
            }
            else
            {
                if (_targets.EditorCol.ActualWidth > 0)
                    _editorPanelWidth = Math.Max(DefaultEditorMinWidth, _targets.EditorCol.ActualWidth);

                _targets.EditorCol.MinWidth = 0;
                _targets.EditorCol.Width = new GridLength(0, GridUnitType.Pixel);
                _targets.EditorWebView.Visibility = Visibility.Collapsed;
            }
        }

        public void ApplyRulerLayout()
        {
            if (_targets.PreviewPanel?.RulerRowDefinition == null || _targets.PreviewPanel.RulerColumnDefinition == null)
                return;

            bool show = _previewVisible && _rulerVisible;
            _targets.PreviewPanel.RulerRowDefinition.Height = show
                ? new GridLength(RulerThickness, GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);
            _targets.PreviewPanel.RulerColumnDefinition.Width = show
                ? new GridLength(RulerThickness, GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);

            var rulerVisibility = show ? Visibility.Visible : Visibility.Collapsed;
            _targets.PreviewPanel.RulerCornerElement.Visibility = rulerVisibility;
            _targets.PreviewPanel.HorizontalRuler.Visibility = rulerVisibility;
            _targets.PreviewPanel.VerticalRuler.Visibility = rulerVisibility;
            _updateTitleBarToggleState();
        }

        public void ApplyBottomPanelLayout()
        {
            bool show = _bottomPanelVisible && _previewVisible;
            _targets.BottomPanelSplitterRow.Height = show
                ? new GridLength(4, GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);
            _targets.BottomPanelRow.Height = show
                ? new GridLength(Math.Max(80, _bottomPanelHeight), GridUnitType.Pixel)
                : new GridLength(0, GridUnitType.Pixel);
            _targets.BottomPanelSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            _targets.BottomPanelHost.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ApplyDefaultWorkspaceLayout(bool force, double actualWidth, double width)
        {
            if (_defaultWorkspaceLayoutApplied && !force)
                return;
            if (_targets.OutlineCol == null || _targets.EditorCol == null || _targets.PreviewCol == null || _targets.SplitterCol == null || _targets.PreviewSplitter == null)
                return;

            double ratioSum = DefaultRatioLeft + DefaultRatioEditor + DefaultRatioPreview;
            double windowWidth = actualWidth > 0 ? actualWidth : width;
            double minRequired = MinOutlinePanelWidth + DefaultEditorMinWidth + MinPreviewVisibleWidth;
            double splitterReserve = OutlineSplitterWidth + 4 + 24;
            double available = Math.Max(minRequired, windowWidth - splitterReserve);
            double unit = available / ratioSum;

            _outlinePanelWidth = Math.Max(MinOutlinePanelWidth, unit * DefaultRatioLeft);
            double editorMinWidth = Math.Max(DefaultEditorMinWidth, unit * DefaultRatioEditor * 0.55);
            _previewPanelWidth = Math.Max(MinPreviewVisibleWidth, unit * DefaultRatioPreview);
            _bottomPanelHeight = Math.Max(_bottomPanelHeight, 160);

            _outlineVisible = false;
            _previewVisible = true;
            _bottomPanelVisible = true;
            _rulerVisible = true;
            _paperPrimaryEditMode = DefaultPaperPrimaryMode;
            _editorVisible = !_paperPrimaryEditMode;

            ApplyOutlineLayout();
            ApplyPreviewLayout();
            _editorPanelWidth = Math.Max(editorMinWidth, unit * DefaultRatioEditor);
            ApplyEditorLayout();
            if (_editorVisible)
            {
                _targets.EditorCol.MinWidth = editorMinWidth;
                if (_targets.EditorCol.Width.Value <= 0)
                    _targets.EditorCol.Width = new GridLength(1, GridUnitType.Star);
            }

            ApplyBottomPanelLayout();
            _defaultWorkspaceLayoutApplied = true;
        }

        public void OnOutlineSplitterDragCompleted()
        {
            if (_targets.OutlineCol?.ActualWidth > 0)
                _outlinePanelWidth = Math.Max(MinOutlinePanelWidth, _targets.OutlineCol.ActualWidth);
        }

        public void OnPreviewSplitterDragCompleted()
        {
            if (_targets.PreviewCol?.ActualWidth > 0)
            {
                _previewPanelWidth = Math.Max(MinPreviewVisibleWidth, _targets.PreviewCol.ActualWidth);
                _targets.PreviewCol.Width = new GridLength(_previewPanelWidth, GridUnitType.Pixel);
            }
        }

        public void OnBottomPanelSplitterDragCompleted()
        {
            if (_targets.BottomPanelRow.ActualHeight > 0)
                _bottomPanelHeight = _targets.BottomPanelRow.ActualHeight;
        }
    }
}
