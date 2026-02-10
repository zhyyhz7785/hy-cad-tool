using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects;
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
                    // 为新文档创建 ViewModel（使用默认构造函数，无 IStyleService）
                    _documentViewModels[docName] = new SettingsPanelViewModel();
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

            ApplyStyleCommand = new RelayCommand(ApplyStyle);
            ResetCommand = new RelayCommand(ResetToDefaults);
            DrawCommand = new RelayCommand(DrawReinforcement);

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
        }

        public SettingsPanelViewModel()
        {
            ApplyStyleCommand = new RelayCommand(() => { });
            ResetCommand = new RelayCommand(() => { });
            DrawCommand = new RelayCommand(() => { });
            CmdGj = CmdGg = new RelayCommand(() => { });
            CmdG1 = CmdG2 = CmdGe = CmdGe1 = CmdGd = new RelayCommand(() => { });
            CmdGb = CmdGb1 = CmdGb2 = new RelayCommand(() => { });
        }

        #endregion

        // ================================================================
        //  Tab A: 样式设置
        // ================================================================

        #region 基础属性

        private double _scale = 40.0;
        public double Scale
        {
            get => _scale;
            set
            {
                if (SetProperty(ref _scale, value))
                {
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

        public string TextStyleName => $"0_Hy_{Scale}";
        public string DimStyleName => $"0_Hy_{Scale}_Dim";
        public string MLeaderStyleName => $"0_Hy_{Scale}_Mleader";
        public string TableStyleName => $"0_Hy_{Scale}_Table";

        #endregion

        #region 文字样式属性

        private string _fontFileName = "tssdeng.shx";
        public string FontFileName { get => _fontFileName; set => SetProperty(ref _fontFileName, value); }

        private string _bigFontFileName = "hztxt.shx";
        public string BigFontFileName { get => _bigFontFileName; set => SetProperty(ref _bigFontFileName, value); }

        private double _textSize = 2.5;
        public double TextSize
        {
            get => _textSize;
            set { if (SetProperty(ref _textSize, value)) OnPropertyChanged(nameof(ActualTextHeight)); }
        }

        private double _textXScale = 0.7;
        public double TextXScale { get => _textXScale; set => SetProperty(ref _textXScale, value); }

        public double ActualTextHeight => TextSize * Scale;

        #endregion

        #region 标注样式属性

        private double _dimtxt = 2.5;
        public double Dimtxt { get => _dimtxt; set => SetProperty(ref _dimtxt, value); }

        private double _dimexo = 1.0;
        public double Dimexo { get => _dimexo; set => SetProperty(ref _dimexo, value); }

        private double _dimexe = 1.0;
        public double Dimexe { get => _dimexe; set => SetProperty(ref _dimexe, value); }

        private double _dimdle = 0.5;
        public double Dimdle { get => _dimdle; set => SetProperty(ref _dimdle, value); }

        private double _dimgap = 1.0;
        public double Dimgap { get => _dimgap; set => SetProperty(ref _dimgap, value); }

        private double _dimasz = 1.0;
        public double Dimasz { get => _dimasz; set => SetProperty(ref _dimasz, value); }

        private string _dimArrowName = "_ARCHTICK";
        public string DimArrowName { get => _dimArrowName; set => SetProperty(ref _dimArrowName, value); }

        #endregion

        #region 引线样式属性

        private double _mleaderArrowSize = 2.0;
        public double MLeaderArrowSize
        {
            get => _mleaderArrowSize;
            set { if (SetProperty(ref _mleaderArrowSize, value)) OnPropertyChanged(nameof(ActualMLeaderArrowSize)); }
        }

        private string _mleaderArrowName = "_DotSmall";
        public string MLeaderArrowName { get => _mleaderArrowName; set => SetProperty(ref _mleaderArrowName, value); }

        private double _mleaderLandingGap = 0.5;
        public double MLeaderLandingGap
        {
            get => _mleaderLandingGap;
            set { if (SetProperty(ref _mleaderLandingGap, value)) OnPropertyChanged(nameof(ActualMLeaderLandingGap)); }
        }

        private int _mleaderTextColorIndex = 7;
        public int MLeaderTextColorIndex { get => _mleaderTextColorIndex; set => SetProperty(ref _mleaderTextColorIndex, value); }

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
        /// </summary>
        private void SendCommand(System.Action commandAction)
        {
            PendingCommand = () =>
            {
                EnsureStylesApplied();
                commandAction();
            };
            try
            {
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
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

                _styleService.CreateTextStyle(TextStyleName, FontFileName, BigFontFileName, TextSize * Scale, TextXScale);
                _styleService.SetCurrentTextStyle(TextStyleName);

                _styleService.CreateDimensionStyle(DimStyleName, TextStyleName, Scale, Dimtxt, Dimexo, Dimexe, Dimdle, Dimgap, Dimasz);
                _styleService.SetCurrentDimensionStyle(DimStyleName);

                _styleService.CreateMLeaderStyle(MLeaderStyleName, TextStyleName, Scale, MLeaderArrowSize, MLeaderLandingGap, TextSize, MLeaderTextColorIndex);
                _styleService.SetCurrentMLeaderStyle(MLeaderStyleName);

                _styleService.CreateTableStyle(TableStyleName, TextStyleName);
                _styleService.SetCurrentTableStyle(TableStyleName);

                StatusMessage = $"样式应用成功 (Scale={Scale})";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 供命令调用：确保当前面板参数已同步到 AutoCAD 样式
        /// 命令执行前调用此方法，保证 MLeader/标注/文字样式与面板参数一致
        /// </summary>
        public void EnsureStylesApplied()
        {
            ApplyStyle();
        }

        private void DrawReinforcement()
        {
            try
            {
                // 先应用样式
                ApplyStyle();
                // 同步参数到旧 Reinforcement 系统，然后发送 gj 命令
                SyncToOldReinforcement();
                var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                doc.SendStringToExecute("gj\n", true, false, false);
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"绘制失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 通过反射将新面板参数同步到旧 Reinforcement 系统
        /// 旧代码从 ReinPanel.ActivePanel 读取参数，此方法设置 ActivePanel 的属性
        /// </summary>
        private void SyncToOldReinforcement()
        {
            try
            {
                // 查找旧 ReinPanel 类型（在已加载的 HyCADtool 程序集中）
                System.Type reinPanelType = null;
                System.Type reinforcementType = null;
                System.Type baseConfigType = null;

                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (asm.GetName().Name == "HyCADtool" || asm.FullName.Contains("HyCADtool"))
                        {
                            reinPanelType = reinPanelType ?? asm.GetType("HyCADTool.Views.ReinPanel");
                            reinforcementType = reinforcementType ?? asm.GetType("HyCADTool.Reinforcement");
                            baseConfigType = baseConfigType ?? asm.GetType("HyCADTool.Config.BaseConfig");
                        }
                    }
                    catch { }
                }

                // 设置 BaseConfig.Scale
                if (baseConfigType != null)
                {
                    var scaleProp = baseConfigType.GetProperty("Scale", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    scaleProp?.SetValue(null, Scale);
                }

                // 设置 Reinforcement.Scale（有 setter）
                if (reinforcementType != null)
                {
                    var scaleProp = reinforcementType.GetProperty("Scale", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    scaleProp?.SetValue(null, Scale);
                }

                // 获取或创建 ReinPanel.ActivePanel，设置其属性
                if (reinPanelType != null)
                {
                    var activePanelProp = reinPanelType.GetProperty("ActivePanel", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var activePanel = activePanelProp?.GetValue(null);

                    if (activePanel == null)
                    {
                        // 没有旧面板实例，创建一个并设为 ActivePanel
                        activePanel = System.Activator.CreateInstance(reinPanelType);
                        activePanelProp?.SetValue(null, activePanel);
                    }

                    // 同步所有属性到旧面板
                    SetOldPanelProperty(reinPanelType, activePanel, "RebarDiameter", RebarDiameter);
                    SetOldPanelProperty(reinPanelType, activePanel, "RebarSpacing", RebarSpacing);
                    SetOldPanelProperty(reinPanelType, activePanel, "AnchorageLength", AnchorageLength);
                    SetOldPanelProperty(reinPanelType, activePanel, "DotSeparation", DotSeparation);
                    SetOldPanelProperty(reinPanelType, activePanel, "BendingLineMinLength", BendingLineMinLength);
                    SetOldPanelProperty(reinPanelType, activePanel, "AnchorageJoinLength", AnchorageJoinLength);
                    SetOldPanelProperty(reinPanelType, activePanel, "HookLength", HookLength);
                    SetOldPanelProperty(reinPanelType, activePanel, "ProtectionThickness", ProtectionThickness);
                    SetOldPanelProperty(reinPanelType, activePanel, "ReinforcementDiameter", ReinforcementDiameter);
                    SetOldPanelProperty(reinPanelType, activePanel, "DotReinOffset", DotReinOffset);
                    SetOldPanelProperty(reinPanelType, activePanel, "TextSize", TextSize);
                    SetOldPanelProperty(reinPanelType, activePanel, "TextXScale", TextXScale);
                    SetOldPanelProperty(reinPanelType, activePanel, "DimensionDistanceInside", DimensionDistanceInside);
                    SetOldPanelProperty(reinPanelType, activePanel, "DimensionDistanceOutside", DimensionDistanceOutside);
                    SetOldPanelProperty(reinPanelType, activePanel, "DimensionDistanceWithDim", DimensionDistanceWithDim);
                    SetOldPanelProperty(reinPanelType, activePanel, "MleaderDistance", MleaderDistance);
                    SetOldPanelProperty(reinPanelType, activePanel, "DimDistanceTolerance", DimDistanceTolerance);
                }
            }
            catch { /* 静默失败，不影响主流程 */ }
        }

        private static void SetOldPanelProperty(System.Type type, object instance, string name, double value)
        {
            var prop = type.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            prop?.SetValue(instance, value);
        }

        private void ResetToDefaults()
        {
            // Tab A
            Scale = 40.0;
            FontFileName = "tssdeng.shx";
            BigFontFileName = "hztxt.shx";
            TextSize = 2.5;
            TextXScale = 0.7;
            Dimtxt = 2.5; Dimexo = 1.0; Dimexe = 1.0; Dimdle = 0.5; Dimgap = 1.0; Dimasz = 1.0;
            DimArrowName = "_ARCHTICK";
            MLeaderArrowSize = 2.0; MLeaderArrowName = "_DotSmall"; MLeaderLandingGap = 0.5; MLeaderTextColorIndex = 7;

            // Tab B
            RebarDiameter = 14.0; RebarSpacing = 200.0;
            AnchorageLength = 500.0; DotSeparation = 200.0; BendingLineMinLength = 150.0;
            AnchorageJoinLength = 1500.0; HookLength = 1.0; ProtectionThickness = 1.0;
            ReinforcementDiameter = 0.35; DotReinOffset = 1.35;
            DimensionDistanceInside = 6.0; DimensionDistanceOutside = 14.0;
            DimensionDistanceWithDim = 6.0; MleaderDistance = 6.0; DimDistanceTolerance = 30.0;

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

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
