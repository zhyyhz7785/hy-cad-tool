using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Tables.TableApp
{
    /// <summary>
    /// HyTable 人类可读摘要（AC7，零 AutoCAD 依赖）。
    /// </summary>
    public sealed class TableSummary
    {
        public Guid TableId { get; set; }

        public string DisplayTitle { get; set; }

        public int RowCount { get; set; }

        public int ColCount { get; set; }

        public int VisibleCellCount { get; set; }

        public int MergeRegionCount { get; set; }

        public int FieldKeyCount { get; set; }

        public IReadOnlyList<string> FieldKeys { get; set; } = Array.Empty<string>();

        public int FormulaCellCount { get; set; }

        public int BoundCellCount { get; set; }

        public bool InvariantsValid { get; set; }

        public string FirstInvariantMessage { get; set; }

        public string SchemaVersion { get; set; }

        public string CarrierHandle { get; set; }
    }
}
