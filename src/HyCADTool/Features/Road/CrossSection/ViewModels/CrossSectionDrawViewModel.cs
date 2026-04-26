using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Presentation.ViewModels;
using HyCADTool.App.Bootstrap;
using HyCADTool.Shared.AutoCAD.Services.Road;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Features.Road.CrossSection.Domain;

namespace HyCADTool.Features.Road.CrossSection.ViewModels
{
    /// <summary>
    /// "横断绘制"窗口（v2 BlenderUI 风格四区布局）的 ViewModel。
    ///
    /// <para>职责（在 <see cref="CrossSectionDesignerViewModel"/> 之上扩展）：</para>
    /// <list type="bullet">
    ///   <item>底部 StatusBar：实时汇总左/分隔带/右板块数 + 总宽 + 规范通过率。</item>
    ///   <item>左 Outliner / 右 PropertyEditor 折叠状态（<see cref="IsOutlinerVisible"/> /
    ///       <see cref="IsPropertyPaneVisible"/>）。</item>
    ///   <item>"复制选中条带"/"粘贴条带"命令（提升复用工作流效率）。</item>
    ///   <item><see cref="DrawCommand"/>：与 ConfirmCommand 等价（语义命名）。</item>
    /// </list>
    ///
    /// <para>预览 / 重算 / 命令路由 / 镜像 / 规范检查 全部沿用基类，本类不重复实现。</para>
    /// </summary>
    public sealed class CrossSectionDrawViewModel : CrossSectionDesignerViewModel
    {
        private readonly CrossSectionPresetService _presetService;
        private IReadOnlyList<PresetDescriptor> _presetsCache;

        /// <summary>
        /// v2 预设列表：内置 + 用户 JSON 预设（若服务不可用则退回内置）。
        /// </summary>
        public new IReadOnlyList<PresetDescriptor> Presets
            => _presetsCache ?? (_presetsCache = LoadPresetsSafe());

        public CrossSectionDrawViewModel(CrossSectionLayout initialLayout = null, Guid? existingTemplateId = null)
            : base(initialLayout, existingTemplateId)
        {
            _presetService = TryResolvePresetService();
            RefreshPresets();

            CopySelectedBandCommand = new RelayCommand(ExecuteCopySelected, () => SelectedBand != null);
            PasteBandCommand = new RelayCommand(ExecutePasteToSelectedSide, () => _bandClipboard != null);
            InsertBandAfterSelectedCommand = new RelayCommand<TemplateComponentKind>(ExecuteInsertBandAfterSelected);
            InsertBandBeforeSelectedCommand = new RelayCommand<TemplateComponentKind>(ExecuteInsertBandBeforeSelected);
            PickBandGeometryCommand = new RelayCommand(() => PickGeometryRequested?.Invoke(this, SelectedBand), () => SelectedBand != null);
            ToggleOutlinerCommand = new RelayCommand(() => IsOutlinerVisible = !IsOutlinerVisible);
            TogglePropertyPaneCommand = new RelayCommand(() => IsPropertyPaneVisible = !IsPropertyPaneVisible);
            RestoreDefaultFillMaterialPresetsCommand = new RelayCommand(RestoreDefaultFillMaterialPresets);
            RefreshDwgBlockNamesCommand = new RelayCommand(() => DwgBlockNameProvider.Refresh());
            DrawCommand = ConfirmCommand;

            // 三段 Outliner：中央隔离带 / 左侧 / 右侧
            _medianNode = new MedianOutlineNode(this);
            _leftSideNode = new SideOutlineNode("左侧", LeftBands);
            _rightSideNode = new SideOutlineNode("右侧", RightBands);
            OutlineRoot = new List<OutlineNodeBase> { _medianNode, _leftSideNode, _rightSideNode };

            PreviewRequested += (_, figure) => UpdateStatus(figure);
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SelectedBand))
                {
                    OnPropertyChanged(nameof(SelectedSideLabel));
                    SyncTreeSelection();
                    (PickBandGeometryCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (CopySelectedBandCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (RemoveBandCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
                else if (e.PropertyName == nameof(CenterMedianWidth))
                {
                    _medianNode.RaiseCenterWidthChanged();
                }
            };

            UpdateStatus(LastFigure);
            AfterCrossSectionShellReady();
        }

        /// <inheritdoc />
        protected override void OnAfterPresetLayoutLoaded()
            => ApplyHySettingsScaleAndPrecisionToDrawing();

        /// <summary>保存用户预设后调用，刷新下拉可见项。</summary>
        public void RefreshPresets()
        {
            _presetsCache = null;
            OnPropertyChanged(nameof(Presets));
        }

        private IReadOnlyList<PresetDescriptor> LoadPresetsSafe()
        {
            try
            {
                if (_presetService != null)
                {
                    var all = _presetService.LoadAll();
                    if (all != null && all.Count > 0) return all;
                }
            }
            catch
            {
                // 预设服务异常时退回内置预设，避免阻塞窗口打开。
            }

            return CrossSectionPresets.All;
        }

        private static CrossSectionPresetService TryResolvePresetService()
        {
            try { return ServiceLocator.Resolve<CrossSectionPresetService>(); }
            catch { return null; }
        }

        private bool _scaleSync;
        private bool _precisionSync;
        private bool _isSubscribedToSettings;
        private string _lastPickedEntityLayer = string.Empty;

        /// <summary>最近一次从图形拾取到的实体所在图层；未拾取时为空串。</summary>
        public string LastPickedEntityLayer
        {
            get => _lastPickedEntityLayer;
            set => SetProperty(ref _lastPickedEntityLayer, value ?? string.Empty);
        }

        private void AfterCrossSectionShellReady()
        {
            LoadRoadMaterialFillPresetsFromSettings();
            DwgBlockNameProvider.Refresh();
            TryWireDrawingScaleToSettings();
            TryApplyDefaultStationRangeForActiveRoute();
            PropertyChanged += OnThisPropertyChanged;
        }

        private static void LoadRoadMaterialFillPresetsFromSettings()
        {
            var settings = SettingsPanelViewModel.Current;
            if (settings == null)
            {
                RoadMaterialFillPresets.RestoreDefaults();
                return;
            }

            RoadMaterialFillPresets.ApplyUserSettings(settings.GetRoadMaterialFillSettings());
        }

        private static void RestoreDefaultFillMaterialPresets()
        {
            RoadMaterialFillPresets.RestoreDefaults();
            var settings = SettingsPanelViewModel.Current;
            settings?.SetRoadMaterialFillSettings(null, saveImmediately: true);
        }

        private void OnThisPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e?.PropertyName == nameof(ScaleDenominator) && !_scaleSync)
            {
                OnDrawingScaleChangedFromPropertyPanel();
            }
            else if (e?.PropertyName == nameof(DisplayPrecision) && !_precisionSync)
            {
                OnDrawingPrecisionChangedFromPropertyPanel();
            }
        }

        private void TryWireDrawingScaleToSettings()
        {
            if (_isSubscribedToSettings) return;
            var s = SettingsPanelViewModel.Current;
            if (s == null) return;
            int denom = (int)Math.Round(s.Scale);
            if (denom > 0) ApplyScaleFromExternal(denom, pushBackToSettings: false);
            ApplyPrecisionFromExternal(s.Precision, pushBackToSettings: false);
            s.PropertyChanged += OnHySettingsPropertyChanged;
            _isSubscribedToSettings = true;
        }

        private void OnHySettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is SettingsPanelViewModel s)) return;
            if (e?.PropertyName == nameof(SettingsPanelViewModel.Scale))
            {
                if (_scaleSync) return;
                int denom = (int)Math.Round(s.Scale);
                if (denom > 0) ApplyScaleFromExternal(denom, pushBackToSettings: false);
            }
            else if (e?.PropertyName == nameof(SettingsPanelViewModel.Precision))
            {
                if (_precisionSync) return;
                ApplyPrecisionFromExternal(s.Precision, pushBackToSettings: false);
            }
        }

        /// <summary>载入预设几何后仍用 Hy 设置面板的绘图比例与标注小数位（与界面设置一致）。</summary>
        private void ApplyHySettingsScaleAndPrecisionToDrawing()
        {
            var s = SettingsPanelViewModel.Current;
            if (s == null) return;
            int denom = (int)Math.Round(s.Scale);
            if (denom > 0) ApplyScaleFromExternal(denom, pushBackToSettings: false);
            ApplyPrecisionFromExternal(s.Precision, pushBackToSettings: false);
        }

        private void ApplyPrecisionFromExternal(int precision, bool pushBackToSettings)
        {
            _precisionSync = true;
            try
            {
                if (DisplayPrecision != precision)
                    DisplayPrecision = precision;
                if (pushBackToSettings)
                {
                    var s = SettingsPanelViewModel.Current;
                    if (s != null && s.Precision != precision)
                        s.Precision = precision;
                }
            }
            finally
            {
                _precisionSync = false;
            }
        }

        /// <summary>代码侧修改 <see cref="DisplayPrecision"/> 时与 <see cref="SettingsPanelViewModel.Precision"/> 对齐。</summary>
        private void OnDrawingPrecisionChangedFromPropertyPanel()
        {
            if (_precisionSync) return;
            TryWireDrawingScaleToSettings();
            ApplyPrecisionFromExternal(DisplayPrecision, pushBackToSettings: true);
        }

        private void ApplyScaleFromExternal(int scaleDenominator, bool pushBackToSettings)
        {
            if (scaleDenominator <= 0) return;
            _scaleSync = true;
            try
            {
                if (ScaleDenominator != scaleDenominator)
                {
                    ScaleDenominator = scaleDenominator;
                }
                if (pushBackToSettings)
                {
                    var s = SettingsPanelViewModel.Current;
                    if (s != null && Math.Abs(s.Scale - scaleDenominator) > 0.1)
                    {
                        s.Scale = scaleDenominator;
                    }
                }
            }
            finally
            {
                _scaleSync = false;
            }
        }

        /// <summary>属性面板或 Hy 侧修改比例时调用：与设置面板 <see cref="SettingsPanelViewModel.Scale"/> 对齐。</summary>
        public void OnDrawingScaleChangedFromPropertyPanel()
        {
            if (_scaleSync) return;
            TryWireDrawingScaleToSettings();
            ApplyScaleFromExternal(ScaleDenominator, pushBackToSettings: true);
        }

        private void TryApplyDefaultStationRangeForActiveRoute()
        {
            double? len;
            try
            {
                len = TryGetFirstAlignmentLengthMeters();
            }
            catch
            {
                return;
            }
            ApplyDefaultStationByRouteLength(len);
        }

        private static double? TryGetFirstAlignmentLengthMeters()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            var reg = HyCADTool.App.Bootstrap.ServiceLocator
                .Resolve<HyCADTool.Shared.AutoCAD.Services.Road.RoadDesignRegistry>();
            if (!reg.TryGet(doc.Name, out var design) || design?.Alignments == null || design.Alignments.Count == 0)
                return null;
            var a = design.Alignments[0];
            if (a?.Centerline == null) return null;
            double m = a.Centerline.GetPlanarLength();
            return m > 1e-6 ? m : (double?)null;
        }

        /// <summary>无有效路线时首段 0~0；有路线时首段 0~路线长（若当前尚未改动默认）。</summary>
        public void ApplyDefaultStationByRouteLength(double? routeLengthMeters)
        {
            if (StationRangeRows == null || StationRangeRows.Count == 0) return;
            if (routeLengthMeters is double l && l > 1e-3)
            {
                var r0 = StationRangeRows[0];
                if (Math.Abs(r0.StartM) < 1e-3 && Math.Abs(r0.EndM) < 1e-3)
                {
                    r0.EndM = l;
                }
            }
        }

        /// <summary>
        /// 让 TreeView 的 IsSelected 标志与 VM 三路选中态（SelectedBand / SelectedMedianNode /
        /// SelectedSideNode）保持一致。任何一路变化都会调用此方法统一扫一遍。
        /// </summary>
        private void SyncTreeSelection()
        {
            if (_isSyncingSelection) return;
            _isSyncingSelection = true;
            try
            {
                // Band：遍历左右集合，仅被选中的那条置 true
                var target = SelectedBand;
                foreach (var row in LeftBands) row.IsSelected = ReferenceEquals(row, target);
                foreach (var row in RightBands) row.IsSelected = ReferenceEquals(row, target);

                _medianNode.IsSelected = ReferenceEquals(_selectedMedianNode, _medianNode)
                    || (_selectedOutlineNode is MedianHalfRowViewModel);
                _medianNode.LeftHalf.IsSelected = ReferenceEquals(_selectedOutlineNode, _medianNode.LeftHalf);
                _medianNode.RightHalf.IsSelected = ReferenceEquals(_selectedOutlineNode, _medianNode.RightHalf);
                _leftSideNode.IsSelected = ReferenceEquals(_selectedSideNode, _leftSideNode);
                _rightSideNode.IsSelected = ReferenceEquals(_selectedSideNode, _rightSideNode);
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }

        private bool _isSyncingSelection;

        // ============================== M7.4 绘图模式 ==============================

        private bool _useSingleLineMode;
        /// <summary>
        /// M7.4：是否使用"单线出图"模式（仅顶面投影 + 中心线，不画结构厚度）。
        /// false = 带结构厚度轮廓（默认，与 v1 行为一致）。
        /// 命令层收尾阶段读取本属性决定 <c>RoadStandardSectionDrawService.Draw</c> 的 mode 参数。
        /// </summary>
        public bool UseSingleLineMode
        {
            get => _useSingleLineMode;
            set { if (SetProperty(ref _useSingleLineMode, value)) OnPropertyChanged(nameof(UseStructureThicknessMode)); }
        }

        /// <summary>与 <see cref="UseSingleLineMode"/> 互补，用于 RadioButton 的 IsChecked 绑定。</summary>
        public bool UseStructureThicknessMode
        {
            get => !_useSingleLineMode;
            set { if (value) UseSingleLineMode = false; }
        }

        private bool _drawSectionStructureFills;
        /// <summary>
        /// 是否在「向 CAD 绘制横断面」时，为每层结构层生成坡面图 <c>SectionFill</c> 的 Hatch 填充。
        /// 默认 false：结构层仍画闭合边界与图块（若启用），但不生成密集斜线 Hatch，避免图面过于拥挤。
        /// 顶栏提供勾选开关，随时可打开重画；数据层 <c>SectionFill.PatternEnabled</c> 不变更。
        /// </summary>
        public bool DrawSectionStructureFills
        {
            get => _drawSectionStructureFills;
            set => SetProperty(ref _drawSectionStructureFills, value);
        }

        // ============================== UI 折叠状态 ==============================

        private bool _isOutlinerVisible = true;
        public bool IsOutlinerVisible
        {
            get => _isOutlinerVisible;
            set => SetProperty(ref _isOutlinerVisible, value);
        }

        private bool _isPropertyPaneVisible = true;
        public bool IsPropertyPaneVisible
        {
            get => _isPropertyPaneVisible;
            set => SetProperty(ref _isPropertyPaneVisible, value);
        }

        // ============================== 状态文本 ==============================

        private string _statusMessage = string.Empty;
        /// <summary>底部状态栏文本：板块数 / 总宽 / 规范通过状况。</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value ?? string.Empty);
        }

        /// <summary>横断面 PaletteSet 出图成功后写回摘要（供 <see cref="Views.Road.CrossSectionDrawPanel"/> 调用）。</summary>
        public void SetCommitOutcomeStatus(string message)
        {
            SetProperty(ref _statusMessage, message ?? string.Empty, nameof(StatusMessage));
        }

        /// <summary>UI 显示用：当前选中条带所在侧别的中文标签。</summary>
        public string SelectedSideLabel
        {
            get
            {
                if (SelectedBand == null) return "未选中";
                switch (SelectedBand.Side)
                {
                    case BandSide.Left: return "左半";
                    case BandSide.Right: return "右半";
                    case BandSide.Center: return "中央";
                    default: return SelectedBand.Side.ToString();
                }
            }
        }

        // ============================== 复制 / 粘贴条带 ==============================

        private CrossSectionBand? _bandClipboard;

        /// <summary>UI 绑定（按钮 IsEnabled）：剪贴板是否有可粘贴条带。</summary>
        public bool HasClipboard => _bandClipboard.HasValue;

        public ICommand CopySelectedBandCommand { get; }
        public ICommand PasteBandCommand { get; }
        public ICommand InsertBandAfterSelectedCommand { get; }
        /// <summary>在选中行「之前」按类型插入条带（大纲右键 / 顶栏「插入」子菜单）。</summary>
        public ICommand InsertBandBeforeSelectedCommand { get; }
        public ICommand PickBandGeometryCommand { get; }
        public ICommand ToggleOutlinerCommand { get; }
        public ICommand TogglePropertyPaneCommand { get; }
        public ICommand RestoreDefaultFillMaterialPresetsCommand { get; }

        /// <summary>与 <see cref="DwgBlockNameProvider"/> 同引用，供结构层图块名 ComboBox 绑定。</summary>
        public System.Collections.ObjectModel.ReadOnlyObservableCollection<string> DwgBlockNames
            => DwgBlockNameProvider.BlockNames;

        public IReadOnlyList<string> HatchPatternNameList
            => RoadMaterialFillPresets.HatchPatternNames;

        public ICommand RefreshDwgBlockNamesCommand { get; }

        public event EventHandler<BandRowViewModel> PickGeometryRequested;

        /// <summary>语义化别名：与 <see cref="CrossSectionDesignerViewModel.ConfirmCommand"/> 同一命令实例。</summary>
        public ICommand DrawCommand { get; }

        private void ExecuteCopySelected()
        {
            if (SelectedBand == null) return;
            _bandClipboard = SelectedBand.ToBand();
            OnPropertyChanged(nameof(HasClipboard));
        }

        private void ExecutePasteToSelectedSide()
        {
            if (_bandClipboard == null) return;
            var band = _bandClipboard.Value;
            // 粘贴到当前选中条带所在侧；未选中时默认左半。
            BandSide targetSide;
            if (SelectedBand?.Side == BandSide.Right) targetSide = BandSide.Right;
            else if (SelectedBand?.Side == BandSide.Left) targetSide = BandSide.Left;
            else targetSide = BandSide.Left;

            var collection = targetSide == BandSide.Right ? RightBands : LeftBands;
            var newRow = new BandRowViewModel(band.WithSide(targetSide));
            collection.Add(newRow);
            SelectedBand = newRow;
        }

        private void ExecuteInsertBandAfterSelected(TemplateComponentKind kind)
        {
            var targetSide = SelectedBand?.Side;
            if (targetSide != BandSide.Left && targetSide != BandSide.Right)
            {
                targetSide = SelectedSideNode == RightSideNode ? BandSide.Right : BandSide.Left;
            }

            var collection = targetSide == BandSide.Right ? RightBands : LeftBands;
            var newBand = CreateDefaultBand(kind, targetSide.Value);
            var newRow = new BandRowViewModel(newBand);

            int insertIndex = collection.Count;
            if (SelectedBand != null && SelectedBand.Side == targetSide)
            {
                var selectedIndex = collection.IndexOf(SelectedBand);
                if (selectedIndex >= 0) insertIndex = selectedIndex + 1;
            }

            collection.Insert(insertIndex, newRow);
            SelectedBand = newRow;
        }

        private void ExecuteInsertBandBeforeSelected(TemplateComponentKind kind)
        {
            var targetSide = SelectedBand?.Side;
            if (targetSide != BandSide.Left && targetSide != BandSide.Right)
            {
                targetSide = SelectedSideNode == RightSideNode ? BandSide.Right : BandSide.Left;
            }

            var collection = targetSide == BandSide.Right ? RightBands : LeftBands;
            var newBand = CreateDefaultBand(kind, targetSide.Value);
            var newRow = new BandRowViewModel(newBand);

            int insertIndex = 0;
            if (SelectedBand != null && SelectedBand.Side == targetSide)
            {
                var selectedIndex = collection.IndexOf(SelectedBand);
                if (selectedIndex >= 0) insertIndex = selectedIndex;
            }
            else if (collection.Count > 0) insertIndex = 0;

            collection.Insert(insertIndex, newRow);
            SelectedBand = newRow;
        }

        private static CrossSectionBand CreateDefaultBand(TemplateComponentKind kind, BandSide side)
        {
            switch (kind)
            {
                case TemplateComponentKind.Pavement:
                    return CrossSectionBand.Lane(3.5, 1.5, side, "机动车道");
                case TemplateComponentKind.NonMotorized:
                    return CrossSectionBand.NonMotor(3.5, 1.5, side, "非机动车道");
                case TemplateComponentKind.Sidewalk:
                    return CrossSectionBand.Sidewalk(2.5, 1.5, side, "人行道");
                case TemplateComponentKind.GreenStrip:
                    return CrossSectionBand.GreenStrip(2.0, side, "绿化带");
                case TemplateComponentKind.Kerb:
                    return CrossSectionBand.Kerb(0.15, side, "路牙");
                case TemplateComponentKind.MedianStrip:
                    return CrossSectionBand.Median(2.0, "分隔带").WithSide(side);
                default:
                    return CrossSectionBand.Lane(3.5, 1.5, side, "机动车道");
            }
        }

        // ============================== Outliner 三段节点 + 选中路由 ==============================

        private readonly MedianOutlineNode _medianNode;
        private readonly SideOutlineNode _leftSideNode;
        private readonly SideOutlineNode _rightSideNode;

        /// <summary>
        /// 左侧 TreeView 的 ItemsSource。三条根：中央隔离带 / 左侧 / 右侧。
        /// </summary>
        public IReadOnlyList<OutlineNodeBase> OutlineRoot { get; }

        /// <summary>大纲根"中央隔离带"节点。对外暴露用于 XAML 精确绑定可见性。</summary>
        public MedianOutlineNode MedianNode => _medianNode;

        /// <summary>大纲根"左侧"节点。</summary>
        public SideOutlineNode LeftSideNode => _leftSideNode;

        /// <summary>大纲根"右侧"节点。</summary>
        public SideOutlineNode RightSideNode => _rightSideNode;

        private object _selectedOutlineNode;
        /// <summary>
        /// TreeView 当前选中节点（MedianOutlineNode / SideOutlineNode / BandRowViewModel 三者之一）。
        /// setter 负责分发到 SelectedBand / SelectedMedianNode / SelectedSideNode 三者之一并置空另外两者。
        /// </summary>
        public object SelectedOutlineNode
        {
            get => _selectedOutlineNode;
            set
            {
                if (ReferenceEquals(_selectedOutlineNode, value)) return;
                _selectedOutlineNode = value;

                switch (value)
                {
                    case BandRowViewModel row:
                        _selectedMedianNode = null;
                        _selectedSideNode = null;
                        OnPropertyChanged(nameof(SelectedMedianNode));
                        OnPropertyChanged(nameof(SelectedSideNode));
                        SelectedBand = row;
                        break;
                    case MedianHalfRowViewModel mh:
                        _selectedSideNode = null;
                        OnPropertyChanged(nameof(SelectedSideNode));
                        SelectedBand = null;
                        SelectedMedianNode = mh.ParentNode;
                        break;
                    case MedianOutlineNode m:
                        _selectedSideNode = null;
                        OnPropertyChanged(nameof(SelectedSideNode));
                        SelectedBand = null;
                        SelectedMedianNode = m;
                        break;
                    case SideOutlineNode s:
                        _selectedMedianNode = null;
                        OnPropertyChanged(nameof(SelectedMedianNode));
                        SelectedBand = null;
                        SelectedSideNode = s;
                        break;
                    default:
                        _selectedMedianNode = null;
                        _selectedSideNode = null;
                        OnPropertyChanged(nameof(SelectedMedianNode));
                        OnPropertyChanged(nameof(SelectedSideNode));
                        SelectedBand = null;
                        break;
                }

                SyncTreeSelection();
                OnPropertyChanged();
            }
        }

        private MedianOutlineNode _selectedMedianNode;
        /// <summary>当前选中的「中央隔离带」节点（非 null 时右侧属性面板显示中央隔离带 Expander）。</summary>
        public MedianOutlineNode SelectedMedianNode
        {
            get => _selectedMedianNode;
            set
            {
                if (!SetProperty(ref _selectedMedianNode, value)) return;
                SyncTreeSelection();
            }
        }

        private SideOutlineNode _selectedSideNode;
        /// <summary>当前选中的「左/右侧」根节点（属性面板显示该侧批量操作提示）。</summary>
        public SideOutlineNode SelectedSideNode
        {
            get => _selectedSideNode;
            set
            {
                if (!SetProperty(ref _selectedSideNode, value)) return;
                SyncTreeSelection();
            }
        }

        // ============================== 状态计算 ==============================

        private void UpdateStatus(CrossSectionFigure figure)
        {
            var sb = new StringBuilder();
            sb.Append("L=").Append(LeftBands.Count)
              .Append(" / 分隔=").Append(CenterMedianWidth > 0 ? "1" : "0")
              .Append(" / R=").Append(RightBands.Count);
            sb.Append("  总宽 ").Append(TotalWidth.ToString("F2", CultureInfo.InvariantCulture)).Append(" m");

            int passed = CheckItems.Count(i => i.Passed);
            int total = CheckItems.Count;
            sb.Append("  规范 ").Append(passed).Append("/").Append(total);
            sb.Append(AllChecksPassed ? "（全部通过）" : "（存在未通过项）");

            if (figure != null)
            {
                sb.Append("  顶点 ").Append(figure.Vertices.Count);
                sb.Append("  面板 ").Append(figure.Panels.Count);
            }

            StatusMessage = sb.ToString();
        }
    }

    // =======================================================================
    //  Outliner 三段节点模型（仅供 CrossSectionDrawViewModel 使用）
    // =======================================================================

    /// <summary>
    /// TreeView 根节点基类。用于在 XAML 用 <c>HierarchicalDataTemplate DataType</c>
    /// 按类型匹配不同的模板（中央隔离带 / 侧 / 条带）。
    /// <para>
    /// <see cref="IsSelected"/> / <see cref="IsExpanded"/> 供 TreeViewItem.IsSelected /
    /// IsExpanded TwoWay 绑定，实现 VM→TreeView 选中回流。
    /// </para>
    /// </summary>
    public abstract class OutlineNodeBase : INotifyPropertyChanged
    {
        public abstract string DisplayTitle { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value) return;
                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>分隔带「左半 / 右半」大纲行。字段绑定到 <see cref="CrossSectionDrawViewModel"/> 的 Median* 系列属性。</summary>
    public sealed class MedianHalfRowViewModel : INotifyPropertyChanged
    {
        private readonly CrossSectionDrawViewModel _vm;
        private readonly bool _isLeft;
        private bool _isSelected;

        public MedianHalfRowViewModel(CrossSectionDrawViewModel vm, MedianOutlineNode parent, bool isLeft)
        {
            _vm = vm ?? throw new ArgumentNullException(nameof(vm));
            ParentNode = parent ?? throw new ArgumentNullException(nameof(parent));
            _isLeft = isLeft;
        }

        public MedianOutlineNode ParentNode { get; }
        public string RowTitle => _isLeft ? "分隔带(左半)" : "分隔带(右半)";

        public bool IsSubWidthReadOnly => !_isLeft;

        /// <summary>左半可编宽度；右半由 总宽 − 左半 联动，只读。</summary>
        public bool IsSubWidthEditable => _isLeft;

        public bool IsDiffFieldsEnabled => !_vm.IsElevationDiffLocked;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public double SubWidth
        {
            get => _isLeft ? _vm.MedianLeftSubWidth : _vm.MedianRightSubWidth;
            set
            {
                if (_isLeft)
                    _vm.MedianLeftSubWidth = value;
            }
        }

        public double CrossSlopePct
        {
            get => _isLeft ? _vm.MedianLeftCrossSlopePct : _vm.MedianRightCrossSlopePct;
            set
            {
                if (_isLeft)
                    _vm.MedianLeftCrossSlopePct = value;
                else
                    _vm.MedianRightCrossSlopePct = value;
            }
        }

        public double InnerElevationDiff
        {
            get => _isLeft ? _vm.MedianLeftInnerElevationDiff : _vm.MedianRightInnerElevationDiff;
            set
            {
                if (_isLeft)
                    _vm.MedianLeftInnerElevationDiff = value;
                else
                    _vm.MedianRightInnerElevationDiff = value;
            }
        }

        public double OuterElevationDiff
        {
            get => _isLeft ? _vm.MedianLeftOuterElevationDiff : _vm.MedianRightOuterElevationDiff;
            set
            {
                if (_isLeft)
                    _vm.MedianLeftOuterElevationDiff = value;
                else
                    _vm.MedianRightOuterElevationDiff = value;
            }
        }

        public void RefreshAll()
        {
            OnPropertyChanged(nameof(SubWidth));
            OnPropertyChanged(nameof(CrossSlopePct));
            OnPropertyChanged(nameof(InnerElevationDiff));
            OnPropertyChanged(nameof(OuterElevationDiff));
            OnPropertyChanged(nameof(IsDiffFieldsEnabled));
            OnPropertyChanged(nameof(IsSubWidthEditable));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// 中央隔离带根节点。<see cref="IsActive"/> 与 <see cref="Width"/> 双向绑定到
    /// <see cref="CrossSectionDrawViewModel.CenterMedianWidth"/>（IsActive=false → 0；true → Width）。
    /// 子行：<see cref="ChildRows"/>（左/右各一行：横坡与内/外 端 高差）。
    /// </summary>
    public sealed class MedianOutlineNode : OutlineNodeBase
    {
        private readonly CrossSectionDrawViewModel _owner;
        private double _lastNonZeroWidth = 2.0;

        internal MedianOutlineNode(CrossSectionDrawViewModel owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (_owner.CenterMedianWidth > 0) _lastNonZeroWidth = _owner.CenterMedianWidth;
            LeftHalf = new MedianHalfRowViewModel(owner, this, isLeft: true);
            RightHalf = new MedianHalfRowViewModel(owner, this, isLeft: false);
            ChildRows = new ObservableCollection<MedianHalfRowViewModel> { LeftHalf, RightHalf };
            _owner.PropertyChanged += OnOwnerPropertyChanged;
        }

        private void OnOwnerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var n = e?.PropertyName;
            if (string.IsNullOrEmpty(n)) return;
            if (n == nameof(CrossSectionDrawViewModel.IsElevationDiffLocked)
                || n == nameof(CrossSectionDrawViewModel.CenterMedianWidth)
                || n == nameof(CrossSectionDrawViewModel.MedianLeftSubWidth)
                || n == nameof(CrossSectionDrawViewModel.MedianLeftCrossSlopePct)
                || n == nameof(CrossSectionDrawViewModel.MedianRightCrossSlopePct)
                || n == nameof(CrossSectionDrawViewModel.MedianLeftOuterElevationDiff)
                || n == nameof(CrossSectionDrawViewModel.MedianLeftInnerElevationDiff)
                || n == nameof(CrossSectionDrawViewModel.MedianRightInnerElevationDiff)
                || n == nameof(CrossSectionDrawViewModel.MedianRightOuterElevationDiff)
                || n == nameof(CrossSectionDrawViewModel.MedianRightSubWidth))
            {
                LeftHalf.RefreshAll();
                RightHalf.RefreshAll();
            }
        }

        public MedianHalfRowViewModel LeftHalf { get; }
        public MedianHalfRowViewModel RightHalf { get; }
        public ObservableCollection<MedianHalfRowViewModel> ChildRows { get; }

        public override string DisplayTitle => "中央隔离带";

        /// <summary>是否启用中央隔离带。false → 将 <see cref="CrossSectionDrawViewModel.CenterMedianWidth"/> 置 0。</summary>
        public bool IsActive
        {
            get => _owner.CenterMedianWidth > 0;
            set
            {
                if (value == IsActive) return;
                if (value)
                {
                    _owner.CenterMedianWidth = _lastNonZeroWidth > 0 ? _lastNonZeroWidth : 2.0;
                }
                else
                {
                    if (_owner.CenterMedianWidth > 0)
                        _lastNonZeroWidth = _owner.CenterMedianWidth;
                    _owner.CenterMedianWidth = 0;
                }
                // CenterMedianWidth setter 会触发 PropertyChanged 冒泡到本节点的 RaiseCenterWidthChanged。
            }
        }

        /// <summary>
        /// 中央隔离带宽度（m）。启用时绑定到 <see cref="CrossSectionDrawViewModel.CenterMedianWidth"/>；
        /// 未启用时（IsActive=false）编辑值会被缓存，下次启用时恢复。
        /// </summary>
        public double Width
        {
            get => IsActive ? _owner.CenterMedianWidth : _lastNonZeroWidth;
            set
            {
                double v = value < 0 ? 0 : value;
                if (IsActive)
                {
                    if (Math.Abs(_owner.CenterMedianWidth - v) < 1e-9) return;
                    _owner.CenterMedianWidth = v;
                    if (v > 0) _lastNonZeroWidth = v;
                }
                else
                {
                    if (Math.Abs(_lastNonZeroWidth - v) < 1e-9) return;
                    _lastNonZeroWidth = v;
                    OnPropertyChanged(nameof(Width));
                }
            }
        }

        /// <summary>由 ViewModel 在 <see cref="CrossSectionDrawViewModel.CenterMedianWidth"/> 变更时调用，刷新两路绑定。</summary>
        internal void RaiseCenterWidthChanged()
        {
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(Width));
        }
    }

    /// <summary>
    /// 「左侧 (N)」/「右侧 (N)」根节点。Children 直接引用 VM 的 <c>LeftBands</c> / <c>RightBands</c>，
    /// <see cref="DisplayTitle"/> 随集合元素数量自动更新。
    /// </summary>
    public sealed class SideOutlineNode : OutlineNodeBase
    {
        private readonly string _baseTitle;

        public SideOutlineNode(string baseTitle, ObservableCollection<BandRowViewModel> children)
        {
            _baseTitle = baseTitle ?? string.Empty;
            Children = children ?? throw new ArgumentNullException(nameof(children));
            Children.CollectionChanged += OnChildrenChanged;
        }

        public ObservableCollection<BandRowViewModel> Children { get; }

        public override string DisplayTitle => $"{_baseTitle} ({Children.Count})";

        private void OnChildrenChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(DisplayTitle));
        }
    }
}
