using Autofac;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using HyCAD.BlenderUI.Theming;
using HyCADTool.Shell.Contracts;
using HyCADTool.Shared.Drawing.Models;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCADTool.Features.Road.Plan.ViewModels;
using HyCADTool.Features.Road.PlanAlignment.Services;
using HyCADTool.Features.Reinforcement.Domain;
using HyCADTool.Shell.Configuration.Global;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.Drawing.ValueObjects;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shared.AutoCAD.Utilities;
using HyCADTool.App.Bootstrap;
using HyCADTool.Features.TitleBlock.Services;
using HyCADTool.Shared.UI.Helpers;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Presentation.ViewModels
{
    /// <summary>
    /// HY 设置面板 ViewModel
    /// Tab A: 样式设置（文字/标注/引线）
    /// Tab B: 钢筋参数（对应旧 ReinPanel）
    /// 样式名称根据 Scale 动态生成
    /// 
    /// 多文档支持：每个文档有独立的 ViewModel 实例
    /// </summary>
    public class SettingsPanelViewModel : INotifyPropertyChanged
    {
        private readonly IStyleService _styleService;

        /// <summary>用户可编辑图层表（与 hy-settings.json 中 <see cref="UserLayerSettings"/> 同步）。</summary>
        public ObservableCollection<LayerDefinitionItem> LayerCatalogItems { get; }
            = new ObservableCollection<LayerDefinitionItem>();

        /// <summary>
        /// 横断面「填料」候选的用户增量设置（相对系统默认的新增/删除记录）。
        /// 由横断面窗口在增删候选或恢复默认时写回 <c>hy-settings.json</c>。
        /// </summary>
        private RoadMaterialFillSettings _roadMaterialFillSettings = new RoadMaterialFillSettings();

        /// <summary>最近一次 <c>hyLtCapture</c> 写入的线型目录快照（嵌入 hy-settings.json）。</summary>
        private LinetypeCatalogSnapshot _linetypeCatalogSnapshot;

        /// <summary>本次 LoadSettings 读到的层表版本；用于 <see cref="LayerCatalogFactory.CurrentCatalogVersion"/> 升级后自动写回 hy-settings。</summary>
        private int _layerCatalogSchemaVersionBeforeLoad = int.MinValue;

        /// <summary>按当前层表在当前 DWG 中创建/更新图层属性。</summary>
        public ICommand ApplyLayerCatalogToDocumentCommand { get; }

        /// <summary>将层表恢复为程序默认并可选写盘（与自动保存联动）。</summary>
        public ICommand RestoreDefaultLayerCatalogCommand { get; }

        /// <summary>
        /// 样式脏标记：参数变更后置 true，样式同步后置 false
        /// 避免每个命令执行前都无条件重建样式（8~10 个事务）
        /// </summary>
        private bool _stylesDirty = true;

        /// <summary>
        /// 文档级 ViewModel 存储（每个文档独立参数）
        /// </summary>
        private static readonly Dictionary<string, SettingsPanelViewModel> _documentViewModels 
            = new Dictionary<string, SettingsPanelViewModel>();

        /// <summary>
        /// 当前活动文档的 ViewModel（供 DrawReinforcementCommand 等外部读取参数）
        /// 对应旧代码 ReinPanel.ActivePanel
        /// </summary>
        public static SettingsPanelViewModel Current
        {
            get
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null) return null;

                var docName = doc.Name;
                if (!_documentViewModels.ContainsKey(docName))
                {
                    // 从 DI 容器获取 IStyleService，确保命令按钮可用
                    IStyleService styleService = null;
                    try { styleService = ServiceLocator.Container?.Resolve<IStyleService>(); }
                    catch { }
                    _documentViewModels[docName] = styleService != null
                        ? new SettingsPanelViewModel(styleService)
                        : new SettingsPanelViewModel();
                }
                return _documentViewModels[docName];
            }
        }

        /// <summary>
        /// 获取或创建指定文档的 ViewModel
        /// </summary>
        public static SettingsPanelViewModel GetOrCreate(string documentName, IStyleService styleService)
        {
            if (!_documentViewModels.ContainsKey(documentName))
            {
                _documentViewModels[documentName] = new SettingsPanelViewModel(styleService);
            }
            return _documentViewModels[documentName];
        }

        /// <summary>
        /// 清理已关闭文档的 ViewModel
        /// </summary>
        public static void RemoveDocument(string documentName)
        {
            _documentViewModels.Remove(documentName);
        }

        #region 构造函数

        public SettingsPanelViewModel(IStyleService styleService)
        {
            _styleService = styleService;

            ApplyStyleCommand = new RelayCommand(SaveAsDefault);
            ResetCommand = new RelayCommand(ResetToDefaults);
            DrawCommand = new RelayCommand(() => SendCommand(() => new Features.Reinforcement.DrawReinforcementCommand().Execute()));

            // 钢筋绘制
            CmdGj = new RelayCommand(() => SendCommand(() => new Features.Reinforcement.DrawReinforcementCommand().Execute()));
            CmdGg = new RelayCommand(() => SendCommand(() => new Features.Reinforcement.DrawOffsetPolylineCommand().Execute()));

            // 钢筋修改
            CmdG1 = new RelayCommand(() => SendCommand(() => new Features.Reinforcement.ReinAddAnchorCommand(isVertical: false).Execute()));
            CmdG2 = new RelayCommand(() => SendCommand(() => new Features.Reinforcement.ReinAddAnchorCommand(isVertical: true).Execute()));
            CmdGe = new RelayCommand(() => SendCommand(() => new Features.Reinforcement.ReinExtendCommand().Execute()));
            CmdGe1 = new RelayCommand(() => SendCommand(() => new Features.Reinforcement.ReinQuickExtendCommand().Execute()));
            CmdGd = new RelayCommand(() => SendCommand(() => new Features.Reinforcement.ReinCutCommand().Execute()));

            // 钢筋标注
            CmdGb = new RelayCommand(() => SendCommand(() => new Features.Reinforcement.MleaderReinCommand(Features.Reinforcement.MleaderReinCommand.Mode.Standard).Execute()));
            CmdGb1 = new RelayCommand(() => SendCommand(() => new Features.Reinforcement.MleaderReinCommand(Features.Reinforcement.MleaderReinCommand.Mode.Single).Execute()));
            CmdGb2 = new RelayCommand(() => SendCommand(() => new Features.Reinforcement.MleaderReinCommand(Features.Reinforcement.MleaderReinCommand.Mode.Six).Execute()));

            // 道路
            CmdRoad = new RelayCommand(() => SendCommand(() => new Features.Road.DrawCrosswalkCommand().Execute()));

            // P0：市政道路设计 ViewModel（Domain 驱动，独立 RoadDesignViewModel）
            RoadDesign = new RoadDesignViewModel();

            // 从持久化文件加载上次保存的设置
            LoadSettings();
            ApplyLayerCatalogToDocumentCommand = new RelayCommand(ApplyLayerCatalogToCurrentDocument);
            RestoreDefaultLayerCatalogCommand = new RelayCommand(RestoreDefaultLayerCatalog);
        }

        public SettingsPanelViewModel()
        {
            ApplyStyleCommand = new RelayCommand(() => { });
            ResetCommand = new RelayCommand(() => { });
            DrawCommand = new RelayCommand(() => { });
            CmdGj = CmdGg = new RelayCommand(() => { });
            CmdG1 = CmdG2 = CmdGe = CmdGe1 = CmdGd = new RelayCommand(() => { });
            CmdGb = CmdGb1 = CmdGb2 = new RelayCommand(() => { });
            CmdRoad = new RelayCommand(() => { });
            ApplyLayerCatalogToDocumentCommand = new RelayCommand(() => { });
            RestoreDefaultLayerCatalogCommand = new RelayCommand(() => { });
        }

        #endregion

        // ================================================================
        //  Tab A: 样式设置
        // ================================================================

        #region 基础属性

        private string _equipmentDataFilePath = @"E:\BaiduSyncdisk\Code\testResult\00equipment_data.md";
        /// <summary>
        /// 设备数据 Markdown 文件路径（设备基础命令使用）
        /// </summary>
        public string EquipmentDataFilePath
        {
            get => _equipmentDataFilePath;
            set => SetProperty(ref _equipmentDataFilePath, value);
        }

        private double _scale = 50.0;
        /// <summary>
        /// 主比例（出图比例 M）：决定纸面标记的真实大小。
        /// 作为 DIMSCALE 直接写入样式；文字样式字高 = TextSize × UnitFactor × Scale。
        /// </summary>
        public double Scale
        {
            get => _scale;
            set
            {
                if (SetProperty(ref _scale, value))
                {
                    _stylesDirty = true;
                    // UseSubScale=false 时 SubScale 始终随 MainScale 退化
                    if (!_useSubScale) _subScale = _scale;
                    NotifyScaleContextChanged();
                }
            }
        }

        private bool _useSubScale;
        /// <summary>
        /// 副比例开关（checkbox）。开启后 SubScale 生效：标注数字读数恒等于真实尺寸（DIMLFAC=SubScale/Scale），
        /// 几何相关属性（LinetypeScale/HatchScale/块插入比例）× GeomMul=Scale/SubScale；
        /// 文字/箭头/间距等纸面标记大小**不变**。
        /// </summary>
        public bool UseSubScale
        {
            get => _useSubScale;
            set
            {
                if (SetProperty(ref _useSubScale, value))
                {
                    _stylesDirty = true;
                    if (!_useSubScale)
                    {
                        // 关闭时归一 SubScale 并刷新 UI 输入框显示
                        _subScale = _scale;
                        OnPropertyChanged(nameof(SubScale));
                    }
                    NotifyScaleContextChanged();
                    // UseSubScale 切换属"模式切换"而非数值微调：立即应用样式，
                    // 让标注 / 引线 同步切回（或切到）对应样式；不等待"置为当前"按钮。
                    TryAutoApplyStyleForModeSwitch();
                }
            }
        }

        /// <summary>
        /// 模式切换（如 UseSubScale 开关）后立即应用样式；若无可用 AutoCAD 文档则静默跳过，
        /// 保留现有 _stylesDirty 状态，用户下次点"置为当前"仍能正确落地。
        /// </summary>
        private void TryAutoApplyStyleForModeSwitch()
        {
            try
            {
                if (_styleService == null) return;
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;
                EnsureStylesApplied();
            }
            catch
            {
                // 静默：setter 里不抛，避免 UI 层未捕获异常。
            }
        }

        private double _subScale = 50.0;
        /// <summary>
        /// 副比例（局部出图比例 S）。UseSubScale=false 时忽略并自动归一为 Scale。
        /// </summary>
        public double SubScale
        {
            get => _subScale;
            set
            {
                if (SetProperty(ref _subScale, value))
                {
                    _stylesDirty = true;
                    NotifyScaleContextChanged();
                }
            }
        }

        private DrawingUnit _unit = DrawingUnit.Millimeter;
        /// <summary>
        /// 绘图单位（mm / cm / m）。变更时：
        /// - 夹紧 Precision 到允许集合；
        /// - 刷新所有派生样式名；
        /// - 同步 AutoCAD INSUNITS（mm=4 / cm=5 / m=6）。
        /// </summary>
        public DrawingUnit Unit
        {
            get => _unit;
            set
            {
                if (SetProperty(ref _unit, value))
                {
                    _stylesDirty = true;
                    // 切单位时把小数位切到该单位的推荐默认值（mm=0 / cm=2 / m=3）。
                    // 允许集合现统一 0..3，用户仍可随后手动微调；初次切换自动给到合理粒度。
                    _precision = ScaleContext.GetDefaultPrecision(_unit);
                    OnPropertyChanged(nameof(Precision));
                    OnPropertyChanged(nameof(AllowedPrecisions));
                    NotifyScaleContextChanged();
                    SyncInsUnits();
                    // 单位切换属"模式切换"：样式名后缀 {u} 改变 → 必须重建新样式并置为当前，
                    // 否则 AutoCAD 里激活的仍是旧单位样式，字高/箭头/间距不会按 UnitFactor 缩放，
                    // 表现为"mm→m 没缩小 1000 倍 / mm→cm 没缩小 100 倍"。
                    TryAutoApplyStyleForModeSwitch();
                }
            }
        }

        private int _precision;
        /// <summary>
        /// 标注小数位（DIMDEC）。允许集合受 Unit 约束：mm={0} / cm={1,2} / m={1,2,3}；
        /// 赋值超出集合会自动夹紧到最近允许值。
        /// </summary>
        public int Precision
        {
            get => _precision;
            set
            {
                int clamped = ScaleContext.ClampPrecision(_unit, value);
                if (SetProperty(ref _precision, clamped))
                {
                    _stylesDirty = true;
                    NotifyScaleContextChanged();
                    // 小数位也写入样式名后缀 {p}，属模式切换 → 立即落盘并置为当前。
                    TryAutoApplyStyleForModeSwitch();
                }
            }
        }

        /// <summary>当前单位允许的小数位（UI ComboBox 数据源）。</summary>
        public int[] AllowedPrecisions => ScaleContext.GetAllowedPrecisions(_unit);

        /// <summary>单位下拉数据源。</summary>
        public DrawingUnit[] UnitOptions { get; } = new[]
        {
            DrawingUnit.Millimeter,
            DrawingUnit.Centimeter,
            DrawingUnit.Meter
        };

        /// <summary>
        /// 构建当前比例上下文快照。UseSubScale=false 时 SubScale 自动归一为 Scale。
        /// </summary>
        public ScaleContext BuildScaleContext()
        {
            double sub = _useSubScale ? _subScale : _scale;
            if (sub <= 0) sub = _scale;
            return new ScaleContext(_scale, sub, _useSubScale, _unit, _precision);
        }

        /// <summary>
        /// 五大比例参数任一变更后：刷新派生样式名、推送全局 ActiveScaleContextProvider、
        /// 同时广播 ActualText*/ActualMLeader* 等派生值变更。
        /// </summary>
        private void NotifyScaleContextChanged()
        {
            OnPropertyChanged(nameof(TextStyleName));
            OnPropertyChanged(nameof(DimStyleName));
            OnPropertyChanged(nameof(MLeaderStyleName));
            OnPropertyChanged(nameof(TableStyleName));
            OnPropertyChanged(nameof(ActualTextHeight));
            OnPropertyChanged(nameof(ActualMLeaderArrowSize));
            OnPropertyChanged(nameof(ActualMLeaderLandingGap));
            try { ActiveScaleContextProvider.Set(BuildScaleContext()); }
            catch { /* 非法比例（如 0）吞掉，UI 下一次合法赋值会恢复 */ }
        }

        /// <summary>
        /// Unit 变更时联动 AutoCAD INSUNITS（mm=4 / cm=5 / m=6）。
        /// 无活动文档或非命令线程时静默跳过，不阻断 UI。
        /// </summary>
        private void SyncInsUnits()
        {
            try
            {
                if (AcApp.DocumentManager.MdiActiveDocument == null) return;
                AcApp.SetSystemVariable("INSUNITS", (int)BuildScaleContext().InsUnitsCode);
            }
            catch { /* 静默 */ }
        }

        #endregion

        #region 动态样式名称

        /// <summary>主文字样式（标注/引线/表格引用），对应 0-hy-说明-S（与副比例/单位解耦）</summary>
        public string TextStyleName => StyleSName;
        /// <summary>0-Hy-{M}-{S}-Dim-{u}-{p}，S=Main 时表示主副一致</summary>
        public string DimStyleName => BuildScaleContext().BuildDimStyleName();
        /// <summary>0-Hy-{M}-{S}-Mleader-{u}-{p}</summary>
        public string MLeaderStyleName => BuildScaleContext().BuildMLeaderStyleName();
        /// <summary>0-Hy-{M}-{S}-Table-{u}（表格无小数位）</summary>
        public string TableStyleName => BuildScaleContext().BuildTableStyleName();

        // ---- "已应用"样式名：状态栏优先显示 AutoCAD 当前激活的标注样式 ----
        // 取不到当前图纸真实样式时，才回退到上次 ApplyStyle 成功时的上下文，
        // 避免面板显示和 AutoCAD 实际激活样式不一致。
        private ScaleContext _appliedContext;

        /// <summary>状态栏显示的当前标注样式名；优先取 AutoCAD 当前激活样式。</summary>
        public string AppliedDimStyleName
        {
            get
            {
                string currentStyleName = TryGetCurrentDimensionStyleName();
                if (!string.IsNullOrWhiteSpace(currentStyleName))
                    return currentStyleName;

                return (_appliedContext ?? BuildScaleContext()).BuildDimStyleName();
            }
        }

        private string TryGetCurrentDimensionStyleName()
        {
            try
            {
                return _styleService?.GetCurrentDimensionStyleName();
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region 文字样式属性（双样式：T=TrueType 说明，S=SHX 工程）

        /// <summary>样式1：0-hy-说明-T，TrueType 字体（标题/说明用）</summary>
        private string _styleTName = "0-hy-说明-T";
        public string StyleTName { get => _styleTName; set { if (SetProperty(ref _styleTName, value)) _stylesDirty = true; } }

        private string _styleTFont = "微软雅黑";
        public string StyleTFont
        {
            get => _styleTFont;
            set
            {
                if (SetProperty(ref _styleTFont, value))
                {
                    _stylesDirty = true;
                    OnPropertyChanged(nameof(StyleTFontOptionsWithCurrent));
                }
            }
        }

        /// <summary>样式2：0-hy-说明-S，SHX 字体（标注/引线/表格用）</summary>
        private string _styleSName = "0-hy-说明-S";
        public string StyleSName { get => _styleSName; set { if (SetProperty(ref _styleSName, value)) _stylesDirty = true; } }

        private string _styleSFont = "tssdeng.shx";
        public string StyleSFont
        {
            get => _styleSFont;
            set
            {
                if (SetProperty(ref _styleSFont, value))
                {
                    _stylesDirty = true;
                    OnPropertyChanged(nameof(StyleSFontOptionsWithCurrent));
                }
            }
        }

        private string _styleSBigFont = "tssdchn.shx";
        public string StyleSBigFont
        {
            get => _styleSBigFont;
            set
            {
                if (SetProperty(ref _styleSBigFont, value))
                {
                    _stylesDirty = true;
                    OnPropertyChanged(nameof(StyleSBigFontOptionsWithCurrent));
                }
            }
        }

        // ===== 字体下拉候选（参考 MarkdownEditor 设计：T=TrueType, S=SHX, BigFont=SHX 大字体） =====

        /// <summary>常用 TrueType 字体（标题/说明用）— ComboBox 数据源</summary>
        public static IReadOnlyList<string> TrueTypeFontOptions { get; } = new[]
        {
            "微软雅黑", "Microsoft YaHei", "宋体", "SimSun", "黑体", "SimHei",
            "楷体", "KaiTi", "仿宋", "FangSong", "Arial", "Times New Roman",
            "Calibri", "Consolas", "Cambria", "Tahoma", "Verdana"
        };

        /// <summary>常用 SHX 字体 — ComboBox 数据源</summary>
        public static IReadOnlyList<string> ShxFontOptions { get; } = new[]
        {
            "tssdeng.shx", "tssdchn.shx", "simplex.shx", "romans.shx", "romand.shx",
            "txt.shx", "hztxt.shx", "gbcbig.shx", "chineset.shx"
        };

        /// <summary>常用大字体（SHX 中文）— ComboBox 数据源</summary>
        public static IReadOnlyList<string> BigFontOptions { get; } = new[]
        {
            "tssdchn.shx", "hztxt.shx", "gbcbig.shx", "chineset.shx"
        };

        /// <summary>TrueType 字体选项（含当前值，确保 ComboBox 能正确显示当前选择）</summary>
        public IReadOnlyList<string> StyleTFontOptionsWithCurrent =>
            string.IsNullOrEmpty(_styleTFont) || TrueTypeFontOptions.Contains(_styleTFont)
                ? TrueTypeFontOptions
                : new[] { _styleTFont }.Concat(TrueTypeFontOptions).ToArray();

        /// <summary>SHX 字体选项（含当前值）</summary>
        public IReadOnlyList<string> StyleSFontOptionsWithCurrent =>
            string.IsNullOrEmpty(_styleSFont) || ShxFontOptions.Contains(_styleSFont)
                ? ShxFontOptions
                : new[] { _styleSFont }.Concat(ShxFontOptions).ToArray();

        /// <summary>大字体选项（含当前值）</summary>
        public IReadOnlyList<string> StyleSBigFontOptionsWithCurrent =>
            string.IsNullOrEmpty(_styleSBigFont) || BigFontOptions.Contains(_styleSBigFont)
                ? BigFontOptions
                : new[] { _styleSBigFont }.Concat(BigFontOptions).ToArray();

        /// <summary>兼容旧字段，映射到 StyleS</summary>
        public string FontFileName { get => StyleSFont; set { StyleSFont = value; } }
        /// <summary>兼容旧字段，映射到 StyleS</summary>
        public string BigFontFileName { get => StyleSBigFont; set { StyleSBigFont = value; } }

        private double _textSize = 2.5;
        public double TextSize
        {
            get => _textSize;
            set { if (SetProperty(ref _textSize, value)) { _stylesDirty = true; OnPropertyChanged(nameof(ActualTextHeight)); } }
        }

        private double _styleTXScale = 1.0;
        /// <summary>样式 1（TrueType 标题/说明）字宽比，默认 1.0</summary>
        public double StyleTXScale
        {
            get => _styleTXScale;
            set { if (SetProperty(ref _styleTXScale, value)) _stylesDirty = true; }
        }

        private double _styleSXScale = 0.7;
        /// <summary>样式 2（SHX 标注/引线/表格）字宽比，默认 0.7</summary>
        public double StyleSXScale
        {
            get => _styleSXScale;
            set
            {
                if (SetProperty(ref _styleSXScale, value))
                {
                    _stylesDirty = true;
                    OnPropertyChanged(nameof(TextXScale));
                }
            }
        }

        /// <summary>兼容旧字段：默认对外暴露 SHX 样式字宽（与历史 0.7 行为一致）</summary>
        public double TextXScale { get => StyleSXScale; set { StyleSXScale = value; } }

        /// <summary>
        /// 模型空间文字实际高度 = TextSize(paper-mm) × UnitFactor × Scale。
        /// 与 DIMSCALE/DIMLFAC 无关：副比例不改纸面大小。
        /// </summary>
        public double ActualTextHeight => TextSize * BuildScaleContext().UnitFactor * Scale;

        #endregion

        #region 标注样式属性

        private double _dimtxt = 2.5;
        public double Dimtxt { get => _dimtxt; set { if (SetProperty(ref _dimtxt, value)) _stylesDirty = true; } }

        private double _dimexo = 1.0;
        public double Dimexo { get => _dimexo; set { if (SetProperty(ref _dimexo, value)) _stylesDirty = true; } }

        private double _dimexe = 1.0;
        public double Dimexe { get => _dimexe; set { if (SetProperty(ref _dimexe, value)) _stylesDirty = true; } }

        private double _dimdle = 0.5;
        public double Dimdle { get => _dimdle; set { if (SetProperty(ref _dimdle, value)) _stylesDirty = true; } }

        private double _dimgap = 1.0;
        public double Dimgap { get => _dimgap; set { if (SetProperty(ref _dimgap, value)) _stylesDirty = true; } }

        private double _dimasz = 1.0;
        public double Dimasz { get => _dimasz; set { if (SetProperty(ref _dimasz, value)) _stylesDirty = true; } }

        private string _dimArrowName = "_ARCHTICK";
        public string DimArrowName { get => _dimArrowName; set => SetProperty(ref _dimArrowName, value); }

        #endregion

        #region 引线样式属性

        private double _mleaderArrowSize = 2.0;
        public double MLeaderArrowSize
        {
            get => _mleaderArrowSize;
            set { if (SetProperty(ref _mleaderArrowSize, value)) { _stylesDirty = true; OnPropertyChanged(nameof(ActualMLeaderArrowSize)); } }
        }

        private string _mleaderArrowName = "_DotSmall";
        public string MLeaderArrowName { get => _mleaderArrowName; set { if (SetProperty(ref _mleaderArrowName, value)) _stylesDirty = true; } }

        private double _mleaderLandingGap = 0.5;
        public double MLeaderLandingGap
        {
            get => _mleaderLandingGap;
            set { if (SetProperty(ref _mleaderLandingGap, value)) { _stylesDirty = true; OnPropertyChanged(nameof(ActualMLeaderLandingGap)); } }
        }

        private int _mleaderTextColorIndex = 7;
        public int MLeaderTextColorIndex { get => _mleaderTextColorIndex; set { if (SetProperty(ref _mleaderTextColorIndex, value)) _stylesDirty = true; } }

        /// <summary>模型空间箭头实际大小 = MLeaderArrowSize(paper-mm) × UnitFactor × Scale</summary>
        public double ActualMLeaderArrowSize => MLeaderArrowSize * BuildScaleContext().UnitFactor * Scale;
        /// <summary>模型空间着陆间距实际大小 = MLeaderLandingGap(paper-mm) × UnitFactor × Scale</summary>
        public double ActualMLeaderLandingGap => MLeaderLandingGap * BuildScaleContext().UnitFactor * Scale;

        #endregion

        // ================================================================
        //  Tab B: 钢筋参数（对应旧 ReinPanel）
        // ================================================================

        #region 钢筋标注参数

        private double _rebarDiameter = 14.0;
        public double RebarDiameter { get => _rebarDiameter; set => SetProperty(ref _rebarDiameter, value); }

        private double _rebarSpacing = 200.0;
        public double RebarSpacing { get => _rebarSpacing; set => SetProperty(ref _rebarSpacing, value); }

        #endregion

        #region 钢筋几何参数

        private double _anchorageLength = 500.0;
        public double AnchorageLength { get => _anchorageLength; set => SetProperty(ref _anchorageLength, value); }

        private double _dotSeparation = 200.0;
        public double DotSeparation { get => _dotSeparation; set => SetProperty(ref _dotSeparation, value); }

        private double _bendingLineMinLength = 150.0;
        public double BendingLineMinLength { get => _bendingLineMinLength; set => SetProperty(ref _bendingLineMinLength, value); }

        private double _anchorageJoinLength = 1500.0;
        public double AnchorageJoinLength { get => _anchorageJoinLength; set => SetProperty(ref _anchorageJoinLength, value); }

        private double _hookLength = 1.0;
        public double HookLength { get => _hookLength; set => SetProperty(ref _hookLength, value); }

        private double _protectionThickness = 1.0;
        public double ProtectionThickness { get => _protectionThickness; set => SetProperty(ref _protectionThickness, value); }

        private double _reinforcementDiameter = 0.35;
        public double ReinforcementDiameter { get => _reinforcementDiameter; set => SetProperty(ref _reinforcementDiameter, value); }

        private double _dotReinOffset = 1.35;
        public double DotReinOffset { get => _dotReinOffset; set => SetProperty(ref _dotReinOffset, value); }

        private double _polylineWidth = 0.4;
        public double PolylineWidth { get => _polylineWidth; set => SetProperty(ref _polylineWidth, value); }

        #endregion

        #region 钢筋尺寸参数

        private double _dimensionDistanceInside = 6.0;
        public double DimensionDistanceInside { get => _dimensionDistanceInside; set => SetProperty(ref _dimensionDistanceInside, value); }

        private double _dimensionDistanceOutside = 14.0;
        public double DimensionDistanceOutside { get => _dimensionDistanceOutside; set => SetProperty(ref _dimensionDistanceOutside, value); }

        private double _dimensionDistanceWithDim = 6.0;
        public double DimensionDistanceWithDim { get => _dimensionDistanceWithDim; set => SetProperty(ref _dimensionDistanceWithDim, value); }

        private double _mleaderDistance = 6.0;
        public double MleaderDistance { get => _mleaderDistance; set => SetProperty(ref _mleaderDistance, value); }

        private double _dimDistanceTolerance = 30.0;
        public double DimDistanceTolerance { get => _dimDistanceTolerance; set => SetProperty(ref _dimDistanceTolerance, value); }

        #endregion

        #region 道路参数

        private double _roadGapWidth = 5.0;
        public double RoadGapWidth { get => _roadGapWidth; set => SetProperty(ref _roadGapWidth, value); }

        private double _roadCrosswalkWidth = 5.0;
        public double RoadCrosswalkWidth { get => _roadCrosswalkWidth; set => SetProperty(ref _roadCrosswalkWidth, value); }

        private double _roadStopLineDistance = 2.0;
        public double RoadStopLineDistance { get => _roadStopLineDistance; set => SetProperty(ref _roadStopLineDistance, value); }

        private double _roadStripeSpacing = 1.0;
        public double RoadStripeSpacing { get => _roadStripeSpacing; set => SetProperty(ref _roadStripeSpacing, value); }

        /// <summary>标准横断面：平面带及上方标注（尺寸、顶字、北南向等）整体相对路顶在图面中上移的距离（m），与 rCs 出图一致，默认 5。</summary>
        private double _roadCrossSectionPlanStripVerticalOffsetM = 5.0;
        public double RoadCrossSectionPlanStripVerticalOffsetM
        {
            get => _roadCrossSectionPlanStripVerticalOffsetM;
            set => SetProperty(ref _roadCrossSectionPlanStripVerticalOffsetM, value);
        }

        /// <summary>
        /// 标准横断面方位箭头左右字组：false=北/南（默认），true=西/东。
        /// 预览中点击"北/南"或"西/东"字即可切换；VM 监听本属性变化触发 Recalculate。
        /// </summary>
        private bool _roadCrossSectionOrientationUseWestEast;
        public bool RoadCrossSectionOrientationUseWestEast
        {
            get => _roadCrossSectionOrientationUseWestEast;
            set => SetProperty(ref _roadCrossSectionOrientationUseWestEast, value);
        }

        // ── 横断面图题（DrawingSheetTitleSpec 持久化） ────────────────────────────
        private bool _sheetTitleShowCrosshair;
        public bool SheetTitleShowCrosshair { get => _sheetTitleShowCrosshair; set => SetProperty(ref _sheetTitleShowCrosshair, value); }

        private bool _sheetTitleShowScale = true;
        public bool SheetTitleShowScale { get => _sheetTitleShowScale; set => SetProperty(ref _sheetTitleShowScale, value); }

        private string _sheetTitleMainTextStyleName = "0-hy-说明-T";
        public string SheetTitleMainTextStyleName { get => _sheetTitleMainTextStyleName; set => SetProperty(ref _sheetTitleMainTextStyleName, value ?? "0-hy-说明-T"); }

        private string _sheetTitleScaleTextStyleName = "0-hy-说明-T";
        public string SheetTitleScaleTextStyleName { get => _sheetTitleScaleTextStyleName; set => SetProperty(ref _sheetTitleScaleTextStyleName, value ?? "0-hy-说明-T"); }

        /// <summary>横断面图题主字高（纸面 mm，出图时 × UnitFactor×Scale 同 <see cref="ActualTextHeight"/>）。</summary>
        private double _sheetTitleMainTextHeight = 4.0;
        public double SheetTitleMainTextHeight { get => _sheetTitleMainTextHeight; set => SetProperty(ref _sheetTitleMainTextHeight, value); }

        /// <summary>图题中比例「1:xxx」字高（纸面 mm）。</summary>
        private double _sheetTitleScaleTextHeight = 2.5;
        public double SheetTitleScaleTextHeight { get => _sheetTitleScaleTextHeight; set => SetProperty(ref _sheetTitleScaleTextHeight, value); }

        private double _sheetTitleScaleTextHeightRatio = 0.55;
        public double SheetTitleScaleTextHeightRatio { get => _sheetTitleScaleTextHeightRatio; set => SetProperty(ref _sheetTitleScaleTextHeightRatio, value); }

        private double _sheetTitleTopLineWidthFactor = 0.07;
        public double SheetTitleTopLineWidthFactor { get => _sheetTitleTopLineWidthFactor; set => SetProperty(ref _sheetTitleTopLineWidthFactor, value); }

        private double _sheetTitleBottomLineWidthFactor = 0.02;
        public double SheetTitleBottomLineWidthFactor { get => _sheetTitleBottomLineWidthFactor; set => SetProperty(ref _sheetTitleBottomLineWidthFactor, value); }

        private double _sheetTitleTextToLinesGapFactor = 0.12;
        public double SheetTitleTextToLinesGapFactor { get => _sheetTitleTextToLinesGapFactor; set => SetProperty(ref _sheetTitleTextToLinesGapFactor, value); }

        private double _sheetTitleDoubleLineSpacingFactor = 0.05;
        public double SheetTitleDoubleLineSpacingFactor { get => _sheetTitleDoubleLineSpacingFactor; set => SetProperty(ref _sheetTitleDoubleLineSpacingFactor, value); }

        private double _sheetTitleScaleGapFromTextFactor = 0.12;
        public double SheetTitleScaleGapFromTextFactor { get => _sheetTitleScaleGapFromTextFactor; set => SetProperty(ref _sheetTitleScaleGapFromTextFactor, value); }

        /// <summary>根据当前设置与 <see cref="HyCADTool.Shared.AutoCAD.Xdata.HyRoadLayers"/> 默认生成图题规格。</summary>
        public DrawingSheetTitleSpec CreateDrawingSheetTitleSpec()
            => DrawingSheetTitleStyleFactory.FromSettings(this);

        // ── 平面线位（Alignment）默认值 — 供 hyRoadAlnByPi 等命令读默认 ─────────────
        private double _alignmentDefaultRadius = 30.0;
        public double AlignmentDefaultRadius { get => _alignmentDefaultRadius; set => SetProperty(ref _alignmentDefaultRadius, value); }

        private double _alignmentDefaultSpiralIn = 0.0;
        public double AlignmentDefaultSpiralIn { get => _alignmentDefaultSpiralIn; set => SetProperty(ref _alignmentDefaultSpiralIn, value); }

        private double _alignmentDefaultSpiralOut = 0.0;
        public double AlignmentDefaultSpiralOut { get => _alignmentDefaultSpiralOut; set => SetProperty(ref _alignmentDefaultSpiralOut, value); }

        private double _alignmentDefaultStartStation = 0.0;
        public double AlignmentDefaultStartStation { get => _alignmentDefaultStartStation; set => SetProperty(ref _alignmentDefaultStartStation, value); }

        // ── 桩号标注配置（RoadStationLabelOptions 的持久化镜像） ──────────────────
        private double _stationMainInterval = 20.0;
        public double StationMainInterval { get => _stationMainInterval; set => SetProperty(ref _stationMainInterval, value); }

        private double _stationSubInterval = 5.0;
        public double StationSubInterval { get => _stationSubInterval; set => SetProperty(ref _stationSubInterval, value); }

        private double _stationTickLengthMain = 4.0;
        public double StationTickLengthMain { get => _stationTickLengthMain; set => SetProperty(ref _stationTickLengthMain, value); }

        private double _stationTickLengthSub = 1.5;
        public double StationTickLengthSub { get => _stationTickLengthSub; set => SetProperty(ref _stationTickLengthSub, value); }

        private double _stationTextHeight = 3.0;
        public double StationTextHeight { get => _stationTextHeight; set => SetProperty(ref _stationTextHeight, value); }

        private double _stationTextMargin = 0.5;
        public double StationTextMargin { get => _stationTextMargin; set => SetProperty(ref _stationTextMargin, value); }

        private bool _stationRotateTextAlongTangent = true;
        public bool StationRotateTextAlongTangent { get => _stationRotateTextAlongTangent; set => SetProperty(ref _stationRotateTextAlongTangent, value); }

        /// <summary>
        /// 文字挂在中心线哪一侧："Left" 或 "Right"（不区分大小写；非法值会回退到 Left）。
        /// 存字符串是为了 JSON 自解释。
        /// </summary>
        private string _stationTextSide = "Left";
        public string StationTextSide { get => _stationTextSide; set => SetProperty(ref _stationTextSide, value ?? "Left"); }

        /// <summary>
        /// 把当前桩号标注配置快照为 <see cref="HyCADTool.Features.Road.PlanAlignment.Services.RoadStationLabelOptions"/>。
        /// 非法字符串 TextSide 会退化为 Left。
        /// </summary>
        public HyCADTool.Features.Road.PlanAlignment.Services.RoadStationLabelOptions CreateStationLabelOptions()
        {
            HyCADTool.Features.Road.PlanAlignment.Services.StationTextSide side =
                HyCADTool.Features.Road.PlanAlignment.Services.StationTextSide.Left;
            if (string.Equals(StationTextSide, "Right", System.StringComparison.OrdinalIgnoreCase))
                side = HyCADTool.Features.Road.PlanAlignment.Services.StationTextSide.Right;
            return new HyCADTool.Features.Road.PlanAlignment.Services.RoadStationLabelOptions
            {
                MainInterval = StationMainInterval,
                SubInterval = StationSubInterval,
                TickLengthMain = StationTickLengthMain,
                TickLengthSub = StationTickLengthSub,
                TextHeight = StationTextHeight,
                TextMargin = StationTextMargin,
                RotateTextAlongTangent = StationRotateTextAlongTangent,
                TextSide = side,
            };
        }

        /// <summary>把桩号标注配置写回当前 ViewModel（触发自动保存）。</summary>
        public void ApplyStationLabelOptions(HyCADTool.Features.Road.PlanAlignment.Services.RoadStationLabelOptions opt)
        {
            if (opt == null) return;
            opt.Validate();
            StationMainInterval = opt.MainInterval;
            StationSubInterval = opt.SubInterval;
            StationTickLengthMain = opt.TickLengthMain;
            StationTickLengthSub = opt.TickLengthSub;
            StationTextHeight = opt.TextHeight;
            StationTextMargin = opt.TextMargin;
            StationRotateTextAlongTangent = opt.RotateTextAlongTangent;
            StationTextSide = opt.TextSide.ToString();
        }

        /// <summary>
        /// 把当前 Alignment 默认值快照成 Domain 值对象（无指针引用，便于 Domain 直接消费）。
        /// </summary>
        public Domain.ValueObjects.Road.AlignmentDefaults CreateAlignmentDefaults()
            => new Domain.ValueObjects.Road.AlignmentDefaults
            {
                DefaultRadius = AlignmentDefaultRadius,
                DefaultSpiralIn = AlignmentDefaultSpiralIn,
                DefaultSpiralOut = AlignmentDefaultSpiralOut,
                DefaultStartStation = AlignmentDefaultStartStation,
            };

        /// <summary>
        /// 反向：把外部传入的 Alignment 默认值写回 ViewModel（含持久化触发）。
        /// </summary>
        public void ApplyAlignmentDefaults(Domain.ValueObjects.Road.AlignmentDefaults d)
        {
            if (d == null) return;
            d.Validate();
            AlignmentDefaultRadius = d.DefaultRadius;
            AlignmentDefaultSpiralIn = d.DefaultSpiralIn;
            AlignmentDefaultSpiralOut = d.DefaultSpiralOut;
            AlignmentDefaultStartStation = d.DefaultStartStation;
        }

        #endregion

        // ================================================================
        //  状态 & 命令
        // ================================================================

        #region 状态

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        /// <summary>
        /// 自动保存开关：关闭后 SetProperty 不再即时写入 hy-settings.json，
        /// 改为由「保存用户设置」按钮显式保存。默认开启（向后兼容原行为）。
        /// </summary>
        private bool _autoSaveEnabled = true;
        public bool AutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set { if (_autoSaveEnabled == value) return; _autoSaveEnabled = value; OnPropertyChanged(); }
        }

        // ----------------------------------------------------------------
        //  hyed 双击编辑开关（替代 AutoCAD 原生 TEXTEDIT/MTEDIT/DDEDIT，绕开 IPE/TTF 扫描）
        //  setter 联动 HyEdDoubleClickInterceptor.Install/Uninstall，立即生效。
        // ----------------------------------------------------------------
        private bool _enableHyEdDoubleClick = true;
        public bool EnableHyEdDoubleClick
        {
            get => _enableHyEdDoubleClick;
            set
            {
                if (_enableHyEdDoubleClick == value) return;
                _enableHyEdDoubleClick = value;
                OnPropertyChanged();

                try
                {
                    if (_enableHyEdDoubleClick)
                        HyCADTool.Features.TextEdit.Services.HyEdDoubleClickInterceptor.Install();
                    else
                        HyCADTool.Features.TextEdit.Services.HyEdDoubleClickInterceptor.Uninstall();
                }
                catch
                {
                    /* AutoCAD 未就绪等场景静默；Initialize 仍会按当前值挂钩 */
                }

                if (!_isLoading && _autoSaveEnabled) SaveSettings();
            }
        }

        /// <summary>
        /// hyed 编辑框相对原文字上移的"文字高度倍数"。默认 <b>0</b>：与原生 IPE 行为对齐——
        /// 编辑时原文字会被隐藏（<see cref="HyEdLauncher"/> 中 entity.Visible=false），编辑框直接覆盖原位置。
        /// 1.2 = 上方一行；2 = 上方两行；适合不想让原文字位置被遮挡时使用。
        /// </summary>
        private double _hyEdAboveOffsetFactor = 0.0;
        public double HyEdAboveOffsetFactor
        {
            get => _hyEdAboveOffsetFactor;
            set
            {
                double v = double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : value;
                if (v < 0) v = 0;
                if (v > 20) v = 20;
                if (Math.Abs(_hyEdAboveOffsetFactor - v) < 1e-9) return;
                _hyEdAboveOffsetFactor = v;
                OnPropertyChanged();
                if (!_isLoading && _autoSaveEnabled) SaveSettings();
            }
        }

        // ----------------------------------------------------------------
        //  界面主题（HyCAD.BlenderUI 调色板）
        //  字符串来源 / 取值：BlenderThemeManager.Parse(...) 容忍大小写与简写
        //  setter 内调 BlenderThemeManager.Apply 实现实时切换；
        //  受 _isLoading + AutoSaveEnabled 守门，避免加载期重复写盘。
        // ----------------------------------------------------------------
        private string _theme = "BlenderDark";
        public string Theme
        {
            get => _theme;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? "BlenderDark" : value.Trim();
                if (string.Equals(_theme, normalized, StringComparison.OrdinalIgnoreCase)) return;
                _theme = normalized;
                OnPropertyChanged();

                try { BlenderThemeManager.Apply(_theme); }
                catch
                {
                    // 主题切换失败（极少：Application 未就绪 / palette XAML 解析异常）不能阻塞 setter，
                    // 否则配置写盘也会跟着失败。失败时颜色保持当前值，下次面板重建会再次 Apply。
                }

                if (!_isLoading && _autoSaveEnabled) SaveSettings();
            }
        }

        // ----------------------------------------------------------------
        //  界面尺寸比例（Metric_* 三类缩放，见 BlenderMetricsScaleManager）
        // ----------------------------------------------------------------
        private double _uiFontScale = 1.0;
        public double UiFontScale
        {
            get => _uiFontScale;
            set
            {
                var v = NormalizeUiMetricScale(value);
                if (Math.Abs(_uiFontScale - v) < 1e-9) return;
                _uiFontScale = v;
                OnPropertyChanged();
                if (!_isLoading)
                {
                    try { BlenderMetricsScaleManager.Apply(_uiFontScale, _uiDensityScale, _uiInputWidthScale); }
                    catch { /* 忽略 */ }
                    if (_autoSaveEnabled) SaveSettings();
                }
            }
        }

        private double _uiDensityScale = 1.0;
        public double UiDensityScale
        {
            get => _uiDensityScale;
            set
            {
                var v = NormalizeUiMetricScale(value);
                if (Math.Abs(_uiDensityScale - v) < 1e-9) return;
                _uiDensityScale = v;
                OnPropertyChanged();
                if (!_isLoading)
                {
                    try { BlenderMetricsScaleManager.Apply(_uiFontScale, _uiDensityScale, _uiInputWidthScale); }
                    catch { /* 忽略 */ }
                    if (_autoSaveEnabled) SaveSettings();
                }
            }
        }

        private double _uiInputWidthScale = 1.0;
        public double UiInputWidthScale
        {
            get => _uiInputWidthScale;
            set
            {
                var v = NormalizeUiMetricScale(value);
                if (Math.Abs(_uiInputWidthScale - v) < 1e-9) return;
                _uiInputWidthScale = v;
                OnPropertyChanged();
                if (!_isLoading)
                {
                    try { BlenderMetricsScaleManager.Apply(_uiFontScale, _uiDensityScale, _uiInputWidthScale); }
                    catch { /* 忽略 */ }
                    if (_autoSaveEnabled) SaveSettings();
                }
            }
        }

        /// <summary>从 JSON 读取或滑块写入时的统一裁剪。</summary>
        private static double NormalizeUiMetricScale(double value)
        {
            if (double.IsNaN(value) || value <= 0) return 1.0;
            return Math.Max(0.5, Math.Min(2.0, value));
        }

        #endregion

        #region 命令

        public ICommand ApplyStyleCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand DrawCommand { get; }

        // 钢筋绘制命令
        public ICommand CmdGj { get; }
        public ICommand CmdGg { get; }

        // 钢筋修改命令
        public ICommand CmdG1 { get; }
        public ICommand CmdG2 { get; }
        public ICommand CmdGe { get; }
        public ICommand CmdGe1 { get; }
        public ICommand CmdGd { get; }

        // 钢筋标注命令
        public ICommand CmdGb { get; }
        public ICommand CmdGb1 { get; }
        public ICommand CmdGb2 { get; }

        // 道路命令
        public ICommand CmdRoad { get; }

        /// <summary>
        /// 市政道路设计 ViewModel（P0 落地）。
        /// XAML 中通过 <c>{Binding RoadDesign.CmdAlignment}</c> 等方式使用。
        /// 默认构造（设计器占位）未实例化；主构造函数中初始化。
        /// </summary>
        public RoadDesignViewModel RoadDesign { get; }

        #endregion

        #region 命令路由（面板按钮 → C1 → AutoCAD 命令线程）

        /// <summary>
        /// 待执行命令：面板按钮设置后通过 _HyExec 在 AutoCAD 命令线程执行
        /// </summary>
        public static System.Action PendingCommand { get; set; }

        /// <summary>
        /// 最后一次执行的命令（用于 _HyExec 重复执行）
        /// </summary>
        public static System.Action LastCommand { get; private set; }

        /// <summary>
        /// 取出待执行命令（不清除，保留用于重复）
        /// </summary>
        public static System.Action ConsumePendingCommand()
        {
            var cmd = PendingCommand;
            if (cmd != null)
            {
                // 有新命令 → 保存为最后命令，清除待执行标记
                LastCommand = cmd;
                PendingCommand = null;
            }
            // 返回最后命令（新命令或重复命令）
            return LastCommand;
        }

        /// <summary>
        /// 从面板按钮发起命令：设置 PendingCommand，然后通过 _HyExec 在正确线程执行
        /// 面板按钮点击时先按需同步样式（仅 dirty 时），再执行命令
        /// </summary>
        private void SendCommand(System.Action commandAction)
        {
            CommitFocusedTextBoxValue();
            SaveSettings();
            if (_stylesDirty) EnsureStylesApplied();

            #region agent log
            AgentDebugLogger.Log("routing", "H6", "SettingsPanelViewModel.SendCommand", "panel send command",
                new
                {
                    commandType = commandAction?.Method?.DeclaringType?.FullName,
                    commandName = commandAction?.Method?.Name
                });
            #endregion

            PendingCommand = () =>
            {
                commandAction();
            };
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                #region agent log
                AgentDebugLogger.Log("routing", "H11", "SettingsPanelViewModel.SendCommand", "before queue c1",
                    new
                    {
                        hasPendingCommand = PendingCommand != null,
                        hasDocument = doc != null,
                        commandInProgress = doc?.CommandInProgress,
                        documentName = doc?.Name
                    });
                #endregion
                doc.SendStringToExecute("_HyExec\n", true, false, false);
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"发送命令失败: {ex.Message}";
                PendingCommand = null;
            }
        }

        #endregion

        #region 命令实现

        /// <param name="includeDimensionAndTable">
        /// 为 false 时只同步文字与多重引线，不 <see cref="IStyleService.CreateDimensionStyle"/> / 不建表格样式；
        /// 供 rCs 等以几何为真值的出图，避免将设置中的单位/比例写入标注样式表。此时不置 <c>_stylesDirty = false</c>、不 <see cref="SaveSettings"/>。
        /// </param>
        private void ApplyStyle(bool includeDimensionAndTable = true)
        {
            try
            {
                if (_styleService == null) { StatusMessage = "StyleService 未初始化"; return; }

                var ctx = BuildScaleContext();
                double uf = ctx.UnitFactor;
                double scale = ctx.MainScale;      // DIMSCALE = M
                double dimlfac = ctx.DimLfac;      // = UseSub ? S/M : 1
                int dimdec = ctx.Precision;

                // 文字样式：model-unit 字高 = paper_mm × uf × scale（字宽不变）
                _styleService.CreateTextStyle(StyleTName, StyleTFont, "", TextSize * uf * scale, StyleTXScale);
                _styleService.CreateTextStyle(StyleSName, StyleSFont, StyleSBigFont, TextSize * uf * scale, StyleSXScale);
                _styleService.SetCurrentTextStyle(StyleSName);

                if (includeDimensionAndTable)
                {
                    // 标注样式：paper-mm 基值原样传入，内部 × uf 存盘，再由 DIMSCALE 放大
                    _styleService.CreateDimensionStyle(
                        ctx.BuildDimStyleName(), TextStyleName, scale,
                        Dimtxt, Dimexo, Dimexe, Dimdle, Dimgap, Dimasz,
                        dimlfac, dimdec, uf);
                    _styleService.SetCurrentDimensionStyle(ctx.BuildDimStyleName());
                }

                // 引线样式：paper-mm 基值原样传入，内部 × uf × scale 写入
                _styleService.CreateMLeaderStyle(
                    ctx.BuildMLeaderStyleName(), TextStyleName, scale,
                    MLeaderArrowSize, MLeaderLandingGap, TextSize, MLeaderTextColorIndex, uf);
                _styleService.SetCurrentMLeaderStyle(ctx.BuildMLeaderStyleName());

                if (includeDimensionAndTable)
                {
                    _styleService.CreateTableStyle(ctx.BuildTableStyleName(), TextStyleName);
                    _styleService.SetCurrentTableStyle(ctx.BuildTableStyleName());
                }

                ActiveScaleContextProvider.Set(ctx);

                _appliedContext = ctx;
                OnPropertyChanged(nameof(AppliedDimStyleName));

                if (includeDimensionAndTable)
                {
                    _stylesDirty = false;
                    SaveSettings();
                    StatusMessage = ctx.UseSubScale
                        ? $"样式应用成功 (M=1:{scale} S=1:{ctx.SubScale} {ctx.UnitShortName} p={dimdec})"
                        : $"样式应用成功 (1:{scale} {ctx.UnitShortName} p={dimdec})";
                }
                else
                {
                    StatusMessage = "已同步文字与多重引线（未写入标注/表格样式，尺寸以几何与图中当前标注样式为准）";
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 供命令调用：确保当前面板参数已同步到 AutoCAD 样式
        /// 仅在参数有变更（dirty）时才执行样式重建，避免不必要的开销
        /// </summary>
        public void EnsureStylesApplied()
        {
            if (!_stylesDirty) return;
            ApplyStyle();
            // 成功路径由 ApplyStyle() 置 _stylesDirty=false；失败或 StyleService 未就绪时保持 dirty，
            // 否则「单位/副比例」等模式切换的 auto-apply 失败后用户再点「置为当前」会被误判为无需同步。
        }

        /// <summary>
        /// 将「界面-设置」中的文字/标注/多重引线/表格样式写入当前 DWG，与按「置为当前」等效（内部 <see cref="ApplyStyle()"/> 全量）。
        /// </summary>
        public void CommitStylesToActiveDocument()
        {
            ApplyStyle(includeDimensionAndTable: true);
        }

        /// <summary>
        /// 仅将文字、多重引线样式按当前设置写入 DWG，不创建/不覆盖标注样式与表格样式。
        /// rCs 出图在「尺寸以几何为真、设置单位可能变化」场景下应使用本方法，尺寸实体上挂 <see cref="Autodesk.AutoCAD.DatabaseServices.Database.Dimstyle"/>（只读取当前图）而非按设置名解析。
        /// </summary>
        public void CommitTextAndMLeaderStylesToActiveDocument()
        {
            ApplyStyle(includeDimensionAndTable: false);
        }

        private void SaveAsDefault()
        {
            CommitFocusedTextBoxValue();
            SaveSettings();
            if (_stylesDirty)
                EnsureStylesApplied();
            StatusMessage = "当前设置已保存为默认值";
        }

        public static void CommitFocusedTextBoxValue()
        {
            if (Keyboard.FocusedElement is TextBox textBox)
            {
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            }

            // 兜底：PaletteSet 中焦点转移到 AutoCAD 命令行时
            // Keyboard.FocusedElement 已不是 TextBox，且 LostFocus 未触发
            TextBoxHelper.CommitPendingInput();
        }

        private void ResetToDefaults()
        {
            // Tab A
            Scale = 50.0;
            UseSubScale = false;
            SubScale = 50.0;
            Unit = DrawingUnit.Millimeter;
            Precision = 0;
            StyleTName = "0-hy-说明-T";
            StyleTFont = "微软雅黑";
            StyleSName = "0-hy-说明-S";
            StyleSFont = "tssdeng.shx";
            StyleSBigFont = "tssdchn.shx";
            TextSize = 2.5;
            StyleTXScale = 1.0;
            StyleSXScale = 0.7;
            Dimtxt = 2.5; Dimexo = 1.0; Dimexe = 1.0; Dimdle = 0.5; Dimgap = 1.0; Dimasz = 1.0;
            DimArrowName = "_ARCHTICK";
            MLeaderArrowSize = 2.0; MLeaderArrowName = "_DotSmall"; MLeaderLandingGap = 0.5; MLeaderTextColorIndex = 7;

            // Tab B
            RebarDiameter = 14.0; RebarSpacing = 200.0;
            AnchorageLength = 500.0; DotSeparation = 200.0; BendingLineMinLength = 150.0;
            AnchorageJoinLength = 1500.0; HookLength = 1.0; ProtectionThickness = 1.0;
            ReinforcementDiameter = 0.35; DotReinOffset = 1.35; PolylineWidth = 0.4;
            DimensionDistanceInside = 6.0; DimensionDistanceOutside = 14.0;
            DimensionDistanceWithDim = 6.0; MleaderDistance = 6.0;             DimDistanceTolerance = 30.0;

            _uiFontScale = _uiDensityScale = _uiInputWidthScale = 1.0;
            OnPropertyChanged(nameof(UiFontScale));
            OnPropertyChanged(nameof(UiDensityScale));
            OnPropertyChanged(nameof(UiInputWidthScale));
            try { BlenderMetricsScaleManager.Apply(1.0, 1.0, 1.0); } catch { /* 忽略 */ }

            LoadLayerCatalogFromSettingsData(null);
            SetRoadMaterialFillSettings(null);
            SaveSettings();
            StatusMessage = "已恢复默认值";
        }

        #endregion

        #region 图层表（UserLayerSettings）

        /// <summary>按语义 ID 从当前 <see cref="LayerCatalogItems"/> 解析落图用图层名；无匹配则 <paramref name="defaultName"/>。</summary>
        public string TryResolveLayerName(string semanticId, string defaultName)
        {
            if (string.IsNullOrWhiteSpace(semanticId)) return defaultName ?? string.Empty;
            var def = defaultName ?? string.Empty;
            foreach (var it in LayerCatalogItems)
            {
                if (it == null) continue;
                if (string.Equals(it.SemanticId, semanticId, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(it.Name)) return it.Name.Trim();
                    return def;
                }
            }
            return def;
        }

        /// <summary>文档初始化前确保内存中层表已加载（与 <see cref="LoadSettings()"/> 一致）。</summary>
        public void EnsureLayerCatalogForDocumentInit()
        {
            if (LayerCatalogItems != null && LayerCatalogItems.Count > 0) return;
            LoadLayerCatalogFromSettingsData(null);
        }

        private void LoadLayerCatalogFromSettingsData(UserLayerSettings s)
        {
            var merged = UserLayerSettingsMerger.MergeWithDefaults(s);
            LayerCatalogItems.Clear();
            foreach (var it in merged.Items)
                LayerCatalogItems.Add(it.Clone());
        }

        private UserLayerSettings BuildUserLayerSettingsForSave()
        {
            if (LayerCatalogItems == null || LayerCatalogItems.Count == 0)
                return UserLayerSettingsMerger.MergeWithDefaults(null);
            return new UserLayerSettings
            {
                Version = LayerCatalogFactory.CurrentCatalogVersion,
                Items = LayerCatalogItems.Select(x => x.Clone()).ToList()
            };
        }

        public RoadMaterialFillSettings GetRoadMaterialFillSettings()
            => CloneRoadMaterialFillSettings(_roadMaterialFillSettings);

        public void SetRoadMaterialFillSettings(RoadMaterialFillSettings settings, bool saveImmediately = false)
        {
            _roadMaterialFillSettings = CloneRoadMaterialFillSettings(settings) ?? new RoadMaterialFillSettings();
            RoadMaterialFillPresets.ApplyUserSettings(_roadMaterialFillSettings);
            if (saveImmediately && !_isLoading)
                SaveSettings();
        }

        /// <summary>
        /// 合并线型目录快照并立即保存 hy-settings.json（供 <c>hyLtCapture</c> 调用）。
        /// </summary>
        public void SetLinetypeCatalogSnapshot(LinetypeCatalogSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            _linetypeCatalogSnapshot = snapshot;
            if (!_isLoading)
                SaveSettings();
        }

        public void ApplyLayerCatalogToCurrentDocument()
        {
            try
            {
                if (LayerCatalogItems == null || LayerCatalogItems.Count == 0)
                    LoadLayerCatalogFromSettingsData(null);
                ILayerService layerService;
                if (!ServiceLocator.TryResolve(out layerService))
                {
                    StatusMessage = "无法解析 ILayerService，图层未应用。";
                    return;
                }
                layerService.EnsureUserLayerItems(LayerCatalogItems.ToList());
                StatusMessage = "已按当前层表更新当前图纸的图层。";
            }
            catch (Exception ex)
            {
                StatusMessage = $"图层应用失败: {ex.Message}";
            }
        }

        public void RestoreDefaultLayerCatalog()
        {
            LoadLayerCatalogFromSettingsData(null);
            StatusMessage = "图层表已恢复为程序默认；若开启自动保存将写入 hy-settings。";
            if (_autoSaveEnabled && !_isLoading) SaveSettings();
        }

        private static RoadMaterialFillSettings CloneRoadMaterialFillSettings(RoadMaterialFillSettings settings)
        {
            return new RoadMaterialFillSettings
            {
                Version = Math.Max(1, settings?.Version ?? 1),
                SurfaceAdded = settings?.SurfaceAdded?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToList()
                               ?? new List<string>(),
                SurfaceRemoved = settings?.SurfaceRemoved?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToList()
                                 ?? new List<string>(),
                BaseAdded = settings?.BaseAdded?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToList()
                            ?? new List<string>(),
                BaseRemoved = settings?.BaseRemoved?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToList()
                              ?? new List<string>(),
                SubbaseAdded = settings?.SubbaseAdded?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToList()
                               ?? new List<string>(),
                SubbaseRemoved = settings?.SubbaseRemoved?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToList()
                                 ?? new List<string>(),
            };
        }

        #endregion

        #region 参数导出

        /// <summary>
        /// 从当前面板属性创建 ReinParameters（供 DrawReinforcementCommand 使用）
        /// 对应旧代码通过 ReinPanel.ActivePanel 读取参数
        /// </summary>
        public ReinParameters CreateReinParameters()
        {
            return new ReinParameters
            {
                Scale = Scale,
                RebarDiameter = RebarDiameter,
                RebarSpacing = RebarSpacing,
                AnchorageLength = AnchorageLength,
                DotSeparation = DotSeparation,
                BendingLineMinLength = BendingLineMinLength,
                AnchorageJoinLength = AnchorageJoinLength,
                HookLength = HookLength,
                ProtectionThickness = ProtectionThickness,
                ReinforcementDiameter = ReinforcementDiameter,
                DotReinOffset = DotReinOffset,
                PolylineWidth = PolylineWidth,
                DimensionDistanceInside = DimensionDistanceInside,
                DimensionDistanceOutside = DimensionDistanceOutside,
                DimensionDistanceWithDim = DimensionDistanceWithDim,
                MleaderDistance = MleaderDistance,
                DimDistanceTolerance = DimDistanceTolerance,
                TextXScale = TextXScale,
                TextSize = TextSize
            };
        }

        #endregion

        // ================================================================
        //  持久化：hy-settings.json（%APPDATA%\HyCADTool）
        // ================================================================

        #region 持久化

        private static string _settingsFilePath;

        /// <summary>
        /// 获取设置文件路径（%APPDATA%\HyCADTool\hy-settings.json）
        /// 使用 AppData 而非 Assembly 目录，避免热重载时路径不稳定
        /// </summary>
        public static string GetSettingsFilePath()
        {
            if (_settingsFilePath != null) return _settingsFilePath;
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HyCADTool");
            Directory.CreateDirectory(appDataDir);
            _settingsFilePath = Path.Combine(appDataDir, "hy-settings.json");
            return _settingsFilePath;
        }

        /// <summary>
        /// 保存当前面板参数到 JSON 文件。
        /// 当 <paramref name="explicitPath"/> 为 null 时写入默认路径 <see cref="GetSettingsFilePath"/>。
        /// </summary>
        public void SaveSettings(string explicitPath = null)
        {
            try
            {
                var data = new SettingsData
                {
                    // Tab A: 样式
                    Scale = Scale,
                    UseSubScale = UseSubScale,
                    SubScale = SubScale,
                    Unit = Unit.ToString(),
                    Precision = Precision,
                    StyleTName = StyleTName,
                    StyleTFont = StyleTFont,
                    StyleSName = StyleSName,
                    StyleSFont = StyleSFont,
                    StyleSBigFont = StyleSBigFont,
                    TextSize = TextSize,
                    StyleTXScale = StyleTXScale,
                    StyleSXScale = StyleSXScale,
                    TextXScale = StyleSXScale, // 兼容旧字段：与 StyleSXScale 同步写盘
                    Dimtxt = Dimtxt,
                    Dimexo = Dimexo,
                    Dimexe = Dimexe,
                    Dimdle = Dimdle,
                    Dimgap = Dimgap,
                    Dimasz = Dimasz,
                    DimArrowName = DimArrowName,
                    MLeaderArrowSize = MLeaderArrowSize,
                    MLeaderArrowName = MLeaderArrowName,
                    MLeaderLandingGap = MLeaderLandingGap,
                    MLeaderTextColorIndex = MLeaderTextColorIndex,
                    // Tab B: 钢筋
                    RebarDiameter = RebarDiameter,
                    RebarSpacing = RebarSpacing,
                    AnchorageLength = AnchorageLength,
                    DotSeparation = DotSeparation,
                    BendingLineMinLength = BendingLineMinLength,
                    AnchorageJoinLength = AnchorageJoinLength,
                    HookLength = HookLength,
                    ProtectionThickness = ProtectionThickness,
                    ReinforcementDiameter = ReinforcementDiameter,
                    DotReinOffset = DotReinOffset,
                    PolylineWidth = PolylineWidth,
                    DimensionDistanceInside = DimensionDistanceInside,
                    DimensionDistanceOutside = DimensionDistanceOutside,
                    DimensionDistanceWithDim = DimensionDistanceWithDim,
                    MleaderDistance = MleaderDistance,
                    DimDistanceTolerance = DimDistanceTolerance,
                    // Tab C: 道路
                    RoadGapWidth = RoadGapWidth,
                    RoadCrosswalkWidth = RoadCrosswalkWidth,
                    RoadStopLineDistance = RoadStopLineDistance,
                    RoadStripeSpacing = RoadStripeSpacing,
                    RoadCrossSectionPlanStripVerticalOffsetM = RoadCrossSectionPlanStripVerticalOffsetM,
                    RoadCrossSectionOrientationUseWestEast = RoadCrossSectionOrientationUseWestEast,
                    SheetTitleShowCrosshair = SheetTitleShowCrosshair,
                    SheetTitleShowScale = SheetTitleShowScale,
                    SheetTitleMainTextStyleName = SheetTitleMainTextStyleName,
                    SheetTitleScaleTextStyleName = SheetTitleScaleTextStyleName,
                    SheetTitleMainTextHeight = SheetTitleMainTextHeight,
                    SheetTitleScaleTextHeight = SheetTitleScaleTextHeight,
                    SheetTitleScaleTextHeightRatio = SheetTitleScaleTextHeightRatio,
                    SheetTitleTopLineWidthFactor = SheetTitleTopLineWidthFactor,
                    SheetTitleBottomLineWidthFactor = SheetTitleBottomLineWidthFactor,
                    SheetTitleTextToLinesGapFactor = SheetTitleTextToLinesGapFactor,
                    SheetTitleDoubleLineSpacingFactor = SheetTitleDoubleLineSpacingFactor,
                    SheetTitleScaleGapFromTextFactor = SheetTitleScaleGapFromTextFactor,
                    // Tab C: Alignment 默认
                    AlignmentDefaultRadius = AlignmentDefaultRadius,
                    AlignmentDefaultSpiralIn = AlignmentDefaultSpiralIn,
                    AlignmentDefaultSpiralOut = AlignmentDefaultSpiralOut,
                    AlignmentDefaultStartStation = AlignmentDefaultStartStation,
                    // Tab C: 桩号标注
                    StationMainInterval = StationMainInterval,
                    StationSubInterval = StationSubInterval,
                    StationTickLengthMain = StationTickLengthMain,
                    StationTickLengthSub = StationTickLengthSub,
                    StationTextHeight = StationTextHeight,
                    StationTextMargin = StationTextMargin,
                    StationRotateTextAlongTangent = StationRotateTextAlongTangent,
                    StationTextSide = StationTextSide,
                    EnableHyEdDoubleClick = EnableHyEdDoubleClick,
                    HyEdAboveOffsetFactor = HyEdAboveOffsetFactor,
                    // 界面外观
                    Theme = Theme,
                    UiFontScale = UiFontScale,
                    UiDensityScale = UiDensityScale,
                    UiInputWidthScale = UiInputWidthScale,
                    // 其他
                    EquipmentDataFilePath = EquipmentDataFilePath,
                    UserLayerSettings = BuildUserLayerSettingsForSave(),
                    RoadMaterialFillSettings = GetRoadMaterialFillSettings(),
                    LinetypeCatalog = _linetypeCatalogSnapshot
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(explicitPath ?? GetSettingsFilePath(), json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
                if (explicitPath != null) throw; // 显式路径失败向上抛，让 UI 提示
            }
        }

        /// <summary>
        /// 显式保存到指定路径（「保存用户设置」按钮在 AutoSave=false 时走此路径）。
        /// </summary>
        public void SaveSettingsToFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("路径不能为空", nameof(path));
            SaveSettings(path);
        }

        /// <summary>
        /// 从 JSON 文件加载设置到当前实例。
        /// 当 <paramref name="explicitPath"/> 为 null 时读取默认路径；
        /// 文件不存在或格式错误时静默使用默认值（显式路径下会抛异常让 UI 提示）。
        /// </summary>
        public void LoadSettings(string explicitPath = null)
        {
            _isLoading = true;
            _layerCatalogSchemaVersionBeforeLoad = int.MinValue;
            try
            {
                string beforeStyleSignature = BuildStyleSignature();
                var path = explicitPath ?? GetSettingsFilePath();
                if (!File.Exists(path))
                {
                    _isLoading = false;
                    if (explicitPath != null) throw new FileNotFoundException("设置文件不存在", path);
                    _layerCatalogSchemaVersionBeforeLoad = 0;
                    LoadLayerCatalogFromSettingsData(null);
                    SetRoadMaterialFillSettings(null);
                    _linetypeCatalogSnapshot = null;
                    return;
                }

                var json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<SettingsData>(json);
                if (data == null)
                {
                    _isLoading = false;
                    _layerCatalogSchemaVersionBeforeLoad = 0;
                    LoadLayerCatalogFromSettingsData(null);
                    SetRoadMaterialFillSettings(null);
                    _linetypeCatalogSnapshot = null;
                    return;
                }

                _layerCatalogSchemaVersionBeforeLoad = data.UserLayerSettings?.Version ?? 0;

                // Tab A: 样式 —— 先读比例相关字段（新 JSON 可能缺失，走向后兼容）
                Scale = data.Scale > 0 ? data.Scale : 50.0;
                UseSubScale = data.UseSubScale;
                SubScale = data.SubScale > 0 ? data.SubScale : Scale;
                if (!string.IsNullOrWhiteSpace(data.Unit)
                    && Enum.TryParse<DrawingUnit>(data.Unit, true, out var unitParsed))
                    Unit = unitParsed;
                else
                    Unit = DrawingUnit.Millimeter;
                Precision = data.Precision;  // setter 自带 ClampPrecision，旧 JSON 缺失=0 兼容 mm
                StyleTName = data.StyleTName ?? _styleTName;
                StyleTFont = data.StyleTFont ?? _styleTFont;
                StyleSName = data.StyleSName ?? _styleSName;
#pragma warning disable CS0618 // 旧 JSON 仍可能只写 FontFileName / BigFontFileName
                StyleSFont = data.StyleSFont ?? data.FontFileName ?? _styleSFont;
                StyleSBigFont = data.StyleSBigFont ?? data.BigFontFileName ?? _styleSBigFont;
#pragma warning restore CS0618
                TextSize = data.TextSize;
                // 字宽：优先读新字段，兜底读旧 TextXScale（旧文件里 SHX 字宽存在 TextXScale，T 字宽默认 1.0）
                StyleTXScale = data.StyleTXScale > 0 ? data.StyleTXScale : 1.0;
                StyleSXScale = data.StyleSXScale > 0 ? data.StyleSXScale
                              : (data.TextXScale > 0 ? data.TextXScale : 0.7);
                Dimtxt = data.Dimtxt;
                Dimexo = data.Dimexo;
                Dimexe = data.Dimexe;
                Dimdle = data.Dimdle;
                Dimgap = data.Dimgap;
                Dimasz = data.Dimasz;
                DimArrowName = data.DimArrowName ?? _dimArrowName;
                MLeaderArrowSize = data.MLeaderArrowSize;
                MLeaderArrowName = data.MLeaderArrowName ?? _mleaderArrowName;
                MLeaderLandingGap = data.MLeaderLandingGap;
                MLeaderTextColorIndex = data.MLeaderTextColorIndex;
                // Tab B: 钢筋
                RebarDiameter = data.RebarDiameter;
                RebarSpacing = data.RebarSpacing;
                AnchorageLength = data.AnchorageLength;
                DotSeparation = data.DotSeparation;
                BendingLineMinLength = data.BendingLineMinLength;
                AnchorageJoinLength = data.AnchorageJoinLength;
                HookLength = data.HookLength;
                ProtectionThickness = data.ProtectionThickness;
                ReinforcementDiameter = data.ReinforcementDiameter;
                DotReinOffset = data.DotReinOffset;
                PolylineWidth = data.PolylineWidth;
                DimensionDistanceInside = data.DimensionDistanceInside;
                DimensionDistanceOutside = data.DimensionDistanceOutside;
                DimensionDistanceWithDim = data.DimensionDistanceWithDim;
                MleaderDistance = data.MleaderDistance;
                DimDistanceTolerance = data.DimDistanceTolerance;
                // Tab C: 道路
                RoadGapWidth = data.RoadGapWidth;
                RoadCrosswalkWidth = data.RoadCrosswalkWidth;
                RoadStopLineDistance = data.RoadStopLineDistance;
                RoadStripeSpacing = data.RoadStripeSpacing;
                if (data.RoadCrossSectionPlanStripVerticalOffsetM != null)
                    RoadCrossSectionPlanStripVerticalOffsetM = data.RoadCrossSectionPlanStripVerticalOffsetM.Value;
                if (data.RoadCrossSectionOrientationUseWestEast != null)
                    RoadCrossSectionOrientationUseWestEast = data.RoadCrossSectionOrientationUseWestEast.Value;
                SheetTitleShowCrosshair = data.SheetTitleShowCrosshair;
                SheetTitleShowScale = data.SheetTitleShowScale;
                if (!string.IsNullOrWhiteSpace(data.SheetTitleMainTextStyleName)) SheetTitleMainTextStyleName = data.SheetTitleMainTextStyleName;
                if (!string.IsNullOrWhiteSpace(data.SheetTitleScaleTextStyleName)) SheetTitleScaleTextStyleName = data.SheetTitleScaleTextStyleName;
                if (data.SheetTitleMainTextHeight > 0) SheetTitleMainTextHeight = data.SheetTitleMainTextHeight;
                if (data.SheetTitleScaleTextHeight > 0) SheetTitleScaleTextHeight = data.SheetTitleScaleTextHeight;
                if (data.SheetTitleScaleTextHeightRatio > 0) SheetTitleScaleTextHeightRatio = data.SheetTitleScaleTextHeightRatio;
                if (data.SheetTitleTopLineWidthFactor > 0) SheetTitleTopLineWidthFactor = data.SheetTitleTopLineWidthFactor;
                if (data.SheetTitleBottomLineWidthFactor > 0) SheetTitleBottomLineWidthFactor = data.SheetTitleBottomLineWidthFactor;
                if (data.SheetTitleTextToLinesGapFactor >= 0) SheetTitleTextToLinesGapFactor = data.SheetTitleTextToLinesGapFactor;
                if (data.SheetTitleDoubleLineSpacingFactor >= 0) SheetTitleDoubleLineSpacingFactor = data.SheetTitleDoubleLineSpacingFactor;
                if (data.SheetTitleScaleGapFromTextFactor >= 0) SheetTitleScaleGapFromTextFactor = data.SheetTitleScaleGapFromTextFactor;
                // Tab C: Alignment 默认
                AlignmentDefaultRadius = data.AlignmentDefaultRadius;
                AlignmentDefaultSpiralIn = data.AlignmentDefaultSpiralIn;
                AlignmentDefaultSpiralOut = data.AlignmentDefaultSpiralOut;
                AlignmentDefaultStartStation = data.AlignmentDefaultStartStation;
                // Tab C: 桩号标注
                if (data.StationMainInterval > 0) StationMainInterval = data.StationMainInterval;
                StationSubInterval = data.StationSubInterval;
                StationTickLengthMain = data.StationTickLengthMain;
                StationTickLengthSub = data.StationTickLengthSub;
                if (data.StationTextHeight > 0) StationTextHeight = data.StationTextHeight;
                StationTextMargin = data.StationTextMargin;
                StationRotateTextAlongTangent = data.StationRotateTextAlongTangent;
                if (!string.IsNullOrWhiteSpace(data.StationTextSide)) StationTextSide = data.StationTextSide;
                EnableHyEdDoubleClick = data.EnableHyEdDoubleClick;
                HyEdAboveOffsetFactor = data.HyEdAboveOffsetFactor;
                // 界面外观（_isLoading 期间 setter 仍会调 BlenderThemeManager.Apply，刷新所有 DynamicResource）
                if (!string.IsNullOrWhiteSpace(data.Theme))
                    Theme = data.Theme;
                _uiFontScale = data.UiFontScale > 0 ? NormalizeUiMetricScale(data.UiFontScale) : 1.0;
                _uiDensityScale = data.UiDensityScale > 0 ? NormalizeUiMetricScale(data.UiDensityScale) : 1.0;
                _uiInputWidthScale = data.UiInputWidthScale > 0 ? NormalizeUiMetricScale(data.UiInputWidthScale) : 1.0;
                OnPropertyChanged(nameof(UiFontScale));
                OnPropertyChanged(nameof(UiDensityScale));
                OnPropertyChanged(nameof(UiInputWidthScale));
                // 其他
                if (!string.IsNullOrEmpty(data.EquipmentDataFilePath))
                    EquipmentDataFilePath = data.EquipmentDataFilePath;

                LoadLayerCatalogFromSettingsData(data.UserLayerSettings);
                SetRoadMaterialFillSettings(data.RoadMaterialFillSettings);
                _linetypeCatalogSnapshot = data.LinetypeCatalog;

                // 只有样式相关参数实际变化时，才标记需要重新同步样式。
                // 否则 gj/gb 等每次执行都会白白重建一轮样式，造成重复执行前的明显停顿。
                string afterStyleSignature = BuildStyleSignature();
                if (!string.Equals(beforeStyleSignature, afterStyleSignature, StringComparison.Ordinal))
                    _stylesDirty = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}（使用默认值）");
                SetRoadMaterialFillSettings(null);
                _linetypeCatalogSnapshot = null;
            }
            finally
            {
                _isLoading = false;
                try
                {
                    BlenderMetricsScaleManager.CaptureBaselineIfNeeded();
                    BlenderMetricsScaleManager.Apply(_uiFontScale, _uiDensityScale, _uiInputWidthScale);
                }
                catch
                {
                    /* 设计器 / 过早：忽略 */
                }

                OnPropertyChanged(nameof(AppliedDimStyleName));

                if (_layerCatalogSchemaVersionBeforeLoad != int.MinValue
                    && _layerCatalogSchemaVersionBeforeLoad < LayerCatalogFactory.CurrentCatalogVersion)
                {
                    try { SaveSettings(); }
                    catch { /* 首启/只读目录等：静默 */ }
                }
                _layerCatalogSchemaVersionBeforeLoad = int.MinValue;
            }
        }

        /// <summary>
        /// 显式保存当前参数到 hy-settings.json（用于「保存用户设置」按钮）。
        /// 即便 AutoSaveEnabled=false 也会强制写入一次。
        /// </summary>
        public void SavePublic() => SaveSettings();

        /// <summary>从指定路径加载（「选择文件恢复」按钮）。</summary>
        public void LoadSettingsFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("路径不能为空", nameof(path));
            LoadSettings(path);
        }

        /// <summary>
        /// 从磁盘重载 hy-settings.json（用于「恢复自动保存」按钮）。
        /// </summary>
        public void ReloadFromDisk() => LoadSettings();

        /// <summary>
        /// 序列化 DTO — 纯数据容器，字段默认值与面板硬编码默认值一致
        /// 新增字段时此处同步加默认值，确保旧 JSON 文件向前兼容
        /// </summary>
        private class SettingsData
        {
            // Tab A: 样式
            public double Scale { get; set; } = 50.0;
            /// <summary>副比例开关（新增）</summary>
            public bool UseSubScale { get; set; } = false;
            /// <summary>副比例数值（新增；UseSubScale=false 时等于 Scale）</summary>
            public double SubScale { get; set; } = 50.0;
            /// <summary>绘图单位（新增）：Millimeter / Centimeter / Meter</summary>
            public string Unit { get; set; } = "Millimeter";
            /// <summary>标注小数位（新增）：mm={0} / cm={1,2} / m={1,2,3}</summary>
            public int Precision { get; set; } = 0;
            public string StyleTName { get; set; } = "0-hy-说明-T";
            public string StyleTFont { get; set; } = "微软雅黑";
            public string StyleSName { get; set; } = "0-hy-说明-S";
            public string StyleSFont { get; set; } = "tssdeng.shx";
            public string StyleSBigFont { get; set; } = "tssdchn.shx";
            [Obsolete("Use StyleSFont")]
            public string FontFileName { get; set; } = "tssdeng.shx";
            [Obsolete("Use StyleSBigFont")]
            public string BigFontFileName { get; set; } = "hztxt.shx";
            public double TextSize { get; set; } = 2.5;
            /// <summary>样式 1（TrueType）字宽，默认 1.0</summary>
            public double StyleTXScale { get; set; } = 1.0;
            /// <summary>样式 2（SHX）字宽，默认 0.7</summary>
            public double StyleSXScale { get; set; } = 0.7;
            /// <summary>兼容旧字段：等同于 StyleSXScale，用于旧版本 JSON 兼容</summary>
            public double TextXScale { get; set; } = 0.7;
            public double Dimtxt { get; set; } = 2.5;
            public double Dimexo { get; set; } = 1.0;
            public double Dimexe { get; set; } = 1.0;
            public double Dimdle { get; set; } = 0.5;
            public double Dimgap { get; set; } = 1.0;
            public double Dimasz { get; set; } = 1.0;
            public string DimArrowName { get; set; } = "_ARCHTICK";
            public double MLeaderArrowSize { get; set; } = 2.0;
            public string MLeaderArrowName { get; set; } = "_DotSmall";
            public double MLeaderLandingGap { get; set; } = 0.5;
            public int MLeaderTextColorIndex { get; set; } = 7;
            // Tab B: 钢筋
            public double RebarDiameter { get; set; } = 14.0;
            public double RebarSpacing { get; set; } = 200.0;
            public double AnchorageLength { get; set; } = 500.0;
            public double DotSeparation { get; set; } = 200.0;
            public double BendingLineMinLength { get; set; } = 150.0;
            public double AnchorageJoinLength { get; set; } = 1500.0;
            public double HookLength { get; set; } = 1.0;
            public double ProtectionThickness { get; set; } = 1.0;
            public double ReinforcementDiameter { get; set; } = 0.35;
            public double DotReinOffset { get; set; } = 1.35;
            public double PolylineWidth { get; set; } = 0.4;
            public double DimensionDistanceInside { get; set; } = 6.0;
            public double DimensionDistanceOutside { get; set; } = 14.0;
            public double DimensionDistanceWithDim { get; set; } = 6.0;
            public double MleaderDistance { get; set; } = 6.0;
            public double DimDistanceTolerance { get; set; } = 30.0;
            // Tab C: 道路
            public double RoadGapWidth { get; set; } = 5.0;
            public double RoadCrosswalkWidth { get; set; } = 5.0;
            public double RoadStopLineDistance { get; set; } = 2.0;
            public double RoadStripeSpacing { get; set; } = 1.0;
            /// <summary>缺省为 null 表示旧版 JSON 未带此字段，加载时用 VM 默认 5m。</summary>
            public double? RoadCrossSectionPlanStripVerticalOffsetM { get; set; }
            /// <summary>缺省为 null 表示旧版 JSON 未带此字段，加载时用 VM 默认 false（北/南）。</summary>
            public bool? RoadCrossSectionOrientationUseWestEast { get; set; }
            public bool SheetTitleShowCrosshair { get; set; } = false;
            public bool SheetTitleShowScale { get; set; } = true;
            public string SheetTitleMainTextStyleName { get; set; } = "0-hy-说明-T";
            public string SheetTitleScaleTextStyleName { get; set; } = "0-hy-说明-T";
            public double SheetTitleMainTextHeight { get; set; } = 4.0;
            public double SheetTitleScaleTextHeight { get; set; } = 2.5;
            public double SheetTitleScaleTextHeightRatio { get; set; } = 0.55;
            public double SheetTitleTopLineWidthFactor { get; set; } = 0.07;
            public double SheetTitleBottomLineWidthFactor { get; set; } = 0.02;
            public double SheetTitleTextToLinesGapFactor { get; set; } = 0.12;
            public double SheetTitleDoubleLineSpacingFactor { get; set; } = 0.05;
            public double SheetTitleScaleGapFromTextFactor { get; set; } = 0.12;
            // Alignment 默认值（hyRoadAlnByPi 等命令读默认）
            public double AlignmentDefaultRadius { get; set; } = 30.0;
            public double AlignmentDefaultSpiralIn { get; set; } = 0.0;
            public double AlignmentDefaultSpiralOut { get; set; } = 0.0;
            public double AlignmentDefaultStartStation { get; set; } = 0.0;
            // 桩号标注（RoadStationLabelOptions 持久化）
            public double StationMainInterval { get; set; } = 20.0;
            public double StationSubInterval { get; set; } = 5.0;
            public double StationTickLengthMain { get; set; } = 4.0;
            public double StationTickLengthSub { get; set; } = 1.5;
            public double StationTextHeight { get; set; } = 3.0;
            public double StationTextMargin { get; set; } = 0.5;
            public bool StationRotateTextAlongTangent { get; set; } = true;
            public string StationTextSide { get; set; } = "Left";
            /// <summary>hyed 双击编辑开关（默认开）</summary>
            public bool EnableHyEdDoubleClick { get; set; } = true;
            /// <summary>hyed 编辑框相对原文字的上移系数（×文字高度，默认 0 表示原位重叠+隐藏原字）</summary>
            public double HyEdAboveOffsetFactor { get; set; } = 0.0;
            // 界面外观：HyCAD.BlenderUI.Theming.BlenderThemeManager 主题枚举名
            // 取值：BlenderDark / BlenderLight / AcadLight / AcadDark / AcadBlue
            public string Theme { get; set; } = "BlenderDark";
            /// <summary>界面字号 Metric_Font* 缩放，默认 1.0。</summary>
            public double UiFontScale { get; set; } = 1.0;
            /// <summary>行高、间距、Thickness 等密度缩放，默认 1.0。</summary>
            public double UiDensityScale { get; set; } = 1.0;
            /// <summary>输入框宽、标签列宽等缩放，默认 1.0。</summary>
            public double UiInputWidthScale { get; set; } = 1.0;
            // 其他
            public string EquipmentDataFilePath { get; set; } = "";

            /// <summary>用户可编辑图层表（v1+）；旧 JSON 缺省则读盘后合并默认。</summary>
            public UserLayerSettings UserLayerSettings { get; set; }

            /// <summary>横断面填料候选的用户增量设置（v1+）。</summary>
            public RoadMaterialFillSettings RoadMaterialFillSettings { get; set; }

            /// <summary>当前图纸线型目录快照（<c>hyLtCapture</c>）；旧 JSON 缺省则为 null。</summary>
            public LinetypeCatalogSnapshot LinetypeCatalog { get; set; }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private string BuildStyleSignature()
        {
            return string.Join("|",
                Scale,
                UseSubScale,
                SubScale,
                Unit,
                Precision,
                StyleTName,
                StyleTFont,
                StyleSName,
                StyleSFont,
                StyleSBigFont,
                TextSize,
                StyleTXScale,
                StyleSXScale,
                Dimtxt,
                Dimexo,
                Dimexe,
                Dimdle,
                Dimgap,
                Dimasz,
                DimArrowName,
                MLeaderArrowSize,
                MLeaderArrowName,
                MLeaderLandingGap,
                MLeaderTextColorIndex);
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 加载中标记：防止 LoadSettings → 属性 setter → SaveSettings 循环
        /// </summary>
        private bool _isLoading;

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);

            // 非加载期间，参数变更即时写入文件（跨程序集共享状态）
            // AutoSaveEnabled=false 时仅 UI 更新，实际文件写入延后到"保存用户设置"按钮
            if (!_isLoading
                && propertyName != nameof(StatusMessage)
                && propertyName != nameof(AutoSaveEnabled)
                && _autoSaveEnabled)
                SaveSettings();

            return true;
        }

        #endregion
    }
}
