using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// 钢筋配置面板 ViewModel
    /// 管理钢筋相关的所有参数和命令
    /// </summary>
    public class ReinPanelViewModel : INotifyPropertyChanged
    {
        #region 字段

        private readonly Domain.Services.IReinService _reinService;

        #endregion

        #region 构造函数

        public ReinPanelViewModel(Domain.Services.IReinService reinService)
        {
            _reinService = reinService;

            // 初始化命令
            ApplyStyleCommand = new RelayCommand(ApplyStyle);
            ResetCommand = new RelayCommand(ResetToDefaults);
            DrawCommand = new RelayCommand(DrawReinforcement);
            Dim1Command = new RelayCommand(() => DimensionRein(1));
            Dim2Command = new RelayCommand(() => DimensionRein(2));
            Dim3Command = new RelayCommand(() => DimensionRein(3));
        }

        #endregion

        #region 基础参数属性

        private double _scale = 40.0;
        public double Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        private double _rebarDiameter = 12.0;
        public double RebarDiameter
        {
            get => _rebarDiameter;
            set => SetProperty(ref _rebarDiameter, value);
        }

        private double _rebarSpacing = 200.0;
        public double RebarSpacing
        {
            get => _rebarSpacing;
            set => SetProperty(ref _rebarSpacing, value);
        }

        #endregion

        #region 钢筋参数属性

        private double _anchorageLength = 500.0;
        public double AnchorageLength
        {
            get => _anchorageLength;
            set => SetProperty(ref _anchorageLength, value);
        }

        private double _dotSeparation = 200.0;
        public double DotSeparation
        {
            get => _dotSeparation;
            set => SetProperty(ref _dotSeparation, value);
        }

        private double _bendingLineMinLength = 150.0;
        public double BendingLineMinLength
        {
            get => _bendingLineMinLength;
            set => SetProperty(ref _bendingLineMinLength, value);
        }

        private double _anchorageJoinLength = 1500.0;
        public double AnchorageJoinLength
        {
            get => _anchorageJoinLength;
            set => SetProperty(ref _anchorageJoinLength, value);
        }

        private double _hookLength = 1.0;
        public double HookLength
        {
            get => _hookLength;
            set => SetProperty(ref _hookLength, value);
        }

        private double _protectionThickness = 1.0;
        public double ProtectionThickness
        {
            get => _protectionThickness;
            set => SetProperty(ref _protectionThickness, value);
        }

        private double _reinforcementDiameter = 0.35;
        public double ReinforcementDiameter
        {
            get => _reinforcementDiameter;
            set => SetProperty(ref _reinforcementDiameter, value);
        }

        private double _dotReinOffset = 1.35;
        public double DotReinOffset
        {
            get => _dotReinOffset;
            set => SetProperty(ref _dotReinOffset, value);
        }

        #endregion

        #region 尺寸参数属性

        private double _dimensionDistanceInside = 6.0;
        public double DimensionDistanceInside
        {
            get => _dimensionDistanceInside;
            set => SetProperty(ref _dimensionDistanceInside, value);
        }

        private double _dimensionDistanceOutside = 14.0;
        public double DimensionDistanceOutside
        {
            get => _dimensionDistanceOutside;
            set => SetProperty(ref _dimensionDistanceOutside, value);
        }

        private double _dimensionDistanceWithDim = 6.0;
        public double DimensionDistanceWithDim
        {
            get => _dimensionDistanceWithDim;
            set => SetProperty(ref _dimensionDistanceWithDim, value);
        }

        private double _mleaderDistance = 6.0;
        public double MleaderDistance
        {
            get => _mleaderDistance;
            set => SetProperty(ref _mleaderDistance, value);
        }

        private double _dimDistanceTolerance = 30.0;
        public double DimDistanceTolerance
        {
            get => _dimDistanceTolerance;
            set => SetProperty(ref _dimDistanceTolerance, value);
        }

        private double _textXScale = 0.7;
        public double TextXScale
        {
            get => _textXScale;
            set => SetProperty(ref _textXScale, value);
        }

        private double _textSize = 3.0;
        public double TextSize
        {
            get => _textSize;
            set => SetProperty(ref _textSize, value);
        }

        #endregion

        #region 命令

        public ICommand ApplyStyleCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand DrawCommand { get; }
        public ICommand Dim1Command { get; }
        public ICommand Dim2Command { get; }
        public ICommand Dim3Command { get; }

        #endregion

        #region 命令实现

        private void ApplyStyle()
        {
            try
            {
                var parameters = CreateParameters();
                _reinService.ApplyStyle(parameters);
                
                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\n样式设置成功。\n");
            }
            catch (System.Exception ex)
            {
                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n样式设置失败: {ex.Message}\n");
            }
        }

        private void ResetToDefaults()
        {
            Scale = 40.0;
            RebarDiameter = 12.0;
            RebarSpacing = 200.0;
            AnchorageLength = 500.0;
            DotSeparation = 200.0;
            BendingLineMinLength = 150.0;
            AnchorageJoinLength = 1500.0;
            HookLength = 1.0;
            ProtectionThickness = 1.0;
            ReinforcementDiameter = 0.35;
            DotReinOffset = 1.35;
            DimensionDistanceInside = 6.0;
            DimensionDistanceOutside = 14.0;
            DimensionDistanceWithDim = 6.0;
            MleaderDistance = 6.0;
            DimDistanceTolerance = 30.0;
            TextXScale = 0.7;
            TextSize = 3.0;

            var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n已恢复默认值。\n");
        }

        private void DrawReinforcement()
        {
            try
            {
                var parameters = CreateParameters();
                _reinService.DrawReinforcement(parameters);
                
                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage("\n钢筋绘制成功。\n");
            }
            catch (System.Exception ex)
            {
                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n钢筋绘制失败: {ex.Message}\n");
            }
        }

        private void DimensionRein(int mode)
        {
            try
            {
                var parameters = CreateParameters();
                _reinService.DimensionRein(mode, parameters);
                
                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n钢筋标注（模式{mode}）成功。\n");
            }
            catch (System.Exception ex)
            {
                var ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument?.Editor;
                ed?.WriteMessage($"\n钢筋标注失败: {ex.Message}\n");
            }
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 从当前属性创建参数对象
        /// </summary>
        private Domain.ValueObjects.ReinParameters CreateParameters()
        {
            return new Domain.ValueObjects.ReinParameters
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
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}

