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
            _pendingAfterSnapshot = null;
            _pendingRequiresCadInteraction = false;
            _pendingExportMode = UniverPublishExportMode.Default;

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
            // #region agent log
            DebugAgentLog646873.Write("H3,H4", "UniverTableEditorHostBridge.NewEmptyTable", "enter", new
            {
                hasTableBefore = _viewModel.HasTable,
                rowCount = _viewModel.RowCount,
                colCount = _viewModel.ColCount,
            });
            // #endregion

            _viewModel.NewEmptyTable();
            NotifyStatusChanged();

            // #region agent log
            DebugAgentLog646873.Write("H3,H4", "UniverTableEditorHostBridge.NewEmptyTable", "done", new
            {
                hasTableAfter = _viewModel.HasTable,
                jsonLength = BuildLoadSnapshotJson()?.Length ?? 0,
                status = _viewModel.StatusMessage,
            });
            // #endregion
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
            _pendingExportMode = UniverPublishExportMode.Default;
            PullSnapshotFromUniverThen(() => _viewModel.RequestPublish(), requiresCadInteraction: true);
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
                    // #region agent log
                    DebugAgentLog646873.Write("H9", "UniverTableEditorHostBridge.ExportJsonSnapshot", "dialog cancelled", new { });
                    // #endregion
                    return;
                }

                var json = JsonConvert.SerializeObject(
                    UniverGridSnapshotMapper.FromTableGrid(grid),
                    Formatting.Indented);
                File.WriteAllText(dialog.FileName, json);
                _viewModel.SetStatusMessage("已导出 JSON：" + dialog.FileName);
                NotifyStatusChanged();
                // #region agent log
                DebugAgentLog646873.Write("H9", "UniverTableEditorHostBridge.ExportJsonSnapshot", "saved", new { path = dialog.FileName });
                // #endregion
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
                    // #region agent log
                    DebugAgentLog646873.Write("H9", "UniverTableEditorHostBridge.ImportXlsx", "dialog cancelled", new { });
                    // #endregion
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
                    // #region agent log
                    DebugAgentLog646873.Write("H9,H11", "UniverTableEditorHostBridge.ImportXlsx", "imported", new
                    {
                        path = dialog.FileName,
                        openXmlAssemblyCount = OpenXmlAssemblyBootstrap.CountLoadedOpenXmlAssemblies(),
                        openXmlAssemblies = OpenXmlAssemblyBootstrap.GetLoadedOpenXmlAssemblies(),
                    });
                    // #endregion
                }
                catch (Exception ex)
                {
                    _viewModel.SetStatusMessage("导入 xlsx 失败：" + ex.Message);
                    NotifyStatusChanged();
                    // #region agent log
                    DebugAgentLog646873.Write("H9,H11", "UniverTableEditorHostBridge.ImportXlsx", "failed", new
                    {
                        error = ex.Message,
                        openXmlAssemblyCount = OpenXmlAssemblyBootstrap.CountLoadedOpenXmlAssemblies(),
                        openXmlAssemblies = OpenXmlAssemblyBootstrap.GetLoadedOpenXmlAssemblies(),
                    });
                    // #endregion
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
                    // #region agent log
                    DebugAgentLog646873.Write("H9", "UniverTableEditorHostBridge.ExportXlsx", "dialog cancelled", new { });
                    // #endregion
                    return;
                }

                try
                {
                    TableGridXlsxAdapter.Export(grid, dialog.FileName);
                    _viewModel.SetStatusMessage("已导出 xlsx：" + dialog.FileName);
                    NotifyStatusChanged();
                    // #region agent log
                    DebugAgentLog646873.Write("H9,H11", "UniverTableEditorHostBridge.ExportXlsx", "saved", new
                    {
                        path = dialog.FileName,
                        openXmlAssemblyCount = OpenXmlAssemblyBootstrap.CountLoadedOpenXmlAssemblies(),
                        openXmlAssemblies = OpenXmlAssemblyBootstrap.GetLoadedOpenXmlAssemblies(),
                    });
                    // #endregion
                }
                catch (Exception ex)
                {
                    _viewModel.SetStatusMessage("导出 xlsx 失败：" + ex.Message);
                    NotifyStatusChanged();
                    // #region agent log
                    DebugAgentLog646873.Write("H9,H11", "UniverTableEditorHostBridge.ExportXlsx", "failed", new
                    {
                        error = ex.Message,
                        openXmlAssemblyCount = OpenXmlAssemblyBootstrap.CountLoadedOpenXmlAssemblies(),
                        openXmlAssemblies = OpenXmlAssemblyBootstrap.GetLoadedOpenXmlAssemblies(),
                    });
                    // #endregion
                }
            }));
        }

        public string GetSummaryText() => _viewModel.SummaryText;

        public string GetStatusMessage() => _viewModel.StatusMessage;

        public void CompleteExportSnapshot(string json, string metaJson = null)
        {
            ClearExportTimeout();

            try
            {
                var exportMode = ResolveExportMode(metaJson);
                var snapshot = string.IsNullOrWhiteSpace(json) ? null : UniverGridSnapshotMapper.Parse(json);
                var clipRect = ParseClipRect(metaJson);

                // #region agent log
                DebugAgentLog646873.Write("H4", "UniverTableEditorHostBridge.CompleteExportSnapshot", "snapshot received", new
                {
                    exportMode = exportMode.ToString(),
                    metaJson,
                    clipRect,
                    snapshotRows = snapshot?.RowCount,
                    snapshotCols = snapshot?.ColCount,
                });
                // #endregion

                if (exportMode == UniverPublishExportMode.RangeFull
                    || exportMode == UniverPublishExportMode.RangeContent)
                {
                    _pendingAfterSnapshot = null;
                    _pendingRequiresCadInteraction = false;
                    _pendingExportMode = UniverPublishExportMode.Default;

                    if (snapshot == null)
                    {
                        _viewModel.SetStatusMessage("exportSnapshot 返回空数据");
                        return;
                    }

                    BeginCadInteraction();
                    _viewModel.RequestPublishFromSnapshot(snapshot, clipRect, mutateEditor: false);
                    return;
                }

                if (string.IsNullOrWhiteSpace(json))
                    _viewModel.SetStatusMessage("exportSnapshot 返回空数据，将使用内存表格落图");
                else
                    _viewModel.ApplyUniverSnapshot(snapshot);

                var pending = _pendingAfterSnapshot;
                var requiresCad = _pendingRequiresCadInteraction;
                _pendingAfterSnapshot = null;
                _pendingRequiresCadInteraction = false;
                _pendingExportMode = UniverPublishExportMode.Default;

                if (pending == null)
                    return;

                if (requiresCad)
                    BeginCadInteraction();

                RunOnUi(pending);
            }
            catch (Exception ex)
            {
                _pendingAfterSnapshot = null;
                _pendingRequiresCadInteraction = false;
                _pendingExportMode = UniverPublishExportMode.Default;
                _viewModel.SetStatusMessage(ex.Message);
                EndCadInteraction();
            }
            finally
            {
                NotifyStatusChanged();
            }
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
            _pendingAfterSnapshot = null;
            _pendingRequiresCadInteraction = true;
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
            _pendingRequiresCadInteraction = requiresCadInteraction;
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
            if (_pendingAfterSnapshot == null && _pendingExportMode == UniverPublishExportMode.Default)
                return;

            _pendingAfterSnapshot = null;
            _pendingRequiresCadInteraction = false;
            _pendingExportMode = UniverPublishExportMode.Default;
            _viewModel.SetStatusMessage("exportSnapshot 超时，请重试");
            NotifyStatusChanged();
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
            try
            {
                PrepareForCadInteraction?.Invoke();
            }
            catch
            {
                // ignore host prepare failures
            }
        }

        private void EndCadInteraction()
        {
            _viewModel.CadInteractionCompleted = null;
            try
            {
                RestoreAfterCadInteraction?.Invoke();
            }
            catch
            {
                // ignore host restore failures
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
