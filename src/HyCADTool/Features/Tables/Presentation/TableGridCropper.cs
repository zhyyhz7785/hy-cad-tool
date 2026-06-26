using System;
using HyCAD.Tables;
using HyCAD.Tables.Operations;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// 从完整 TableGrid 裁剪矩形子区域（含 topology / merge / data）。
    /// </summary>
    public static class TableGridCropper
    {
        public static TableGrid Crop(TableGrid source, int startRow, int startCol, int endRow, int endCol)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            ValidateBounds(source, startRow, startCol, endRow, endCol);

            var grid = source;

            for (var r = grid.Structure.Topology.RowCount - 1; r > endRow; r--)
                grid = GridEditor.DeleteRow(grid, r);

            for (var i = 0; i < startRow; i++)
                grid = GridEditor.DeleteRow(grid, 0);

            for (var c = grid.Structure.Topology.ColCount - 1; c > endCol; c--)
                grid = GridEditor.DeleteColumn(grid, c);

            for (var i = 0; i < startCol; i++)
                grid = GridEditor.DeleteColumn(grid, 0);

            return grid;
        }

        private static void ValidateBounds(TableGrid source, int startRow, int startCol, int endRow, int endCol)
        {
            var rows = source.Structure.Topology.RowCount;
            var cols = source.Structure.Topology.ColCount;

            // #region agent log
            DebugAgentLog646873.Write("H1,H4,H5", "TableGridCropper.ValidateBounds", "crop bounds check", new
            {
                gridRows = rows,
                gridCols = cols,
                startRow,
                startCol,
                endRow,
                endCol,
                endRowInRange = endRow < rows,
                endColInRange = endCol < cols,
            });
            // #endregion

            if (startRow < 0 || startCol < 0 || endRow < startRow || endCol < startCol)
                throw new ArgumentOutOfRangeException(nameof(startRow), "裁剪区域无效。");

            if (endRow >= rows || endCol >= cols)
                throw new ArgumentOutOfRangeException(nameof(endRow), "裁剪区域超出表格拓扑。");
        }
    }
}
