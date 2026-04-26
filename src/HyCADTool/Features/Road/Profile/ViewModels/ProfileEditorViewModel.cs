using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;

using HyCADTool.Features.Road.PlanAlignment.Domain;
using HyCADTool.Features.Road.PlanProfile.Domain;
using HyCADTool.Presentation.ViewModels;
using HyCADTool.Features.Road.PlanProfile.Views;
namespace HyCADTool.Features.Road.PlanProfile.ViewModels
{
    /// <summary>
    /// 纵断面设计窗口的 ViewModel。
    ///
    /// 职责：
    /// - 暴露 PVI 列表（<see cref="Vertices"/>，每行支持原地编辑桩号 / 标高 / 半径）；
    /// - 暴露设计速度、Alignment 长度、4 项规范检查；
    /// - 每次 PVI 列表 / 设计速度变化 → 重算 <see cref="ProfileFgDesigner.Build"/> + <see cref="ProfileCodeChecker.Check"/>，
    ///   重生成 <see cref="RenderSegments"/>（纯数据：直坡端点 / 竖曲线 BCV-PVI-ECV 控制点）；
    /// - 提供 Add / Remove PVI 命令（窗口 DataGrid 工具按钮 + Canvas 单击空白）；
    /// - Confirm 触发 <see cref="Confirmed"/> 事件，命令层接收最终 PVI 列表。
    ///
    /// 架构约束：
    /// - VM 不引用 WPF Media 类型（PathGeometry / LineSegment 等）；
    ///   <see cref="RenderSegments"/> 是纯 struct 列表，由窗口 Code-behind 构造 PathGeometry，
    ///   保证单元测试不依赖 PresentationCore，与 PiThreeUnitViewModel 保持同一风格。
    ///
    /// 数学要点（与 <see cref="ProfileFgDesigner"/> 一致）：
    /// - 二次抛物线 <c>y = h_BCV + g_in·x + ω·x²/(2L)</c> 与 QuadraticBezier(P0=BCV, P1=PVI, P2=ECV) 完全等价
    ///   （在 t = 0.5 处 Bezier 高程 = h_PVI + ω·L/8 = 抛物线在 PVI 桩号处的真实高程）。
    /// </summary>
    public sealed class ProfileEditorViewModel : INotifyPropertyChanged
    {
        private readonly Alignment _alignment;
        private readonly Profile _profile;
        private bool _suspendRecalc;

        /// <summary>用户点【确定】触发，附带最终 PVI 列表（已按桩号升序）。</summary>
        public event EventHandler<IList<ProfileVertex>> Confirmed;

        /// <summary>用户点【取消】或关闭窗口。</summary>
        public event EventHandler Cancelled;

        /// <summary>请求关闭窗口（XAML Code-behind 订阅）。bool? = DialogResult。</summary>
        public event EventHandler<bool?> CloseRequested;

        /// <summary>
        /// 二次确认回调：传入"未通过摘要"，返回 true 表示用户仍然强制确定。
        /// 单测默认 true（不弹窗）；UI Code-behind 替换为 MessageBox。
        /// </summary>
        public Func<string, bool> NonCompliantConfirm { get; set; } = _ => true;

        /// <summary>
        /// 构造。
        /// </summary>
        /// <param name="alignment">关联的平面线位（用于读取长度 / 名称，不修改）。</param>
        /// <param name="profile">要编辑的 Profile（直接读取 PVI / DesignSpeed 作为初始值，不在 VM 内修改）。</param>
        public ProfileEditorViewModel(Alignment alignment, global::HyCADTool.Domain.Models.Road.Profile profile)
        {
            _alignment = alignment ?? throw new ArgumentNullException(nameof(alignment));
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));

            Title = $"纵断面设计 — {alignment.Name ?? "(未命名)"}";
            AlignmentLength = alignment.Centerline?.GetPlanarLength() ?? 0;
            _designSpeed = profile.DesignSpeed > 0 ? profile.DesignSpeed : 60;

            Vertices = new ObservableCollection<ProfileVertexRow>();
            Vertices.CollectionChanged += OnVerticesCollectionChanged;

            // 初始化：复制 Profile 已有 PVI；若为空，按 Alignment 长度生成 2 个默认 PVI
            _suspendRecalc = true;
            if (profile.Vertices.Count > 0)
            {
                foreach (var v in profile.Vertices) AddRow(new ProfileVertex
                {
                    Id = v.Id,
                    Station = v.Station,
                    Elevation = v.Elevation,
                    CurveRadius = v.CurveRadius,
                });
            }
            else
            {
                double endStation = AlignmentLength > 1 ? AlignmentLength : 100;
                AddRow(new ProfileVertex { Station = 0, Elevation = 100 });
                AddRow(new ProfileVertex { Station = endStation, Elevation = 100 });
            }
            _suspendRecalc = false;

            ConfirmCommand = new RelayCommand(ExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);
            AddPviCommand = new RelayCommand(ExecuteAddPvi);
            RemovePviCommand = new RelayCommand<ProfileVertexRow>(ExecuteRemovePvi);

            Recalculate();
        }

        // ================= UI 绑定：标题 / 只读派生 =================

        public string Title { get; }

        /// <summary>关联 Alignment 的平面长度（m），UI 用于显示总长 / Canvas 横轴上限。</summary>
        public double AlignmentLength { get; }

        // ================= UI 绑定：可编辑参数 =================

        private int _designSpeed;
        /// <summary>
        /// 设计速度（km/h）。修改后立即触发规范重算；不在窗口生命周期内改 Profile.DesignSpeed，
        /// 由 Confirm 落盘时一并写回。
        /// </summary>
        public int DesignSpeed
        {
            get => _designSpeed;
            set { if (SetProperty(ref _designSpeed, value)) Recalculate(); }
        }

        /// <summary>设计速度备选项（驱动 ComboBox），与 <see cref="ProfileCodeChecker"/> 对齐。</summary>
        public IReadOnlyList<int> AvailableSpeeds => ProfileCodeChecker.SupportedSpeeds;

        // ================= UI 绑定：PVI 表 =================

        /// <summary>PVI 行（DataGrid 数据源）。</summary>
        public ObservableCollection<ProfileVertexRow> Vertices { get; }

        // ================= UI 绑定：规范检查 / 设计线几何 =================

        public ObservableCollection<CodeCheckItem> CheckItems { get; } = new ObservableCollection<CodeCheckItem>();

        private bool _allChecksPassed = true;
        public bool AllChecksPassed { get => _allChecksPassed; private set => SetProperty(ref _allChecksPassed, value); }

        private string _summaryText = "";
        /// <summary>UI 状态栏使用的"4/4 通过"或"2 警告 1 错误"摘要。</summary>
        public string SummaryText { get => _summaryText; private set => SetProperty(ref _summaryText, value); }

        private IReadOnlyList<ProfileRenderSegment> _renderSegments = Array.Empty<ProfileRenderSegment>();
        /// <summary>
        /// 设计线渲染段（纯数据，世界坐标：X=桩号、Y=标高）。Window code-behind 在 PropertyChanged 时
        /// 拼成 PathGeometry，再通过 RenderTransform 映射到屏幕。
        /// </summary>
        public IReadOnlyList<ProfileRenderSegment> RenderSegments
        {
            get => _renderSegments;
            private set => SetProperty(ref _renderSegments, value);
        }

        private ProfileFgResult _lastFgResult;
        public ProfileFgResult LastFgResult => _lastFgResult;

        private ProfileCheckReport _lastCheckReport;
        public ProfileCheckReport LastCheckReport => _lastCheckReport;

        // ================= UI 绑定：命令 =================

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand AddPviCommand { get; }
        public ICommand RemovePviCommand { get; }

        // ================= 对外：最终结果 =================

        /// <summary>用户点【确定】后产出的最终 PVI 列表（按桩号升序），【取消】时为 null。</summary>
        public IList<ProfileVertex> ConfirmedVertices { get; private set; }

        // ================= 行为 =================

        /// <summary>
        /// 在指定桩号插入一个 PVI（Canvas 空白单击调用）；高程取设计线在该桩号的当前值（无设计线时取相邻 PVI 线性插值）。
        /// </summary>
        public ProfileVertexRow InsertPviAt(double station, double radius = 0)
        {
            double elev = _lastFgResult != null && _lastFgResult.IsValid && _lastFgResult.Pvis.Count > 0
                ? _lastFgResult.ElevationAt(station)
                : (Vertices.Count > 0 ? Vertices[0].Elevation : 100);

            var row = AddRow(new ProfileVertex { Station = station, Elevation = elev, CurveRadius = radius });
            ResortAndRecalc();
            return row;
        }

        /// <summary>
        /// 提取当前 PVI 列表为 Domain ProfileVertex（按桩号升序）。
        /// </summary>
        public List<ProfileVertex> SnapshotVertices()
        {
            var list = new List<ProfileVertex>(Vertices.Count);
            foreach (var row in Vertices.OrderBy(r => r.Station))
            {
                list.Add(new ProfileVertex
                {
                    Id = row.Id,
                    Station = row.Station,
                    Elevation = row.Elevation,
                    CurveRadius = row.CurveRadius,
                });
            }
            return list;
        }

        /// <summary>
        /// 主重算：FG 几何 + 规范 + Canvas Path。
        /// </summary>
        public void Recalculate()
        {
            if (_suspendRecalc) return;

            // 1) 行内排序（按桩号），刷新 Index
            for (int i = 0; i < Vertices.Count; i++) Vertices[i].Index = i;

            // 2) 几何
            var domainVerts = SnapshotVertices();
            _lastFgResult = ProfileFgDesigner.Build(domainVerts);

            // 3) 把 PVI 派生量回写到 Row（驱动 DataGrid 副列）
            for (int i = 0; i < Vertices.Count && i < _lastFgResult.Pvis.Count; i++)
            {
                var info = _lastFgResult.Pvis[i];
                var row = Vertices[i];
                row.GradePercentIn = info.GradeIn * 100;
                row.GradePercentOut = info.GradeOut * 100;
                row.OmegaPercent = info.Omega * 100;
                row.CurveLength = info.CurveLength;
                row.CurveTypeText = info.CurveLength <= 0
                    ? (i == 0 || i == Vertices.Count - 1 ? "—" : "折线")
                    : (info.IsSag ? "凹" : "凸");
            }

            // 4) 规范
            _lastCheckReport = ProfileCodeChecker.Check(_designSpeed, _lastFgResult);
            CheckItems.Clear();
            foreach (var it in _lastCheckReport.Items) CheckItems.Add(it);
            AllChecksPassed = _lastCheckReport.AllPassed && _lastFgResult.IsValid;

            // 5) 摘要文本
            int passed = _lastCheckReport.Items.Count(i => i.Passed);
            int total = _lastCheckReport.Items.Count;
            int errors = _lastFgResult.Errors.Count;
            int warns = _lastFgResult.Warnings.Count;
            SummaryText = errors > 0
                ? $"{errors} 项错误 / {warns} 项警告 / 规范 {passed}/{total} 通过"
                : (warns > 0
                    ? $"{warns} 项几何告警 / 规范 {passed}/{total} 通过"
                    : (AllChecksPassed ? $"规范 {passed}/{total} 全部通过" : $"规范 {passed}/{total} 通过"));

            // 6) 渲染段（纯数据，View 拼 PathGeometry）
            RenderSegments = BuildRenderSegments(_lastFgResult);

            OnPropertyChanged(nameof(Vertices)); // 驱动 DataGrid 派生列刷新
        }

        /// <summary>
        /// 把 <see cref="ProfileFgResult.Segments"/> 转成纯数据渲染段：
        /// - Tangent → <see cref="ProfileRenderSegment"/> 直坡（仅起终点）；
        /// - VerticalCurve → 抛物线段，附带 PVI 控制点桩号 / 高程，
        ///   View 用 QuadraticBezier(P0=BCV, P1=PVI, P2=ECV) 即可精确还原 y = h_BCV + g·x + ω·x²/(2L)。
        /// </summary>
        private static IReadOnlyList<ProfileRenderSegment> BuildRenderSegments(ProfileFgResult fg)
        {
            if (fg == null || fg.Pvis.Count < 2 || fg.Segments.Count == 0)
                return Array.Empty<ProfileRenderSegment>();

            var list = new List<ProfileRenderSegment>(fg.Segments.Count);
            foreach (var seg in fg.Segments)
            {
                if (seg.Type == ProfileSegmentType.Tangent)
                {
                    list.Add(ProfileRenderSegment.Tangent(
                        seg.StartStation, seg.StartElevation,
                        seg.EndStation, seg.EndElevation));
                }
                else
                {
                    var pvi = fg.Pvis[seg.PviIndex].Vertex;
                    list.Add(ProfileRenderSegment.VerticalCurve(
                        seg.StartStation, seg.StartElevation,
                        seg.EndStation, seg.EndElevation,
                        pvi.Station, pvi.Elevation));
                }
            }
            return list;
        }

        // ================= 命令 handler =================

        private void ExecuteAddPvi()
        {
            // 在末尾再增加一个 PVI（默认放在末段中点，便于可见）
            double s = Vertices.Count > 0
                ? (Vertices[Vertices.Count - 1].Station + 100)
                : 0;
            double e = Vertices.Count > 0 ? Vertices[Vertices.Count - 1].Elevation : 100;
            AddRow(new ProfileVertex { Station = s, Elevation = e });
            ResortAndRecalc();
        }

        private void ExecuteRemovePvi(ProfileVertexRow row)
        {
            if (row == null) return;
            if (Vertices.Count <= 2) return; // 保留至少 2 个端点
            Vertices.Remove(row);
            ResortAndRecalc();
        }

        private void ExecuteConfirm()
        {
            if (_lastFgResult != null && !_lastFgResult.IsValid)
            {
                if (!NonCompliantConfirm(BuildIssuesSummary())) return;
            }
            else if (_lastCheckReport != null && !_lastCheckReport.AllPassed)
            {
                if (!NonCompliantConfirm(BuildIssuesSummary())) return;
            }

            ConfirmedVertices = SnapshotVertices();
            Confirmed?.Invoke(this, ConfirmedVertices);
            CloseRequested?.Invoke(this, true);
        }

        private void ExecuteCancel()
        {
            ConfirmedVertices = null;
            Cancelled?.Invoke(this, EventArgs.Empty);
            CloseRequested?.Invoke(this, false);
        }

        private string BuildIssuesSummary()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("当前方案存在未通过的规范 / 几何项，确定将按当前 PVI 写入 JSON。");
            sb.AppendLine();

            if (_lastFgResult != null)
            {
                foreach (var err in _lastFgResult.Errors)
                {
                    sb.Append("• [错误] ").AppendLine(err);
                }
                foreach (var w in _lastFgResult.Warnings)
                {
                    sb.Append("• [告警] ").AppendLine(w);
                }
            }
            if (_lastCheckReport != null)
            {
                foreach (var it in _lastCheckReport.Items)
                {
                    if (it.Passed) continue;
                    sb.Append("• ").Append(it.Name).Append("：").AppendLine(it.Message);
                    if (it.HasSuggestion) sb.Append("   → ").AppendLine(it.Suggestion);
                }
            }
            sb.AppendLine();
            sb.Append("是否仍然继续？");
            return sb.ToString();
        }

        // ================= 内部：行管理 =================

        private ProfileVertexRow AddRow(ProfileVertex source)
        {
            var row = new ProfileVertexRow(source);
            row.PropertyChanged += OnRowPropertyChanged;
            Vertices.Add(row);
            return row;
        }

        private void OnVerticesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (ProfileVertexRow row in e.OldItems) row.PropertyChanged -= OnRowPropertyChanged;
            }
            if (e.NewItems != null)
            {
                foreach (ProfileVertexRow row in e.NewItems) row.PropertyChanged += OnRowPropertyChanged;
            }
        }

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 派生列变化（Index/GradePercentIn/CurveLength/...）不触发重算，只有原始字段变化才需要重算
            if (e.PropertyName == nameof(ProfileVertexRow.Station)
                || e.PropertyName == nameof(ProfileVertexRow.Elevation)
                || e.PropertyName == nameof(ProfileVertexRow.CurveRadius))
            {
                ResortAndRecalc();
            }
        }

        private void ResortAndRecalc()
        {
            if (_suspendRecalc) return;

            // 排序：用 List 排好再写回 ObservableCollection（避免 in-place sort 不稳定）
            var sorted = Vertices.OrderBy(r => r.Station).ToList();
            bool changed = false;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (!ReferenceEquals(Vertices[i], sorted[i])) { changed = true; break; }
            }
            if (changed)
            {
                _suspendRecalc = true;
                Vertices.Clear();
                foreach (var r in sorted) Vertices.Add(r);
                _suspendRecalc = false;
            }
            Recalculate();
        }

        // ================= INotifyPropertyChanged =================

        public event PropertyChangedEventHandler PropertyChanged;

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 设计线渲染段（纯数据 / 不依赖 WPF）。
    ///
    /// 字段语义：
    /// - <see cref="StartStation"/> / <see cref="StartElevation"/>：段起点（世界坐标）；
    /// - <see cref="EndStation"/> / <see cref="EndElevation"/>：段终点；
    /// - <see cref="ControlStation"/> / <see cref="ControlElevation"/>：仅竖曲线段使用，
    ///   表示该段所属 PVI 的桩号 / 高程（QuadraticBezier 的 P1 控制点）。
    /// </summary>
    public readonly struct ProfileRenderSegment
    {
        public ProfileRenderSegmentKind Kind { get; }
        public double StartStation { get; }
        public double StartElevation { get; }
        public double EndStation { get; }
        public double EndElevation { get; }
        public double ControlStation { get; }
        public double ControlElevation { get; }

        private ProfileRenderSegment(
            ProfileRenderSegmentKind kind,
            double startStation, double startElevation,
            double endStation, double endElevation,
            double controlStation, double controlElevation)
        {
            Kind = kind;
            StartStation = startStation;
            StartElevation = startElevation;
            EndStation = endStation;
            EndElevation = endElevation;
            ControlStation = controlStation;
            ControlElevation = controlElevation;
        }

        public static ProfileRenderSegment Tangent(double s0, double e0, double s1, double e1)
            => new ProfileRenderSegment(ProfileRenderSegmentKind.Tangent, s0, e0, s1, e1, 0, 0);

        public static ProfileRenderSegment VerticalCurve(
            double s0, double e0, double s1, double e1, double cs, double ce)
            => new ProfileRenderSegment(ProfileRenderSegmentKind.VerticalCurve, s0, e0, s1, e1, cs, ce);
    }

    public enum ProfileRenderSegmentKind
    {
        Tangent,
        VerticalCurve,
    }

    /// <summary>
    /// PVI 表的行 ViewModel。
    ///
    /// - 主字段（Station / Elevation / CurveRadius）双向绑定到 DataGrid 编辑列；
    /// - 派生字段（GradePercent* / OmegaPercent / CurveLength / CurveTypeText）由 VM 在 <see cref="ProfileEditorViewModel.Recalculate"/>
    ///   中回填；只读列。
    /// </summary>
    public sealed class ProfileVertexRow : INotifyPropertyChanged
    {
        public Guid Id { get; }

        public ProfileVertexRow(ProfileVertex source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id;
            _station = source.Station;
            _elevation = source.Elevation;
            _curveRadius = source.CurveRadius;
        }

        // === 主字段（可编辑）===

        private double _station;
        public double Station { get => _station; set => SetProperty(ref _station, value); }

        private double _elevation;
        public double Elevation { get => _elevation; set => SetProperty(ref _elevation, value); }

        private double _curveRadius;
        public double CurveRadius
        {
            get => _curveRadius;
            set => SetProperty(ref _curveRadius, value < 0 ? 0 : value);
        }

        // === 派生字段（只读，VM 回填）===

        private int _index;
        public int Index { get => _index; set => SetProperty(ref _index, value); }

        private double _gradePercentIn;
        public double GradePercentIn { get => _gradePercentIn; set => SetProperty(ref _gradePercentIn, value); }

        private double _gradePercentOut;
        public double GradePercentOut { get => _gradePercentOut; set => SetProperty(ref _gradePercentOut, value); }

        private double _omegaPercent;
        public double OmegaPercent { get => _omegaPercent; set => SetProperty(ref _omegaPercent, value); }

        private double _curveLength;
        public double CurveLength { get => _curveLength; set => SetProperty(ref _curveLength, value); }

        private string _curveTypeText = "—";
        public string CurveTypeText { get => _curveTypeText; set => SetProperty(ref _curveTypeText, value); }

        // === INPC ===

        public event PropertyChangedEventHandler PropertyChanged;

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
