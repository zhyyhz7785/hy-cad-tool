using HyCADTool.Refactored.Domain.Enums;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Models.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// BaseReinPanel 的 ViewModel
    /// 管理所有 UI 状态、命令和业务逻辑调用
    /// 注意：涉及 AutoCAD 交互（选择/绘制）的步骤必须通过 SendCommand → C1 路由到命令线程
    /// </summary>
    public class BaseReinPanelViewModel : INotifyPropertyChanged
    {
        private readonly IBaseReinforcementService _reinforcementService;
        private BaseReinforcementConfig _config;

        #region 构造函数

        public BaseReinPanelViewModel(IBaseReinforcementService reinforcementService)
        {
            _reinforcementService = reinforcementService ?? throw new ArgumentNullException(nameof(reinforcementService));
            
            // 加载配置
            _config = _reinforcementService.GetCurrentConfig();

            // 步骤命令涉及 AutoCAD 交互，必须路由到命令线程
            StepOneCommand = new RelayCommand(() => SendCommand(() => _reinforcementService.OptimizeBasemap()));
            StepTwoCommand = new RelayCommand(() => SendCommand(() =>
            {
                char[] delimiters = new char[] { ' ', ',', '，' };
                var fixedValues = FilterValues
                    .Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();
                _reinforcementService.SelectAndDeleteUnusedText(fixedValues);
            }));
            StepFourCommand = new RelayCommand(() => SendCommand(() =>
            {
                SaveConfigToService();
                _reinforcementService.GenerateReinforcementArea(_config);
            }));
            StepFiveCommand = new RelayCommand(() => SendCommand(() =>
            {
                SaveConfigToService();
                _reinforcementService.DrawReinforcement(_config, DimAll);
            }));
            StepSixCommand = new RelayCommand(() => SendCommand(() =>
                _reinforcementService.DimensionReinforcementArea(SelectedDimDirection)));
        }

        #endregion

        #region 属性 - 使用简化的 setter 模式

        public double PlateThickness
        {
            get => _config.PlateThickness;
            set { if (_config.PlateThickness != value) { _config.PlateThickness = value; OnPropertyChanged(); } }
        }

        public double RebarDiameter
        {
            get => _config.RebarDiameter;
            set { if (_config.RebarDiameter != value) { _config.RebarDiameter = value; OnPropertyChanged(); } }
        }

        public double RebarSpacing
        {
            get => _config.RebarSpacing;
            set { if (_config.RebarSpacing != value) { _config.RebarSpacing = value; OnPropertyChanged(); } }
        }

        public double MinAdditionalDiameter
        {
            get => _config.MinAdditionalDiameter;
            set { if (_config.MinAdditionalDiameter != value) { _config.MinAdditionalDiameter = value; OnPropertyChanged(); } }
        }

        public double AdditionalSpacing
        {
            get => _config.AdditionalSpacing;
            set { if (_config.AdditionalSpacing != value) { _config.AdditionalSpacing = value; OnPropertyChanged(); } }
        }

        public double ReinforceSafety
        {
            get => _config.ReinforceSafety;
            set { if (_config.ReinforceSafety != value) { _config.ReinforceSafety = value; OnPropertyChanged(); } }
        }

        public double ReinforceTextDistanceX
        {
            get => _config.ReinforceTextDistanceX;
            set { if (_config.ReinforceTextDistanceX != value) { _config.ReinforceTextDistanceX = value; OnPropertyChanged(); } }
        }

        public double ReinforceTextDistanceY
        {
            get => _config.ReinforceTextDistanceY;
            set { if (_config.ReinforceTextDistanceY != value) { _config.ReinforceTextDistanceY = value; OnPropertyChanged(); } }
        }

        public double AnchorFactor
        {
            get => _config.AnchorFactor;
            set { if (_config.AnchorFactor != value) { _config.AnchorFactor = value; OnPropertyChanged(); } }
        }

        public double ProximityThreshold
        {
            get => _config.ProximityThreshold;
            set { if (_config.ProximityThreshold != value) { _config.ProximityThreshold = value; OnPropertyChanged(); } }
        }

        public double ReinforceDistance
        {
            get => _config.ReinforceDistance;
            set { if (_config.ReinforceDistance != value) { _config.ReinforceDistance = value; OnPropertyChanged(); } }
        }

        public double TextToLineDistance
        {
            get => _config.TextToLineDistance;
            set { if (_config.TextToLineDistance != value) { _config.TextToLineDistance = value; OnPropertyChanged(); } }
        }

        public double HookLength
        {
            get => _config.HookLength;
            set { if (_config.HookLength != value) { _config.HookLength = value; OnPropertyChanged(); } }
        }

        public double PolylineWidth
        {
            get => _config.PolylineWidth;
            set { if (_config.PolylineWidth != value) { _config.PolylineWidth = value; OnPropertyChanged(); } }
        }

        public double AxisExtend
        {
            get => _config.AxisExtend;
            set { if (_config.AxisExtend != value) { _config.AxisExtend = value; OnPropertyChanged(); } }
        }

        public double DimensionDistanceWithDim
        {
            get => _config.DimensionDistanceWithDim;
            set { if (_config.DimensionDistanceWithDim != value) { _config.DimensionDistanceWithDim = value; OnPropertyChanged(); } }
        }

        public double Interval
        {
            get => _config.Interval;
            set { if (_config.Interval != value) { _config.Interval = value; OnPropertyChanged(); } }
        }

        public bool AddAnchorLength
        {
            get => _config.AddAnchorLength;
            set { if (_config.AddAnchorLength != value) { _config.AddAnchorLength = value; OnPropertyChanged(); } }
        }

        public bool DimAll
        {
            get => _config.DimAll;
            set { if (_config.DimAll != value) { _config.DimAll = value; OnPropertyChanged(); } }
        }

        public bool ExistingRebar
        {
            get => _config.ExistingRebar;
            set
            {
                if (_config.ExistingRebar != value)
                {
                    _config.ExistingRebar = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsRebarInputEnabled));
                }
            }
        }

        public bool IsRebarInputEnabled => ExistingRebar;

        public RebarDirection SelectedDirection
        {
            get => _config.Direction;
            set { if (_config.Direction != value) { _config.Direction = value; OnPropertyChanged(); } }
        }

        public IntersectionsDirection SelectedDimDirection
        {
            get => _config.DimDirection;
            set { if (_config.DimDirection != value) { _config.DimDirection = value; OnPropertyChanged(); } }
        }

        public string FilterValues
        {
            get => _config.FilterValues;
            set { if (_config.FilterValues != value) { _config.FilterValues = value; OnPropertyChanged(); } }
        }

        #endregion

        #region 命令

        public ICommand StepOneCommand { get; }
        public ICommand StepTwoCommand { get; }
        public ICommand StepFourCommand { get; }
        public ICommand StepFiveCommand { get; }
        public ICommand StepSixCommand { get; }

        #endregion

        #region 命令路由

        /// <summary>
        /// 通过 C1 路由到 AutoCAD 命令线程
        /// 复用 SettingsPanelViewModel 的 PendingCommand 机制
        /// 执行前自动从设置面板同步 Scale 并确保样式已应用
        /// </summary>
        private void SendCommand(Action commandAction)
        {
            SettingsPanelViewModel.PendingCommand = () =>
            {
                // 从设置面板同步 Scale，确保样式已应用
                var settingsVm = SettingsPanelViewModel.Current;
                if (settingsVm != null)
                {
                    _config.Scale = settingsVm.Scale;
                    settingsVm.EnsureStylesApplied();
                }
                commandAction();
            };
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                doc.SendStringToExecute("C1\n", true, false, false);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"发送命令失败: {ex.Message}");
                SettingsPanelViewModel.PendingCommand = null;
            }
        }

        #endregion

        #region 辅助方法

        private void SaveConfigToService()
        {
            _reinforcementService.SaveConfig(_config);
        }

        #endregion

        #region INotifyPropertyChanged 实现

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}


