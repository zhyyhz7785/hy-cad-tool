using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using HyCADTool.Features.Tables.Presentation;
using HyCADTool.Features.Tables.ViewModels;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace HyCADTool.Features.Tables.Services
{
    /// <summary>
    /// 将 TableEditorViewModel 桥接到 UniverEditor WebView2 宿主。
    /// </summary>
    public sealed class UniverTableEditorHostBridge : IDisposable
    {
        private readonly TableEditorViewModel _viewModel;
        private bool _suppressPush;
        private Action _pendingAfterSnapshot;

        public UniverTableEditorHostBridge(TableEditorViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        public TableEditorViewModel ViewModel => _viewModel;

        public event Action GridChanged;

        private Action _gridChangedForward;

        public void SetGridChangedForward(Action forward)
        {
            if (_gridChangedForward != null)
                GridChanged -= _gridChangedForward;

            _gridChangedForward = forward;
            if (_gridChangedForward != null)
                GridChanged += _gridChangedForward;
        }

        public event Action RequestExportSnapshot;

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
        }

        public void LoadPersonnelSample()
        {
            _viewModel.LoadSample();
        }

        public void RequestPick()
        {
            _viewModel.RequestPick();
        }

        public void RequestPublish()
        {
            PullSnapshotFromUniverThen(() => _viewModel.RequestPublish());
        }

        public void ExportJsonSnapshot()
        {
            PullSnapshotFromUniverThen(() =>
            {
                var grid = _viewModel.EditorGrid;
                if (grid == null)
                {
                    _viewModel.SetStatusMessage("请先加载表格");
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "JSON 快照 (*.json)|*.json",
                    FileName = "hytable-snapshot.json",
                };
                if (dialog.ShowDialog() != true)
                    return;

                var json = JsonConvert.SerializeObject(
                    UniverGridSnapshotMapper.FromTableGrid(grid),
                    Formatting.Indented);
                File.WriteAllText(dialog.FileName, json);
                _viewModel.SetStatusMessage("已导出 JSON：" + dialog.FileName);
            });
        }

        public void ImportXlsx()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
            };
            if (dialog.ShowDialog() != true)
                return;

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
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusMessage("导入 xlsx 失败：" + ex.Message);
            }
        }

        public void ExportXlsx()
        {
            PullSnapshotFromUniverThen(() =>
            {
                var grid = _viewModel.EditorGrid;
                if (grid == null)
                {
                    _viewModel.SetStatusMessage("请先加载表格");
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "Excel 工作簿 (*.xlsx)|*.xlsx",
                    FileName = "hytable-export.xlsx",
                };
                if (dialog.ShowDialog() != true)
                    return;

                TableGridXlsxAdapter.Export(grid, dialog.FileName);
                _viewModel.SetStatusMessage("已导出 xlsx：" + dialog.FileName);
            });
        }

        public string GetSummaryText() => _viewModel.SummaryText;

        public string GetStatusMessage() => _viewModel.StatusMessage;

        public void CompleteExportSnapshot(string json)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(json))
                    _viewModel.ApplyUniverSnapshot(UniverGridSnapshotMapper.Parse(json));

                var pending = _pendingAfterSnapshot;
                _pendingAfterSnapshot = null;
                pending?.Invoke();
            }
            catch (Exception ex)
            {
                _viewModel.SetStatusMessage(ex.Message);
            }
        }

        private void PullSnapshotFromUniverThen(Action action)
        {
            _pendingAfterSnapshot = action;
            RequestExportSnapshot?.Invoke();
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
        }

        public void Dispose()
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }
}
