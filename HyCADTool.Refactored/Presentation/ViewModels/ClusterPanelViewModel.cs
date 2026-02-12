using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Models.Configuration;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Cluster;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// 聚类分析面板 ViewModel
    /// 已从旧项目完全解耦，所有属性本地化
    /// </summary>
    public class ClusterPanelViewModel : INotifyPropertyChanged
    {
        #region 静态实例

        /// <summary>
        /// 当前活动实例，供外部命令读取配置
        /// </summary>
        public static ClusterPanelViewModel Current { get; private set; }

        #endregion

        #region 命令

        public ICommand ExecuteDrawCommand { get; }
        public ICommand LoadPreset1Command { get; }
        public ICommand LoadPreset2Command { get; }
        public ICommand LoadPreset3Command { get; }

        #endregion

        #region 预设方案

        private int _selectedPreset = 1;
        public int SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset != value)
                {
                    _selectedPreset = value;
                    OnPropertyChanged();
                    ApplyPreset(_selectedPreset);
                }
            }
        }

        private void ApplyPreset(int presetId)
        {
            switch (presetId)
            {
                case 1:
                    ClusterConfigX.EpsilonX = 6000;
                    ClusterConfigX.EpsilonY = 300;
                    ClusterConfigY.EpsilonX = 300;
                    ClusterConfigY.EpsilonY = 6000;
                    DistanceThreshold = 6000;
                    MinPoints = 1;
                    ExpandMarginLeft = ExpandMarginRight = ExpandMarginTop = ExpandMarginBottom = 300;

                    DrawClusterX = true;
                    DrawClusterY = false;
                    DrawClusterEnvelopePolyline = false;
                    DrawClusterExpandedEnvelope = false;
                    DrawClusterHull = false;
                    DrawClusterPts = false;

                    DrawDimX = true;
                    DrawDimY = true;

                    DrawBPs = false;
                    DrawAAPs = false;
                    DrawBAPs = false;
                    DrawABs = false;
                    DrawSteelPlatePs = false;

                    IncludeBPs = true;
                    IncludeAAPs = true;
                    IncludeBAPs = true;
                    IncludeABs = false;
                    IncludeSteelPlatePs = false;
                    break;

                case 2:
                    ClusterConfigX.EpsilonX = 6000;
                    ClusterConfigX.EpsilonY = 300;
                    ClusterConfigY.EpsilonX = 300;
                    ClusterConfigY.EpsilonY = 6000;
                    DistanceThreshold = 6000;
                    MinPoints = 1;
                    ExpandMarginLeft = ExpandMarginRight = ExpandMarginTop = ExpandMarginBottom = 300;

                    DrawClusterX = true;
                    DrawClusterY = false;
                    DrawClusterEnvelopePolyline = false;
                    DrawClusterExpandedEnvelope = false;
                    DrawClusterHull = false;
                    DrawClusterPts = false;

                    DrawDimX = true;
                    DrawDimY = true;

                    DrawBPs = false;
                    DrawAAPs = false;
                    DrawBAPs = false;
                    DrawABs = false;
                    DrawSteelPlatePs = false;

                    IncludeBPs = false;
                    IncludeAAPs = true;
                    IncludeBAPs = false;
                    IncludeABs = true;
                    IncludeSteelPlatePs = true;
                    break;

                case 3:
                    ClusterConfigX.EpsilonX = 1500;
                    ClusterConfigX.EpsilonY = 1500;
                    ClusterConfigY.EpsilonX = 1500;
                    ClusterConfigY.EpsilonY = 1500;
                    DistanceThreshold = 6000;
                    MinPoints = 1;
                    ExpandMarginLeft = ExpandMarginRight = ExpandMarginTop = ExpandMarginBottom = 300;

                    DrawClusterX = true;
                    DrawClusterY = false;
                    DrawClusterEnvelopePolyline = false;
                    DrawClusterExpandedEnvelope = false;
                    DrawClusterHull = false;
                    DrawClusterPts = false;

                    DrawDimX = true;
                    DrawDimY = true;

                    DrawBPs = false;
                    DrawAAPs = false;
                    DrawBAPs = false;
                    DrawABs = false;
                    DrawSteelPlatePs = false;

                    IncludeBPs = false;
                    IncludeAAPs = true;
                    IncludeBAPs = false;
                    IncludeABs = true;
                    IncludeSteelPlatePs = true;
                    break;
            }

            OnPropertyChanged(nameof(ClusterConfigX));
            OnPropertyChanged(nameof(ClusterConfigY));
        }

        #endregion

        #region 聚类逻辑

        private void ExecuteDraw()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            // Scale 统一从设置面板读取
            var scale = SettingsPanelViewModel.Current?.Scale ?? 40.0;

            try
            {
                using (doc.LockDocument())
                {
                    // 1. 采集输入
                    var inputSvc = new ClusterInputService();
                    var input = inputSvc.GatherInput(
                        IncludeBPs, IncludeAAPs, IncludeBAPs, IncludeABs, IncludeSteelPlatePs);

                    if (input == null || input.FilteredPoints.Count == 0)
                    {
                        ed.WriteMessage("\n未获取到有效的选择集。\n");
                        return;
                    }

                    // 2. 轴线分析 + 区域划分 + 点分区
                    var axisSvc = new AxisAnalysisService();
                    var axes = axisSvc.AnalyzeAxes(input.AxisLines, input.FilteredPoints, scale);

                    // 3. 同步聚类参数
                    ClusterConfigX.ExpandMargins = (ExpandMarginLeft, ExpandMarginTop, ExpandMarginRight, ExpandMarginBottom);
                    ClusterConfigY.ExpandMargins = (ExpandMarginLeft, ExpandMarginTop, ExpandMarginRight, ExpandMarginBottom);
                    ClusterConfigX.MinPoints = MinPoints;
                    ClusterConfigY.MinPoints = MinPoints;

                    // 4. 聚类 + 标注生成
                    var clusterFactory = new ClusterFactoryService();
                    var dimSvc = new DimensionService();
                    var dimOptions = new ClusterDimOptions
                    {
                        Scale = scale,
                        DistanceThreshold = DistanceThreshold,
                        XDirectionIsUp = false,
                        YDirectionIsRight = false
                    };

                    var dimResult = dimSvc.BuildDimensionsByRegion(
                        axes.PointsMap, ClusterConfigX, ClusterConfigY,
                        dimOptions, clusterFactory);

                    // 5. 绘制输出
                    var drawSvc = new ClusterDrawService();
                    var switches = new DrawSwitches
                    {
                        DrawBPs = DrawBPs,
                        DrawAAPs = DrawAAPs,
                        DrawBAPs = DrawBAPs,
                        DrawABs = DrawABs,
                        DrawSteelPlatePs = DrawSteelPlatePs,
                        DrawClusterX = DrawClusterX,
                        DrawClusterY = DrawClusterY,
                        DrawClusterEnvelopePolyline = DrawClusterEnvelopePolyline,
                        DrawClusterExpandedEnvelope = DrawClusterExpandedEnvelope,
                        DrawClusterHull = DrawClusterHull,
                        DrawClusterPts = DrawClusterPts,
                        DrawDimX = DrawDimX,
                        DrawDimY = DrawDimY,
                        DrawAxisCircle = DrawAxisCircle,
                        DrawAxisText = DrawAxisText,
                        DrawRegionFrame = DrawRegionFrame,
                        DrawRegionText = DrawRegionText
                    };

                    drawSvc.Draw(input, axes, dimResult.Clusters, dimResult.AllDimensions, switches);
                    ed.WriteMessage("\n区域聚类标注完成。\n");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n致命错误：{ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}\n");
            }
        }

        #endregion

        #region 属性 - 基础参数

        public int MinPoints { get => _minPoints; set { _minPoints = value; OnPropertyChanged(); } }
        private int _minPoints = 3;

        public ClusterConfig ClusterConfigX { get; set; } = new ClusterConfig();
        public ClusterConfig ClusterConfigY { get; set; } = new ClusterConfig();

        public double ExpandMarginLeft { get => _expandMarginLeft; set { _expandMarginLeft = value; OnPropertyChanged(); } }
        public double ExpandMarginTop { get => _expandMarginTop; set { _expandMarginTop = value; OnPropertyChanged(); } }
        public double ExpandMarginRight { get => _expandMarginRight; set { _expandMarginRight = value; OnPropertyChanged(); } }
        public double ExpandMarginBottom { get => _expandMarginBottom; set { _expandMarginBottom = value; OnPropertyChanged(); } }

        private double _expandMarginLeft = 300;
        private double _expandMarginTop = 300;
        private double _expandMarginRight = 300;
        private double _expandMarginBottom = 300;

        public double DistanceThreshold { get => _distanceThreshold; set { _distanceThreshold = value; OnPropertyChanged(); } }
        private double _distanceThreshold = 6000;

        #endregion

        #region 属性 - 绘图开关（本地 bool）

        public bool DrawClusterX { get => _drawClusterX; set { _drawClusterX = value; OnPropertyChanged(); } }
        public bool DrawClusterY { get => _drawClusterY; set { _drawClusterY = value; OnPropertyChanged(); } }
        private bool _drawClusterX = true;
        private bool _drawClusterY = false;

        public bool DrawBPs { get => _drawBPs; set { _drawBPs = value; OnPropertyChanged(); } }
        public bool DrawAAPs { get => _drawAAPs; set { _drawAAPs = value; OnPropertyChanged(); } }
        public bool DrawBAPs { get => _drawBAPs; set { _drawBAPs = value; OnPropertyChanged(); } }
        public bool DrawABs { get => _drawABs; set { _drawABs = value; OnPropertyChanged(); } }
        public bool DrawSteelPlatePs { get => _drawSteelPlatePs; set { _drawSteelPlatePs = value; OnPropertyChanged(); } }
        private bool _drawBPs = false;
        private bool _drawAAPs = false;
        private bool _drawBAPs = false;
        private bool _drawABs = false;
        private bool _drawSteelPlatePs = false;

        public bool DrawClusterEnvelopePolyline { get => _drawClusterEnvelopePolyline; set { _drawClusterEnvelopePolyline = value; OnPropertyChanged(); } }
        public bool DrawClusterExpandedEnvelope { get => _drawClusterExpandedEnvelope; set { _drawClusterExpandedEnvelope = value; OnPropertyChanged(); } }
        public bool DrawClusterHull { get => _drawClusterHull; set { _drawClusterHull = value; OnPropertyChanged(); } }
        public bool DrawClusterPts { get => _drawClusterPts; set { _drawClusterPts = value; OnPropertyChanged(); } }
        private bool _drawClusterEnvelopePolyline = false;
        private bool _drawClusterExpandedEnvelope = false;
        private bool _drawClusterHull = false;
        private bool _drawClusterPts = false;

        public bool DrawDimX { get => _drawDimX; set { _drawDimX = value; OnPropertyChanged(); } }
        public bool DrawDimY { get => _drawDimY; set { _drawDimY = value; OnPropertyChanged(); } }
        private bool _drawDimX = true;
        private bool _drawDimY = true;

        public bool DrawAxisCircle { get => _drawAxisCircle; set { _drawAxisCircle = value; OnPropertyChanged(); } }
        public bool DrawAxisText { get => _drawAxisText; set { _drawAxisText = value; OnPropertyChanged(); } }
        public bool DrawRegionFrame { get => _drawRegionFrame; set { _drawRegionFrame = value; OnPropertyChanged(); } }
        public bool DrawRegionText { get => _drawRegionText; set { _drawRegionText = value; OnPropertyChanged(); } }
        private bool _drawAxisCircle = false;
        private bool _drawAxisText = false;
        private bool _drawRegionFrame = false;
        private bool _drawRegionText = false;

        #endregion

        #region 属性 - 聚类点开关（本地 bool）

        public bool IncludeBPs { get => _includeBPs; set { _includeBPs = value; OnPropertyChanged(); } }
        public bool IncludeAAPs { get => _includeAAPs; set { _includeAAPs = value; OnPropertyChanged(); } }
        public bool IncludeBAPs { get => _includeBAPs; set { _includeBAPs = value; OnPropertyChanged(); } }
        public bool IncludeABs { get => _includeABs; set { _includeABs = value; OnPropertyChanged(); } }
        public bool IncludeSteelPlatePs { get => _includeSteelPlatePs; set { _includeSteelPlatePs = value; OnPropertyChanged(); } }
        private bool _includeBPs = true;
        private bool _includeAAPs = true;
        private bool _includeBAPs = true;
        private bool _includeABs = false;
        private bool _includeSteelPlatePs = false;

        #endregion

        #region 构造函数

        public ClusterPanelViewModel()
        {
            ExecuteDrawCommand = new RelayCommand(ExecuteDraw);
            LoadPreset1Command = new RelayCommand(() => SelectedPreset = 1);
            LoadPreset2Command = new RelayCommand(() => SelectedPreset = 2);
            LoadPreset3Command = new RelayCommand(() => SelectedPreset = 3);
            ApplyPreset(1); // 默认方案1

            Current = this;
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        #endregion
    }
}
