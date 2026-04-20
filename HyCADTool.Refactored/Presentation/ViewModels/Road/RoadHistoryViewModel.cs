using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;

namespace HyCADTool.Refactored.Presentation.ViewModels.Road
{
    /// <summary>
    /// 历史还原窗口（hyRoadHistory）的 ViewModel（M6.3）。
    ///
    /// <para><b>数据源</b></para>
    /// <see cref="HistoryService.LoadIndex"/> 的 <see cref="HistoryIndex.Entries"/>。
    ///
    /// <para><b>对应图 1</b></para>
    /// 表格三列：编号 / 时间 / 描述；右侧两个按钮：还原 / 取消；
    /// 底部状态栏：条目数、历史目录、最新快照时间。
    ///
    /// <para><b>与命令层的交互</b></para>
    /// 命令层（<c>RoadHistoryCommand</c>）构造 ViewModel 时传入历史目录 + <see cref="HistoryService"/>；
    /// 用户点"还原"按钮时，VM 通过 <see cref="RestoreRequested"/> 事件把选中行反馈给命令层，命令层去调用
    /// <see cref="HistoryService.Restore"/> + <c>RoadDesignRegistry.Replace</c> + 发布事件总线事件。
    /// VM 不直接调 AutoCAD API，保持可单测。
    /// </summary>
    public sealed class RoadHistoryViewModel : INotifyPropertyChanged
    {
        private readonly HistoryService _service;
        private readonly string _historyDir;

        /// <summary>表格数据，按 <see cref="HistoryEntry.SeqNo"/> 升序（新的在底部，与图 1 风格一致）。</summary>
        public ObservableCollection<HistoryEntryRow> Rows { get; } = new ObservableCollection<HistoryEntryRow>();

        public string HeaderTitle { get; }

        /// <summary>窗口底部状态栏文本。</summary>
        private string _statusText;
        public string StatusText
        {
            get => _statusText;
            private set { _statusText = value; OnPropertyChanged(); }
        }

        /// <summary>历史目录（只读展示）。</summary>
        public string HistoryDirectory => _historyDir;

        private HistoryEntryRow _selected;
        public HistoryEntryRow SelectedRow
        {
            get => _selected;
            set
            {
                if (ReferenceEquals(_selected, value)) return;
                _selected = value;
                OnPropertyChanged();
                // RelayCommand 的 CanExecuteChanged 是 noop（见 RelayCommand 源码）；
                // WPF 通过 DataGrid 绑定 Button.IsEnabled 到 SelectedRow != null 即可生效，无需手动 raise。
                OnPropertyChanged(nameof(CanRestore));
            }
        }

        /// <summary>UI 按钮 IsEnabled 绑定入口。</summary>
        public bool CanRestore => SelectedRow != null;

        public ICommand RestoreCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>用户点击「还原」按钮时触发；命令层订阅后执行实际恢复流程。</summary>
        public event EventHandler<HistoryEntry> RestoreRequested;

        /// <summary>请求关闭窗口；参数 = DialogResult（true=已还原 / false=取消）。</summary>
        public event EventHandler<bool?> CloseRequested;

        public RoadHistoryViewModel(HistoryService service, string historyDir)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _historyDir = historyDir ?? string.Empty;

            HeaderTitle = "数据还原";

            RestoreCommand = new RelayCommand(ExecuteRestore, () => SelectedRow != null);
            DeleteCommand = new RelayCommand(ExecuteDelete, () => SelectedRow != null);
            CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(this, false));

            Reload();
        }

        /// <summary>重新从磁盘加载索引。</summary>
        public void Reload()
        {
            Rows.Clear();
            var idx = _service.LoadIndex(_historyDir);
            foreach (var e in idx.Entries.OrderBy(e => e.SeqNo))
            {
                Rows.Add(HistoryEntryRow.From(e));
            }
            UpdateStatus(idx);
        }

        private void UpdateStatus(HistoryIndex idx)
        {
            if (idx == null || idx.Entries.Count == 0)
            {
                StatusText = $"无还原点 · 目录 {_historyDir}";
                return;
            }
            var last = idx.Entries.OrderByDescending(e => e.SeqNo).First();
            StatusText = $"共 {idx.Entries.Count} 条 · 最近 #{last.SeqNo} {last.TimestampLocal:yyyy-MM-dd HH:mm} · {_historyDir}";
        }

        private void ExecuteRestore()
        {
            if (SelectedRow == null) return;
            RestoreRequested?.Invoke(this, SelectedRow.Source);
            CloseRequested?.Invoke(this, true);
        }

        private void ExecuteDelete()
        {
            if (SelectedRow == null) return;
            _service.DeleteEntry(_historyDir, SelectedRow.SeqNo);
            Reload();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>
    /// 历史条目在 WPF DataGrid 里的呈现行（图 1 样式）。
    /// </summary>
    public sealed class HistoryEntryRow
    {
        public int SeqNo { get; set; }
        public string TimeText { get; set; }
        public string Description { get; set; }

        /// <summary>原始实体，<see cref="RoadHistoryViewModel.RestoreRequested"/> 携带回命令层。</summary>
        public HistoryEntry Source { get; set; }

        public static HistoryEntryRow From(HistoryEntry e)
        {
            return new HistoryEntryRow
            {
                SeqNo = e.SeqNo,
                TimeText = e.TimestampLocal.ToString("yyyy-MM-dd HH:mm"),
                Description = e.Description ?? string.Empty,
                Source = e,
            };
        }

        public override string ToString()
            => $"#{SeqNo} {TimeText} {Description}";
    }
}
