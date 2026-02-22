using System;
using System.Threading.Tasks;
using HyCADTool.MarkdownEditor.Views.Controls;

namespace HyCADTool.MarkdownEditor.Services
{
    internal sealed class EditorUiEventRouter
    {
        private readonly TitleBarControl _titleBar;
        private readonly LeftPanelControl _leftPanel;
        private readonly PreviewPanelControl _previewPanel;

        public EditorUiEventRouter(
            TitleBarControl titleBar,
            LeftPanelControl leftPanel,
            PreviewPanelControl previewPanel)
        {
            _titleBar = titleBar;
            _leftPanel = leftPanel;
            _previewPanel = previewPanel;
        }

        public void Wire(EditorUiEventHandlers handlers)
        {
            _titleBar.ThemeDarkRequested += (_, __) => handlers.OnThemeDark();
            _titleBar.ThemeLightRequested += (_, __) => handlers.OnThemeLight();
            _titleBar.ToggleRulerRequested += (_, __) => handlers.OnToggleRuler();
            _titleBar.ToggleOutlineRequested += (_, __) => handlers.OnToggleOutline();
            _titleBar.ToggleBottomPanelRequested += (_, __) => handlers.OnToggleBottomPanel();
            _titleBar.TogglePreviewRequested += (_, __) => handlers.OnTogglePreview();
            _titleBar.SettingsRequested += (_, __) => handlers.OnOpenSettingsPanel();
            _titleBar.InsertCadRequested += async (_, __) => await handlers.OnInsertCadClickAsync();
            _titleBar.ConfirmRequested += async (_, __) => await handlers.OnConfirmClickAsync();
            _titleBar.MinimizeRequested += (_, __) => handlers.OnMinimizeRequested();
            _titleBar.MaxRestoreRequested += (_, __) => handlers.OnMaxRestoreRequested();
            _titleBar.CloseRequested += (_, __) => handlers.OnCloseRequested();
            _titleBar.WorkspaceModeRequested += (_, e) => handlers.OnWorkspaceModeRequested(e.Mode);
            _titleBar.DragMoveRequested += (_, __) => handlers.OnDragMoveRequested();
            _titleBar.SystemMenuRequested += (_, e) => handlers.OnSystemMenuRequested(e);

            _leftPanel.FileOpenRequested += (_, path) => handlers.OnFileOpenRequested(path);
            _leftPanel.OutlineHeadingSelected += async (_, heading) => await handlers.OnOutlineHeadingSelectedAsync(heading);
            _leftPanel.StatusChanged += (_, status) => handlers.OnLeftPanelStatusChanged(status);
            _leftPanel.CurrentFilePathChanged += (_, e) => handlers.OnCurrentFilePathChanged(e.NewPath);
            _leftPanel.CurrentFileClearedRequested += (_, __) => handlers.OnCurrentFileClearedRequested();

            _previewPanel.TogglePageOrientationRequested += (_, __) => handlers.OnTogglePageOrientationRequested();
            _previewPanel.ToggleColumnHeightSyncRequested += (_, __) => handlers.OnToggleColumnHeightSyncRequested();
            _previewPanel.ResetLayoutRequested += async (_, __) => await handlers.OnResetLayoutRequestedAsync();
            _previewPanel.PreviousPageRequested += async (_, __) => await handlers.OnPreviousPageRequestedAsync();
            _previewPanel.NextPageRequested += async (_, __) => await handlers.OnNextPageRequestedAsync();
            _previewPanel.JumpPageRequested += async (_, page) => await handlers.OnJumpPageRequestedAsync(page);
        }
    }

    internal sealed class EditorUiEventHandlers
    {
        public Action OnThemeDark { get; init; } = () => { };
        public Action OnThemeLight { get; init; } = () => { };
        public Action OnToggleRuler { get; init; } = () => { };
        public Action OnToggleOutline { get; init; } = () => { };
        public Action OnToggleBottomPanel { get; init; } = () => { };
        public Action OnTogglePreview { get; init; } = () => { };
        public Action OnOpenSettingsPanel { get; init; } = () => { };
        public Func<Task> OnInsertCadClickAsync { get; init; } = () => Task.CompletedTask;
        public Func<Task> OnConfirmClickAsync { get; init; } = () => Task.CompletedTask;
        public Action OnMinimizeRequested { get; init; } = () => { };
        public Action OnMaxRestoreRequested { get; init; } = () => { };
        public Action OnCloseRequested { get; init; } = () => { };
        public Action<WorkspaceMode> OnWorkspaceModeRequested { get; init; } = _ => { };
        public Action OnDragMoveRequested { get; init; } = () => { };
        public Action<ScreenPointEventArgs> OnSystemMenuRequested { get; init; } = _ => { };
        public Action<string> OnFileOpenRequested { get; init; } = _ => { };
        public Func<string, Task> OnOutlineHeadingSelectedAsync { get; init; } = _ => Task.CompletedTask;
        public Action<string> OnLeftPanelStatusChanged { get; init; } = _ => { };
        public Action<string> OnCurrentFilePathChanged { get; init; } = _ => { };
        public Action OnCurrentFileClearedRequested { get; init; } = () => { };
        public Action OnTogglePageOrientationRequested { get; init; } = () => { };
        public Action OnToggleColumnHeightSyncRequested { get; init; } = () => { };
        public Func<Task> OnResetLayoutRequestedAsync { get; init; } = () => Task.CompletedTask;
        public Func<Task> OnPreviousPageRequestedAsync { get; init; } = () => Task.CompletedTask;
        public Func<Task> OnNextPageRequestedAsync { get; init; } = () => Task.CompletedTask;
        public Func<int, Task> OnJumpPageRequestedAsync { get; init; } = _ => Task.CompletedTask;
    }
}
