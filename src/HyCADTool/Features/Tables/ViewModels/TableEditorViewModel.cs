using System.Collections.ObjectModel;
using System.Linq;
using HyCAD.Tables.Layout;
using HyCADTool.Features.Tables.Presentation;

namespace HyCADTool.Features.Tables.ViewModels
{
    /// <summary>
    /// 018 独立表格编辑器窗口 ViewModel；M0：Z0 纸张 + 布局 Tab + Z3 网格 + Z8 落图。
    /// </summary>
    public sealed class TableEditorViewModel : TablePanelViewModel
    {
        private readonly TableViewport _viewport = TableViewport.CreateDefault();
        private PaperPresetOption _selectedPaperPresetOption;
        private double _targetWidthMm = PaperPresetCatalog.DefaultTargetWidthMm(PaperPreset.A3);
        private string _paperSummaryText = "A3 · 400 mm";
        private int _matrixColCount;

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
        }

        public ObservableCollection<PaperPresetOption> PaperPresetOptions { get; }

        public ObservableCollection<TableMatrixRowVm> MatrixRows { get; }

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

        protected override void RefreshRows()
        {
            RefreshMatrix();
        }

        private void RefreshMatrix()
        {
            MatrixRows.Clear();
            MatrixColCount = 0;

            var grid = CurrentGrid;
            if (grid == null)
                return;

            var matrix = TableMatrixGridAdapter.BuildMatrix(grid);
            MatrixColCount = matrix.ColCount;

            foreach (var row in matrix.Rows)
            {
                MatrixRows.Add(new TableMatrixRowVm(row, CommitCellText));
            }
        }

        protected override void RefreshSummary()
        {
            base.RefreshSummary();
            UpdatePaperSummaryText();
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
