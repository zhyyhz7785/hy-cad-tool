using System;

using System.Collections.ObjectModel;

using System.ComponentModel;
using System.Linq;

using System.Runtime.CompilerServices;

using System.Windows;

using System.Windows.Input;

using HyCAD.Tables;

using HyCAD.Tables.Data;

using HyCAD.Tables.Operations;

using HyCAD.Tables.Structure;

using HyCADTool.Features.Tables.Presentation;

using HyCADTool.Features.Tables.TableApp;

using HyCADTool.Shell.ViewModels;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;



namespace HyCADTool.Features.Tables.ViewModels

{

    /// <summary>

    /// AC11 表格填值面板 ViewModel；016 Step1 扩展结构操作（新建空表 / 模板）。

    /// </summary>

    public class TablePanelViewModel : INotifyPropertyChanged

    {

        private const int MinDimension = 1;

        private const int MaxDimension = 256;



        private TableOpLog _opLog;

        private TablePublishContext _publishContext;

        private string _summaryText = "未加载表格 — 请「新建空表」或「拾取表」";

        private string _statusMessage = string.Empty;

        private int _rowCount = 5;

        private int _colCount = 4;

        private double _defaultRowHeightMm = TableConstants.DefaultRowHeight;

        private double _defaultColWidthMm = TableConstants.DefaultColWidth;

        private TableTemplateOption _selectedTemplate;

        private double _scale = ResolveDefaultScale();



        public TablePanelViewModel()

        {

            TemplateOptions = new ObservableCollection<TableTemplateOption>

            {

                new TableTemplateOption(TableTemplateKind.Blank, "空白"),

                new TableTemplateOption(TableTemplateKind.Personnel, "人员"),

                new TableTemplateOption(TableTemplateKind.Family, "家庭"),

            };

            _selectedTemplate = TemplateOptions[0];



            NewEmptyTableCommand = new RelayCommand(NewEmptyTable);

            LoadTemplateCommand = new RelayCommand(LoadTemplate);

            LoadSampleCommand = new RelayCommand(LoadSample);

            PickCommand = new RelayCommand(RequestPick);

            PublishCommand = new RelayCommand(RequestPublish);

        }



        /// <summary>
        /// 落图比例（口径 B：纸面 mm × Scale = 模型空间 mm）。
        /// 默认取 hy 面板 Scale；布局 Tab 可改，不持久化（重开恢复 hy 值）。
        /// </summary>
        public double Scale
        {
            get => _scale;
            set
            {
                var v = value > 0 ? value : 1.0;
                if (Math.Abs(_scale - v) < 1e-9)
                    return;
                _scale = v;
                OnPropertyChanged();
            }
        }

        private static double ResolveDefaultScale()
        {
            var s = SettingsPanelViewModel.Current?.Scale ?? 1.0;
            return s > 0 ? s : 1.0;
        }



        public ObservableCollection<TableFillRowVm> Rows { get; } = new ObservableCollection<TableFillRowVm>();



        public ObservableCollection<TableTemplateOption> TemplateOptions { get; }



        public string SummaryText

        {

            get => _summaryText;

            private set

            {

                if (_summaryText == value)

                    return;

                _summaryText = value;

                OnPropertyChanged();

            }

        }



        public string StatusMessage
        {
            get => _statusMessage;
            protected set
            {
                if (_statusMessage == value)
                    return;

                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public void SetStatusMessage(string message) => StatusMessage = message;

        /// <summary>Pick/Publish 经 _HyExec 完成后回调（Univer 宿主用于恢复 Topmost 等）。</summary>
        public Action CadInteractionCompleted { get; set; }

        public int RowCount

        {

            get => _rowCount;

            set

            {

                var clamped = ClampDimension(value);

                if (_rowCount == clamped)

                    return;

                _rowCount = clamped;

                OnPropertyChanged();

            }

        }



        public int ColCount

        {

            get => _colCount;

            set

            {

                var clamped = ClampDimension(value);

                if (_colCount == clamped)

                    return;

                _colCount = clamped;

                OnPropertyChanged();

            }

        }



        public double DefaultRowHeightMm

        {

            get => _defaultRowHeightMm;

            set

            {

                var normalized = NormalizePositiveSize(value, TableConstants.DefaultRowHeight);

                if (Math.Abs(_defaultRowHeightMm - normalized) < 0.001)

                    return;

                _defaultRowHeightMm = normalized;

                OnPropertyChanged();

            }

        }



        public double DefaultColWidthMm

        {

            get => _defaultColWidthMm;

            set

            {

                var normalized = NormalizePositiveSize(value, TableConstants.DefaultColWidth);

                if (Math.Abs(_defaultColWidthMm - normalized) < 0.001)

                    return;

                _defaultColWidthMm = normalized;

                OnPropertyChanged();

            }

        }



        public TableTemplateOption SelectedTemplate

        {

            get => _selectedTemplate;

            set

            {

                if (value == null || ReferenceEquals(_selectedTemplate, value))

                    return;

                _selectedTemplate = value;

                OnPropertyChanged();

            }

        }



        public bool HasTable => _opLog != null;



        public ICommand NewEmptyTableCommand { get; }



        public ICommand LoadTemplateCommand { get; }



        public ICommand LoadSampleCommand { get; }



        public ICommand PickCommand { get; }



        public ICommand PublishCommand { get; }



        public virtual void NewEmptyTable()

        {

            if (!TryReadTopologyInputs(out var rows, out var cols, out var rowHeight, out var colWidth, out var error))

            {

                StatusMessage = error;

                return;

            }



            var grid = TableGrid.CreateEmpty(rows, cols, rowHeight, colWidth);

            ApplyGrid(grid, null);

            StatusMessage = $"已新建 {rows}×{cols} 空表";

        }



        public void LoadTemplate()

        {

            var doc = AcApp.DocumentManager.MdiActiveDocument;

            if (doc == null)

            {

                StatusMessage = "无活动文档";

                return;

            }



            var service = new TablePanelService(doc.Database);

            var template = SelectedTemplate ?? TemplateOptions[0];



            TableGrid grid;

            string message;

            switch (template.Kind)

            {

                case TableTemplateKind.Personnel:

                    grid = service.LoadPersonnelSample();

                    message = "已加载人员基本情况样表";

                    break;

                case TableTemplateKind.Family:

                    grid = service.LoadFamilySample();

                    message = "已加载家庭成员样表";

                    break;

                default:

                    if (!TryReadTopologyInputs(out var rows, out var cols, out var rowHeight, out var colWidth, out var error))

                    {

                        StatusMessage = error;

                        return;

                    }



                    grid = TableGrid.CreateEmpty(rows, cols, rowHeight, colWidth);

                    message = $"已新建 {rows}×{cols} 空表";

                    break;

            }



            ApplyGrid(grid, null);

            StatusMessage = message;

        }



        public void LoadSample()

        {

            SelectedTemplate = TemplateOptions.First(t => t.Kind == TableTemplateKind.Personnel);

            LoadTemplate();

        }



        public void RequestPick()

        {

            SendCadCommand(() =>

            {

                var doc = AcApp.DocumentManager.MdiActiveDocument;

                var ed = doc?.Editor;

                if (ed == null)

                {

                    RunOnUi(InvokeCadInteractionCompleted);

                    return;

                }



                var service = new TablePanelService(doc.Database);

                var result = service.TryPick(ed);

                if (result == null)

                {

                    RunOnUi(() =>
                    {
                        StatusMessage = "拾取已取消或非 HyTable";
                        InvokeCadInteractionCompleted();
                    });

                    return;

                }



                var context = new TablePublishContext(

                    result.Grid.Id,

                    result.InsertionPoint,

                    result.CarrierId.Handle.ToString());



                RunOnUi(() =>

                {

                    ApplyGrid(result.Grid, context);

                    StatusMessage = "已拾取：" + result.Summary.DisplayTitle;

                    InvokeCadInteractionCompleted();

                });

            });

        }



        public void RequestPublish()

        {

            if (_opLog == null)

            {

                StatusMessage = "请先加载或拾取表格";

                InvokeCadInteractionCompleted();

                return;

            }



            SendCadCommand(() =>

            {

                var doc = AcApp.DocumentManager.MdiActiveDocument;

                var ed = doc?.Editor;

                if (ed == null)

                {

                    RunOnUi(InvokeCadInteractionCompleted);

                    return;

                }



                var service = new TablePanelService(doc.Database);

                var result = service.TryPublish(ed, _opLog.Current, Scale);

                if (result.IsCancelled)

                {

                    RunOnUi(() =>
                    {
                        StatusMessage = "写入已取消";
                        InvokeCadInteractionCompleted();
                    });

                    return;

                }



                if (result.IsFailed)

                {

                    RunOnUi(() =>
                    {
                        StatusMessage = "写入图面失败";
                        InvokeCadInteractionCompleted();
                    });

                    return;

                }



                RunOnUi(() =>

                {

                    StatusMessage = "已写入图面";

                    RefreshSummary();

                    InvokeCadInteractionCompleted();

                });

            });

        }



        internal void CommitCellText(CellAddr addr, string text)

        {

            if (_opLog == null)

                return;



            var current = GridEditor.GetValue(_opLog.Current, addr);

            var normalized = text ?? string.Empty;

            if (string.Equals(TableSummaryBuilder.FormatCellValue(current), normalized, StringComparison.Ordinal))

                return;



            try

            {

                _opLog.Apply(new SetValueOp(addr, new CellValue(normalized)));

                RefreshSummary();

                OnAfterCellCommitted(addr);

            }

            catch (Exception ex)

            {

                StatusMessage = "更新失败：" + ex.Message;

                RefreshRows();

            }

        }



        protected TableGrid CurrentGrid => _opLog?.Current;

        protected TableOpLog OpLog => _opLog;

        /// <summary>供 Univer 宿主加载外部 TableGrid（xlsx 导入等）。</summary>
        public void ApplyExternalGrid(TableGrid grid, TablePublishContext context)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));

            ApplyGrid(grid, context);
        }

        /// <summary>将 Univer 导出的文本快照合并进当前 OpLog。</summary>
        public void ApplyUniverSnapshot(UniverGridSnapshot snapshot)
        {
            if (_opLog == null || snapshot == null)
                return;

            UniverGridSnapshotMapper.EnsureGridFits(_opLog, snapshot);
            UniverGridSnapshotMapper.ApplyTextValues(_opLog, snapshot);
            RefreshRows();
            RefreshSummary();
        }

        /// <summary>
        /// 从 Univer 快照落图。<paramref name="mutateEditor"/> 为 false 时不写回编辑器 OpLog（范围落图）。
        /// </summary>
        public void RequestPublishFromSnapshot(
            UniverGridSnapshot snapshot,
            UniverClipRect clipRect,
            bool mutateEditor = true)
        {
            if (_opLog == null)
            {
                StatusMessage = "请先加载或拾取表格";
                InvokeCadInteractionCompleted();
                return;
            }

            if (snapshot == null)
            {
                StatusMessage = "exportSnapshot 返回空数据";
                InvokeCadInteractionCompleted();
                return;
            }

            TableGrid publishGrid;
            if (mutateEditor)
            {
                ApplyUniverSnapshot(snapshot);
                publishGrid = _opLog.Current;
            }
            else
            {
                if (clipRect == null)
                {
                    StatusMessage = "范围落图缺少选区信息";
                    InvokeCadInteractionCompleted();
                    return;
                }

                var extendOpLog = new TableOpLog(_opLog.Current);
                UniverGridSnapshotMapper.EnsureGridFits(extendOpLog, new UniverGridSnapshot
                {
                    RowCount = clipRect.EndRow + 1,
                    ColCount = clipRect.EndCol + 1,
                });

                var cropped = TableGridCropper.Crop(
                    extendOpLog.Current,
                    clipRect.StartRow,
                    clipRect.StartCol,
                    clipRect.EndRow,
                    clipRect.EndCol);

                var tempOpLog = new TableOpLog(cropped);
                UniverGridSnapshotMapper.EnsureGridFits(tempOpLog, snapshot);
                UniverGridSnapshotMapper.ApplyTextValues(tempOpLog, snapshot);
                publishGrid = tempOpLog.Current;
            }

            var gridToPublish = publishGrid;
            SendCadCommand(() =>
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var ed = doc?.Editor;
                if (ed == null)
                {
                    RunOnUi(InvokeCadInteractionCompleted);
                    return;
                }

                var service = new TablePanelService(doc.Database);
                var result = service.TryPublish(ed, gridToPublish, Scale);
                if (result.IsCancelled)
                {
                    RunOnUi(() =>
                    {
                        StatusMessage = "写入已取消";
                        InvokeCadInteractionCompleted();
                    });
                    return;
                }

                if (result.IsFailed)
                {
                    RunOnUi(() =>
                    {
                        StatusMessage = "写入图面失败";
                        InvokeCadInteractionCompleted();
                    });
                    return;
                }

                RunOnUi(() =>
                {
                    StatusMessage = "已写入图面";
                    RefreshSummary();
                    InvokeCadInteractionCompleted();
                });
            });
        }

        protected virtual void OnAfterApplyGrid()
        {
        }

        protected virtual void OnAfterCellCommitted(CellAddr addr)
        {
        }

        protected void ApplyOperation(TableOperation op)
        {
            if (_opLog == null)
                return;

            _opLog.Apply(op);
            RefreshRows();
            RefreshSummary();
        }

        /// <summary>批量应用多个操作，结束后只刷新一次。</summary>
        protected void ApplyOperations(System.Collections.Generic.IEnumerable<TableOperation> ops)
        {
            if (_opLog == null || ops == null)
                return;

            var applied = false;
            foreach (var op in ops)
            {
                _opLog.Apply(op);
                applied = true;
            }

            if (!applied)
                return;

            RefreshRows();
            RefreshSummary();
        }



        private void ApplyGrid(TableGrid grid, TablePublishContext context)

        {

            _opLog = new TableOpLog(grid);

            _publishContext = TablePanelService.CoalescePublishContext(context, grid);

            SyncTopologyFromGrid(grid);

            OnPropertyChanged(nameof(HasTable));

            RefreshRows();

            RefreshSummary();

            OnAfterApplyGrid();

        }



        private void SyncTopologyFromGrid(TableGrid grid)

        {

            var topology = grid.Structure.Topology;

            _rowCount = topology.RowCount;

            _colCount = topology.ColCount;

            OnPropertyChanged(nameof(RowCount));

            OnPropertyChanged(nameof(ColCount));



            if (topology.Rows.Count > 0)

            {

                _defaultRowHeightMm = topology.Rows[0].Size;

                OnPropertyChanged(nameof(DefaultRowHeightMm));

            }



            if (topology.Cols.Count > 0)

            {

                _defaultColWidthMm = topology.Cols[0].Size;

                OnPropertyChanged(nameof(DefaultColWidthMm));

            }

        }



        private bool TryReadTopologyInputs(

            out int rows,

            out int cols,

            out double rowHeight,

            out double colWidth,

            out string error)

        {

            rows = RowCount;

            cols = ColCount;

            rowHeight = DefaultRowHeightMm;

            colWidth = DefaultColWidthMm;



            if (rows < MinDimension || rows > MaxDimension)

            {

                error = $"行数须在 {MinDimension}–{MaxDimension} 之间";

                return false;

            }



            if (cols < MinDimension || cols > MaxDimension)

            {

                error = $"列数须在 {MinDimension}–{MaxDimension} 之间";

                return false;

            }



            if (rowHeight <= 0 || colWidth <= 0)

            {

                error = "行高与列宽须大于 0 mm";

                return false;

            }



            error = null;

            return true;

        }



        private static int ClampDimension(int value) =>

            value < MinDimension ? MinDimension : value > MaxDimension ? MaxDimension : value;



        private static double NormalizePositiveSize(double value, double fallback) =>

            value <= 0 ? fallback : value;



        protected virtual void RefreshRows()

        {

            Rows.Clear();

            if (_opLog == null)

                return;



            foreach (var item in TableFillGridAdapter.BuildRows(_opLog.Current))

            {

                Rows.Add(new TableFillRowVm(item, CommitCellText));

            }

        }



        protected virtual void RefreshSummary()

        {

            if (_opLog == null)

            {

                SummaryText = "未加载表格";

                return;

            }



            var grid = _opLog.Current;

            var summary = TableSummaryBuilder.Build(grid);

            var namePreview = grid.Structure.FieldIndex.TryGetValue("name", out var nameAddr)

                ? TableSummaryBuilder.FormatCellValue(GridEditor.GetValue(grid, nameAddr))

                : null;



            SummaryText = namePreview != null

                ? $"{summary.DisplayTitle} · {summary.RowCount}×{summary.ColCount} · name={namePreview}"

                : $"{summary.DisplayTitle} · {summary.RowCount}×{summary.ColCount}";



            OnPropertyChanged(nameof(HasTable));

        }



        private void SendCadCommand(Action action)

        {

            SettingsPanelViewModel.PendingCommand = action;

            try

            {

                var doc = AcApp.DocumentManager.MdiActiveDocument;

                if (doc == null)

                {

                    StatusMessage = "无活动文档";

                    SettingsPanelViewModel.PendingCommand = null;

                    InvokeCadInteractionCompleted();

                    return;

                }



                doc.SendStringToExecute("_HyExec\n", true, false, false);

            }

            catch (Exception ex)

            {

                SettingsPanelViewModel.PendingCommand = null;

                StatusMessage = "发送命令失败：" + ex.Message;

            }

        }



        private void InvokeCadInteractionCompleted()

        {

            try

            {

                CadInteractionCompleted?.Invoke();

            }

            catch

            {

                // ignore host callback failures

            }

        }



        private static void RunOnUi(Action action)

        {

            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher == null || dispatcher.CheckAccess())

            {

                action();

                return;

            }



            dispatcher.Invoke(action);

        }



        public event PropertyChangedEventHandler PropertyChanged;



        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    }



    public sealed class TableFillRowVm : INotifyPropertyChanged

    {

        private readonly Action<CellAddr, string> _commit;

        private string _text;



        public TableFillRowVm(TableFillRowItem item, Action<CellAddr, string> commit)

        {

            if (item == null)

                throw new ArgumentNullException(nameof(item));



            _commit = commit;

            Address = item.Address;

            AddressText = item.AddressText;

            RoleText = item.RoleText;

            FieldKey = item.FieldKey;

            IsEditable = item.IsEditable;

            _text = item.Text;

        }



        public CellAddr Address { get; }



        public string AddressText { get; }



        public string RoleText { get; }



        public string FieldKey { get; }



        public bool IsEditable { get; }



        public bool IsReadOnly => !IsEditable;



        public string Text

        {

            get => _text;

            set

            {

                if (!IsEditable || _text == value)

                    return;



                _text = value ?? string.Empty;

                OnPropertyChanged();

                _commit?.Invoke(Address, _text);

            }

        }



        public event PropertyChangedEventHandler PropertyChanged;



        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    }

}


