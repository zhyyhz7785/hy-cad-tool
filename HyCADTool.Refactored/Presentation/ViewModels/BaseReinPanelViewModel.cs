using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Models.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using static HyCADTool.BaseRein;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// BaseReinPanel 的 ViewModel
    /// 管理所有 UI 状态、命令和业务逻辑调用
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

            // 初始化命令
            ApplyStyleCommand = new RelayCommand(ExecuteApplyStyle);
            ResetCommand = new RelayCommand(ExecuteReset);
            StepOneCommand = new RelayCommand(ExecuteStepOne);
            StepTwoCommand = new RelayCommand(ExecuteStepTwo);
            StepFourCommand = new RelayCommand(ExecuteStepFour);
            StepFiveCommand = new RelayCommand(ExecuteStepFive);
            StepSixCommand = new RelayCommand(ExecuteStepSix);
        }

        #endregion

        #region 属性 - 使用简化的 setter 模式

        public double Scale
        {
            get => _config.Scale;
            set { if (_config.Scale != value) { _config.Scale = value; OnPropertyChanged(); SaveConfigToService(); } }
        }

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

        public ICommand ApplyStyleCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand StepOneCommand { get; }
        public ICommand StepTwoCommand { get; }
        public ICommand StepFourCommand { get; }
        public ICommand StepFiveCommand { get; }
        public ICommand StepSixCommand { get; }

        #endregion

        #region 命令执行方法

        private void ExecuteApplyStyle()
        {
            try
            {
                _reinforcementService.ApplyStyles(Scale);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"应用样式失败: {ex.Message}");
            }
        }

        private void ExecuteReset()
        {
            try
            {
                _config = _reinforcementService.ResetToDefault();
                OnPropertyChanged(string.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"重置失败: {ex.Message}");
            }
        }

        private void ExecuteStepOne()
        {
            try
            {
                _reinforcementService.OptimizeBasemap();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"整理底图失败: {ex.Message}");
            }
        }

        private void ExecuteStepTwo()
        {
            try
            {
                char[] delimiters = new char[] { ' ', ',', '，' };
                List<string> fixedValues = FilterValues
                    .Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                    .ToList();

                _reinforcementService.SelectAndDeleteUnusedText(fixedValues);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"选择删除失败: {ex.Message}");
            }
        }

        private void ExecuteStepFour()
        {
            try
            {
                SaveConfigToService();
                _reinforcementService.GenerateReinforcementArea(_config);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"生成配筋面积失败: {ex.Message}");
            }
        }

        private void ExecuteStepFive()
        {
            try
            {
                SaveConfigToService();
                _reinforcementService.DrawReinforcement(_config, DimAll);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"绘制钢筋失败: {ex.Message}");
            }
        }

        private void ExecuteStepSix()
        {
            try
            {
                _reinforcementService.DimensionReinforcementArea(SelectedDimDirection);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"标注配筋区域失败: {ex.Message}");
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
