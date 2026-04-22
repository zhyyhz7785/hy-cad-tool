using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Presentation.ViewModels.Road
{
    /// <summary>
    /// "标准横断面图设计器"窗口的 ViewModel。
    ///
    /// 职责：
    /// <list type="bullet">
    ///   <item>维护左右两侧条带的 <see cref="BandRowViewModel"/>，+ 中分带宽 / 设计速度 / 比例尺。</item>
    ///   <item>每次变更后重算：<see cref="CrossSectionLayout"/> → <see cref="CrossSectionFigure"/> + 规范检查。</item>
    ///   <item>触发 <see cref="PreviewRequested"/> 让 WPF Canvas 重绘。</item>
    ///   <item>确认/取消时通过 <see cref="Confirmed"/> / <see cref="Cancelled"/> 事件回传。</item>
    ///   <item>恒可确认；有违规时走 <see cref="NonCompliantConfirm"/> 回调做二次确认。</item>
    /// </list>
    ///
    /// 承袭自 <see cref="PiThreeUnitViewModel"/> 的交互模式：
    /// - <see cref="ConfirmCommand"/> 永远 CanExecute；
    /// - WPF Window 用 <see cref="Func{T,TResult}"/> 注入 MessageBox 实现做二次确认；
    /// - VM 本身不引用 WPF.MessageBox，单测能直接构造。
    /// </summary>
    public class CrossSectionDesignerViewModel : INotifyPropertyChanged
    {
        /// <summary>支持的比例尺分母（1:50 / 1:100 / 1:200）。</summary>
        public static readonly IReadOnlyList<int> AvailableScales = new[] { 50, 100, 150, 200 };

        /// <summary>界面显示用小数位。</summary>
        public static readonly IReadOnlyList<int> AvailableDisplayPrecisions = new[] { 0, 1, 2, 3 };

        /// <summary>支持的设计速度（沿用 <see cref="AlignmentCodeChecker.SupportedSpeeds"/>）。</summary>
        public IReadOnlyList<int> AvailableSpeeds => AlignmentCodeChecker.SupportedSpeeds;

        /// <summary>供 UI 的 Kind 下拉：覆盖所有常用条带类型。</summary>
        public static readonly IReadOnlyList<TemplateComponentKind> AvailableKinds = new[]
        {
            TemplateComponentKind.Pavement,
            TemplateComponentKind.NonMotorized,
            TemplateComponentKind.Sidewalk,
            TemplateComponentKind.GreenStrip,
            TemplateComponentKind.Kerb,
            TemplateComponentKind.Shoulder,
            TemplateComponentKind.MedianStrip,
        };

        /// <summary>供 UI 的路牙类型下拉。</summary>
        public static readonly IReadOnlyList<RoadKerbType> AvailableKerbTypes = new[]
        {
            RoadKerbType.None,
            RoadKerbType.Curb,
            RoadKerbType.Plain,
            RoadKerbType.Combined,
        };

        /// <summary>供 UI 的坡型下拉。</summary>
        public static readonly IReadOnlyList<RoadSlopeType> AvailableSlopeTypes = new[]
        {
            RoadSlopeType.Single,
            RoadSlopeType.Double,
        };

        /// <summary>供 UI 的路拱形式下拉。</summary>
        public static readonly IReadOnlyList<RoadCrownProfile> AvailableCrownProfiles = new[]
        {
            RoadCrownProfile.Linear,
            RoadCrownProfile.Parabolic,
            RoadCrownProfile.Folded,
        };

        /// <summary>供 UI 的路面结构下拉。</summary>
        public static readonly IReadOnlyList<RoadSurfaceLayer> AvailableSurfaceLayers = new[]
        {
            RoadSurfaceLayer.None,
            RoadSurfaceLayer.PavementSurface,
            RoadSurfaceLayer.SidewalkPaving,
            RoadSurfaceLayer.NonMotorPaving,
            RoadSurfaceLayer.GreenSoil,
        };

        /// <summary>可用预设。</summary>
        public IReadOnlyList<PresetDescriptor> Presets => CrossSectionPresets.All;

        /// <summary>每次 <see cref="Recalculate"/> 后触发，参数为最新 Figure。</summary>
        public event EventHandler<CrossSectionFigure> PreviewRequested;

        /// <summary>用户点【确定】时触发，参数为 Template（持久化对象）+ Figure（用于出图）+ 选中的 Guid 标识。</summary>
        public event EventHandler<CrossSectionDesignerResult> Confirmed;

        /// <summary>用户点【取消】或关闭窗口时触发。</summary>
        public event EventHandler Cancelled;

        /// <summary>请求窗口关闭（View 层订阅后调用 <c>Close()</c>）。</summary>
        public event EventHandler<bool?> CloseRequested;

        /// <summary>带"有未通过项"提示的二次确认。默认放行（用于单测）。</summary>
        public Func<string, bool> NonCompliantConfirm { get; set; } = _ => true;

        private bool _isBulkUpdating;
        private bool _isRefreshing;

        private CodeCheckReport _lastReport;
        private CrossSectionFigure _lastFigure;
        private CrossSectionLayout _lastLayout;

        /// <summary>外部传入的已有模板 Guid（用于"编辑现有模板"场景）。null 表示新建。</summary>
        public Guid? ExistingTemplateId { get; }

        // =========================================================================
        //  构造
        // =========================================================================

        /// <summary>
        /// 使用预设或指定布局开始设计。<paramref name="initialLayout"/> 为 null 时用主干路预设。
        /// </summary>
        public CrossSectionDesignerViewModel(CrossSectionLayout initialLayout = null, Guid? existingTemplateId = null)
        {
            ExistingTemplateId = existingTemplateId;

            LeftBands = new ObservableCollection<BandRowViewModel>();
            RightBands = new ObservableCollection<BandRowViewModel>();

            LeftBands.CollectionChanged += OnBandCollectionChanged;
            RightBands.CollectionChanged += OnBandCollectionChanged;

            AddLeftBandCommand = new RelayCommand(() => AddBand(BandSide.Left));
            AddRightBandCommand = new RelayCommand(() => AddBand(BandSide.Right));
            RemoveBandCommand = new RelayCommand(RemoveSelectedBand, () => SelectedBand != null);
            MoveUpCommand = new RelayCommand(MoveSelectedBandUp, () => CanMoveSelected(up: true));
            MoveDownCommand = new RelayCommand(MoveSelectedBandDown, () => CanMoveSelected(up: false));
            ConfirmCommand = new RelayCommand(ExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);
            LoadPresetCommand = new RelayCommand<PresetDescriptor>(p =>
            {
                if (p == null) return;
                LoadLayout(p.Create());
            });

            LoadLayout(initialLayout ?? CrossSectionPresets.CreateCjj37UrbanArterial());
        }

        // =========================================================================
        //  布局加载 / 重新填充
        // =========================================================================

        /// <summary>
        /// 用 <paramref name="layout"/> 重填所有字段。触发一次 <see cref="Recalculate"/>。
        /// </summary>
        public void LoadLayout(CrossSectionLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));

            _isBulkUpdating = true;
            try
            {
                DetachAll();
                LeftBands.Clear();
                RightBands.Clear();
                foreach (var b in layout.LeftBands) LeftBands.Add(Wrap(b));
                foreach (var b in layout.RightBands) RightBands.Add(Wrap(b));
                AttachAll();

                SetProperty(ref _centerMedianWidth, layout.CenterMedianWidth, nameof(CenterMedianWidth));
                SetProperty(ref _designSpeed, layout.DesignSpeed, nameof(DesignSpeed));
                SetProperty(ref _scaleDenominator, layout.ScaleDenominator, nameof(ScaleDenominator));
                SetProperty(ref _title, layout.Title, nameof(Title));
                SetProperty(ref _isMirror, layout.IsSymmetric, nameof(IsMirror));

                SetProperty(ref _centerlinePosition, layout.CenterlinePosition, nameof(CenterlinePosition));
                SetProperty(ref _profileElevationOffset, layout.ProfileElevationOffset, nameof(ProfileElevationOffset));
                SetProperty(ref _isEmptyAssembly, layout.IsEmptyAssembly, nameof(IsEmptyAssembly));
                SetProperty(ref _stationStart, layout.StationStart, nameof(StationStart));
                SetProperty(ref _stationEnd, layout.StationEnd, nameof(StationEnd));
            }
            finally
            {
                _isBulkUpdating = false;
            }
            Recalculate();
        }

        private BandRowViewModel Wrap(CrossSectionBand band) => new BandRowViewModel(band);

        private void AttachAll()
        {
            foreach (var row in LeftBands) row.PropertyChanged += OnBandRowChanged;
            foreach (var row in RightBands) row.PropertyChanged += OnBandRowChanged;
        }

        private void DetachAll()
        {
            foreach (var row in LeftBands) row.PropertyChanged -= OnBandRowChanged;
            foreach (var row in RightBands) row.PropertyChanged -= OnBandRowChanged;
        }

        // =========================================================================
        //  集合：条带
        // =========================================================================

        public ObservableCollection<BandRowViewModel> LeftBands { get; }
        public ObservableCollection<BandRowViewModel> RightBands { get; }

        private BandRowViewModel _selectedBand;
        public BandRowViewModel SelectedBand
        {
            get => _selectedBand;
            set { if (SetProperty(ref _selectedBand, value)) RaiseCommandsChanged(); }
        }

        // =========================================================================
        //  其他参数
        // =========================================================================

        private double _centerMedianWidth;
        public double CenterMedianWidth
        {
            get => _centerMedianWidth;
            set { if (SetProperty(ref _centerMedianWidth, Sanitize(value))) Recalculate(); }
        }

        private int _designSpeed;
        public int DesignSpeed
        {
            get => _designSpeed;
            set { if (SetProperty(ref _designSpeed, value)) Recalculate(); }
        }

        private int _scaleDenominator;
        public int ScaleDenominator
        {
            get => _scaleDenominator;
            set { if (SetProperty(ref _scaleDenominator, value <= 0 ? 100 : value)) Recalculate(); }
        }

        private int _displayPrecision = 2;
        public int DisplayPrecision
        {
            get => _displayPrecision;
            set
            {
                int clamped = value < 0 ? 0 : (value > 3 ? 3 : value);
                SetProperty(ref _displayPrecision, clamped);
            }
        }

        private string _title;
        public string Title
        {
            get => _title;
            set { if (SetProperty(ref _title, value ?? string.Empty)) Recalculate(); }
        }

        private bool _isMirror;
        /// <summary>镜像开关：打开时左半条带变 → 同步改右半，右半条带变 → 同步改左半。</summary>
        public bool IsMirror
        {
            get => _isMirror;
            set
            {
                if (!SetProperty(ref _isMirror, value)) return;
                if (value) MirrorLeftToRight();
            }
        }

        // ============================== v2 全局字段（Layout 透传） ==============================

        private double _centerlinePosition;
        /// <summary>
        /// 中心线相对图纸原点的横向偏移（m）。<see cref="double.NaN"/> 表示自动取断面几何中心。
        /// 仅作为 Layout 持久化字段，不影响 ToFigure 渲染（v2 阶段）。
        /// </summary>
        public double CenterlinePosition
        {
            get => _centerlinePosition;
            set { if (SetProperty(ref _centerlinePosition, value)) Recalculate(); }
        }

        private double _profileElevationOffset;
        /// <summary>路面中线设计高程相对纵断面参考线的抬升量（m）。</summary>
        public double ProfileElevationOffset
        {
            get => _profileElevationOffset;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) return;
                if (SetProperty(ref _profileElevationOffset, value)) Recalculate();
            }
        }

        private bool _isEmptyAssembly;
        /// <summary>是否标记为"空装配"（仅占位，不参与出图）。</summary>
        public bool IsEmptyAssembly
        {
            get => _isEmptyAssembly;
            set { if (SetProperty(ref _isEmptyAssembly, value)) Recalculate(); }
        }

        private double _stationStart;
        /// <summary>断面适用的起始桩号（m）。<see cref="double.NaN"/> 表示不限制起始。</summary>
        public double StationStart
        {
            get => _stationStart;
            set { if (SetProperty(ref _stationStart, value)) Recalculate(); }
        }

        private double _stationEnd;
        /// <summary>断面适用的终止桩号（m）。<see cref="double.NaN"/> 表示不限制终止。</summary>
        public double StationEnd
        {
            get => _stationEnd;
            set { if (SetProperty(ref _stationEnd, value)) Recalculate(); }
        }

        // =========================================================================
        //  派生量（给 UI 读）
        // =========================================================================

        private double _leftHalfWidth;
        public double LeftHalfWidth { get => _leftHalfWidth; private set => SetProperty(ref _leftHalfWidth, value); }

        private double _rightHalfWidth;
        public double RightHalfWidth { get => _rightHalfWidth; private set => SetProperty(ref _rightHalfWidth, value); }

        private double _totalWidth;
        public double TotalWidth { get => _totalWidth; private set => SetProperty(ref _totalWidth, value); }

        public ObservableCollection<CodeCheckItem> CheckItems { get; } = new ObservableCollection<CodeCheckItem>();

        private bool _allChecksPassed = true;
        public bool AllChecksPassed { get => _allChecksPassed; private set => SetProperty(ref _allChecksPassed, value); }

        public CodeCheckReport LastReport => _lastReport;
        public CrossSectionFigure LastFigure => _lastFigure;
        public CrossSectionLayout LastLayout => _lastLayout;

        // =========================================================================
        //  命令
        // =========================================================================

        public ICommand AddLeftBandCommand { get; }
        public ICommand AddRightBandCommand { get; }
        public ICommand RemoveBandCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand LoadPresetCommand { get; }

        // =========================================================================
        //  重算核心
        // =========================================================================

        /// <summary>
        /// 重新构建 Layout / Figure / CheckReport，并触发 <see cref="PreviewRequested"/>。
        /// 批量更新时（例如 <see cref="LoadLayout"/>）会被 <see cref="_isBulkUpdating"/> 抑制。
        /// </summary>
        public void Recalculate()
        {
            if (_isBulkUpdating) return;
            if (_isRefreshing) return;

            _isRefreshing = true;
            try
            {
                _lastLayout = BuildLayout();
                _lastFigure = CrossSectionLayoutBuilder.ToFigure(_lastLayout);
                _lastReport = CrossSectionCodeChecker.Check(_lastLayout);

                LeftHalfWidth = _lastLayout.LeftHalfWidth;
                RightHalfWidth = _lastLayout.RightHalfWidth;
                TotalWidth = _lastLayout.TotalWidth;

                CheckItems.Clear();
                foreach (var it in _lastReport.Items) CheckItems.Add(it);
                AllChecksPassed = _lastReport.AllPassed;

                PreviewRequested?.Invoke(this, _lastFigure);
            }
            finally
            {
                _isRefreshing = false;
            }

            RaiseCommandsChanged();
        }

        /// <summary>
        /// 构建当前布局。子类可 override 以注入额外字段或在变换前后做装配修正。
        /// </summary>
        protected virtual CrossSectionLayout BuildLayout()
        {
            var left = LeftBands.Select(r => r.ToBand(BandSide.Left)).ToList();
            var right = RightBands.Select(r => r.ToBand(BandSide.Right)).ToList();
            return CrossSectionLayout.Create(
                left, right,
                centerMedianWidth: Math.Max(0, _centerMedianWidth),
                designSpeed: _designSpeed <= 0 ? 60 : _designSpeed,
                scaleDenominator: _scaleDenominator <= 0 ? 100 : _scaleDenominator,
                title: _title,
                centerlinePosition: _centerlinePosition,
                profileElevationOffset: _profileElevationOffset,
                isEmptyAssembly: _isEmptyAssembly,
                stationStart: _stationStart,
                stationEnd: _stationEnd);
        }

        // =========================================================================
        //  条带 CRUD + 镜像
        // =========================================================================

        private void AddBand(BandSide side)
        {
            var defaultBand = CrossSectionBand.Lane(3.5, 1.5, side, "机动车道");
            var row = Wrap(defaultBand);
            row.PropertyChanged += OnBandRowChanged;
            var target = side == BandSide.Left ? LeftBands : RightBands;
            target.Add(row);

            if (_isMirror && !_isBulkUpdating)
            {
                MirrorBandAdd(side, row);
            }

            SelectedBand = row;
            Recalculate();
        }

        private void RemoveSelectedBand()
        {
            if (SelectedBand == null) return;
            var side = FindSide(SelectedBand, out var col, out var idx);
            if (col == null) return;
            col.RemoveAt(idx);
            SelectedBand.PropertyChanged -= OnBandRowChanged;

            if (_isMirror && !_isBulkUpdating)
            {
                MirrorBandRemove(side, idx);
            }

            SelectedBand = null;
            Recalculate();
        }

        private bool CanMoveSelected(bool up)
        {
            if (SelectedBand == null) return false;
            FindSide(SelectedBand, out var col, out var idx);
            if (col == null) return false;
            return up ? idx > 0 : idx < col.Count - 1;
        }

        private void MoveSelectedBandUp()
        {
            if (SelectedBand == null) return;
            var side = FindSide(SelectedBand, out var col, out var idx);
            if (col == null || idx <= 0) return;
            col.Move(idx, idx - 1);

            if (_isMirror && !_isBulkUpdating)
            {
                MirrorBandMove(side, idx, idx - 1);
            }

            Recalculate();
        }

        private void MoveSelectedBandDown()
        {
            if (SelectedBand == null) return;
            var side = FindSide(SelectedBand, out var col, out var idx);
            if (col == null || idx >= col.Count - 1) return;
            col.Move(idx, idx + 1);

            if (_isMirror && !_isBulkUpdating)
            {
                MirrorBandMove(side, idx, idx + 1);
            }

            Recalculate();
        }

        private BandSide FindSide(BandRowViewModel row, out ObservableCollection<BandRowViewModel> collection, out int idx)
        {
            idx = LeftBands.IndexOf(row);
            if (idx >= 0) { collection = LeftBands; return BandSide.Left; }
            idx = RightBands.IndexOf(row);
            if (idx >= 0) { collection = RightBands; return BandSide.Right; }
            collection = null;
            return BandSide.Center;
        }

        private void MirrorLeftToRight()
        {
            _isBulkUpdating = true;
            try
            {
                DetachAll();
                RightBands.Clear();
                foreach (var row in LeftBands)
                {
                    var mirrored = Wrap(row.ToBand(BandSide.Right));
                    RightBands.Add(mirrored);
                }
                AttachAll();
            }
            finally
            {
                _isBulkUpdating = false;
            }
            Recalculate();
        }

        private void MirrorBandAdd(BandSide sourceSide, BandRowViewModel sourceRow)
        {
            var target = sourceSide == BandSide.Left ? RightBands : LeftBands;
            var mirroredSide = sourceSide == BandSide.Left ? BandSide.Right : BandSide.Left;
            var mirrored = Wrap(sourceRow.ToBand(mirroredSide));
            _isBulkUpdating = true;
            try
            {
                mirrored.PropertyChanged += OnBandRowChanged;
                target.Add(mirrored);
            }
            finally { _isBulkUpdating = false; }
        }

        private void MirrorBandRemove(BandSide sourceSide, int idx)
        {
            var target = sourceSide == BandSide.Left ? RightBands : LeftBands;
            if (idx < 0 || idx >= target.Count) return;
            _isBulkUpdating = true;
            try
            {
                var row = target[idx];
                row.PropertyChanged -= OnBandRowChanged;
                target.RemoveAt(idx);
            }
            finally { _isBulkUpdating = false; }
        }

        private void MirrorBandMove(BandSide sourceSide, int from, int to)
        {
            var target = sourceSide == BandSide.Left ? RightBands : LeftBands;
            if (from < 0 || to < 0 || from >= target.Count || to >= target.Count) return;
            _isBulkUpdating = true;
            try { target.Move(from, to); }
            finally { _isBulkUpdating = false; }
        }

        // =========================================================================
        //  事件
        // =========================================================================

        private void OnBandCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isBulkUpdating) return;
            if (e.NewItems != null)
            {
                foreach (BandRowViewModel r in e.NewItems)
                    r.PropertyChanged += OnBandRowChanged;
            }
            if (e.OldItems != null)
            {
                foreach (BandRowViewModel r in e.OldItems)
                    r.PropertyChanged -= OnBandRowChanged;
            }
            Recalculate();
        }

        private void OnBandRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isBulkUpdating) return;

            // 镜像：行属性变化时把对应 index 的对侧 band 更新成"镜像"（含 v2 字段）
            if (_isMirror && sender is BandRowViewModel row)
            {
                var side = FindSide(row, out var col, out var idx);
                if (col != null)
                {
                    var target = side == BandSide.Left ? RightBands : LeftBands;
                    var mirroredSide = side == BandSide.Left ? BandSide.Right : BandSide.Left;
                    if (idx >= 0 && idx < target.Count)
                    {
                        _isBulkUpdating = true;
                        try
                        {
                            var mirror = target[idx];
                            mirror.CopyFrom(row);
                            mirror.Side = mirroredSide;
                        }
                        finally { _isBulkUpdating = false; }
                    }
                }
            }
            Recalculate();
        }

        // =========================================================================
        //  确认 / 取消
        // =========================================================================

        private void ExecuteConfirm()
        {
            if (_lastReport != null && !_lastReport.AllPassed)
            {
                var summary = BuildNonCompliantSummary(_lastReport);
                if (!NonCompliantConfirm(summary)) return;
            }

            var template = CrossSectionLayoutBuilder.ToTemplate(_lastLayout,
                templateId: ExistingTemplateId,
                name: string.IsNullOrWhiteSpace(_lastLayout.Title) ? "标准横断面图" : _lastLayout.Title);
            var result = new CrossSectionDesignerResult(template, _lastFigure, _lastLayout);
            Confirmed?.Invoke(this, result);
            CloseRequested?.Invoke(this, true);
        }

        private void ExecuteCancel()
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
            CloseRequested?.Invoke(this, false);
        }

        private static string BuildNonCompliantSummary(CodeCheckReport report)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("当前方案存在未通过的规范项，继续确定将按当前参数出图与保存。");
            sb.AppendLine();
            foreach (var it in report.Items)
            {
                if (it.Passed) continue;
                sb.Append("• ").Append(it.Name).Append("：").AppendLine(it.Message);
                if (it.HasSuggestion) sb.Append("   → ").AppendLine(it.Suggestion);
            }
            sb.AppendLine();
            sb.Append("是否仍然继续？");
            return sb.ToString();
        }

        // =========================================================================
        //  辅助
        // =========================================================================

        private static double Sanitize(double v) => v < 0 ? 0 : v;

        private void RaiseCommandsChanged()
        {
            // 简单实现：WPF 使用的 RelayCommand.CanExecuteChanged 没订阅 CommandManager，
            // 数据绑定会在属性通知时重查 CanExecute（DataTrigger），足以覆盖 UI 刷新需要。
            OnPropertyChanged(nameof(SelectedBand));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 单行条带的 WPF 绑定视图模型。
    ///
    /// DataGrid / ListBox 把它作为 ItemsSource；字段改动触发 <see cref="INotifyPropertyChanged"/>，
    /// 上游 VM 订阅后调 Recalculate。
    ///
    /// <para>
    /// v2 字段：
    /// <list type="bullet">
    ///   <item>路牙：拆分为 OuterKerbType / OuterKerbModel / OuterKerbHeight / OuterKerbWidth（内侧同名 Inner*），便于 PropertyEditor 一行一项绑定。</item>
    ///   <item>坡型：<see cref="SlopeType"/>（Single/Double）。</item>
    ///   <item>路拱：<see cref="CrownProfile"/>（Linear/Parabolic/Folded）。</item>
    ///   <item>路面结构：<see cref="SurfaceLayer"/>。</item>
    ///   <item>车道数：<see cref="LaneCount"/>（仅 Pavement / NonMotorized 类型有意义）。</item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class BandRowViewModel : INotifyPropertyChanged
    {
        public BandRowViewModel(CrossSectionBand band)
        {
            _name = band.Name;
            _kind = band.Kind;
            _width = band.Width;
            _slope = band.CrossSlopePct;
            _side = band.Side;

            _outerKerbType = band.OuterKerb.Type;
            _outerKerbModel = band.OuterKerb.Model ?? string.Empty;
            _outerKerbHeight = band.OuterKerb.Height;
            _outerKerbWidth = band.OuterKerb.Width;

            _innerKerbType = band.InnerKerb.Type;
            _innerKerbModel = band.InnerKerb.Model ?? string.Empty;
            _innerKerbHeight = band.InnerKerb.Height;
            _innerKerbWidth = band.InnerKerb.Width;

            _slopeType = band.SlopeType;
            _crownProfile = band.CrownProfile;
            _surfaceLayer = band.SurfaceLayer;
            _laneCount = band.LaneCount;
            _elevationDiff = band.ElevationDiff;

            _isActive = true;

            StructureLayers = new ObservableCollection<StructureLayerNode>();
            StructureLayers.CollectionChanged += OnStructureLayersChanged;

            // 方案优先级：band 自带 → 按 Kind 默认注入 → 空（非承载结构）。
            if (band.StructureScheme != null && band.StructureScheme.Layers != null)
            {
                foreach (var layer in band.StructureScheme.Layers)
                    StructureLayers.Add(StructureLayerNode.From(layer));
            }
            else if (IsStructureBearing(band.Kind))
            {
                InjectDefaultStructure(band.Kind);
            }

            AddSurfaceLayerCommand = new RelayCommand(() => AppendLayer(StructureLayerKind.Surface), () => CanAddSurfaceLayer);
            AddBaseLayerCommand = new RelayCommand(() => AppendLayer(StructureLayerKind.Base), () => CanAddBaseLayer);
            AddSubbaseLayerCommand = new RelayCommand(() => AppendLayer(StructureLayerKind.Subbase), () => CanAddSubbaseLayer);
            RemoveSelectedLayerCommand = new RelayCommand(RemoveActiveLayer, () => _activeStructureLayer != null);
            MoveLayerUpCommand = new RelayCommand(MoveActiveLayerUp, () => CanMoveActive(up: true));
            MoveLayerDownCommand = new RelayCommand(MoveActiveLayerDown, () => CanMoveActive(up: false));
        }

        // ============================== 基础字段 ==============================

        private string _name;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, string.IsNullOrWhiteSpace(value) ? "条带" : value.Trim());
        }

        private TemplateComponentKind _kind;
        public TemplateComponentKind Kind
        {
            get => _kind;
            set
            {
                if (!SetProperty(ref _kind, value)) return;
                OnPropertyChanged(nameof(HasStructureLayers));

                // 切到承载结构的 Kind 且 StructureLayers 为空 → 从 DefaultStructureSchemes.For 注入；
                // 切到不承载结构（绿化带 / 中分带 / 缘石） → 清空层。
                if (IsStructureBearing(value))
                {
                    if (StructureLayers.Count == 0)
                        InjectDefaultStructure(value);
                }
                else
                {
                    ClearStructureLayers();
                }
                OnPropertyChanged(nameof(SurfaceLayersView));
                OnPropertyChanged(nameof(BaseLayersView));
                OnPropertyChanged(nameof(SubbaseLayersView));
                OnPropertyChanged(nameof(CanAddSurfaceLayer));
                OnPropertyChanged(nameof(CanAddBaseLayer));
                OnPropertyChanged(nameof(CanAddSubbaseLayer));
                (AddSurfaceLayerCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddBaseLayerCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddSubbaseLayerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        // ============================== v2.1: 启用状态 + 结构层 ==============================

        private bool _isActive;
        /// <summary>
        /// 大纲勾选「启用」。当前 Phase 1 仅用于 UI 视觉提示，未来 Recalculate 可据此跳过条带。
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        /// <summary>
        /// 路面结构层（面 / 基 / 垫）。由 <see cref="Kind"/> setter 按需自动注入或清空；
        /// 也可由 UI Toolbar 的 +面 / +基 / +垫 / 删除 / 上移 / 下移 命令手动增删。
        /// </summary>
        public ObservableCollection<StructureLayerNode> StructureLayers { get; }

        private StructureLayerNode _activeStructureLayer;
        /// <summary>UI 在 ListBox 中选中的那一层（用于右侧"结构层细表"区域的 DataContext）。</summary>
        public StructureLayerNode ActiveStructureLayer
        {
            get => _activeStructureLayer;
            set
            {
                if (!SetProperty(ref _activeStructureLayer, value)) return;
                (RemoveSelectedLayerCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveLayerUpCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (MoveLayerDownCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// UI 派生：当前 Kind 是否承载结构层（Pavement / NonMotorized / Sidewalk）。
        /// 决定右侧"路面结构层"Expander 的可见性。
        /// </summary>
        public bool HasStructureLayers => IsStructureBearing(_kind);

        public IEnumerable<StructureLayerNode> SurfaceLayersView => StructureLayers.Where(l => l.LayerKind == StructureLayerKind.Surface);
        public IEnumerable<StructureLayerNode> BaseLayersView => StructureLayers.Where(l => l.LayerKind == StructureLayerKind.Base);
        public IEnumerable<StructureLayerNode> SubbaseLayersView => StructureLayers.Where(l => l.LayerKind == StructureLayerKind.Subbase);

        public bool CanAddSurfaceLayer => StructureLayers.Count(l => l.LayerKind == StructureLayerKind.Surface) < 3;
        public bool CanAddBaseLayer => StructureLayers.Count(l => l.LayerKind == StructureLayerKind.Base) < 5;
        public bool CanAddSubbaseLayer => StructureLayers.Count(l => l.LayerKind == StructureLayerKind.Subbase) < 2;

        // ============================== 结构层命令 ==============================

        public ICommand AddSurfaceLayerCommand { get; }
        public ICommand AddBaseLayerCommand { get; }
        public ICommand AddSubbaseLayerCommand { get; }
        public ICommand RemoveSelectedLayerCommand { get; }
        public ICommand MoveLayerUpCommand { get; }
        public ICommand MoveLayerDownCommand { get; }

        private static bool IsStructureBearing(TemplateComponentKind kind)
            => kind == TemplateComponentKind.Pavement
               || kind == TemplateComponentKind.NonMotorized
               || kind == TemplateComponentKind.Sidewalk;

        private void InjectDefaultStructure(TemplateComponentKind kind)
        {
            var scheme = DefaultStructureSchemes.For(kind);
            if (scheme == null) return;
            foreach (var layer in scheme.Layers)
            {
                StructureLayers.Add(StructureLayerNode.From(layer));
            }
        }

        private void ClearStructureLayers()
        {
            if (StructureLayers.Count == 0) return;
            StructureLayers.Clear();
            ActiveStructureLayer = null;
        }

        private void AppendLayer(StructureLayerKind kind)
        {
            if ((kind == StructureLayerKind.Surface && !CanAddSurfaceLayer)
                || (kind == StructureLayerKind.Base && !CanAddBaseLayer)
                || (kind == StructureLayerKind.Subbase && !CanAddSubbaseLayer))
            {
                return;
            }

            var node = new StructureLayerNode
            {
                Name = DefaultNameFor(kind),
                LayerKind = kind,
                ThicknessCm = DefaultThicknessFor(kind),
            };
            StructureLayers.Add(node);
            ActiveStructureLayer = node;
        }

        private void RemoveActiveLayer()
        {
            if (_activeStructureLayer == null) return;
            int idx = StructureLayers.IndexOf(_activeStructureLayer);
            if (idx < 0) return;
            StructureLayers.RemoveAt(idx);
            ActiveStructureLayer = StructureLayers.Count == 0
                ? null
                : StructureLayers[Math.Min(idx, StructureLayers.Count - 1)];
        }

        private bool CanMoveActive(bool up)
        {
            if (_activeStructureLayer == null) return false;
            int idx = StructureLayers.IndexOf(_activeStructureLayer);
            if (idx < 0) return false;
            return up ? idx > 0 : idx < StructureLayers.Count - 1;
        }

        private void MoveActiveLayerUp()
        {
            int idx = StructureLayers.IndexOf(_activeStructureLayer);
            if (idx <= 0) return;
            StructureLayers.Move(idx, idx - 1);
        }

        private void MoveActiveLayerDown()
        {
            int idx = StructureLayers.IndexOf(_activeStructureLayer);
            if (idx < 0 || idx >= StructureLayers.Count - 1) return;
            StructureLayers.Move(idx, idx + 1);
        }

        private void OnStructureLayersChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            // 集合变化触发 ToBand 重新包一份方案，VM 层 OnBandRowChanged 会 Recalculate。
            OnPropertyChanged(nameof(StructureLayers));
            OnPropertyChanged(nameof(SurfaceLayersView));
            OnPropertyChanged(nameof(BaseLayersView));
            OnPropertyChanged(nameof(SubbaseLayersView));
            OnPropertyChanged(nameof(CanAddSurfaceLayer));
            OnPropertyChanged(nameof(CanAddBaseLayer));
            OnPropertyChanged(nameof(CanAddSubbaseLayer));
            (AddSurfaceLayerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddBaseLayerCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddSubbaseLayerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private static string DefaultNameFor(StructureLayerKind kind)
        {
            switch (kind)
            {
                case StructureLayerKind.Surface: return "面层";
                case StructureLayerKind.Base: return "基层";
                case StructureLayerKind.Subbase: return "垫层";
                default: return "结构层";
            }
        }

        private static double DefaultThicknessFor(StructureLayerKind kind)
        {
            switch (kind)
            {
                case StructureLayerKind.Surface: return 4;
                case StructureLayerKind.Base: return 20;
                case StructureLayerKind.Subbase: return 15;
                default: return 10;
            }
        }

        private double _width;
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value <= 0 ? 0.01 : value);
        }

        private double _slope;
        public double CrossSlopePct
        {
            get => _slope;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) return;
                double clamped = Math.Max(-20, Math.Min(20, value));
                SetProperty(ref _slope, clamped);
            }
        }

        private double _elevationDiff;
        public double ElevationDiff
        {
            get => _elevationDiff;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value)) return;
                double clamped = Math.Max(-2.0, Math.Min(2.0, value));
                SetProperty(ref _elevationDiff, clamped);
            }
        }

        private BandSide _side;
        public BandSide Side
        {
            get => _side;
            set => SetProperty(ref _side, value);
        }

        private bool _isSelected;
        /// <summary>
        /// TreeView 选中状态的两向绑定载体。VM 层在 <c>SelectedBand</c> 变化时同步设置，
        /// XAML 层 <c>TreeViewItem.IsSelected</c> TwoWay 绑定它，实现 VM↔TreeView 双向选中。
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // ============================== v2: 外侧路牙 ==============================

        private RoadKerbType _outerKerbType;
        /// <summary>外侧路牙类型；None 表示无路牙凸起。</summary>
        public RoadKerbType OuterKerbType
        {
            get => _outerKerbType;
            set
            {
                if (!SetProperty(ref _outerKerbType, value)) return;
                // 切到非 None 且高度/宽度仍为 0 时，自动套用对应预设的尺寸。
                if (value != RoadKerbType.None && _outerKerbHeight <= 1e-6)
                {
                    var preset = PickKerbPreset(value);
                    SetProperty(ref _outerKerbModel, preset.Model, nameof(OuterKerbModel));
                    SetProperty(ref _outerKerbHeight, preset.Height, nameof(OuterKerbHeight));
                    SetProperty(ref _outerKerbWidth, preset.Width, nameof(OuterKerbWidth));
                }
                OnPropertyChanged(nameof(HasOuterKerb));
            }
        }

        private string _outerKerbModel;
        public string OuterKerbModel
        {
            get => _outerKerbModel;
            set => SetProperty(ref _outerKerbModel, value ?? string.Empty);
        }

        private double _outerKerbHeight;
        public double OuterKerbHeight
        {
            get => _outerKerbHeight;
            set => SetProperty(ref _outerKerbHeight, value < 0 ? 0 : value);
        }

        private double _outerKerbWidth;
        public double OuterKerbWidth
        {
            get => _outerKerbWidth;
            set => SetProperty(ref _outerKerbWidth, value < 0 ? 0 : value);
        }

        /// <summary>UI 派生：外侧路牙是否生效（用于属性面板"路牙尺寸"分组的可见性）。</summary>
        public bool HasOuterKerb => _outerKerbType != RoadKerbType.None && _outerKerbHeight > 1e-6;

        // ============================== v2: 内侧路牙 ==============================

        private RoadKerbType _innerKerbType;
        public RoadKerbType InnerKerbType
        {
            get => _innerKerbType;
            set
            {
                if (!SetProperty(ref _innerKerbType, value)) return;
                if (value != RoadKerbType.None && _innerKerbHeight <= 1e-6)
                {
                    var preset = PickKerbPreset(value);
                    SetProperty(ref _innerKerbModel, preset.Model, nameof(InnerKerbModel));
                    SetProperty(ref _innerKerbHeight, preset.Height, nameof(InnerKerbHeight));
                    SetProperty(ref _innerKerbWidth, preset.Width, nameof(InnerKerbWidth));
                }
                OnPropertyChanged(nameof(HasInnerKerb));
            }
        }

        private string _innerKerbModel;
        public string InnerKerbModel
        {
            get => _innerKerbModel;
            set => SetProperty(ref _innerKerbModel, value ?? string.Empty);
        }

        private double _innerKerbHeight;
        public double InnerKerbHeight
        {
            get => _innerKerbHeight;
            set => SetProperty(ref _innerKerbHeight, value < 0 ? 0 : value);
        }

        private double _innerKerbWidth;
        public double InnerKerbWidth
        {
            get => _innerKerbWidth;
            set => SetProperty(ref _innerKerbWidth, value < 0 ? 0 : value);
        }

        public bool HasInnerKerb => _innerKerbType != RoadKerbType.None && _innerKerbHeight > 1e-6;

        // ============================== v2: 坡型 / 路拱 / 铺装 / 车道数 ==============================

        private RoadSlopeType _slopeType;
        public RoadSlopeType SlopeType
        {
            get => _slopeType;
            set => SetProperty(ref _slopeType, value);
        }

        private RoadCrownProfile _crownProfile;
        public RoadCrownProfile CrownProfile
        {
            get => _crownProfile;
            set => SetProperty(ref _crownProfile, value);
        }

        private RoadSurfaceLayer _surfaceLayer;
        public RoadSurfaceLayer SurfaceLayer
        {
            get => _surfaceLayer;
            set => SetProperty(ref _surfaceLayer, value);
        }

        private int _laneCount;
        public int LaneCount
        {
            get => _laneCount;
            set => SetProperty(ref _laneCount, value < 0 ? 0 : value);
        }

        // ============================== 转换 ==============================

        public CrossSectionBand ToBand(BandSide? overrideSide = null)
        {
            var outerKerb = OuterKerbType == RoadKerbType.None
                ? KerbSpec.None
                : new KerbSpec(OuterKerbType, OuterKerbModel, OuterKerbHeight, OuterKerbWidth);
            var innerKerb = InnerKerbType == RoadKerbType.None
                ? KerbSpec.None
                : new KerbSpec(InnerKerbType, InnerKerbModel, InnerKerbHeight, InnerKerbWidth);

            StructureLayerScheme scheme = null;
            if (IsStructureBearing(Kind) && StructureLayers.Count > 0)
            {
                scheme = new StructureLayerScheme
                {
                    Name = $"{Name}(结构)",
                    IsBuiltIn = false,
                };
                foreach (var node in StructureLayers)
                {
                    scheme.Layers.Add(node.ToModel());
                }
            }

            return new CrossSectionBand(
                Name, Kind, Width, CrossSlopePct, overrideSide ?? Side,
                outerKerb, innerKerb,
                SlopeType, CrownProfile, SurfaceLayer, LaneCount,
                scheme, ElevationDiff);
        }

        /// <summary>
        /// 从镜像源行复制全部 v2 字段（不含 Side，由调用方决定）。
        /// 结构层做深拷贝：每个 <see cref="StructureLayerNode"/> 新建一份实例，避免镜像后两侧共享节点。
        /// </summary>
        public void CopyFrom(BandRowViewModel source)
        {
            if (source == null) return;
            Name = source.Name;
            Kind = source.Kind;
            Width = source.Width;
            CrossSlopePct = source.CrossSlopePct;
            OuterKerbType = source.OuterKerbType;
            OuterKerbModel = source.OuterKerbModel;
            OuterKerbHeight = source.OuterKerbHeight;
            OuterKerbWidth = source.OuterKerbWidth;
            InnerKerbType = source.InnerKerbType;
            InnerKerbModel = source.InnerKerbModel;
            InnerKerbHeight = source.InnerKerbHeight;
            InnerKerbWidth = source.InnerKerbWidth;
            SlopeType = source.SlopeType;
            CrownProfile = source.CrownProfile;
            SurfaceLayer = source.SurfaceLayer;
            LaneCount = source.LaneCount;
            ElevationDiff = source.ElevationDiff;
            IsActive = source.IsActive;

            // 结构层深拷贝：先清空（避免 Kind setter 已经按新 Kind 注入的默认层与源层混在一起），
            // 再按源行的节点逐个 From(ToModel()) 复制，保留厚度 / 填料 / 加宽 / 坡度等。
            StructureLayers.Clear();
            foreach (var srcLayer in source.StructureLayers)
            {
                StructureLayers.Add(StructureLayerNode.From(srcLayer.ToModel()));
            }
            ActiveStructureLayer = null;
        }

        private static KerbSpec PickKerbPreset(RoadKerbType type)
        {
            switch (type)
            {
                case RoadKerbType.Curb: return KerbSpec.DefaultCurb();
                case RoadKerbType.Plain: return KerbSpec.DefaultPlain();
                case RoadKerbType.Combined: return KerbSpec.DefaultCombined();
                default: return KerbSpec.None;
            }
        }

        // ============================== INPC ==============================

        public event PropertyChangedEventHandler PropertyChanged;
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// 设计器确认时回传的完整结果包。
    /// </summary>
    public sealed class CrossSectionDesignerResult
    {
        public Template Template { get; }
        public CrossSectionFigure Figure { get; }
        public CrossSectionLayout Layout { get; }

        public CrossSectionDesignerResult(Template template, CrossSectionFigure figure, CrossSectionLayout layout)
        {
            Template = template;
            Figure = figure;
            Layout = layout;
        }
    }
}
