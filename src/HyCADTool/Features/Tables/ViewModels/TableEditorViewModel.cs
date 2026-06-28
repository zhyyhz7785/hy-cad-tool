using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using HyCAD.Tables;
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
        private int _selStartRow = -1;
        private int _selStartCol = -1;
        private int _selEndRow = -1;
        private int _selEndCol = -1;
        private int _gridRevision;
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
        private bool _selectedIsVertical;
        private double _selectedTextHeight = 3.5;
        private bool _isStructureMode = true;
        private double _selectedRowHeightMm = 10;
        private double _selectedColWidthMm = 25;
        private string _editableFieldKey = string.Empty;
        private CellRole? _selectedRole;
        private PaperOrientation _paperOrientation = PaperOrientation.Landscape;
        private double _marginMm = PaperPresetCatalog.DefaultMarginMm;
        private TableEditorClipboardData _clipboardData;
        private TableStylePreset _selectedStylePreset = TableStylePreset.EngineeringDefault;

        private const double BorderPresetWidthMm = 0.35;

        /// <summary>推导行列数的种子行高（mm）。</summary>
        public const double SeedRowHeightMm = 5.0;

        /// <summary>推导行列数的种子列宽（mm）。</summary>
        public const double SeedColWidthMm = 25.0;

        private const int MaxPaperDimension = 256;

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

            RoleOptions = new ObservableCollection<CellRoleOption>(
                new[]
                {
                    new CellRoleOption("（无）", null),
                    new CellRoleOption("Title", CellRole.Title),
                    new CellRoleOption("Header", CellRole.Header),
                    new CellRoleOption("Label", CellRole.Label),
                    new CellRoleOption("Value", CellRole.Value),
                    new CellRoleOption("PhotoSlot", CellRole.PhotoSlot),
                    new CellRoleOption("Spacer", CellRole.Spacer),
                });

            ApplyAlignmentCommand = new RelayCommand<string>(ApplyAlignmentFromParameter, CanApplySelectionCommand);
            ToggleWrapCommand = new RelayCommand(ToggleWrap, () => HasSelection);
            ApplyBorderPresetCommand = new RelayCommand<string>(ApplyBorderPreset, CanApplySelectionCommand);
            InsertRowCommand = new RelayCommand(InsertRowAfterSelection, () => HasTable && IsStructureMode);
            DeleteRowCommand = new RelayCommand(DeleteSelectedRows, () => HasTable && IsStructureMode && HasSelection);
            InsertColumnCommand = new RelayCommand(InsertColumnAfterSelection, () => HasTable && IsStructureMode);
            DeleteColumnCommand = new RelayCommand(DeleteSelectedColumns, () => HasTable && IsStructureMode && HasSelection);
            MergeSelectionCommand = new RelayCommand(MergeSelection, () => HasTable && IsStructureMode && HasSelection);
            UnmergeCommand = new RelayCommand(UnmergeActiveCell, () => HasTable && IsStructureMode && HasSelection);
            CopySelectionCommand = new RelayCommand(CopySelection, () => HasSelection);
            PasteSelectionCommand = new RelayCommand(PasteSelection, () => HasSelection && _clipboardData != null);
            ApplyTableStylePresetCommand = new RelayCommand(ApplyTableStylePreset, () => HasTable);
            FitColumnsToPaperCommand = new RelayCommand(FitColumnsToPaper, () => HasTable);

            StylePresetOptions = new ObservableCollection<TableStylePreset>
            {
                TableStylePreset.EngineeringDefault,
                TableStylePreset.PersonnelOuterBold,
                TableStylePreset.NoBorder,
            };
            _selectedStylePreset = StylePresetOptions[0];

            DefaultRowHeightMm = SeedRowHeightMm;
            DefaultColWidthMm = SeedColWidthMm;
            RegenerateToPaper(reseedCounts: true);
        }

        public ObservableCollection<CellRoleOption> RoleOptions { get; }

        public ObservableCollection<PaperPresetOption> PaperPresetOptions { get; }

        public ObservableCollection<TableMatrixRowVm> MatrixRows { get; }

        public ICommand ApplyAlignmentCommand { get; }

        public ICommand ToggleWrapCommand { get; }

        public ICommand ApplyBorderPresetCommand { get; }

        public ICommand InsertRowCommand { get; }

        public ICommand DeleteRowCommand { get; }

        public ICommand InsertColumnCommand { get; }

        public ICommand DeleteColumnCommand { get; }

        public ICommand MergeSelectionCommand { get; }

        public ICommand UnmergeCommand { get; }

        public ICommand CopySelectionCommand { get; }

        public ICommand PasteSelectionCommand { get; }

        public ICommand ApplyTableStylePresetCommand { get; }

        public ICommand FitColumnsToPaperCommand { get; }

        public ObservableCollection<TableStylePreset> StylePresetOptions { get; }

        public TableStylePreset SelectedStylePreset
        {
            get => _selectedStylePreset;
            set
            {
                if (_selectedStylePreset == value)
                    return;

                _selectedStylePreset = value;
                OnPropertyChanged();
            }
        }

        public bool CanPaste => _clipboardData != null;

        public bool IsStructureMode
        {
            get => _isStructureMode;
            set
            {
                if (_isStructureMode == value)
                    return;

                _isStructureMode = value;
                OnPropertyChanged();
            }
        }

        public double SelectedRowHeightMm
        {
            get => _selectedRowHeightMm;
            set
            {
                if (_suppressSelectionSync || !HasTable)
                    return;

                var normalized = value <= 0 ? 10 : value;
                if (System.Math.Abs(_selectedRowHeightMm - normalized) < 0.001)
                    return;

                ApplyTrackSizes(rowHeight: normalized, colWidth: null);
            }
        }

        public double SelectedColWidthMm
        {
            get => _selectedColWidthMm;
            set
            {
                if (_suppressSelectionSync || !HasTable)
                    return;

                var normalized = value <= 0 ? 25 : value;
                if (System.Math.Abs(_selectedColWidthMm - normalized) < 0.001)
                    return;

                ApplyTrackSizes(rowHeight: null, colWidth: normalized);
            }
        }

        public string EditableFieldKey
        {
            get => _editableFieldKey;
            set
            {
                if (_suppressSelectionSync || _editableFieldKey == value)
                    return;

                _editableFieldKey = value ?? string.Empty;
                OnPropertyChanged();
                CommitFieldKey(_editableFieldKey);
            }
        }

        public CellRole? SelectedRole
        {
            get => _selectedRole;
            set
            {
                if (_suppressSelectionSync || _selectedRole == value)
                    return;

                _selectedRole = value;
                OnPropertyChanged();
                CommitRole(value);
            }
        }

        public PaperOrientation PaperOrientation
        {
            get => _paperOrientation;
            set
            {
                if (_paperOrientation == value)
                    return;

                _paperOrientation = value;
                _viewport.Orientation = value;
                OnPropertyChanged();
                RefreshTargetWidthFromPreset();
                UpdatePaperSummaryText();
            }
        }

        public double MarginMm
        {
            get => _marginMm;
            set
            {
                var normalized = value < 0 ? 0 : value;
                if (System.Math.Abs(_marginMm - normalized) < 0.001)
                    return;

                _marginMm = normalized;
                _viewport.MarginMm = normalized;
                OnPropertyChanged();
                RefreshTargetWidthFromPreset();
                UpdatePaperSummaryText();
            }
        }

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

        /// <summary>当前矩形选区（归一化，含 anchor 与 active 端，0-based 闭区间）。</summary>
        public int SelRowStart => Math.Min(_selStartRow, _selEndRow);

        public int SelRowEnd => Math.Max(_selStartRow, _selEndRow);

        public int SelColStart => Math.Min(_selStartCol, _selEndCol);

        public int SelColEnd => Math.Max(_selStartCol, _selEndCol);

        public bool HasRangeSelection =>
            HasSelection && (SelRowStart != SelRowEnd || SelColStart != SelColEnd);

        /// <summary>网格结构版本号；结构变化时自增，供视图整树重建。</summary>
        public int GridRevision => _gridRevision;

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
                OnPropertyChanged(nameof(EditableCellPreviewText));
            }
        }

        /// <summary>地址栏可编辑单元格内容（提交到 OpLog）。</summary>
        public string EditableCellPreviewText
        {
            get => _selectedCellPreviewText;
            set
            {
                if (_suppressSelectionSync || !HasSelection)
                    return;

                var normalized = value ?? string.Empty;
                if (_selectedCellPreviewText == normalized)
                    return;

                _selectedCellPreviewText = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCellPreviewText));
                CommitActiveCellPreview(normalized);
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

        public bool SelectedIsVertical
        {
            get => _selectedIsVertical;
            set
            {
                if (_suppressSelectionSync || _selectedIsVertical == value)
                    return;

                SetOrientation(value);
            }
        }

        public double SelectedTextHeight
        {
            get => _selectedTextHeight;
            set
            {
                if (_suppressSelectionSync)
                    return;

                var normalized = value <= 0 ? 3.5 : value;
                if (System.Math.Abs(_selectedTextHeight - normalized) < 0.001)
                    return;

                SetTextHeight(normalized);
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
                    TargetWidthMm = PaperPresetCatalog.ResolveTargetWidthMm(
                        value.Preset, _viewport.MarginMm, _viewport.Orientation);
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

        /// <summary>构建 Excel 网格渲染快照（无表返回 null）。</summary>
        public ExcelGridSnapshot BuildSnapshot()
        {
            var grid = CurrentGrid;
            return grid == null ? null : ExcelGridSnapshotBuilder.Build(grid);
        }

        /// <summary>供 ReoGrid 宿主读取当前 Domain 网格（Presentation 层只读）。</summary>
        public TableGrid EditorGrid => CurrentGrid;

        /// <summary>由视图提交单元格文本（公开 internal CommitCellText）。</summary>
        public void CommitCell(CellAddr addr, string text) => CommitCellText(addr, text);

        public void SetSelectedCell(int row, int col)
        {
            SetSelectedRange(row, col, row, col);
        }

        /// <summary>设置矩形选区；anchor=(startRow,startCol)，active=(endRow,endCol)。</summary>
        public void SetSelectedRange(int startRow, int startCol, int endRow, int endCol)
        {
            if (!HasTable)
            {
                ClearSelection();
                return;
            }

            var grid = CurrentGrid;
            var topology = grid.Structure.Topology;
            if (!InBounds(topology, startRow, startCol) || !InBounds(topology, endRow, endCol))
                return;

            _selStartRow = startRow;
            _selStartCol = startCol;
            _selEndRow = endRow;
            _selEndCol = endCol;

            SelectedRow = startRow;
            SelectedCol = startCol;

            OnPropertyChanged(nameof(SelRowStart));
            OnPropertyChanged(nameof(SelRowEnd));
            OnPropertyChanged(nameof(SelColStart));
            OnPropertyChanged(nameof(SelColEnd));
            OnPropertyChanged(nameof(HasRangeSelection));

            RefreshSelectionProperties();
        }

        private static bool InBounds(GridTopology topology, int row, int col) =>
            row >= 0 && col >= 0 && row < topology.RowCount && col < topology.ColCount;

        public void ClearSelection()
        {
            _selStartRow = _selStartCol = _selEndRow = _selEndCol = -1;
            SelectedRow = -1;
            SelectedCol = -1;
            OnPropertyChanged(nameof(SelRowStart));
            OnPropertyChanged(nameof(SelRowEnd));
            OnPropertyChanged(nameof(SelColStart));
            OnPropertyChanged(nameof(SelColEnd));
            OnPropertyChanged(nameof(HasRangeSelection));
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

            _gridRevision++;
            OnPropertyChanged(nameof(GridRevision));

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
                _editableFieldKey = string.Empty;
                _selectedRole = null;
                OnPropertyChanged(nameof(EditableFieldKey));
                OnPropertyChanged(nameof(SelectedRole));

                _suppressSelectionSync = true;
                _selectedHAlign = TextAlign.Start;
                _selectedVAlign = TextAlign.Center;
                _selectedIsVertical = false;
                _selectedAllowWrap = false;
                OnPropertyChanged(nameof(SelectedHAlign));
                OnPropertyChanged(nameof(SelectedVAlign));
                OnPropertyChanged(nameof(SelectedIsVertical));
                OnPropertyChanged(nameof(SelectedAllowWrap));
                _suppressSelectionSync = false;
                return;
            }

            var grid = CurrentGrid;
            var addr = new CellAddr(SelectedRow, SelectedCol);
            var anchor = grid.Structure.GetAnchorOf(addr);

            SelectedCellAddressText = HasRangeSelection
                ? $"{CellAddressFormatter.Format(SelRowStart, SelColStart)}:{CellAddressFormatter.Format(SelRowEnd, SelColEnd)}"
                : CellAddressFormatter.Format(addr.Row, addr.Col);
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
            _editableFieldKey = fieldKey ?? string.Empty;
            _selectedRole = grid.Structure.Roles.TryGetValue(anchor, out var roleVal) ? roleVal : (CellRole?)null;
            OnPropertyChanged(nameof(EditableFieldKey));
            OnPropertyChanged(nameof(SelectedRole));

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
            _selectedIsVertical = style.Orientation == TextOrientation.VerticalStacked;
            _selectedTextHeight = style.TextHeight;
            _selectedAllowWrap = GridEditor.GetCellAllowWrap(grid, anchor);
            _selectedRowHeightMm = grid.Structure.Topology.Rows[SelectedRow].Size;
            _selectedColWidthMm = grid.Structure.Topology.Cols[SelectedCol].Size;
            OnPropertyChanged(nameof(SelectedHAlign));
            OnPropertyChanged(nameof(SelectedVAlign));
            OnPropertyChanged(nameof(SelectedIsVertical));
            OnPropertyChanged(nameof(SelectedTextHeight));
            OnPropertyChanged(nameof(SelectedAllowWrap));
            OnPropertyChanged(nameof(SelectedRowHeightMm));
            OnPropertyChanged(nameof(SelectedColWidthMm));
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

        /// <summary>枚举当前选区内去重后的 anchor 地址。</summary>
        private System.Collections.Generic.IEnumerable<CellAddr> EnumerateSelectedAnchors()
        {
            var grid = OpLog?.Current;
            if (grid == null || !HasSelection)
                yield break;

            var seen = new System.Collections.Generic.HashSet<CellAddr>();
            for (var r = SelRowStart; r <= SelRowEnd; r++)
            {
                for (var c = SelColStart; c <= SelColEnd; c++)
                {
                    var anchor = grid.Structure.GetAnchorOf(new CellAddr(r, c));
                    if (seen.Add(anchor))
                        yield return anchor;
                }
            }
        }

        private void ApplyStyleToSelection(Func<CellStyle, CellStyle> transform, string status)
        {
            if (!HasSelection || OpLog == null)
                return;

            try
            {
                var ops = new System.Collections.Generic.List<TableOperation>();
                foreach (var anchor in EnumerateSelectedAnchors())
                {
                    var style = GridEditor.GetCellStyle(OpLog.Current, anchor);
                    ops.Add(new SetStyleOp(anchor, transform(style)));
                }

                ApplyOperations(ops);
                StatusMessage = status;
            }
            catch (Exception ex)
            {
                StatusMessage = "样式设置失败：" + ex.Message;
            }
        }

        private void ApplyAlignment(TextAlign hAlign, TextAlign vAlign)
        {
            ApplyStyleToSelection(
                style => new CellStyle(
                    style.Orientation,
                    hAlign,
                    vAlign,
                    style.TextHeight,
                    style.FontKey,
                    style.Borders,
                    style.BackColor),
                $"已设置对齐 {hAlign}/{vAlign}");

            _suppressSelectionSync = true;
            _selectedHAlign = hAlign;
            _selectedVAlign = vAlign;
            OnPropertyChanged(nameof(SelectedHAlign));
            OnPropertyChanged(nameof(SelectedVAlign));
            _suppressSelectionSync = false;
        }

        private void SetOrientation(bool vertical)
        {
            var orientation = vertical ? TextOrientation.VerticalStacked : TextOrientation.Horizontal;
            ApplyStyleToSelection(
                style => new CellStyle(
                    orientation,
                    style.HAlign,
                    style.VAlign,
                    style.TextHeight,
                    style.FontKey,
                    style.Borders,
                    style.BackColor),
                vertical ? "已设置竖排文字" : "已设置横排文字");

            _suppressSelectionSync = true;
            _selectedIsVertical = vertical;
            OnPropertyChanged(nameof(SelectedIsVertical));
            _suppressSelectionSync = false;
        }

        private void SetTextHeight(double height)
        {
            ApplyStyleToSelection(
                style => new CellStyle(
                    style.Orientation,
                    style.HAlign,
                    style.VAlign,
                    height,
                    style.FontKey,
                    style.Borders,
                    style.BackColor),
                $"已设置字高 {height:0.##} mm");

            _suppressSelectionSync = true;
            _selectedTextHeight = height;
            OnPropertyChanged(nameof(SelectedTextHeight));
            _suppressSelectionSync = false;
        }

        private void ApplyBorderPreset(string preset)
        {
            if (!HasSelection || OpLog == null || string.IsNullOrEmpty(preset))
                return;

            var r0 = SelRowStart;
            var r1 = SelRowEnd;
            var c0 = SelColStart;
            var c1 = SelColEnd;

            try
            {
                var grid = OpLog.Current;
                var ops = new System.Collections.Generic.List<TableOperation>();
                foreach (var anchor in EnumerateSelectedAnchors())
                {
                    var style = GridEditor.GetCellStyle(grid, anchor);
                    var rowSpan = 1;
                    var colSpan = 1;
                    if (grid.Structure.TryGetMergeAt(anchor, out var merge))
                    {
                        rowSpan = merge.RowSpan;
                        colSpan = merge.ColSpan;
                    }

                    BorderSet borders;
                    switch (preset)
                    {
                        case "none":
                            borders = BorderSet.None;
                            break;
                        case "all":
                            borders = BorderSet.Uniform(BorderPresetWidthMm);
                            break;
                        case "outer":
                            var top = anchor.Row == r0 ? BorderPresetWidthMm : 0.0;
                            var left = anchor.Col == c0 ? BorderPresetWidthMm : 0.0;
                            var bottom = anchor.Row + rowSpan - 1 == r1 ? BorderPresetWidthMm : 0.0;
                            var right = anchor.Col + colSpan - 1 == c1 ? BorderPresetWidthMm : 0.0;
                            borders = new BorderSet(top, right, bottom, left);
                            break;
                        default:
                            return;
                    }

                    ops.Add(new SetStyleOp(anchor, new CellStyle(
                        style.Orientation,
                        style.HAlign,
                        style.VAlign,
                        style.TextHeight,
                        style.FontKey,
                        borders,
                        style.BackColor)));
                }

                ApplyOperations(ops);
                StatusMessage = preset == "none" ? "已清除边框"
                    : preset == "all" ? "已设置全框"
                    : "已设置外框";
            }
            catch (Exception ex)
            {
                StatusMessage = "边框设置失败：" + ex.Message;
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
                var ops = new System.Collections.Generic.List<TableOperation>();
                foreach (var anchor in EnumerateSelectedAnchors())
                    ops.Add(new SetCellWrapOp(anchor, allowWrap));

                ApplyOperations(ops);

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
            var orient = _viewport.Orientation == PaperOrientation.Landscape ? "横" : "纵";
            var topology = HasTable ? $"{RowCount}×{ColCount}" : "未建表";
            PaperSummaryText = $"{presetName} · {orient} · {TargetWidthMm:F0} mm · 边距 {_marginMm:F0} · {topology}";
        }

        private void RefreshTargetWidthFromPreset()
        {
            if (SelectedPaperPresetOption?.Preset == PaperPreset.Custom)
                return;

            TargetWidthMm = PaperPresetCatalog.ResolveTargetWidthMm(
                _viewport.PaperPreset,
                _viewport.MarginMm,
                _viewport.Orientation);
        }

        private void CommitFieldKey(string key)
        {
            if (!HasSelection || OpLog == null)
                return;

            try
            {
                var addr = new CellAddr(SelectedRow, SelectedCol);
                var normalized = string.IsNullOrWhiteSpace(key) ? null : key.Trim();
                ApplyOperation(new SetFieldKeyOp(addr, normalized));
                RefreshSelectionProperties();
                StatusMessage = normalized == null ? "已清除 FieldKey" : $"FieldKey={normalized}";
            }
            catch (Exception ex)
            {
                StatusMessage = "FieldKey 设置失败：" + ex.Message;
            }
        }

        private void CommitRole(CellRole? role)
        {
            if (!HasSelection || OpLog == null)
                return;

            try
            {
                var addr = new CellAddr(SelectedRow, SelectedCol);
                ApplyOperation(new SetRoleOp(addr, role));
                RefreshSelectionProperties();
                StatusMessage = role == null ? "已清除 Role" : $"Role={role}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Role 设置失败：" + ex.Message;
            }
        }

        private void ApplyTrackSizes(double? rowHeight, double? colWidth)
        {
            if (!HasSelection || OpLog == null)
                return;

            try
            {
                var ops = new System.Collections.Generic.List<TableOperation>();
                if (rowHeight.HasValue)
                {
                    for (var r = SelRowStart; r <= SelRowEnd; r++)
                        ops.Add(new SetTrackSizeOp(true, r, rowHeight.Value));
                }

                if (colWidth.HasValue)
                {
                    for (var c = SelColStart; c <= SelColEnd; c++)
                        ops.Add(new SetTrackSizeOp(false, c, colWidth.Value));
                }

                ApplyOperations(ops);

                _suppressSelectionSync = true;
                if (rowHeight.HasValue)
                {
                    _selectedRowHeightMm = rowHeight.Value;
                    OnPropertyChanged(nameof(SelectedRowHeightMm));
                }

                if (colWidth.HasValue)
                {
                    _selectedColWidthMm = colWidth.Value;
                    OnPropertyChanged(nameof(SelectedColWidthMm));
                }

                _suppressSelectionSync = false;

                StatusMessage = rowHeight.HasValue
                    ? $"已设置行高 {rowHeight.Value:0.##} mm"
                    : $"已设置列宽 {colWidth!.Value:0.##} mm";
            }
            catch (Exception ex)
            {
                StatusMessage = "尺寸设置失败：" + ex.Message;
            }
        }

        private void InsertRowAfterSelection()
        {
            if (!HasTable || OpLog == null)
                return;

            var index = HasSelection ? SelRowEnd + 1 : RowCount;
            ApplyOperation(new InsertRowOp(index));
            SetSelectedCell(Math.Min(index, RowCount - 1), SelectedCol >= 0 ? SelectedCol : 0);
            StatusMessage = $"已在第 {index + 1} 行前插入";
        }

        private void DeleteSelectedRows()
        {
            if (!HasTable || !HasSelection || OpLog == null)
                return;

            try
            {
                for (var r = SelRowEnd; r >= SelRowStart; r--)
                    OpLog.Apply(new DeleteRowOp(r));

                RefreshRows();
                RefreshSummary();
                if (RowCount > 0)
                    SetSelectedCell(Math.Min(SelRowStart, RowCount - 1), Math.Min(SelColStart, ColCount - 1));
                else
                    ClearSelection();

                StatusMessage = "已删除选中行";
            }
            catch (Exception ex)
            {
                StatusMessage = "删除行失败：" + ex.Message;
            }
        }

        private void InsertColumnAfterSelection()
        {
            if (!HasTable || OpLog == null)
                return;

            var index = HasSelection ? SelColEnd + 1 : ColCount;
            ApplyOperation(new InsertColumnOp(index));
            SetSelectedCell(SelectedRow >= 0 ? SelectedRow : 0, Math.Min(index, ColCount - 1));
            StatusMessage = $"已在第 {index + 1} 列前插入";
        }

        private void DeleteSelectedColumns()
        {
            if (!HasTable || !HasSelection || OpLog == null)
                return;

            try
            {
                for (var c = SelColEnd; c >= SelColStart; c--)
                    OpLog.Apply(new DeleteColumnOp(c));

                RefreshRows();
                RefreshSummary();
                if (ColCount > 0)
                    SetSelectedCell(Math.Min(SelRowStart, RowCount - 1), Math.Min(SelColStart, ColCount - 1));
                else
                    ClearSelection();

                StatusMessage = "已删除选中列";
            }
            catch (Exception ex)
            {
                StatusMessage = "删除列失败：" + ex.Message;
            }
        }

        private void MergeSelection()
        {
            if (!HasSelection || OpLog == null)
                return;

            var rowSpan = SelRowEnd - SelRowStart + 1;
            var colSpan = SelColEnd - SelColStart + 1;
            if (rowSpan <= 1 && colSpan <= 1)
            {
                StatusMessage = "选区须大于 1×1 才能合并";
                return;
            }

            try
            {
                ApplyOperation(new MergeOp(new CellAddr(SelRowStart, SelColStart), rowSpan, colSpan));
                SetSelectedRange(SelRowStart, SelColStart, SelRowStart, SelColStart);
                StatusMessage = $"已合并 {rowSpan}×{colSpan}";
            }
            catch (Exception ex)
            {
                StatusMessage = "合并失败：" + ex.Message;
            }
        }

        private void UnmergeActiveCell()
        {
            if (!HasSelection || OpLog == null)
                return;

            var grid = OpLog.Current;
            var anchor = grid.Structure.GetAnchorOf(new CellAddr(SelectedRow, SelectedCol));
            if (!grid.Structure.TryGetMergeAt(anchor, out _))
            {
                StatusMessage = "当前格未合并";
                return;
            }

            try
            {
                ApplyOperation(new UnmergeOp(anchor));
                SetSelectedCell(anchor.Row, anchor.Col);
                StatusMessage = "已拆分合并区";
            }
            catch (Exception ex)
            {
                StatusMessage = "拆分失败：" + ex.Message;
            }
        }

        private void CommitActiveCellPreview(string text)
        {
            if (!HasSelection || OpLog == null)
                return;

            var grid = OpLog.Current;
            var anchor = grid.Structure.GetAnchorOf(new CellAddr(SelectedRow, SelectedCol));
            CommitCell(anchor, text);
        }

        private void CopySelection()
        {
            if (!HasSelection || OpLog == null)
                return;

            var grid = OpLog.Current;
            var rowCount = SelRowEnd - SelRowStart + 1;
            var colCount = SelColEnd - SelColStart + 1;
            var cells = new System.Collections.Generic.List<TableEditorClipboardCell>();

            foreach (var anchor in EnumerateSelectedAnchors())
            {
                var rowOffset = anchor.Row - SelRowStart;
                var colOffset = anchor.Col - SelColStart;
                var text = TableSummaryBuilder.FormatCellValue(HyCAD.Tables.Operations.GridEditor.GetValue(grid, anchor));
                var style = HyCAD.Tables.Operations.GridEditor.GetCellStyle(grid, anchor);
                var allowWrap = HyCAD.Tables.Operations.GridEditor.GetCellAllowWrap(grid, anchor);
                cells.Add(new TableEditorClipboardCell(rowOffset, colOffset, text, style, allowWrap));
            }

            _clipboardData = new TableEditorClipboardData(rowCount, colCount, cells);
            OnPropertyChanged(nameof(CanPaste));
            ((RelayCommand)PasteSelectionCommand).RaiseCanExecuteChanged();
            StatusMessage = $"已复制 {cells.Count} 个单元格";
        }

        private void PasteSelection()
        {
            if (!HasSelection || OpLog == null || _clipboardData == null)
                return;

            if (_clipboardData.RowCount != (SelRowEnd - SelRowStart + 1)
                || _clipboardData.ColCount != (SelColEnd - SelColStart + 1))
            {
                StatusMessage = "粘贴失败：目标选区尺寸须与复制时一致";
                return;
            }

            try
            {
                var grid = OpLog.Current;
                var topology = grid.Structure.Topology;
                var ops = new System.Collections.Generic.List<TableOperation>();

                foreach (var cell in _clipboardData.Cells)
                {
                    var row = SelRowStart + cell.RowOffset;
                    var col = SelColStart + cell.ColOffset;
                    if (row >= topology.RowCount || col >= topology.ColCount)
                        continue;

                    var addr = new CellAddr(row, col);
                    var anchor = grid.Structure.GetAnchorOf(addr);
                    ops.Add(new SetValueOp(anchor, new HyCAD.Tables.Data.CellValue(cell.Text)));
                    ops.Add(new SetStyleOp(anchor, cell.Style));
                    ops.Add(new SetCellWrapOp(anchor, cell.AllowWrap));
                }

                ApplyOperations(ops);
                StatusMessage = $"已粘贴 { _clipboardData.Cells.Count} 个单元格";
            }
            catch (Exception ex)
            {
                StatusMessage = "粘贴失败：" + ex.Message;
            }
        }

        private void ApplyTableStylePreset()
        {
            if (!HasTable || OpLog == null || SelectedStylePreset == null)
                return;

            try
            {
                var grid = OpLog.Current;
                var topology = grid.Structure.Topology;
                var ops = new System.Collections.Generic.List<TableOperation>();
                var preset = SelectedStylePreset;

                if (preset.Id == "personnel-outer")
                {
                    var r0 = 0;
                    var r1 = topology.RowCount - 1;
                    var c0 = 0;
                    var c1 = topology.ColCount - 1;
                    foreach (var anchor in EnumerateAllAnchors(grid))
                    {
                        var style = HyCAD.Tables.Operations.GridEditor.GetCellStyle(grid, anchor);
                        var rowSpan = 1;
                        var colSpan = 1;
                        if (grid.Structure.TryGetMergeAt(anchor, out var merge))
                        {
                            rowSpan = merge.RowSpan;
                            colSpan = merge.ColSpan;
                        }

                        var top = anchor.Row == r0 ? 0.7 : BorderPresetWidthMm;
                        var left = anchor.Col == c0 ? 0.7 : BorderPresetWidthMm;
                        var bottom = anchor.Row + rowSpan - 1 == r1 ? 0.7 : BorderPresetWidthMm;
                        var right = anchor.Col + colSpan - 1 == c1 ? 0.7 : BorderPresetWidthMm;
                        var borders = new BorderSet(top, right, bottom, left);
                        ops.Add(new SetStyleOp(anchor, new CellStyle(
                            style.Orientation,
                            style.HAlign,
                            style.VAlign,
                            preset.DefaultTextHeightMm,
                            style.FontKey,
                            borders,
                            style.BackColor)));
                    }
                }
                else
                {
                    var borders = preset.DefaultBorder;
                    foreach (var anchor in EnumerateAllAnchors(grid))
                    {
                        var style = HyCAD.Tables.Operations.GridEditor.GetCellStyle(grid, anchor);
                        ops.Add(new SetStyleOp(anchor, new CellStyle(
                            style.Orientation,
                            style.HAlign,
                            style.VAlign,
                            preset.DefaultTextHeightMm,
                            style.FontKey,
                            borders,
                            style.BackColor)));
                    }
                }

                ApplyOperations(ops);
                StatusMessage = $"已应用表样式「{preset.DisplayName}」";
            }
            catch (Exception ex)
            {
                StatusMessage = "表样式应用失败：" + ex.Message;
            }
        }

        private void FitColumnsToPaper()
        {
            if (!HasTable || OpLog == null)
                return;

            var grid = OpLog.Current;
            var topology = grid.Structure.Topology;
            var usable = TargetWidthMm - 2 * _marginMm;
            if (usable <= 0)
            {
                StatusMessage = "可用纸宽无效";
                return;
            }

            var total = 0.0;
            for (var c = 0; c < topology.ColCount; c++)
                total += topology.Cols[c].Size;

            if (total <= 0)
                return;

            var scale = usable / total;
            var ops = new System.Collections.Generic.List<TableOperation>();
            for (var c = 0; c < topology.ColCount; c++)
            {
                var next = topology.Cols[c].Size * scale;
                ops.Add(new SetTrackSizeOp(false, c, next));
            }

            ApplyOperations(ops);
            StatusMessage = $"列宽已缩放至纸宽 {TargetWidthMm:F0} mm（边距 {_marginMm:F0}）";
        }

        /// <summary>网页自动调整行/列高宽后批量写回 Domain track（mm）。</summary>
        public void ApplyTrackSizesBatch(bool isRow, int startIndex, System.Collections.Generic.IReadOnlyList<double> sizesMm)
        {
            if (!HasTable || sizesMm == null || sizesMm.Count == 0)
                return;

            var ops = new System.Collections.Generic.List<TableOperation>();
            for (var i = 0; i < sizesMm.Count; i++)
            {
                var mm = sizesMm[i];
                if (mm <= 0)
                    continue;
                ops.Add(new SetTrackSizeOp(isRow, startIndex + i, mm));
            }

            if (ops.Count == 0)
                return;

            ApplyOperations(ops);
            StatusMessage = isRow
                ? $"已自动调整 {sizesMm.Count} 行行高"
                : $"已自动调整 {sizesMm.Count} 列列宽";
        }

        /// <summary>解析纸面可用宽高（口径 B，已扣边距，mm）。</summary>
        public void ResolvePaperAvailable(out double availW, out double availH)
        {
            availW = TargetWidthMm;
            var preset = SelectedPaperPresetOption?.Preset ?? _viewport.PaperPreset;
            availH = PaperPresetCatalog.ResolveTargetHeightMm(preset, _marginMm, _paperOrientation);
        }

        /// <summary>
        /// 按纸面尺寸重算网格：reseedCounts 时以 5×25 推导行列数；否则保持当前行列数并均分铺满。
        /// </summary>
        public void RegenerateToPaper(bool reseedCounts)
        {
            ResolvePaperAvailable(out var availW, out var availH);
            if (availW <= 0 || availH <= 0)
            {
                StatusMessage = "可用纸面尺寸无效";
                return;
            }

            if (reseedCounts)
            {
                RowCount = ClampPaperDimension((int)Math.Round(availH / SeedRowHeightMm));
                ColCount = ClampPaperDimension((int)Math.Round(availW / SeedColWidthMm));
            }

            var targetRows = RowCount;
            var targetCols = ColCount;
            var rowH = availH / targetRows;
            var colW = availW / targetCols;

            DefaultRowHeightMm = rowH;
            DefaultColWidthMm = colW;

            if (!HasTable || OpLog == null)
            {
                UpdatePaperSummaryText();
                return;
            }

            try
            {
                var ops = new System.Collections.Generic.List<TableOperation>();
                var topology = OpLog.Current.Structure.Topology;
                var currentRows = topology.RowCount;
                var currentCols = topology.ColCount;

                while (currentRows > targetRows)
                {
                    ops.Add(new DeleteRowOp(currentRows - 1));
                    currentRows--;
                }

                while (currentRows < targetRows)
                {
                    ops.Add(new InsertRowOp(currentRows));
                    currentRows++;
                }

                while (currentCols > targetCols)
                {
                    ops.Add(new DeleteColumnOp(currentCols - 1));
                    currentCols--;
                }

                while (currentCols < targetCols)
                {
                    ops.Add(new InsertColumnOp(currentCols));
                    currentCols++;
                }

                for (var r = 0; r < targetRows; r++)
                    ops.Add(new SetTrackSizeOp(true, r, rowH));
                for (var c = 0; c < targetCols; c++)
                    ops.Add(new SetTrackSizeOp(false, c, colW));

                ApplyOperations(ops);

                if (HasSelection)
                {
                    var grid = CurrentGrid;
                    var topo = grid.Structure.Topology;
                    if (SelectedRow >= topo.RowCount || SelectedCol >= topo.ColCount)
                        SetSelectedCell(0, 0);
                    else
                        RefreshSelectionProperties();
                }

                UpdatePaperSummaryText();
                StatusMessage = reseedCounts
                    ? $"已按纸面重算 {targetRows}×{targetCols}（{rowH:0.##}×{colW:0.##} mm）"
                    : $"已按 {targetRows}×{targetCols} 均分纸面（{rowH:0.##}×{colW:0.##} mm）";
            }
            catch (Exception ex)
            {
                StatusMessage = "纸面重算失败：" + ex.Message;
            }
        }

        public override void NewEmptyTable()
        {
            RegenerateToPaper(reseedCounts: true);
            base.NewEmptyTable();
        }

        private static int ClampPaperDimension(int value) =>
            value < 1 ? 1 : value > MaxPaperDimension ? MaxPaperDimension : value;

        private static System.Collections.Generic.IEnumerable<CellAddr> EnumerateAllAnchors(TableGrid grid)
        {
            var structure = grid.Structure;
            var topology = structure.Topology;
            for (var row = 0; row < topology.RowCount; row++)
            {
                for (var col = 0; col < topology.ColCount; col++)
                {
                    var addr = new CellAddr(row, col);
                    if (structure.IsHidden(addr))
                        continue;
                    if (addr != structure.GetAnchorOf(addr))
                        continue;
                    yield return addr;
                }
            }
        }
    }
}
