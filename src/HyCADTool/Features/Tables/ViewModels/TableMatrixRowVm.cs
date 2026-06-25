using System;
using System.Collections.ObjectModel;
using System.Linq;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.Presentation;

namespace HyCADTool.Features.Tables.ViewModels
{
    public sealed class TableMatrixRowVm
    {
        public TableMatrixRowVm(TableMatrixRowItem item, Action<CellAddr, string> commit)
        {
            RowIndex = item.RowIndex;
            RowNumber = item.RowIndex + 1;
            Cells = new ObservableCollection<TableMatrixCellVm>(
                item.Cells.Select(c => new TableMatrixCellVm(c, commit)));
        }

        public int RowIndex { get; }

        public int RowNumber { get; }

        public ObservableCollection<TableMatrixCellVm> Cells { get; }
    }
}
