using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Presentation.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Pile.ViewModels
{
    /// <summary>
    /// 桩基布置面板 ViewModel（自含式，不依赖旧项目服务）
    /// 参数自管理 + JSON 持久化，DrawPiles 通过 SendCommand → C1 路由到 AutoCAD 命令线程
    /// 参照 SettingsPanelViewModel 的持久化模式
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
                try
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
                catch
                {
                    return null;
                }
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
            ResetCommand = new RelayCommand(ResetToDefaults);
            DrawPilesCommand = new RelayCommand(
                () => SendCommand(() => new Features.Pile.DrawPilesCommand().Execute()));

            GroupCirclesByElevationCommand = new RelayCommand(
                () => SendCommand(() => new Features.Pile.GroupCirclesByElevationCommand().Execute()));
            MarkElevationAtCentroidsCommand = new RelayCommand(
                () => SendCommand(() => new Features.Pile.GroupCirclesByElevationCommand().ExecutePlaceElevationTextAtCentroids()));
            PileVoronoiOptimizationCommand = new RelayCommand(
                () => SendCommand(() => new Features.Pile.PileVoronoiOptimizationCommand().Execute()));
            PileVoronoiFromCirclesCommand = new RelayCommand(
                () => SendCommand(() => new Features.Pile.PileVoronoiOptimizationCommand().ExecuteWithExistingCircles()));

            // 从持久化文件加载上次保存的设置
            LoadSettings();
        }

        #endregion

        #region 只读属性：从设置面板同步

        /// <summary>
        /// 当前比例（只读，从设置面板读取，供面板显示用）
        /// </summary>
        public double CurrentScale => SettingsPanelViewModel.Current?.Scale ?? 40.0;

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

        #region 桩参数（默认值参考旧项目 PileConfig）

        private double _diameterOrEdge = 400.0;
        /// <summary>桩直径/边长 (mm)，旧默认 400</summary>
        public double DiameterOrEdge
        {
            get => _diameterOrEdge;
            set => SetProperty(ref _diameterOrEdge, value);
        }

        private double _minPileCenterDistance = 1200.0;
        /// <summary>最小桩中心距 (mm)，旧默认 1200</summary>
        public double MinPileCenterDistance
        {
            get => _minPileCenterDistance;
            set => SetProperty(ref _minPileCenterDistance, value);
        }

        private double _inputDisplacementRate = 0.02;
        /// <summary>输入置换率，旧默认 0.02</summary>
        public double InputDisplacementRate
        {
            get => _inputDisplacementRate;
            set => SetProperty(ref _inputDisplacementRate, value);
        }

        private double _pileArrangeRate = 0.5;
        /// <summary>桩布置比例，旧默认 0.5</summary>
        public double PileArrangeRate
        {
            get => _pileArrangeRate;
            set => SetProperty(ref _pileArrangeRate, value);
        }

        private double _inputDistanceFromContour = 400.0;
        /// <summary>整体轮廓距离 (mm)，旧默认 400</summary>
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
        /// <summary>上边距 (mm)，旧默认 400</summary>
        public double MarginUp
        {
            get => _marginUp;
            set => SetProperty(ref _marginUp, value);
        }

        private double _marginDown = 400.0;
        /// <summary>下边距 (mm)，旧默认 400</summary>
        public double MarginDown
        {
            get => _marginDown;
            set => SetProperty(ref _marginDown, value);
        }

        private double _marginLeft = 400.0;
        /// <summary>左边距 (mm)，旧默认 400</summary>
        public double MarginLeft
        {
            get => _marginLeft;
            set => SetProperty(ref _marginLeft, value);
        }

        private double _marginRight = 400.0;
        /// <summary>右边距 (mm)，旧默认 400</summary>
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

        private double _markerElevation;
        /// <summary>批量写入形心文字使用的标高值</summary>
        public double MarkerElevation
        {
            get => _markerElevation;
            set => SetProperty(ref _markerElevation, value);
        }

        private double _markerTextHeightScale = 1.0;
        /// <summary>文字高度倍率（最终高度 = 倍率 * 当前比例）</summary>
        public double MarkerTextHeightScale
        {
            get => _markerTextHeightScale;
            set => SetProperty(ref _markerTextHeightScale, value);
        }

        #endregion

        #region 命令

        public ICommand ResetCommand { get; }
        public ICommand DrawPilesCommand { get; }

        public ICommand GroupCirclesByElevationCommand { get; }
        public ICommand MarkElevationAtCentroidsCommand { get; }
        public ICommand PileVoronoiOptimizationCommand { get; }
        public ICommand PileVoronoiFromCirclesCommand { get; }

        #endregion

        #region 命令路由

        /// <summary>
        /// 通过 C1 路由到 AutoCAD 命令线程（Refactored 命令用）
        /// 复用 SettingsPanelViewModel 的 PendingCommand 机制
        /// 执行前自动确保样式已应用
        /// </summary>
        private void SendCommand(Action commandAction)
        {
            SaveSettings();
            SettingsPanelViewModel.PendingCommand = () =>
            {
                // 确保设置面板样式已应用
                var settingsVm = SettingsPanelViewModel.Current;
                settingsVm?.EnsureStylesApplied();
                commandAction();
            };
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    StatusMessage = "无活动文档";
                    SettingsPanelViewModel.PendingCommand = null;
                    return;
                }
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

        private void ResetToDefaults()
        {
            _isLoading = true;
            try
            {
                // 参考旧项目 PileConfig 默认值
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
                MarkerElevation = 0.0;
                MarkerTextHeightScale = 1.0;
                StatusMessage = "已恢复默认值";
            }
            finally
            {
                _isLoading = false;
            }
            SaveSettings();
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

        #region 持久化：hy-pile-settings.json（%APPDATA%\HyCADTool）

        private static string _settingsFilePath;

        /// <summary>
        /// 获取桩基设置文件路径
        /// </summary>
        private static string GetSettingsFilePath()
        {
            if (_settingsFilePath != null) return _settingsFilePath;
            var appDataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HyCADTool");
            Directory.CreateDirectory(appDataDir);
            _settingsFilePath = Path.Combine(appDataDir, "hy-pile-settings.json");
            return _settingsFilePath;
        }

        /// <summary>
        /// 保存当前桩基参数到 JSON 文件
        /// </summary>
        public void SaveSettings()
        {
            if (_isLoading) return;
            try
            {
                var data = new PileSettingsData
                {
                    DiameterOrEdge = DiameterOrEdge,
                    MinPileCenterDistance = MinPileCenterDistance,
                    InputDisplacementRate = InputDisplacementRate,
                    PileArrangeRate = PileArrangeRate,
                    InputDistanceFromContour = InputDistanceFromContour,
                    MarginUp = MarginUp,
                    MarginDown = MarginDown,
                    MarginLeft = MarginLeft,
                    MarginRight = MarginRight,
                    NX = NX,
                    NY = NY,
                    IsRectangular = IsRectangular,
                    IsCircular = IsCircular,
                    IsCirclePile = IsCirclePile,
                    IsRectPile = IsRectPile,
                    ManualControl = ManualControl,
                    MarkerElevation = MarkerElevation,
                    MarkerTextHeightScale = MarkerTextHeightScale
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(GetSettingsFilePath(), json);
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存桩基设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从 JSON 文件加载桩基设置
        /// 文件不存在或格式错误时静默使用默认值
        /// </summary>
        public void LoadSettings()
        {
            _isLoading = true;
            try
            {
                var path = GetSettingsFilePath();
                if (!File.Exists(path)) { _isLoading = false; return; }

                var json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<PileSettingsData>(json);
                if (data == null) { _isLoading = false; return; }

                DiameterOrEdge = data.DiameterOrEdge;
                MinPileCenterDistance = data.MinPileCenterDistance;
                InputDisplacementRate = data.InputDisplacementRate;
                PileArrangeRate = data.PileArrangeRate;
                InputDistanceFromContour = data.InputDistanceFromContour;
                MarginUp = data.MarginUp;
                MarginDown = data.MarginDown;
                MarginLeft = data.MarginLeft;
                MarginRight = data.MarginRight;
                NX = data.NX;
                NY = data.NY;
                IsRectangular = data.IsRectangular;
                IsCircular = data.IsCircular;
                IsCirclePile = data.IsCirclePile;
                IsRectPile = data.IsRectPile;
                ManualControl = data.ManualControl;
                MarkerElevation = data.MarkerElevation;
                MarkerTextHeightScale = data.MarkerTextHeightScale;
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载桩基设置失败: {ex.Message}（使用默认值）");
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// 序列化 DTO — 纯数据容器，字段默认值与旧项目 PileConfig 一致
        /// </summary>
        private class PileSettingsData
        {
            public double DiameterOrEdge { get; set; } = 400.0;
            public double MinPileCenterDistance { get; set; } = 1200.0;
            public double InputDisplacementRate { get; set; } = 0.02;
            public double PileArrangeRate { get; set; } = 0.5;
            public double InputDistanceFromContour { get; set; } = 400.0;
            public double MarginUp { get; set; } = 400.0;
            public double MarginDown { get; set; } = 400.0;
            public double MarginLeft { get; set; } = 400.0;
            public double MarginRight { get; set; } = 400.0;
            public int NX { get; set; } = 2;
            public int NY { get; set; } = 2;
            public bool IsRectangular { get; set; } = true;
            public bool IsCircular { get; set; }
            public bool IsCirclePile { get; set; } = true;
            public bool IsRectPile { get; set; }
            public bool ManualControl { get; set; }
            public double MarkerElevation { get; set; }
            public double MarkerTextHeightScale { get; set; } = 1.0;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 加载中标记：防止 LoadSettings → 属性 setter → SaveSettings 循环
        /// </summary>
        private bool _isLoading;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);

            // 非加载期间，参数变更即时写入文件
            if (!_isLoading && propertyName != nameof(StatusMessage))
                SaveSettings();

            return true;
        }

        #endregion
    }
}
