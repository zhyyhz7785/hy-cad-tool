using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// 桩基布置面板 ViewModel（自含式，不依赖旧项目服务）
    /// 参数自管理，DrawPiles 通过 SendCommand → C1 路由到 AutoCAD 命令线程
    /// 旧命令（GroupCircles / VoronoiPile）通过 SendStringToExecute 调用已注册的 [CommandMethod]
    /// </summary>
    public class PilePanelViewModel : INotifyPropertyChanged
    {
        #region 多文档支持

        private static readonly Dictionary<string, PilePanelViewModel> _documentViewModels
            = new Dictionary<string, PilePanelViewModel>();

        /// <summary>
        /// 当前活动文档的 ViewModel（供外部读取参数）
        /// </summary>
        public static PilePanelViewModel Current
        {
            get
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null) return null;

                var docName = doc.Name;
                if (!_documentViewModels.ContainsKey(docName))
                {
                    _documentViewModels[docName] = new PilePanelViewModel();
                }
                return _documentViewModels[docName];
            }
        }

        /// <summary>
        /// 获取或创建指定文档的 ViewModel
        /// </summary>
        public static PilePanelViewModel GetOrCreate(string documentName)
        {
            if (!_documentViewModels.ContainsKey(documentName))
            {
                _documentViewModels[documentName] = new PilePanelViewModel();
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

        #endregion

        #region 构造函数

        public PilePanelViewModel()
        {
            ApplyCommand = new RelayCommand(Apply);
            ResetCommand = new RelayCommand(ResetToDefaults);
            DrawPilesCommand = new RelayCommand(
                () => SendCommand(() => new Commands.DrawPilesCommand().Execute()),
                CanDrawPiles);

            GroupCirclesByElevationCommand = new RelayCommand(
                () => SendCommand(() => new Commands.GroupCirclesByElevationCommand().Execute()));
            PileVoronoiOptimizationCommand = new RelayCommand(
                () => SendCommand(() => new Commands.PileVoronoiOptimizationCommand().Execute()));
            PileVoronoiFromCirclesCommand = new RelayCommand(
                () => SendCommand(() => new Commands.PileVoronoiOptimizationCommand().ExecuteWithExistingCircles()));

        }

        #endregion

        #region 基础参数

        private double _scale = 40.0;
        public double Scale
        {
            get => _scale;
            set => SetProperty(ref _scale, value);
        }

        #endregion

        #region 布置选项

        private bool _isRectangular = true;
        public bool IsRectangular
        {
            get => _isRectangular;
            set
            {
                if (SetProperty(ref _isRectangular, value) && value)
                    IsCircular = false;
            }
        }

        private bool _isCircular;
        public bool IsCircular
        {
            get => _isCircular;
            set
            {
                if (SetProperty(ref _isCircular, value) && value)
                    IsRectangular = false;
            }
        }

        private bool _isCirclePile = true;
        public bool IsCirclePile
        {
            get => _isCirclePile;
            set
            {
                if (SetProperty(ref _isCirclePile, value) && value)
                    IsRectPile = false;
            }
        }

        private bool _isRectPile;
        public bool IsRectPile
        {
            get => _isRectPile;
            set
            {
                if (SetProperty(ref _isRectPile, value) && value)
                    IsCirclePile = false;
            }
        }

        private bool _manualControl;
        public bool ManualControl
        {
            get => _manualControl;
            set => SetProperty(ref _manualControl, value);
        }

        private int _ny = 2;
        public int NY
        {
            get => _ny;
            set => SetProperty(ref _ny, value);
        }

        private int _nx = 2;
        public int NX
        {
            get => _nx;
            set => SetProperty(ref _nx, value);
        }

        #endregion

        #region 桩参数

        private double _diameterOrEdge = 400.0;
        public double DiameterOrEdge
        {
            get => _diameterOrEdge;
            set => SetProperty(ref _diameterOrEdge, value);
        }

        private double _minPileCenterDistance = 1200.0;
        public double MinPileCenterDistance
        {
            get => _minPileCenterDistance;
            set => SetProperty(ref _minPileCenterDistance, value);
        }

        private double _inputDisplacementRate = 0.02;
        public double InputDisplacementRate
        {
            get => _inputDisplacementRate;
            set => SetProperty(ref _inputDisplacementRate, value);
        }

        private double _pileArrangeRate = 0.5;
        public double PileArrangeRate
        {
            get => _pileArrangeRate;
            set => SetProperty(ref _pileArrangeRate, value);
        }

        private double _inputDistanceFromContour = 400.0;
        public double InputDistanceFromContour
        {
            get => _inputDistanceFromContour;
            set
            {
                if (SetProperty(ref _inputDistanceFromContour, value))
                    SyncMargins(value);
            }
        }

        private double _marginUp = 400.0;
        public double MarginUp
        {
            get => _marginUp;
            set => SetProperty(ref _marginUp, value);
        }

        private double _marginDown = 400.0;
        public double MarginDown
        {
            get => _marginDown;
            set => SetProperty(ref _marginDown, value);
        }

        private double _marginLeft = 400.0;
        public double MarginLeft
        {
            get => _marginLeft;
            set => SetProperty(ref _marginLeft, value);
        }

        private double _marginRight = 400.0;
        public double MarginRight
        {
            get => _marginRight;
            set => SetProperty(ref _marginRight, value);
        }

        #endregion

        #region 状态

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region 命令

        public ICommand ApplyCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand DrawPilesCommand { get; }

        public ICommand GroupCirclesByElevationCommand { get; }
        public ICommand PileVoronoiOptimizationCommand { get; }
        public ICommand PileVoronoiFromCirclesCommand { get; }

        #endregion

        #region 命令路由

        /// <summary>
        /// 通过 C1 路由到 AutoCAD 命令线程（Refactored 命令用）
        /// 复用 SettingsPanelViewModel 的 PendingCommand 机制
        /// </summary>
        private void SendCommand(Action commandAction)
        {
            SettingsPanelViewModel.PendingCommand = commandAction;
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                doc.SendStringToExecute("C1\n", true, false, false);
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"发送命令失败: {ex.Message}";
                SettingsPanelViewModel.PendingCommand = null;
            }
        }


        #endregion

        #region 命令实现

        private void Apply()
        {
            try
            {
                // 同步 Scale 到 SettingsPanel（共享样式）
                var settingsVm = SettingsPanelViewModel.Current;
                if (settingsVm != null)
                {
                    settingsVm.Scale = Scale;
                    settingsVm.EnsureStylesApplied();
                }
                StatusMessage = $"样式应用成功 (Scale={Scale})";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"应用失败: {ex.Message}";
            }
        }

        private bool CanDrawPiles() => Scale > 0 && DiameterOrEdge > 0;

        private void ResetToDefaults()
        {
            Scale = 40.0;
            DiameterOrEdge = 400.0;
            MinPileCenterDistance = 1200.0;
            InputDisplacementRate = 0.02;
            PileArrangeRate = 0.5;
            InputDistanceFromContour = 400.0;
            MarginUp = 400.0;
            MarginDown = 400.0;
            MarginLeft = 400.0;
            MarginRight = 400.0;
            NX = 2;
            NY = 2;
            IsRectangular = true;
            IsCirclePile = true;
            ManualControl = false;
            StatusMessage = "已恢复默认值";
        }

        private void SyncMargins(double value)
        {
            _marginUp = value;
            _marginDown = value;
            _marginLeft = value;
            _marginRight = value;
            OnPropertyChanged(nameof(MarginUp));
            OnPropertyChanged(nameof(MarginDown));
            OnPropertyChanged(nameof(MarginLeft));
            OnPropertyChanged(nameof(MarginRight));
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
