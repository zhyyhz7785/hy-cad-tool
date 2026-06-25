using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HyCAD.Tables.Structure;
using HyCADTool.Features.Tables.Presentation;
using HyCADTool.Features.Tables.ViewModels;

namespace HyCADTool.Features.Tables.Views
{
    /// <summary>
    /// Excel 式网格控件：真合并跨格 + 行列头点选 + 当前格强边框 + 矩形区域选区。
    /// 基于 <see cref="Grid"/> 在代码中构建可视树，结构变化时整树重建。
    /// </summary>
    public partial class ExcelGridControl : UserControl
    {
        private const double HeaderColWidth = 38;
        private const double HeaderRowHeight = 22;
        private const double ColScale = 1.6;
        private const double RowScale = 2.0;
        private const double MinColPx = 52;
        private const double MaxColPx = 260;
        private const double MinRowPx = 22;
        private const double MaxRowPx = 140;

        private static readonly Brush HeaderBg = new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));
        private static readonly Brush HeaderSelBg = new SolidColorBrush(Color.FromRgb(0xCB, 0xDD, 0xF7));
        private static readonly Brush HeaderFg = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        private static readonly Brush GridLine = new SolidColorBrush(Color.FromRgb(0xD4, 0xD4, 0xD4));
        private static readonly Brush CellBg = Brushes.White;
        private static readonly Brush HiddenBg = new SolidColorBrush(Color.FromRgb(0xEC, 0xEC, 0xEC));
        private static readonly Brush RangeBg = new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xFE));
        private static readonly Brush ActiveBorder = new SolidColorBrush(Color.FromRgb(0x21, 0x6F, 0xDB));
        private static readonly Brush PhotoBg = new SolidColorBrush(Color.FromRgb(0xF7, 0xF2, 0xE8));

        private readonly List<CellVisual> _cellVisuals = new List<CellVisual>();
        private readonly List<Border> _colHeaders = new List<Border>();
        private readonly List<Border> _rowHeaders = new List<Border>();

        private TableEditorViewModel _vm;
        private int _lastRevision = -1;
        private bool _isDragging;
        private int _dragStartRow;
        private int _dragStartCol;
        private TextBox _editor;
        private CellAddr _editorAnchor;
        private int _rowCount;
        private int _colCount;

        public ExcelGridControl()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Scroll.PreviewMouseLeftButtonUp += (s, e) => _isDragging = false;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_vm != null)
                _vm.PropertyChanged -= OnVmPropertyChanged;

            _vm = e.NewValue as TableEditorViewModel;
            if (_vm != null)
            {
                _vm.PropertyChanged += OnVmPropertyChanged;
                Rebuild();
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
                    Rebuild();
                    break;
                case nameof(TableEditorViewModel.SelectedRow):
                case nameof(TableEditorViewModel.SelectedCol):
                case nameof(TableEditorViewModel.SelRowStart):
                case nameof(TableEditorViewModel.SelColStart):
                    UpdateSelectionVisuals();
                    break;
            }
        }

        private void Rebuild()
        {
            _lastRevision = _vm?.GridRevision ?? -1;
            CommitEditor(false);

            RootGrid.Children.Clear();
            RootGrid.RowDefinitions.Clear();
            RootGrid.ColumnDefinitions.Clear();
            _cellVisuals.Clear();
            _colHeaders.Clear();
            _rowHeaders.Clear();
            _editor = null;

            var snapshot = _vm?.BuildSnapshot();
            if (snapshot == null)
            {
                _rowCount = 0;
                _colCount = 0;
                return;
            }

            _rowCount = snapshot.RowCount;
            _colCount = snapshot.ColCount;

            RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(HeaderColWidth) });
            for (var c = 0; c < snapshot.ColCount; c++)
            {
                var px = Clamp(snapshot.ColWidthsMm[c] * ColScale, MinColPx, MaxColPx);
                RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(px) });
            }

            RootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderRowHeight) });
            for (var r = 0; r < snapshot.RowCount; r++)
            {
                var px = Clamp(snapshot.RowHeightsMm[r] * RowScale, MinRowPx, MaxRowPx);
                RootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(px) });
            }

            BuildCorner();
            for (var c = 0; c < snapshot.ColCount; c++)
                BuildColumnHeader(c);
            for (var r = 0; r < snapshot.RowCount; r++)
                BuildRowHeader(r);

            foreach (var cell in snapshot.Cells)
                BuildCell(cell);

            BuildEditor();
            UpdateSelectionVisuals();
        }

        private void BuildCorner()
        {
            var corner = new Border
            {
                Background = HeaderBg,
                BorderBrush = GridLine,
                BorderThickness = new Thickness(0, 0, 1, 1),
            };
            corner.MouseLeftButtonDown += (s, e) =>
            {
                if (_rowCount > 0 && _colCount > 0)
                    _vm.SetSelectedRange(0, 0, _rowCount - 1, _colCount - 1);
            };
            Grid.SetRow(corner, 0);
            Grid.SetColumn(corner, 0);
            RootGrid.Children.Add(corner);
        }

        private void BuildColumnHeader(int col)
        {
            var header = new Border
            {
                Background = HeaderBg,
                BorderBrush = GridLine,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Child = new TextBlock
                {
                    Text = ColumnHeaderFormatter.Format(col),
                    Foreground = HeaderFg,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                Cursor = Cursors.Hand,
            };
            var c = col;
            header.MouseLeftButtonDown += (s, e) =>
            {
                if (_rowCount > 0)
                    _vm.SetSelectedRange(0, c, _rowCount - 1, c);
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, col + 1);
            RootGrid.Children.Add(header);
            _colHeaders.Add(header);
        }

        private void BuildRowHeader(int row)
        {
            var header = new Border
            {
                Background = HeaderBg,
                BorderBrush = GridLine,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Child = new TextBlock
                {
                    Text = (row + 1).ToString(),
                    Foreground = HeaderFg,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                Cursor = Cursors.Hand,
            };
            var r = row;
            header.MouseLeftButtonDown += (s, e) =>
            {
                if (_colCount > 0)
                    _vm.SetSelectedRange(r, 0, r, _colCount - 1);
            };
            Grid.SetRow(header, row + 1);
            Grid.SetColumn(header, 0);
            RootGrid.Children.Add(header);
            _rowHeaders.Add(header);
        }

        private void BuildCell(ExcelCellRender cell)
        {
            var text = new TextBlock
            {
                Text = cell.Text,
                Foreground = Brushes.Black,
                FontSize = 12,
                Margin = new Thickness(3, 1, 3, 1),
                TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = ToHorizontal(cell.HAlign),
                VerticalAlignment = ToVertical(cell.VAlign),
            };

            var border = new Border
            {
                Background = cell.IsPhotoSlot ? PhotoBg : CellBg,
                BorderBrush = GridLine,
                BorderThickness = new Thickness(0, 0, 1, 1),
                Child = text,
                Tag = cell.Anchor,
            };

            Grid.SetRow(border, cell.Anchor.Row + 1);
            Grid.SetColumn(border, cell.Anchor.Col + 1);
            if (cell.RowSpan > 1)
                Grid.SetRowSpan(border, cell.RowSpan);
            if (cell.ColSpan > 1)
                Grid.SetColumnSpan(border, cell.ColSpan);

            border.MouseLeftButtonDown += OnCellMouseDown;
            border.MouseEnter += OnCellMouseEnter;
            border.MouseLeftButtonUp += (s, e) => _isDragging = false;

            RootGrid.Children.Add(border);
            _cellVisuals.Add(new CellVisual(border, text, cell));
        }

        private void BuildEditor()
        {
            _editor = new TextBox
            {
                Visibility = Visibility.Collapsed,
                FontSize = 12,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2, 0, 2, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Brushes.White,
            };
            _editor.KeyDown += OnEditorKeyDown;
            _editor.LostFocus += (s, e) => CommitEditor(true);
            Grid.SetRow(_editor, 1);
            Grid.SetColumn(_editor, 1);
            Panel.SetZIndex(_editor, 100);
            RootGrid.Children.Add(_editor);
        }

        private void OnCellMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is Border border) || !(border.Tag is CellAddr anchor) || _vm == null)
                return;

            Focus();

            if (e.ClickCount == 2)
            {
                BeginEdit(border, anchor);
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                _vm.SetSelectedRange(_vm.SelectedRow, _vm.SelectedCol, anchor.Row, anchor.Col);
                return;
            }

            _isDragging = true;
            _dragStartRow = anchor.Row;
            _dragStartCol = anchor.Col;
            _vm.SetSelectedRange(anchor.Row, anchor.Col, anchor.Row, anchor.Col);
        }

        private void OnCellMouseEnter(object sender, MouseEventArgs e)
        {
            if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
                return;

            if (!(sender is Border border) || !(border.Tag is CellAddr anchor) || _vm == null)
                return;

            _vm.SetSelectedRange(_dragStartRow, _dragStartCol, anchor.Row, anchor.Col);
        }

        private void BeginEdit(Border border, CellAddr anchor)
        {
            if (_editor == null || _vm == null)
                return;

            var visual = _cellVisuals.Find(v => v.Anchor.Equals(anchor));
            if (visual == null || !visual.Render.IsEditable)
                return;

            _editorAnchor = anchor;
            Grid.SetRow(_editor, anchor.Row + 1);
            Grid.SetColumn(_editor, anchor.Col + 1);
            Grid.SetRowSpan(_editor, visual.Render.RowSpan);
            Grid.SetColumnSpan(_editor, visual.Render.ColSpan);
            _editor.Text = visual.Render.Text;
            _editor.Visibility = Visibility.Visible;
            _editor.Focus();
            _editor.SelectAll();
        }

        private void OnEditorKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitEditor(true);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CommitEditor(false);
                e.Handled = true;
            }
        }

        private void CommitEditor(bool commit)
        {
            if (_editor == null || _editor.Visibility != Visibility.Visible)
                return;

            var text = _editor.Text;
            _editor.Visibility = Visibility.Collapsed;

            if (commit && _vm != null)
                _vm.CommitCell(_editorAnchor, text);
        }

        private void UpdateSelectionVisuals()
        {
            if (_vm == null || !_vm.HasSelection)
            {
                foreach (var v in _cellVisuals)
                {
                    v.Border.Background = v.Render.IsPhotoSlot ? PhotoBg : CellBg;
                    v.Border.BorderBrush = GridLine;
                    v.Border.BorderThickness = new Thickness(0, 0, 1, 1);
                }
                ResetHeaderHighlight();
                return;
            }

            var activeAnchorRow = _vm.SelectedRow;
            var activeAnchorCol = _vm.SelectedCol;
            var r0 = _vm.SelRowStart;
            var r1 = _vm.SelRowEnd;
            var c0 = _vm.SelColStart;
            var c1 = _vm.SelColEnd;

            foreach (var v in _cellVisuals)
            {
                var aR = v.Render.Anchor.Row;
                var aC = v.Render.Anchor.Col;
                var aR2 = aR + v.Render.RowSpan - 1;
                var aC2 = aC + v.Render.ColSpan - 1;

                var inRange = aR <= r1 && aR2 >= r0 && aC <= c1 && aC2 >= c0;
                var isActive = aR <= activeAnchorRow && aR2 >= activeAnchorRow
                    && aC <= activeAnchorCol && aC2 >= activeAnchorCol;

                v.Border.Background = isActive
                    ? (v.Render.IsPhotoSlot ? PhotoBg : CellBg)
                    : (inRange ? RangeBg : (v.Render.IsPhotoSlot ? PhotoBg : CellBg));

                if (isActive)
                {
                    v.Border.BorderBrush = ActiveBorder;
                    v.Border.BorderThickness = new Thickness(2);
                }
                else
                {
                    v.Border.BorderBrush = GridLine;
                    v.Border.BorderThickness = new Thickness(0, 0, 1, 1);
                }
            }

            HighlightHeaders(r0, r1, c0, c1);
        }

        private void HighlightHeaders(int r0, int r1, int c0, int c1)
        {
            for (var c = 0; c < _colHeaders.Count; c++)
                _colHeaders[c].Background = c >= c0 && c <= c1 ? HeaderSelBg : HeaderBg;
            for (var r = 0; r < _rowHeaders.Count; r++)
                _rowHeaders[r].Background = r >= r0 && r <= r1 ? HeaderSelBg : HeaderBg;
        }

        private void ResetHeaderHighlight()
        {
            foreach (var h in _colHeaders)
                h.Background = HeaderBg;
            foreach (var h in _rowHeaders)
                h.Background = HeaderBg;
        }

        private static double Clamp(double v, double min, double max) =>
            v < min ? min : v > max ? max : v;

        private static HorizontalAlignment ToHorizontal(TextAlign a) => a switch
        {
            TextAlign.Start => HorizontalAlignment.Left,
            TextAlign.Center => HorizontalAlignment.Center,
            TextAlign.End => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left,
        };

        private static VerticalAlignment ToVertical(TextAlign a) => a switch
        {
            TextAlign.Start => VerticalAlignment.Top,
            TextAlign.Center => VerticalAlignment.Center,
            TextAlign.End => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center,
        };

        private sealed class CellVisual
        {
            public CellVisual(Border border, TextBlock text, ExcelCellRender render)
            {
                Border = border;
                Text = text;
                Render = render;
            }

            public Border Border { get; }

            public TextBlock Text { get; }

            public ExcelCellRender Render { get; }

            public CellAddr Anchor => Render.Anchor;
        }
    }
}
