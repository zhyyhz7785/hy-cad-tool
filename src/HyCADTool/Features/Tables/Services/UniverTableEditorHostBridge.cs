using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Tables.Presentation;
using HyCADTool.Features.Tables.ViewModels;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.Features.Tables.Services
{
    /// <summary>
    /// 将 TableEditorViewModel 桥接到 UniverEditor WebView2 宿主。
    /// </summary>
    public sealed class UniverTableEditorHostBridge : IDisposable
    {
        private const int ExportTimeoutMs = 3000;

        private readonly TableEditorViewModel _viewModel;
        private bool _suppressPush;
        private Action _pendingAfterSnapshot;
        private bool _pendingRequiresCadInteraction;
        private UniverPublishExportMode _pendingExportMode = UniverPublishExportMode.Default;
        private Action _exportSnapshotHandler;
        private Action<string> _exportForPublishWebHandler;
        private Action<UniverPublishExportMode> _exportForPublishHandler;
        private Timer _exportTimeoutTimer;
        private readonly object _exportSync = new object();
        private bool _publishAfterSnapshot;

        public UniverTableEditorHostBridge(TableEditorViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        public TableEditorViewModel ViewModel => _viewModel;

        public event Action GridChanged;

        public event Action RequestExportSnapshot;

        public event Action<UniverPublishExportMode> RequestExportSnapshotForPublish;

        public event Action StatusChanged;

        public Action PrepareForCadInteraction { get; set; }

        public Action RestoreAfterCadInteraction { get; set; }

        private Action _gridChangedForward;

        public void SetGridChangedForward(Action forward)
        {
            if (_gridChangedForward != null)
                GridChanged -= _gridChangedForward;

            _gridChangedForward = forward;
            if (_gridChangedForward != null)
                GridChanged += _gridChangedForward;
        }

        public void SetExportSnapshotHandler(Action handler)
        {
            _exportSnapshotHandler = handler;
        }

        public void SetExportForPublishHandler(Action<UniverPublishExportMode> handler)
        {
            _exportForPublishHandler = handler;
        }

        public void SetExportForPublishWebHandler(Action<string> handler)
        {
            _exportForPublishWebHandler = handler;
        }

        public void CancelExportPending(string message)
        {
            ClearExportTimeout();
            ResetExportState();

            if (!string.IsNullOrWhiteSpace(message))
                _viewModel.SetStatusMessage(message);

            EndCadInteraction();
            NotifyStatusChanged();
        }

        public string BuildLoadSnapshotJson()
        {
            var grid = _viewModel.EditorGrid;
            return grid == null ? null : UniverGridSnapshotMapper.ToJson(grid);
        }

        public void OnCellChanged(int row, int col, string text)
        {
            if (!_viewModel.HasTable)
                return;

            var grid = _viewModel.EditorGrid;
            if (grid == null)
                return;

            var addr = new HyCAD.Tables.Structure.CellAddr(row, col);
            var anchor = grid.Structure.GetAnchorOf(addr);
            _viewModel.CommitCell(anchor, text ?? string.Empty);
        }

        public void NewEmptyTable()
        {
            _viewModel.NewEmptyTable();
            NotifyStatusChanged();
        }

        public void LoadPersonnelSample()
        {
            _viewModel.LoadSample();
            NotifyStatusChanged();
        }

        public void RequestPick()
        {
            BeginCadInteraction();
            _viewModel.RequestPick();
        }

        public void RequestPublish()
        {
            PullSnapshotForDefaultPublish();
        }

        public void RequestPublishRangeFull()
        {
            PullSnapshotForRangePublish(UniverPublishExportMode.RangeFull);
        }

        public void RequestPublishRangeContent()
        {
            PullSnapshotForRangePublish(UniverPublishExportMode.RangeContent);
        }

        public void ExportJsonSnapshot()
        {
            _pendingExportMode = UniverPublishExportMode.Default;
            PullSnapshotFromUniverThen(() => RunFileDialog(() =>
            {
                var grid = _viewModel.EditorGrid;
                if (grid == null)
                {
                    _viewModel.SetStatusMessage("请先加载表格");
                    NotifyStatusChanged();
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "JSON 快照 (*.json)|*.json",
                    FileName = "hytable-snapshot.json",
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                var json = JsonConvert.SerializeObject(
                    UniverGridSnapshotMapper.FromTableGrid(grid),
                    Formatting.Indented);
                File.WriteAllText(dialog.FileName, json);
                _viewModel.SetStatusMessage("已导出 JSON：" + dialog.FileName);
                NotifyStatusChanged();
            }));
        }

        public void ImportXlsx()
        {
            RunFileDialog(() =>
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                try
                {
                    var grid = TableGridXlsxAdapter.Import(
                        dialog.FileName,
                        _viewModel.DefaultRowHeightMm,
                        _viewModel.DefaultColWidthMm);
                    _suppressPush = true;
                    try
                    {
                        _viewModel.ApplyExternalGrid(grid, null);
                    }
                    finally
                    {
                        _suppressPush = false;
                    }

                    GridChanged?.Invoke();
                    _viewModel.SetStatusMessage("已导入 xlsx：" + dialog.FileName);
                    NotifyStatusChanged();
                }
                catch (Exception ex)
                {
                    _viewModel.SetStatusMessage("导入 xlsx 失败：" + ex.Message);
                    NotifyStatusChanged();
                }
            });
        }

        public void ExportXlsx()
        {
            _pendingExportMode = UniverPublishExportMode.Default;
            PullSnapshotFromUniverThen(() => RunFileDialog(() =>
            {
                var grid = _viewModel.EditorGrid;
                if (grid == null)
                {
                    _viewModel.SetStatusMessage("请先加载表格");
                    NotifyStatusChanged();
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
                    FileName = "hytable-export.xlsx",
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                try
                {
                    TableGridXlsxAdapter.Export(grid, dialog.FileName);
                    _viewModel.SetStatusMessage("已导出 xlsx：" + dialog.FileName);
                    NotifyStatusChanged();
                }
                catch (Exception ex)
                {
                    _viewModel.SetStatusMessage("导出 xlsx 失败：" + ex.Message);
                    NotifyStatusChanged();
                }
            }));
        }

        public string GetSummaryText() => _viewModel.SummaryText;

        public string GetStatusMessage() => _viewModel.StatusMessage;

        /// <summary>读取当前落图比例（口径 B）。</summary>
        public double GetScale() => _viewModel.Scale;

        /// <summary>布局 Tab 改比例：仅改 VM.Scale，不动 grid、不回灌。</summary>
        public void SetScale(double scale)
        {
            _viewModel.Scale = scale;
            NotifyStatusChanged();
        }

        public bool GetStructureMode() => _viewModel.IsStructureMode;

        public string GetViewportJson()
        {
            var paperIndex = _viewModel.PaperPresetOptions.IndexOf(_viewModel.SelectedPaperPresetOption);
            if (paperIndex < 0)
                paperIndex = 1;

            var templateIndex = _viewModel.TemplateOptions.IndexOf(_viewModel.SelectedTemplate);
            if (templateIndex < 0)
                templateIndex = 0;

            var obj = new JObject
            {
                ["paperPresetIndex"] = paperIndex,
                ["orientation"] = (int)_viewModel.PaperOrientation,
                ["targetWidthMm"] = _viewModel.TargetWidthMm,
                ["marginMm"] = _viewModel.MarginMm,
                ["rowCount"] = _viewModel.RowCount,
                ["colCount"] = _viewModel.ColCount,
                ["templateIndex"] = templateIndex,
                ["structureMode"] = _viewModel.IsStructureMode,
            };
            return obj.ToString(Newtonsoft.Json.Formatting.None);
        }

        /// <summary>网页自动调整行/列高宽后回写 Domain（axis: row=true/col=false）。</summary>
        public void SetTrackSizesMm(bool isRow, int startIndex, double[] sizesMm)
        {
            if (sizesMm == null || sizesMm.Length == 0)
                return;

            _viewModel.ApplyTrackSizesBatch(isRow, startIndex, sizesMm);
            NotifyStatusChanged();
        }

        /// <summary>网页选区变化 → 同步到 VM 选区（布局/尺寸操作以此为准）。</summary>
        public void OnSelectionChanged(int startRow, int startCol, int endRow, int endCol)
        {
            _viewModel.SetSelectedRange(startRow, startCol, endRow, endCol);
        }

        /// <summary>
        /// 布局 Tab 行列/合并/尺寸操作。结构变更经 VM 命令 → GridRevision++ → GridChanged 回灌。
        /// 尺寸操作作用于当前选区（须先 OnSelectionChanged）。
        /// </summary>
        public void OnLayoutOp(string op, double value)
        {
            if (string.IsNullOrEmpty(op))
                return;

            switch (op)
            {
                case "insertRow":
                    Execute(_viewModel.InsertRowCommand);
                    break;
                case "deleteRow":
                    Execute(_viewModel.DeleteRowCommand);
                    break;
                case "insertCol":
                    Execute(_viewModel.InsertColumnCommand);
                    break;
                case "deleteCol":
                    Execute(_viewModel.DeleteColumnCommand);
                    break;
                case "merge":
                    Execute(_viewModel.MergeSelectionCommand);
                    break;
                case "unmerge":
                    Execute(_viewModel.UnmergeCommand);
                    break;
                case "setRowHeight":
                    _viewModel.SelectedRowHeightMm = value;
                    break;
                case "setColWidth":
                    _viewModel.SelectedColWidthMm = value;
                    break;
                case "setStructureMode":
                    _viewModel.IsStructureMode = value > 0.5;
                    break;
                case "setRowCount":
                    _viewModel.RowCount = (int)value;
                    _viewModel.RegenerateToPaper(reseedCounts: false);
                    break;
                case "setColCount":
                    _viewModel.ColCount = (int)value;
                    _viewModel.RegenerateToPaper(reseedCounts: false);
                    break;
                case "setPaperPreset":
                    ApplyPaperPresetByIndex((int)value);
                    _viewModel.RegenerateToPaper(reseedCounts: true);
                    break;
                case "setOrientation":
                    _viewModel.PaperOrientation = value > 0.5
                        ? HyCAD.Tables.Layout.PaperOrientation.Portrait
                        : HyCAD.Tables.Layout.PaperOrientation.Landscape;
                    _viewModel.RegenerateToPaper(reseedCounts: true);
                    break;
                case "setTargetWidth":
                    _viewModel.TargetWidthMm = value;
                    _viewModel.RegenerateToPaper(reseedCounts: false);
                    break;
                case "setMargin":
                    _viewModel.MarginMm = value;
                    _viewModel.RegenerateToPaper(reseedCounts: true);
                    break;
                case "fitColumnsToPaper":
                    Execute(_viewModel.FitColumnsToPaperCommand);
                    break;
                case "setTemplate":
                    ApplyTemplateByIndex((int)value);
                    break;
                case "newTable":
                    _viewModel.NewEmptyTable();
                    NotifyStatusChanged();
                    break;
                case "loadTemplate":
                    _viewModel.LoadTemplate();
                    NotifyStatusChanged();
                    break;
            }
        }

        private void ApplyPaperPresetByIndex(int index)
        {
            var options = _viewModel.PaperPresetOptions;
            if (options == null || options.Count == 0)
                return;

            if (index < 0)
                index = 0;
            if (index >= options.Count)
                index = options.Count - 1;

            _viewModel.SelectedPaperPresetOption = options[index];
        }

        private void ApplyTemplateByIndex(int index)
        {
            var options = _viewModel.TemplateOptions;
            if (options == null || options.Count == 0)
                return;

            if (index < 0)
                index = 0;
            if (index >= options.Count)
                index = options.Count - 1;

            _viewModel.SelectedTemplate = options[index];
        }

        private static void Execute(System.Windows.Input.ICommand command)
        {
            if (command != null && command.CanExecute(null))
                command.Execute(null);
        }

        public void CompleteExportSnapshot(string json, string metaJson = null)
        {
            ClearExportTimeout();

            try
            {
                var exportMode = ResolveExportMode(metaJson);
                var snapshot = string.IsNullOrWhiteSpace(json) ? null : UniverGridSnapshotMapper.Parse(json);
                var clipRect = ParseClipRect(metaJson);

                if (exportMode == UniverPublishExportMode.RangeFull
                    || exportMode == UniverPublishExportMode.RangeContent)
                {
                    ResetExportState();

                    if (snapshot == null)
                    {
                        _viewModel.SetStatusMessage("exportSnapshot 返回空数据");
                        return;
                    }

                    BeginCadInteraction();
                    _viewModel.RequestPublishFromSnapshot(snapshot, clipRect, mutateEditor: false);
                    return;
                }

                TakeExportContinuation(out var publishOnly, out var pending, out var requiresCad);

                if (publishOnly)
                {
                    if (snapshot == null)
                    {
                        _viewModel.SetStatusMessage("exportSnapshot 返回空数据");
                        return;
                    }

                    BeginCadInteraction();
                    _viewModel.RequestPublishFromSnapshot(snapshot, clipRect: null, mutateEditor: false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(json))
                    _viewModel.SetStatusMessage("exportSnapshot 返回空数据，将使用内存表格落图");
                else
                    ApplySnapshotToEditorSilently(snapshot);

                if (pending == null)
                    return;

                if (requiresCad)
                    BeginCadInteraction();

                RunOnUi(pending);
            }
            catch (Exception ex)
            {
                ResetExportState();
                _viewModel.SetStatusMessage(ex.Message);
                EndCadInteraction();
            }
            finally
            {
                NotifyStatusChanged();
            }
        }

        private void PullSnapshotForDefaultPublish()
        {
            if (_exportSnapshotHandler == null && RequestExportSnapshot == null)
            {
                _viewModel.SetStatusMessage("Univer 未就绪，请等待加载完成");
                NotifyStatusChanged();
                return;
            }

            lock (_exportSync)
            {
                _pendingExportMode = UniverPublishExportMode.Default;
                _pendingAfterSnapshot = null;
                _pendingRequiresCadInteraction = true;
                _publishAfterSnapshot = true;
            }

            ClearExportTimeout();
            _exportTimeoutTimer = new Timer(_ => OnExportTimeout(), null, ExportTimeoutMs, Timeout.Infinite);

            if (_exportSnapshotHandler != null)
                _exportSnapshotHandler();
            else
                RequestExportSnapshot?.Invoke();
        }

        private void PullSnapshotForRangePublish(UniverPublishExportMode mode)
        {
            if (!TryInvokeExportForPublish(mode))
            {
                _viewModel.SetStatusMessage("Univer 未就绪，请等待加载完成");
                NotifyStatusChanged();
                return;
            }

            _pendingExportMode = mode;
            lock (_exportSync)
            {
                _pendingAfterSnapshot = null;
                _pendingRequiresCadInteraction = true;
                _publishAfterSnapshot = false;
            }
            ClearExportTimeout();
            _exportTimeoutTimer = new Timer(_ => OnExportTimeout(), null, ExportTimeoutMs, Timeout.Infinite);
        }

        private bool TryInvokeExportForPublish(UniverPublishExportMode mode)
        {
            if (_exportForPublishWebHandler != null)
            {
                _exportForPublishWebHandler(MapPublishModeToWeb(mode));
                return true;
            }

            if (_exportForPublishHandler != null)
            {
                _exportForPublishHandler(mode);
                return true;
            }

            if (RequestExportSnapshotForPublish != null)
            {
                RequestExportSnapshotForPublish.Invoke(mode);
                return true;
            }

            return false;
        }

        private static string MapPublishModeToWeb(UniverPublishExportMode mode)
        {
            switch (mode)
            {
                case UniverPublishExportMode.RangeFull:
                    return "full";
                case UniverPublishExportMode.RangeContent:
                    return "content";
                default:
                    return "default";
            }
        }

        private void PullSnapshotFromUniverThen(Action action, bool requiresCadInteraction = false)
        {
            if (_pendingExportMode == UniverPublishExportMode.Default
                && _exportSnapshotHandler == null
                && RequestExportSnapshot == null)
            {
                _viewModel.SetStatusMessage("Univer 未就绪，请等待加载完成");
                NotifyStatusChanged();
                return;
            }

            _pendingAfterSnapshot = action;
            lock (_exportSync)
            {
                _pendingRequiresCadInteraction = requiresCadInteraction;
                _publishAfterSnapshot = false;
            }
            ClearExportTimeout();
            _exportTimeoutTimer = new Timer(_ => OnExportTimeout(), null, ExportTimeoutMs, Timeout.Infinite);

            if (_pendingExportMode == UniverPublishExportMode.Default)
            {
                if (_exportSnapshotHandler != null)
                    _exportSnapshotHandler();
                else
                    RequestExportSnapshot?.Invoke();
            }
            else
            {
                TryInvokeExportForPublish(_pendingExportMode);
            }
        }

        private static UniverPublishExportMode ResolveExportMode(string metaJson)
        {
            if (string.IsNullOrWhiteSpace(metaJson))
                return UniverPublishExportMode.Default;

            try
            {
                var token = JObject.Parse(metaJson);
                var mode = token.Value<string>("mode");
                if (string.Equals(mode, "full", StringComparison.OrdinalIgnoreCase))
                    return UniverPublishExportMode.RangeFull;
                if (string.Equals(mode, "content", StringComparison.OrdinalIgnoreCase))
                    return UniverPublishExportMode.RangeContent;
            }
            catch
            {
                // ignore malformed meta
            }

            return UniverPublishExportMode.Default;
        }

        private static UniverClipRect ParseClipRect(string metaJson)
        {
            if (string.IsNullOrWhiteSpace(metaJson))
                return null;

            try
            {
                var token = JObject.Parse(metaJson);
                var clip = token["clipRect"] as JObject;
                if (clip == null)
                    return null;

                return new UniverClipRect(
                    clip.Value<int>("startRow"),
                    clip.Value<int>("startCol"),
                    clip.Value<int>("endRow"),
                    clip.Value<int>("endCol"));
            }
            catch
            {
                return null;
            }
        }

        private void OnExportTimeout()
        {
            TakeExportContinuation(out var publishOnly, out var pending, out _);
            if (!publishOnly && pending == null)
                return;

            RunOnUi(() =>
            {
                _viewModel.SetStatusMessage("exportSnapshot 超时，请重试");
                NotifyStatusChanged();
                EndCadInteraction();
            });
        }

        private void ApplySnapshotToEditorSilently(UniverGridSnapshot snapshot)
        {
            _suppressPush = true;
            try
            {
                _viewModel.ApplyUniverSnapshot(snapshot);
            }
            finally
            {
                _suppressPush = false;
            }
        }

        private void ResetExportState()
        {
            lock (_exportSync)
            {
                _pendingAfterSnapshot = null;
                _pendingRequiresCadInteraction = false;
                _pendingExportMode = UniverPublishExportMode.Default;
                _publishAfterSnapshot = false;
            }
        }

        private void TakeExportContinuation(out bool publishOnly, out Action pending, out bool requiresCad)
        {
            lock (_exportSync)
            {
                publishOnly = _publishAfterSnapshot;
                _publishAfterSnapshot = false;
                pending = _pendingAfterSnapshot;
                _pendingAfterSnapshot = null;
                requiresCad = _pendingRequiresCadInteraction;
                _pendingRequiresCadInteraction = false;
                _pendingExportMode = UniverPublishExportMode.Default;
            }
        }

        private void ClearExportTimeout()
        {
            _exportTimeoutTimer?.Dispose();
            _exportTimeoutTimer = null;
        }

        private void RunFileDialog(Action showDialog)
        {
            try
            {
                PrepareForCadInteraction?.Invoke();
                showDialog();
            }
            finally
            {
                RestoreAfterCadInteraction?.Invoke();
            }
        }

        private static void RunOnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.Invoke(action);
        }

        private void BeginCadInteraction()
        {
            _viewModel.CadInteractionCompleted = EndCadInteraction;

            if (PrepareForCadInteraction == null)
            {
                _viewModel.SetStatusMessage("编辑器窗口交互未初始化，无法进行 CAD 交互");
                return;
            }

            try
            {
                PrepareForCadInteraction.Invoke();
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusMessage($"准备 CAD 交互时出错：{ex.Message}");
            }
        }

        private void EndCadInteraction()
        {
            _viewModel.CadInteractionCompleted = null;

            if (RestoreAfterCadInteraction == null)
            {
                // 不是致命错误，只记录日志
                System.Diagnostics.Debug.WriteLine("RestoreAfterCadInteraction 未初始化");
            }
            else
            {
                try
                {
                    RestoreAfterCadInteraction.Invoke();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"恢复窗口状态时出错：{ex.Message}");
                }
            }

            NotifyStatusChanged();
        }

        private void NotifyStatusChanged()
        {
            try
            {
                StatusChanged?.Invoke();
            }
            catch
            {
                // ignore
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_suppressPush)
                return;

            if (e.PropertyName == nameof(TableEditorViewModel.GridRevision)
                || e.PropertyName == nameof(TableEditorViewModel.HasTable))
            {
                GridChanged?.Invoke();
            }

            if (e.PropertyName == nameof(TableEditorViewModel.StatusMessage))
                NotifyStatusChanged();
        }

        public void Dispose()
        {
            ClearExportTimeout();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}
