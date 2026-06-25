using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HyCADTool.Features.Tables.Presentation;
using HyCADTool.Features.Tables.ViewModels;

namespace HyCADTool.Features.Tables.Views
{
    public partial class TableExcelGridView : UserControl
    {
        private TableEditorViewModel _editorVm;
        private int _builtColCount = -1;
        private bool _suppressGridSelectionSync;

        public TableExcelGridView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            MatrixGrid.CurrentCellChanged += OnCurrentCellChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_editorVm != null)
                _editorVm.PropertyChanged -= OnViewModelPropertyChanged;

            _editorVm = e.NewValue as TableEditorViewModel;
            if (_editorVm != null)
            {
                _editorVm.PropertyChanged += OnViewModelPropertyChanged;
                RebuildColumnsIfNeeded(_editorVm.MatrixColCount);
                SyncGridSelectionFromViewModel();
            }
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_editorVm == null)
                return;

            if (e.PropertyName == nameof(TableEditorViewModel.MatrixColCount))
                RebuildColumnsIfNeeded(_editorVm.MatrixColCount);

            if (e.PropertyName == nameof(TableEditorViewModel.SelectedRow)
                || e.PropertyName == nameof(TableEditorViewModel.SelectedCol))
            {
                SyncGridSelectionFromViewModel();
            }
        }

        private void OnCurrentCellChanged(object sender, System.EventArgs e)
        {
            if (_suppressGridSelectionSync || _editorVm == null)
                return;

            if (MatrixGrid.CurrentCell.Item == null || MatrixGrid.CurrentCell.Column == null)
                return;

            var row = MatrixGrid.Items.IndexOf(MatrixGrid.CurrentCell.Item);
            var col = MatrixGrid.CurrentCell.Column.DisplayIndex;
            if (row < 0 || col < 0)
                return;

            _editorVm.SetSelectedCell(row, col);
        }

        private void SyncGridSelectionFromViewModel()
        {
            if (_editorVm == null || !_editorVm.HasSelection || MatrixGrid.Items.Count == 0)
                return;

            var row = _editorVm.SelectedRow;
            var col = _editorVm.SelectedCol;
            if (row < 0 || row >= MatrixGrid.Items.Count || col < 0 || col >= MatrixGrid.Columns.Count)
                return;

            _suppressGridSelectionSync = true;
            try
            {
                var item = MatrixGrid.Items[row];
                var column = MatrixGrid.Columns[col];
                MatrixGrid.CurrentCell = new DataGridCellInfo(item, column);
                MatrixGrid.ScrollIntoView(item, column);
            }
            finally
            {
                _suppressGridSelectionSync = false;
            }
        }

        private void RebuildColumnsIfNeeded(int colCount)
        {
            if (colCount <= 0)
            {
                MatrixGrid.Columns.Clear();
                _builtColCount = -1;
                return;
            }

            if (colCount == _builtColCount)
                return;

            MatrixGrid.Columns.Clear();
            for (var col = 0; col < colCount; col++)
            {
                var colIndex = col;
                var column = new DataGridTextColumn
                {
                    Header = ColumnHeaderFormatter.Format(col),
                    Width = 64,
                    Binding = new Binding($"Cells[{colIndex}].Text")
                    {
                        UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
                        Mode = BindingMode.TwoWay,
                    },
                };

                column.ElementStyle = (Style)Resources["ExcelCellTextBlock"];
                column.EditingElementStyle = (Style)Resources["ExcelCellTextBox"];
                MatrixGrid.Columns.Add(column);
            }

            _builtColCount = colCount;
            SyncGridSelectionFromViewModel();
        }

        private void OnLoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is TableMatrixRowVm rowVm)
                e.Row.Header = rowVm.RowNumber.ToString();
        }

        private void OnBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            if (e.Column == null || !(e.Row?.Item is TableMatrixRowVm rowVm))
            {
                e.Cancel = true;
                return;
            }

            var colIndex = MatrixGrid.Columns.IndexOf(e.Column);
            if (colIndex < 0 || colIndex >= rowVm.Cells.Count)
            {
                e.Cancel = true;
                return;
            }

            var cell = rowVm.Cells[colIndex];
            if (cell.IsReadOnly || cell.IsHidden)
                e.Cancel = true;
        }
    }
}
