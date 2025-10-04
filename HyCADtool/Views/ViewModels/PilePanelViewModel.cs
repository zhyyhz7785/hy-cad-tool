using HyCADTool.Config;
using HyCADTool.HelpClass;
using HyCADTool.Interfaces;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
namespace HyCADTool.ViewModels
{
    public class PilePanelViewModel : INotifyPropertyChanged
    {
        private readonly ICadService _cadService;
        private readonly IAreaFactory _areaFactory;
        private readonly IConfigService _configService;
        #region 属性
        private double _scale = 40.0;
        private double _diameterOrEdge = 400.0;
        private double _minPileCenterDistance = 1200.0;
        private double _inputDisplacementRate = 0.02;
        private double _pileArrangeRate = 0.5;
        private double _inputDistanceFromContour = 400.0;
        private double _marginUp = 400.0;
        private double _marginDown = 400.0;
        private double _marginLeft = 400.0;
        private double _marginRight = 400.0;
        private int _NX = 2;
        private int _NY = 2;
        private bool _isRectangular = true;
        private bool _isCirclePile = true;
        private bool _isCircular;
        private bool _isRectPile;
        private bool _manualControl;
        public double Scale
        {
            get => _scale;
            set { _scale = value; _configService.Scale = value; OnPropertyChanged(); }
        }
        public double DiameterOrEdge
        {
            get => _diameterOrEdge;
            set { _diameterOrEdge = value; _configService.DiameterOrEdge = value; OnPropertyChanged(); }
        }
        public double MinPileCenterDistance
        {
            get => _minPileCenterDistance;
            set { _minPileCenterDistance = value; _configService.MinPileCenterDistance = value; OnPropertyChanged(); }
        }
        public double InputDisplacementRate
        {
            get => _inputDisplacementRate;
            set { _inputDisplacementRate = value; _configService.InputDisplacementRate = value; OnPropertyChanged(); }
        }
        public double PileArrangeRate
        {
            get => _pileArrangeRate;
            set { _pileArrangeRate = value; _configService.PileArrangeRate = value; OnPropertyChanged(); }
        }
        public double InputDistanceFromContour
        {
            get => _inputDistanceFromContour;
            set
            {
                _inputDistanceFromContour = value;
                _configService.InputDistanceFromContour = value;
                SyncMargins(value);
                OnPropertyChanged();
            }
        }
        public double MarginUp
        {
            get => _marginUp;
            set { _marginUp = value; UpdateMargin(); OnPropertyChanged(); }
        }
        public double MarginDown
        {
            get => _marginDown;
            set { _marginDown = value; UpdateMargin(); OnPropertyChanged(); }
        }
        public double MarginLeft
        {
            get => _marginLeft;
            set { _marginLeft = value; UpdateMargin(); OnPropertyChanged(); }
        }
        public double MarginRight
        {
            get => _marginRight;
            set { _marginRight = value; UpdateMargin(); OnPropertyChanged(); }
        }
        public int NX
        {
            get => _NX;
            set { _NX = (int)value; }
        }
        public int NY
        {
            get => _NY;
            set { _NY = (int)value; }
        }
        public bool ManualControl
        {
            get { return _manualControl; }
            set { _manualControl = value; OnPropertyChanged(); }
        }
        public bool IsRectangular
        {
            get { return _isRectangular; }
            set
            {
                if (_isRectangular != value)
                {
                    _isRectangular = value;
                    OnPropertyChanged(nameof(IsRectangular));
                    if (value)
                    {
                        IsCircular = false;
                    }
                }
            }
        }
        public bool IsCircular
        {
            get { return _isCircular; }
            set
            {
                if (_isCircular != value)
                {
                    _isCircular = value;
                    OnPropertyChanged(nameof(IsCircular));
                    if (value)
                    {
                        IsRectangular = false;
                    }
                }
            }
        }
        public bool IsCirclePile
        {
            get { return _isCirclePile; }
            set
            {
                if (_isCirclePile != value)
                {
                    _isCirclePile = value;
                    OnPropertyChanged(nameof(IsCirclePile));
                    if (value)
                    {
                        IsRectPile = false;
                    }
                }
            }
        }
        public bool IsRectPile
        {
            get { return _isRectPile; }
            set
            {
                if (_isRectPile != value)
                {
                    _isRectPile = value;
                    OnPropertyChanged(nameof(IsRectPile));
                    if (value)
                    {
                        IsCirclePile = false;
                    }
                }
            }
        }
        #endregion
        #region 命令
        public ICommand ApplyCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand DrawPilesCommand { get; }
        #endregion
        public PilePanelViewModel(ICadService cadService, IAreaFactory areaFactory, IConfigService configService)
        {
            _cadService = cadService;
            _areaFactory = areaFactory;
            _configService = configService;
            ApplyCommand = new RelayCommand(Apply);
            ResetCommand = new RelayCommand(ResetToDefaultValues);
            DrawPilesCommand = new RelayCommand(DrawPiles, CanDrawPiles);
        }
        #region 逻辑方法
        private void Apply()
        {
            BaseConfig.Scale = Scale;
            BaseConfig.InitializeStyle();
            _cadService.WriteMessage("\nStyles applied successfully.\n");
        }
        private void DrawPiles()
        {
            try
            {
                var polyline = _cadService.SelectPolyline();
                if (polyline == null)
                {
                    _cadService.WriteMessage("\nPolyline is null\n");
                    return;
                }
                var sectionType = IsCirclePile ? PileSectionType.Circle : PileSectionType.Square;
                var area = IsRectangular
                                    ? _areaFactory.CreateRectangularArea(polyline, sectionType, DiameterOrEdge, Scale)
                                    : _areaFactory.CreateCircularArea(polyline, sectionType, DiameterOrEdge, Scale);
                if (ManualControl)
                {
                    area.AdjustGridSize(NX, NY);
                }
                area.DrawInCAD();
            }
            catch (Exception ex)
            {
                _cadService.WriteMessage($"\nError in DrawPiles: {ex.Message}\n");
            }
        }
        private bool CanDrawPiles() => Scale > 0 && DiameterOrEdge > 0;
        //private void ResetToDefaultValues()
        //{
        //    Scale = 40.0;
        //    DiameterOrEdge = 400.0;
        //    MinPileCenterDistance = 1200.0;
        //    InputDisplacementRate = 0.02;
        //    PileArrangeRate = 0.5;
        //    InputDistanceFromContour = 400.0;
        //    MarginUp = MarginDown = MarginLeft = MarginRight = 400.0;
        //    IsRectangular = true;
        //    IsCirclePile = true;
        //}
        private void ResetToDefaultValues()
        {
            // 从 CSV 中导入配置
            ConfigManager.ImportConfigFromCsv("Pile");

            // 同步 PileConfig.Instance 中的值到 ViewModel
            var config = PileConfig.Instance;

            Scale = BaseConfig.Scale;
            DiameterOrEdge = config.DiameterOrEdge;
            MinPileCenterDistance = config.MinPileCenterDistance;
            InputDisplacementRate = config.InputDisplacementRate;
            PileArrangeRate = config.PileArrangeRate;
            InputDistanceFromContour = config.InputDistanceFromContour;

            MarginUp = config.Margin.up;
            MarginDown = config.Margin.down;
            MarginLeft = config.Margin.left;
            MarginRight = config.Margin.right;

            IsRectangular = config.ArrangementType == PileArrangementType.Rectangle;
            IsCircular = config.ArrangementType == PileArrangementType.Circular;

            IsCirclePile = config.Section == PileSectionType.Circle;
            IsRectPile = config.Section == PileSectionType.Square;
        }

        private void SyncMargins(double value)
        {
            MarginUp = MarginDown = MarginLeft = MarginRight = value;
            OnPropertyChanged(nameof(MarginUp));
            OnPropertyChanged(nameof(MarginDown));
            OnPropertyChanged(nameof(MarginLeft));
            OnPropertyChanged(nameof(MarginRight));
        }
        private void UpdateMargin()
        {
            _configService.Margin = (MarginUp, MarginDown, MarginLeft, MarginRight);
        }
        #endregion
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    // 命令实现
    //public class RelayCommand : ICommand
    //{
    //    private readonly Action _execute;
    //    private readonly Func<bool> _canExecute;
    //    public RelayCommand(Action execute, Func<bool> canExecute = null)
    //    {
    //        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    //        _canExecute = canExecute;
    //    }
    //    public bool CanExecute(object parameter) => _canExecute == null || _canExecute();
    //    public void Execute(object parameter) => _execute();
    //    public event EventHandler CanExecuteChanged
    //    {
    //        add { CommandManager.RequerySuggested += value; }
    //        remove { CommandManager.RequerySuggested -= value; }
    //    }
    //}
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (() => true); // 默认返回 true
        }

        public bool CanExecute(object parameter) => _canExecute();

        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged
        {
            //add { CommandManager.RequerySuggested += value; }
            //remove { CommandManager.RequerySuggested -= value; }
            add { } // 不注册
            remove { }
        }
    }

}