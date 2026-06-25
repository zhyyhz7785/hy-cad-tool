using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.Presentation;
using HyCADTool.Features.Tables.TableApp;
using HyCADTool.Shell.ViewModels;

namespace HyCADTool.Features.Tables.ViewModels
{
    /// <summary>
    /// 018 独立表格编辑器窗口 ViewModel；M1：选区 + 开始 Tab + Inspector + 模板/拾取。
    /// </summary>
    public sealed class TableEditorViewModel : TablePanelViewModel
    {
        private readonly TableViewport _viewport = TableViewport.CreateDefault();
        private PaperPresetOption _selectedPaperPresetOption;
        private double _targetWidthMm = PaperPresetCatalog.DefaultTargetWidthMm(PaperPreset.A3);
        private string _paperSummaryText = "A3 · 400 mm";
        private int _matrixColCount;
        private int _selectedRow = -1;
        private int _selectedCol = -1;
        private bool _suppressSelectionSync;
        private string _selectedCellAddressText = string.Empty;
        private string _selectedCellPreviewText = string.Empty;
        private string _selectedRoleText = "—";
        private string _selectedFieldKey = "—";
        private int _selectedRowSpan = 1;
        private int _selectedColSpan = 1;
        private TextAlign _selectedHAlign = TextAlign.Start;
        private TextAlign _selectedVAlign = TextAlign.Center;
        private bool _selectedAllowWrap;

        public TableEditorViewModel()
        {
            PaperPresetOptions = new ObservableCollection<PaperPresetOption>(
                new[]
                {
                    PaperPreset.A4,
                    PaperPreset.A3,
                    PaperPreset.A2,
                    PaperPreset.Custom,
                }.Select(p => new PaperPresetOption(p, PaperPresetCatalog.GetDisplayName(p))));

            _selectedPaperPresetOption = PaperPresetOptions.First(o => o.Preset == PaperPreset.A3);
            MatrixRows = new ObservableCollection<TableMatrixRowVm>();

            ApplyAlignmentCommand = new RelayCommand<string>(ApplyAlignmentFromParameter, CanApplySelectionCommand);
            ToggleWrapCommand = new RelayCommand(ToggleWrap, () => HasSelection);
        }

        public ObservableCollection<PaperPresetOption> PaperPresetOptions { get; }

        public ObservableCollection<TableMatrixRowVm> MatrixRows { get; }

        public ICommand ApplyAlignmentCommand { get; }

        public ICommand ToggleWrapCommand { get; }

        public int MatrixColCount
        {
            get => _matrixColCount;
            private set
            {
                if (_matrixColCount == value)
                    return;

                _matrixColCount = value;
                OnPropertyChanged();
            }
        }

        public int SelectedRow
        {
            get => _selectedRow;
            private set
            {
                if (_selectedRow == value)
                    return;

                _selectedRow = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
            }
        }

        public int SelectedCol
        {
            get => _selectedCol;
            private set
            {
                if (_selectedCol == value)
                    return;

                _selectedCol = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
            }
        }

        public bool HasSelection => HasTable && _selectedRow >= 0 && _selectedCol >= 0;

        public string SelectedCellAddressText
        {
            get => _selectedCellAddressText;
            private set
            {
                if (_selectedCellAddressText == value)
                    return;

                _selectedCellAddressText = value;
                OnPropertyChanged();
            }
        }

        public string SelectedCellPreviewText
        {
            get => _selectedCellPreviewText;
            private set
            {
                if (_selectedCellPreviewText == value)
                    return;

                _selectedCellPreviewText = value;
                OnPropertyChanged();
            }
        }

        public string SelectedRoleText
        {
            get => _selectedRoleText;
            private set
            {
                if (_selectedRoleText == value)
                    return;

                _selectedRoleText = value;
                OnPropertyChanged();
            }
        }

        public string SelectedFieldKey
        {
            get => _selectedFieldKey;
            private set
            {
                if (_selectedFieldKey == value)
                    return;

                _selectedFieldKey = value;
                OnPropertyChanged();
            }
        }

        public int SelectedRowSpan
        {
            get => _selectedRowSpan;
            private set
            {
                if (_selectedRowSpan == value)
                    return;

                _selectedRowSpan = value;
                OnPropertyChanged();
            }
        }

        public int SelectedColSpan
        {
            get => _selectedColSpan;
            private set
            {
                if (_selectedColSpan == value)
                    return;

                _selectedColSpan = value;
                OnPropertyChanged();
            }
        }

        public TextAlign SelectedHAlign
        {
            get => _selectedHAlign;
            set
            {
                if (_suppressSelectionSync || _selectedHAlign == value)
                    return;

                ApplyAlignment(value, _selectedVAlign);
            }
        }

        public TextAlign SelectedVAlign
        {
            get => _selectedVAlign;
            set
            {
                if (_suppressSelectionSync || _selectedVAlign == value)
                    return;

                ApplyAlignment(_selectedHAlign, value);
            }
        }

        public bool SelectedAllowWrap
        {
            get => _selectedAllowWrap;
            set
            {
                if (_suppressSelectionSync || _selectedAllowWrap == value)
                    return;

                SetAllowWrap(value);
            }
        }

        public PaperPresetOption SelectedPaperPresetOption
        {
            get => _selectedPaperPresetOption;
            set
            {
                if (value == null || ReferenceEquals(_selectedPaperPresetOption, value))
                    return;

                _selectedPaperPresetOption = value;
                _viewport.PaperPreset = value.Preset;
                OnPropertyChanged();

                if (value.Preset != PaperPreset.Custom)
                {
                    TargetWidthMm = PaperPresetCatalog.ResolveTargetWidthMm(value.Preset, _viewport.MarginMm);
                }

                UpdatePaperSummaryText();
            }
        }

        public double TargetWidthMm
        {
            get => _targetWidthMm;
            set
            {
                var normalized = value <= 0 ? PaperPresetCatalog.CustomFallbackWidthMm : value;
                if (System.Math.Abs(_targetWidthMm - normalized) < 0.001)
                    return;

                _targetWidthMm = normalized;
                _viewport.TargetWidthMm = normalized;
                OnPropertyChanged();
                UpdatePaperSummaryText();
            }
        }

        public string PaperSummaryText
        {
            get => _paperSummaryText;
            private set
            {
                if (_paperSummaryText == value)
                    return;

                _paperSummaryText = value;
                OnPropertyChanged();
            }
        }

        public TableViewport Viewport => _viewport;

        public void SetSelectedCell(int row, int col)
        {
            if (!HasTable)
            {
                ClearSelection();
                return;
            }

            var grid = CurrentGrid;
            var topology = grid.Structure.Topology;
            if (row < 0 || col < 0 || row >= topology.RowCount || col >= topology.ColCount)
                return;

            SelectedRow = row;
            SelectedCol = col;
            RefreshSelectionProperties();
        }

        public void ClearSelection()
        {
            SelectedRow = -1;
            SelectedCol = -1;
            RefreshSelectionProperties();
        }

        protected override void RefreshRows()
        {
            RefreshMatrix();
        }

        protected override void OnAfterApplyGrid()
        {
            if (HasTable)
                SetSelectedCell(0, 0);
            else
                ClearSelection();
        }

        protected override void OnAfterCellCommitted(CellAddr addr)
        {
            if (!HasSelection || OpLog == null)
                return;

            var grid = OpLog.Current;
            var selected = new CellAddr(SelectedRow, SelectedCol);
            if (grid.Structure.GetAnchorOf(selected) != grid.Structure.GetAnchorOf(addr))
                return;

            SelectedCellPreviewText = TableSummaryBuilder.FormatCellValue(GridEditor.GetValue(grid, addr));
        }

        private void RefreshMatrix()
        {
            MatrixRows.Clear();
            MatrixColCount = 0;

            var grid = CurrentGrid;
            if (grid == null)
            {
                ClearSelection();
                return;
            }

            var matrix = TableMatrixGridAdapter.BuildMatrix(grid);
            MatrixColCount = matrix.ColCount;

            foreach (var row in matrix.Rows)
            {
                MatrixRows.Add(new TableMatrixRowVm(row, CommitCellText));
            }

            if (HasSelection)
            {
                var topology = grid.Structure.Topology;
                if (SelectedRow >= topology.RowCount || SelectedCol >= topology.ColCount)
                    SetSelectedCell(0, 0);
                else
                    RefreshSelectionProperties();
            }
        }

        protected override void RefreshSummary()
        {
            base.RefreshSummary();
            UpdatePaperSummaryText();
        }

        private void RefreshSelectionProperties()
        {
            if (!HasSelection || CurrentGrid == null)
            {
                SelectedCellAddressText = string.Empty;
                SelectedCellPreviewText = string.Empty;
                SelectedRoleText = "—";
                SelectedFieldKey = "—";
                SelectedRowSpan = 1;
                SelectedColSpan = 1;

                _suppressSelectionSync = true;
                _selectedHAlign = TextAlign.Start;
                _selectedVAlign = TextAlign.Center;
                _selectedAllowWrap = false;
                OnPropertyChanged(nameof(SelectedHAlign));
                OnPropertyChanged(nameof(SelectedVAlign));
                OnPropertyChanged(nameof(SelectedAllowWrap));
                _suppressSelectionSync = false;
                return;
            }

            var grid = CurrentGrid;
            var addr = new CellAddr(SelectedRow, SelectedCol);
            var anchor = grid.Structure.GetAnchorOf(addr);

            SelectedCellAddressText = CellAddressFormatter.Format(addr.Row, addr.Col);
            SelectedCellPreviewText = TableSummaryBuilder.FormatCellValue(GridEditor.GetValue(grid, anchor));

            SelectedRoleText = grid.Structure.Roles.TryGetValue(anchor, out var role)
                ? role.ToString()
                : "—";

            string fieldKey = null;
            foreach (var entry in grid.Structure.FieldIndex)
            {
                if (entry.Value == anchor)
                {
                    fieldKey = entry.Key;
                    break;
                }
            }

            SelectedFieldKey = fieldKey ?? "—";

            if (grid.Structure.TryGetMergeAt(anchor, out var merge))
            {
                SelectedRowSpan = merge.RowSpan;
                SelectedColSpan = merge.ColSpan;
            }
            else
            {
                SelectedRowSpan = 1;
                SelectedColSpan = 1;
            }

            var style = GridEditor.GetCellStyle(grid, anchor);
            _suppressSelectionSync = true;
            _selectedHAlign = style.HAlign;
            _selectedVAlign = style.VAlign;
            _selectedAllowWrap = GridEditor.GetCellAllowWrap(grid, anchor);
            OnPropertyChanged(nameof(SelectedHAlign));
            OnPropertyChanged(nameof(SelectedVAlign));
            OnPropertyChanged(nameof(SelectedAllowWrap));
            _suppressSelectionSync = false;
        }

        private bool CanApplySelectionCommand(string parameter) => HasSelection;

        private void ApplyAlignmentFromParameter(string parameter)
        {
            if (string.IsNullOrEmpty(parameter))
                return;

            var parts = parameter.Split(',');
            if (parts.Length != 2)
                return;

            if (!Enum.TryParse(parts[0], out TextAlign hAlign)
                || !Enum.TryParse(parts[1], out TextAlign vAlign))
            {
                return;
            }

            ApplyAlignment(hAlign, vAlign);
        }

        private void ApplyAlignment(TextAlign hAlign, TextAlign vAlign)
        {
            if (!HasSelection || OpLog == null)
                return;

            try
            {
                var addr = new CellAddr(SelectedRow, SelectedCol);
                var style = GridEditor.GetCellStyle(OpLog.Current, addr);
                var newStyle = new CellStyle(
                    style.Orientation,
                    hAlign,
                    vAlign,
                    style.TextHeight,
                    style.FontKey,
                    style.Borders,
                    style.BackColor);
                ApplyOperation(new SetStyleOp(addr, newStyle));

                _suppressSelectionSync = true;
                _selectedHAlign = hAlign;
                _selectedVAlign = vAlign;
                OnPropertyChanged(nameof(SelectedHAlign));
                OnPropertyChanged(nameof(SelectedVAlign));
                _suppressSelectionSync = false;

                StatusMessage = $"已设置对齐 {hAlign}/{vAlign}";
            }
            catch (Exception ex)
            {
                StatusMessage = "对齐失败：" + ex.Message;
            }
        }

        private void ToggleWrap()
        {
            SetAllowWrap(!SelectedAllowWrap);
        }

        private void SetAllowWrap(bool allowWrap)
        {
            if (!HasSelection || OpLog == null)
                return;

            try
            {
                var addr = new CellAddr(SelectedRow, SelectedCol);
                ApplyOperation(new SetCellWrapOp(addr, allowWrap));

                _suppressSelectionSync = true;
                _selectedAllowWrap = allowWrap;
                OnPropertyChanged(nameof(SelectedAllowWrap));
                _suppressSelectionSync = false;

                StatusMessage = allowWrap ? "已启用自动换行" : "已关闭自动换行";
            }
            catch (Exception ex)
            {
                StatusMessage = "换行设置失败：" + ex.Message;
            }
        }

        private void UpdatePaperSummaryText()
        {
            var presetName = SelectedPaperPresetOption?.DisplayName
                ?? PaperPresetCatalog.GetDisplayName(_viewport.PaperPreset);
            var topology = HasTable ? $"{RowCount}×{ColCount}" : "未建表";
            PaperSummaryText = $"{presetName} · {TargetWidthMm:F0} mm · {topology}";
        }
    }
}
