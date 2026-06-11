using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Infrastructure.AutoCadMirror;
using HyCADTool.Features.DataExchange.Hyob.Infrastructure.Diff;
using HyCADTool.Features.DataExchange.Hyob.Infrastructure.RoundTrip;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;
using HyCADTool.Features.DataExchange.Hyob.Presentation.Commands;
using HyCADTool.Shell.ViewModels;

namespace HyCADTool.Features.DataExchange.Hyob.Presentation.ViewModels
{
    /// <summary>
    /// hyob 历史面板 ViewModel（M10）。展示当前活动 DWG 旁的 .hyob/ 仓库 commit DAG，
    /// 支持选中两个 commit 做 diff（hash 级 + 字段级）；以及从 UI 触发 hyobR 自检。
    ///
    /// 数据来源：<see cref="HyobLayoutPaths"/> / <see cref="HyobObjectStore"/> /
    /// <see cref="HyobRefStore"/> / <see cref="AutoCadDatabaseMirror.WalkHistory"/>。
    /// 与 AutoCAD Document 解耦：只存储路径字符串与已加载的 commit 列表，多文档切换时由外部调用 <see cref="LoadFor"/>。
    /// </summary>
    public sealed class HyobHistoryPanelViewModel : INotifyPropertyChanged
    {
        private const int MaxCommits = 200;

        private string _repoPath;
        private string _branchName;
        private string _statusText;
        private string _diffText;
        private bool _isRepoOpen;
        private HyobHistoryItem _selectedItem;
        private HyobHistoryItem _compareLeft;
        private HyobHistoryItem _compareRight;

        public ObservableCollection<HyobHistoryItem> Commits { get; } = new ObservableCollection<HyobHistoryItem>();

        public string RepoPath
        {
            get => _repoPath;
            private set => SetField(ref _repoPath, value);
        }

        public string BranchName
        {
            get => _branchName;
            private set => SetField(ref _branchName, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetField(ref _statusText, value);
        }

        public string DiffText
        {
            get => _diffText;
            private set => SetField(ref _diffText, value);
        }

        public bool IsRepoOpen
        {
            get => _isRepoOpen;
            private set
            {
                if (SetField(ref _isRepoOpen, value))
                {
                    (RefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (RoundTripCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DiffWipCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>当前选中的 commit（单击）。点击 commit 后底部 diff 显示「该 commit vs 父 commit」。</summary>
        public HyobHistoryItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetField(ref _selectedItem, value))
                {
                    OnSelectionChanged();
                }
            }
        }

        /// <summary>双 commit 对比模式的 from 端（旧）。Ctrl + 点击设置。</summary>
        public HyobHistoryItem CompareLeft
        {
            get => _compareLeft;
            set
            {
                if (SetField(ref _compareLeft, value))
                {
                    OnSelectionChanged();
                    (DiffSelectedPairCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>双 commit 对比模式的 to 端（新）。Ctrl + 点击设置。</summary>
        public HyobHistoryItem CompareRight
        {
            get => _compareRight;
            set
            {
                if (SetField(ref _compareRight, value))
                {
                    OnSelectionChanged();
                    (DiffSelectedPairCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand DiffSelectedPairCommand { get; }
        public ICommand DiffWipCommand { get; }
        public ICommand RoundTripCommand { get; }
        public ICommand ClearComparePinsCommand { get; }

        public HyobHistoryPanelViewModel()
        {
            RefreshCommand = new RelayCommand(Refresh, () => IsRepoOpen);
            DiffSelectedPairCommand = new RelayCommand(DiffSelectedPair, () => CompareLeft != null && CompareRight != null);
            DiffWipCommand = new RelayCommand(DiffWipVsHead, () => IsRepoOpen);
            RoundTripCommand = new RelayCommand(RunRoundTrip, () => IsRepoOpen);
            ClearComparePinsCommand = new RelayCommand(() =>
            {
                CompareLeft = null;
                CompareRight = null;
            });

            StatusText = "未打开 hyob 仓库";
            DiffText = "提示：在列表中点击单个 commit → 查看与父 commit 的 diff；\n      Ctrl + 点击设置 from/to 两端 → 任意 commit 对比；\n      WIP 按钮预览未提交修改。";
        }

        /// <summary>
        /// 把面板切换到指定 DWG 的仓库。dwgPath 为空或仓库未初始化时显示空状态。
        /// 多文档切换由 PanelManager 调用。
        /// </summary>
        public void LoadFor(string dwgPath)
        {
            if (string.IsNullOrEmpty(dwgPath))
            {
                ResetEmpty("当前文档未保存");
                return;
            }
            try
            {
                var paths = new HyobLayoutPaths(dwgPath);
                if (!paths.Exists())
                {
                    ResetEmpty($"DWG 旁尚未初始化 .hyob/（执行 hyobI）");
                    return;
                }
                RepoPath = paths.HyobRoot;
                IsRepoOpen = true;
                Refresh();
            }
            catch (Exception ex)
            {
                ResetEmpty($"加载失败：{ex.Message}");
            }
        }

        private void ResetEmpty(string msg)
        {
            Commits.Clear();
            CompareLeft = null;
            CompareRight = null;
            SelectedItem = null;
            RepoPath = null;
            BranchName = null;
            DiffText = string.Empty;
            StatusText = msg;
            IsRepoOpen = false;
        }

        public void Refresh()
        {
            if (!IsRepoOpen || string.IsNullOrEmpty(RepoPath)) return;
            try
            {
                var paths = new HyobLayoutPaths(RepoPath.EndsWith(".hyob")
                    ? RepoPath.Substring(0, RepoPath.Length - ".hyob".Length)
                    : RepoPath);
                var store = new HyobObjectStore(paths.ObjectsDir);
                var refs = new HyobRefStore(paths.HyobRoot);
                if (!refs.TryReadHead(out var branch, out var detached))
                {
                    StatusText = "HEAD 未设置";
                    Commits.Clear();
                    return;
                }
                Hash startHash;
                if (branch != null)
                {
                    if (!refs.TryReadBranchTip(branch, out startHash))
                    {
                        BranchName = branch;
                        StatusText = $"分支 '{branch}' 尚无 commit";
                        Commits.Clear();
                        return;
                    }
                }
                else
                {
                    startHash = detached;
                }

                BranchName = branch ?? "(detached)";
                var mirror = new AutoCadDatabaseMirror(store, refs);

                Commits.Clear();
                int count = 0;
                foreach (var (hash, commit) in mirror.WalkHistory(startHash, MaxCommits))
                {
                    string ec = commit.Meta != null && commit.Meta.TryGetValue("entityCount", out var v) ? v : "-";
                    Commits.Add(new HyobHistoryItem(
                        shortHash: hash.ToHex().Substring(0, 10),
                        fullHash: hash.ToHex(),
                        time: commit.Time,
                        command: commit.Command,
                        message: commit.Message ?? string.Empty,
                        entityCount: ec,
                        parentCount: commit.Parents?.Count ?? 0));
                    count++;
                }
                int totalObjs = 0;
                try { totalObjs = store.EnumerateAll().Count(); } catch { }
                int packCount = store.PackCount;
                StatusText = $"分支 {BranchName} · {count} commit{(count == MaxCommits ? "（截断）" : "")} · {totalObjs} obj · {packCount} pack";
                if (count > 0 && SelectedItem == null)
                {
                    SelectedItem = Commits[0];
                }
            }
            catch (Exception ex)
            {
                StatusText = $"刷新失败：{ex.Message}";
            }
        }

        private void OnSelectionChanged()
        {
            if (CompareLeft != null && CompareRight != null)
            {
                ShowDiffBetween(CompareLeft, CompareRight, "双 commit 对比");
                return;
            }
            if (SelectedItem == null)
            {
                DiffText = string.Empty;
                return;
            }
            if (SelectedItem.ParentCount == 0)
            {
                DiffText = $"{SelectedItem.ShortHash}  {SelectedItem.Message}\n\n（root commit，无 parent）";
                return;
            }

            try
            {
                var paths = new HyobLayoutPaths(RepoPath.EndsWith(".hyob")
                    ? RepoPath.Substring(0, RepoPath.Length - ".hyob".Length)
                    : RepoPath);
                var store = new HyobObjectStore(paths.ObjectsDir);
                var refs = new HyobRefStore(paths.HyobRoot);

                var commitHash = Hash.FromHex(SelectedItem.FullHash);
                var commitBlob = HyobCommit.Decode(store.Read(commitHash));
                var parentHash = commitBlob.Parents[0];
                var differ = new TreeDiffer(store);
                var report = differ.Diff(parentHash, commitHash);
                DiffText = FormatDiff(parentHash.ToHex().Substring(0, 10), SelectedItem.ShortHash, report);
            }
            catch (Exception ex)
            {
                DiffText = $"diff 计算失败：{ex.Message}";
            }
        }

        private void DiffSelectedPair()
        {
            if (CompareLeft == null || CompareRight == null) return;
            ShowDiffBetween(CompareLeft, CompareRight, "对比 (Ctrl-pin)");
        }

        private void ShowDiffBetween(HyobHistoryItem from, HyobHistoryItem to, string label)
        {
            try
            {
                var paths = new HyobLayoutPaths(RepoPath.EndsWith(".hyob")
                    ? RepoPath.Substring(0, RepoPath.Length - ".hyob".Length)
                    : RepoPath);
                var store = new HyobObjectStore(paths.ObjectsDir);
                var differ = new TreeDiffer(store);
                var fromHash = Hash.FromHex(from.FullHash);
                var toHash = Hash.FromHex(to.FullHash);
                var report = differ.Diff(fromHash, toHash);
                DiffText = $"[{label}]\n" + FormatDiff(from.ShortHash, to.ShortHash, report);
            }
            catch (Exception ex)
            {
                DiffText = $"diff 计算失败：{ex.Message}";
            }
        }

        private void DiffWipVsHead()
        {
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    DiffText = "WIP 对比需要打开的 DWG 文档。";
                    return;
                }
                var paths = new HyobLayoutPaths(doc.Name);
                if (!paths.Exists())
                {
                    DiffText = "当前 DWG 未初始化 hyob 仓库（执行 hyobI）。";
                    return;
                }
                var store = new HyobObjectStore(paths.ObjectsDir);
                var refs = new HyobRefStore(paths.HyobRoot);
                if (!refs.TryReadHead(out var branch, out _) || branch == null
                    || !refs.TryReadBranchTip(branch, out var headHash))
                {
                    DiffText = "HEAD 未指向任何 commit。";
                    return;
                }
                var headCommit = HyobCommit.Decode(store.Read(headHash));
                var mirror = new AutoCadDatabaseMirror(store, refs);
                Hash currentTreeHash = mirror.BuildSnapshot(doc.Database).RootTreeHash;

                var differ = new TreeDiffer(store);
                var report = differ.DiffTrees(headCommit.Tree, currentTreeHash);
                DiffText = "[WIP vs HEAD]\n" + FormatDiff("HEAD", "WIP", report);
            }
            catch (Exception ex)
            {
                DiffText = $"WIP diff 失败：{ex.Message}";
            }
        }

        private void RunRoundTrip()
        {
            if (string.IsNullOrEmpty(RepoPath)) return;
            try
            {
                var paths = new HyobLayoutPaths(RepoPath.EndsWith(".hyob")
                    ? RepoPath.Substring(0, RepoPath.Length - ".hyob".Length)
                    : RepoPath);
                var store = new HyobObjectStore(paths.ObjectsDir);
                var refs = new HyobRefStore(paths.HyobRoot);
                var validator = new RoundTripValidator(store, refs);
                var report = validator.Validate();
                var sb = new StringBuilder();
                sb.AppendLine($"[hyobR 自检]");
                sb.AppendLine($"commits : {report.CommitCount}");
                sb.AppendLine($"trees   : {report.TreeCount}");
                sb.AppendLine($"objects : {report.ObjectCount}  (Opaque={report.OpaqueCount}, Typed={report.TypedCount})");
                if (report.TypedByKind.Count > 0)
                {
                    sb.Append("typed   : ");
                    var parts = report.TypedByKind
                        .OrderByDescending(kv => kv.Value)
                        .ThenBy(kv => (ushort)kv.Key)
                        .Select(kv => $"{HyobReporting.KindName(kv.Key)}={kv.Value}");
                    sb.AppendLine(string.Join(", ", parts));
                }
                if (report.Errors.Count > 0)
                {
                    sb.AppendLine($"errors  : {report.Errors.Count}");
                    foreach (var e in report.Errors.Take(8)) sb.AppendLine("  ✗ " + e);
                }
                else
                {
                    sb.AppendLine("status  : ✓ 无错误，仓库链路完整");
                }
                DiffText = sb.ToString();
            }
            catch (Exception ex)
            {
                DiffText = $"自检失败：{ex.Message}";
            }
        }

        private static string FormatDiff(string fromShort, string toShort, HyobDiffReport report)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{fromShort} → {toShort}");
            sb.AppendLine($"+{report.TotalAdded} ~{report.TotalModified} -{report.TotalDeleted} （{report.Entries.Count} 条）");
            sb.AppendLine();
            int shown = 0;
            const int Limit = 80;
            foreach (var e in report.Entries)
            {
                if (shown++ >= Limit) { sb.AppendLine("... (已截断)"); break; }
                string sign = e.Kind == HyobDiffKind.Added ? "+"
                            : e.Kind == HyobDiffKind.Deleted ? "-"
                            : "~";
                string typeName = HyobReporting.KindName(e.NewType != HyobObjectKind.Unknown ? e.NewType : e.OldType);
                sb.AppendLine($"  {sign} {Trim(e.Path, 60)}  [{typeName}]");
                if (e.Changes != null && e.Changes.Count > 0)
                {
                    foreach (var ch in e.Changes.Take(3))
                    {
                        sb.AppendLine($"      {ch.Field}: {Trim(ch.OldValue, 24)} → {Trim(ch.NewValue, 24)}");
                    }
                    if (e.Changes.Count > 3) sb.AppendLine($"      … 共 {e.Changes.Count} 项字段变更");
                }
            }
            return sb.ToString();
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }
}
