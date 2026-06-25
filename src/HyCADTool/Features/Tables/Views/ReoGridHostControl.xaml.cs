using System.ComponentModel;
using System.Windows.Controls;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.Presentation;
using HyCADTool.Features.Tables.ViewModels;
using unvell.ReoGrid;
using unvell.ReoGrid.Events;

namespace HyCADTool.Features.Tables.Views
{
    /// <summary>
    /// ReoGrid 宿主：加载 TableGrid 快照、同步选区、单元格编辑回写 OpLog。
    /// </summary>
    public partial class ReoGridHostControl : UserControl
    {
        private TableEditorViewModel _vm;
        private Worksheet _sheet;
        private bool _suppressEvents;
        private int _lastRevision = -1;
        private int _savedSelRowStart = -1;
        private int _savedSelColStart = -1;
        private int _savedSelRowEnd = -1;
        private int _savedSelColEnd = -1;

        public ReoGridHostControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += (s, e) => EnsureSheet();
        }

        private void EnsureSheet()
        {
            if (_sheet != null)
                return;

            _sheet = GridControl.CurrentWorksheet ?? GridControl.NewWorksheet();
            _sheet.SelectionRangeChanged += OnSelectionRangeChanged;
            _sheet.CellDataChanged += OnCellDataChanged;
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null)
                _vm.PropertyChanged -= OnVmPropertyChanged;

            _vm = e.NewValue as TableEditorViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
                ReloadIfNeeded(force: true);
            }
        }

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_vm == null)
                return;

            switch (e.PropertyName)
            {
                case nameof(TableEditorViewModel.GridRevision):
                case nameof(TableEditorViewModel.HasTable):
                    ReloadIfNeeded(force: false);
                    break;
            }
        }

        private void ReloadIfNeeded(bool force)
        {
            EnsureSheet();
            if (_vm == null || _sheet == null)
                return;

            if (!force && _lastRevision == _vm.GridRevision)
                return;

            if (_vm.HasSelection)
            {
                _savedSelRowStart = _vm.SelRowStart;
                _savedSelColStart = _vm.SelColStart;
                _savedSelRowEnd = _vm.SelRowEnd;
                _savedSelColEnd = _vm.SelColEnd;
            }

            _lastRevision = _vm.GridRevision;
            var grid = _vm.EditorGrid;
            if (grid == null)
            {
                _sheet.Reset(1, 1);
                return;
            }

            _suppressEvents = true;
            try
            {
                TableGridReoGridAdapter.Load(grid, _sheet);
                RestoreSelectionIfValid(grid.Structure.Topology.RowCount, grid.Structure.Topology.ColCount);
            }
            finally
            {
                _suppressEvents = false;
            }
        }

        private void RestoreSelectionIfValid(int rowCount, int colCount)
        {
            if (_savedSelRowStart < 0 || _savedSelColStart < 0)
                return;

            if (_savedSelRowStart >= rowCount || _savedSelColStart >= colCount)
                return;

            var endRow = System.Math.Min(_savedSelRowEnd, rowCount - 1);
            var endCol = System.Math.Min(_savedSelColEnd, colCount - 1);
            if (endRow < 0)
                endRow = _savedSelRowStart;
            if (endCol < 0)
                endCol = _savedSelColStart;

            _sheet.SelectionRange = new RangePosition(
                _savedSelRowStart,
                _savedSelColStart,
                endRow - _savedSelRowStart + 1,
                endCol - _savedSelColStart + 1);
        }

        private void OnSelectionRangeChanged(object sender, RangeEventArgs e)
        {
            if (_suppressEvents || _vm == null || _sheet == null)
                return;

            var range = _sheet.SelectionRange;
            if (range.Rows <= 0 || range.Cols <= 0)
                return;

            var startRow = range.Row;
            var startCol = range.Col;
            var endRow = range.Row + range.Rows - 1;
            var endCol = range.Col + range.Cols - 1;
            _vm.SetSelectedRange(startRow, startCol, endRow, endCol);
        }

        private void OnCellDataChanged(object sender, CellEventArgs e)
        {
            if (_suppressEvents || _vm == null || _sheet == null || e.Cell == null)
                return;

            if (!TableGridReoGridAdapter.TryGetAnchor(_sheet, e.Cell.Row, e.Cell.Column, out var anchor))
                return;

            var text = e.Cell.DisplayText ?? string.Empty;
            var grid = _vm.EditorGrid;
            if (grid != null)
            {
                var style = HyCAD.Tables.Operations.GridEditor.GetCellStyle(grid, anchor);
                if (style.Orientation == TextOrientation.VerticalStacked)
                    text = text.Replace("\r", string.Empty).Replace("\n", string.Empty);
            }

            _vm.CommitCell(anchor, text);
        }
    }
}
