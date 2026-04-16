using Autofac;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.Views.Helpers;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.ViewModels
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
            DrawCommand = new RelayCommand(() => SendCommand(() => new Commands.DrawReinforcementCommand().Execute()));

            // 钢筋绘制
            CmdGj = new RelayCommand(() => SendCommand(() => new Commands.DrawReinforcementCommand().Execute()));
            CmdGg = new RelayCommand(() => SendCommand(() => new Commands.DrawOffsetPolylineCommand().Execute()));

            // 钢筋修改
            CmdG1 = new RelayCommand(() => SendCommand(() => new Commands.ReinAddAnchorCommand(isVertical: false).Execute()));
            CmdG2 = new RelayCommand(() => SendCommand(() => new Commands.ReinAddAnchorCommand(isVertical: true).Execute()));
            CmdGe = new RelayCommand(() => SendCommand(() => new Commands.ReinExtendCommand().Execute()));
            CmdGe1 = new RelayCommand(() => SendCommand(() => new Commands.ReinQuickExtendCommand().Execute()));
            CmdGd = new RelayCommand(() => SendCommand(() => new Commands.ReinCutCommand().Execute()));

            // 钢筋标注
            CmdGb = new RelayCommand(() => SendCommand(() => new Commands.MleaderReinCommand(Commands.MleaderReinCommand.Mode.Standard).Execute()));
            CmdGb1 = new RelayCommand(() => SendCommand(() => new Commands.MleaderReinCommand(Commands.MleaderReinCommand.Mode.Single).Execute()));
            CmdGb2 = new RelayCommand(() => SendCommand(() => new Commands.MleaderReinCommand(Commands.MleaderReinCommand.Mode.Six).Execute()));

            // 道路
            CmdRoad = new RelayCommand(() => SendCommand(() => new Commands.DrawCrosswalkCommand().Execute()));

            // P0：市政道路设计 ViewModel（Domain 驱动，独立 RoadDesignViewModel）
            RoadDesign = new RoadDesignViewModel();

            // 从持久化文件加载上次保存的设置
            LoadSettings();
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

        private double _scale = 40.0;
        public double Scale
        {
            get => _scale;
            set
            {
                if (SetProperty(ref _scale, value))
                {
                    _stylesDirty = true;
                    OnPropertyChanged(nameof(TextStyleName));
                    OnPropertyChanged(nameof(DimStyleName));
                    OnPropertyChanged(nameof(MLeaderStyleName));
                    OnPropertyChanged(nameof(TableStyleName));
                    OnPropertyChanged(nameof(ActualTextHeight));
                    OnPropertyChanged(nameof(ActualMLeaderArrowSize));
                    OnPropertyChanged(nameof(ActualMLeaderLandingGap));
                }
            }
        }

        #endregion

        #region 动态样式名称

        /// <summary>主文字样式（标注/引线/表格引用），对应 0-hy-说明-S</summary>
        public string TextStyleName => StyleSName;
        public string DimStyleName => $"0_Hy_{Scale}_Dim";
        public string MLeaderStyleName => $"0_Hy_{Scale}_Mleader";
        public string TableStyleName => $"0_Hy_{Scale}_Table";

        #endregion

        #region 文字样式属性（双样式：T=TrueType 说明，S=SHX 工程）

        /// <summary>样式1：0-hy-说明-T，TrueType 字体（标题/说明用）</summary>
        private string _styleTName = "0-hy-说明-T";
        public string StyleTName { get => _styleTName; set { if (SetProperty(ref _styleTName, value)) _stylesDirty = true; } }

        private string _styleTFont = "微软雅黑";
        public string StyleTFont { get => _styleTFont; set { if (SetProperty(ref _styleTFont, value)) _stylesDirty = true; } }

        /// <summary>样式2：0-hy-说明-S，SHX 字体（标注/引线/表格用）</summary>
        private string _styleSName = "0-hy-说明-S";
        public string StyleSName { get => _styleSName; set { if (SetProperty(ref _styleSName, value)) _stylesDirty = true; } }

        private string _styleSFont = "tssdeng.shx";
        public string StyleSFont { get => _styleSFont; set { if (SetProperty(ref _styleSFont, value)) _stylesDirty = true; } }

        private string _styleSBigFont = "tssdchn.shx";
        public string StyleSBigFont { get => _styleSBigFont; set { if (SetProperty(ref _styleSBigFont, value)) _stylesDirty = true; } }

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

        private double _textXScale = 1.0;
        public double TextXScale { get => _textXScale; set { if (SetProperty(ref _textXScale, value)) _stylesDirty = true; } }

        public double ActualTextHeight => TextSize * Scale;

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

        public double ActualMLeaderArrowSize => MLeaderArrowSize * Scale;
        public double ActualMLeaderLandingGap => MLeaderLandingGap * Scale;

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

        #endregion

        // ================================================================
        //  状态 & 命令
        // ================================================================

        #region 状态

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

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
        /// 待执行命令：面板按钮设置后通过 C1 在 AutoCAD 命令线程执行
        /// </summary>
        public static System.Action PendingCommand { get; set; }

        /// <summary>
        /// 最后一次执行的命令（用于 C1 重复执行）
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
        /// 从面板按钮发起命令：设置 PendingCommand，然后通过 C1 在正确线程执行
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
                doc.SendStringToExecute("C1\n", true, false, false);
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"发送命令失败: {ex.Message}";
                PendingCommand = null;
            }
        }

        #endregion

        #region 命令实现

        private void ApplyStyle()
        {
            try
            {
                if (_styleService == null) { StatusMessage = "StyleService 未初始化"; return; }

                // 样式1：0-hy-说明-T，TrueType 微软雅黑（标题/说明）
                _styleService.CreateTextStyle(StyleTName, StyleTFont, "", TextSize * Scale, TextXScale);
                // 样式2：0-hy-说明-S，SHX tssdeng+tssdchn（标注/引线/表格）
                _styleService.CreateTextStyle(StyleSName, StyleSFont, StyleSBigFont, TextSize * Scale, TextXScale);
                _styleService.SetCurrentTextStyle(StyleSName);

                _styleService.CreateDimensionStyle(DimStyleName, TextStyleName, Scale, Dimtxt, Dimexo, Dimexe, Dimdle, Dimgap, Dimasz);
                _styleService.SetCurrentDimensionStyle(DimStyleName);

                _styleService.CreateMLeaderStyle(MLeaderStyleName, TextStyleName, Scale, MLeaderArrowSize, MLeaderLandingGap, TextSize, MLeaderTextColorIndex);
                _styleService.SetCurrentMLeaderStyle(MLeaderStyleName);

                _styleService.CreateTableStyle(TableStyleName, TextStyleName);
                _styleService.SetCurrentTableStyle(TableStyleName);

                _stylesDirty = false;
                SaveSettings();
                StatusMessage = $"样式应用成功 (Scale={Scale})";
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
            _stylesDirty = false;
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
            Scale = 40.0;
            StyleTName = "0-hy-说明-T";
            StyleTFont = "微软雅黑";
            StyleSName = "0-hy-说明-S";
            StyleSFont = "tssdeng.shx";
            StyleSBigFont = "tssdchn.shx";
            TextSize = 2.5;
            TextXScale = 1.0;
            Dimtxt = 2.5; Dimexo = 1.0; Dimexe = 1.0; Dimdle = 0.5; Dimgap = 1.0; Dimasz = 1.0;
            DimArrowName = "_ARCHTICK";
            MLeaderArrowSize = 2.0; MLeaderArrowName = "_DotSmall"; MLeaderLandingGap = 0.5; MLeaderTextColorIndex = 7;

            // Tab B
            RebarDiameter = 14.0; RebarSpacing = 200.0;
            AnchorageLength = 500.0; DotSeparation = 200.0; BendingLineMinLength = 150.0;
            AnchorageJoinLength = 1500.0; HookLength = 1.0; ProtectionThickness = 1.0;
            ReinforcementDiameter = 0.35; DotReinOffset = 1.35; PolylineWidth = 0.4;
            DimensionDistanceInside = 6.0; DimensionDistanceOutside = 14.0;
            DimensionDistanceWithDim = 6.0; MleaderDistance = 6.0; DimDistanceTolerance = 30.0;

            SaveSettings();
            StatusMessage = "已恢复默认值";
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
        /// 保存当前面板参数到 JSON 文件
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                var data = new SettingsData
                {
                    // Tab A: 样式
                    Scale = Scale,
                    StyleTName = StyleTName,
                    StyleTFont = StyleTFont,
                    StyleSName = StyleSName,
                    StyleSFont = StyleSFont,
                    StyleSBigFont = StyleSBigFont,
                    TextSize = TextSize,
                    TextXScale = TextXScale,
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
                    // 其他
                    EquipmentDataFilePath = EquipmentDataFilePath
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(GetSettingsFilePath(), json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 JSON 文件加载设置到当前实例
        /// 文件不存在或格式错误时静默使用默认值
        /// </summary>
        public void LoadSettings()
        {
            _isLoading = true;
            try
            {
                string beforeStyleSignature = BuildStyleSignature();
                var path = GetSettingsFilePath();
                if (!File.Exists(path)) { _isLoading = false; return; }

                var json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<SettingsData>(json);
                if (data == null) { _isLoading = false; return; }

                // Tab A: 样式
                Scale = data.Scale;
                StyleTName = data.StyleTName ?? _styleTName;
                StyleTFont = data.StyleTFont ?? _styleTFont;
                StyleSName = data.StyleSName ?? _styleSName;
                StyleSFont = data.StyleSFont ?? data.FontFileName ?? _styleSFont;
                StyleSBigFont = data.StyleSBigFont ?? data.BigFontFileName ?? _styleSBigFont;
                TextSize = data.TextSize;
                TextXScale = data.TextXScale;
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
                // 其他
                if (!string.IsNullOrEmpty(data.EquipmentDataFilePath))
                    EquipmentDataFilePath = data.EquipmentDataFilePath;

                // 只有样式相关参数实际变化时，才标记需要重新同步样式。
                // 否则 gj/gb 等每次执行都会白白重建一轮样式，造成重复执行前的明显停顿。
                string afterStyleSignature = BuildStyleSignature();
                if (!string.Equals(beforeStyleSignature, afterStyleSignature, StringComparison.Ordinal))
                    _stylesDirty = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载设置失败: {ex.Message}（使用默认值）");
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// 序列化 DTO — 纯数据容器，字段默认值与面板硬编码默认值一致
        /// 新增字段时此处同步加默认值，确保旧 JSON 文件向前兼容
        /// </summary>
        private class SettingsData
        {
            // Tab A: 样式
            public double Scale { get; set; } = 40.0;
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
            public double TextXScale { get; set; } = 1.0;
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
            // 其他
            public string EquipmentDataFilePath { get; set; } = "";
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        private string BuildStyleSignature()
        {
            return string.Join("|",
                Scale,
                StyleTName,
                StyleTFont,
                StyleSName,
                StyleSFont,
                StyleSBigFont,
                TextSize,
                TextXScale,
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
            if (!_isLoading && propertyName != nameof(StatusMessage))
                SaveSettings();

            return true;
        }

        #endregion
    }
}
