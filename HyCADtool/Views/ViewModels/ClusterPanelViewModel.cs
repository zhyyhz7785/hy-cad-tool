using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Models.Cluster;
using HyCADTool.Tools;
using Autodesk.AutoCAD.ApplicationServices;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;
using System;
using HyCADTool.Drawing;
using HyCADTool.Models;
using Autodesk.AutoCAD.DatabaseServices;
using System.Linq;

namespace HyCADTool.ViewModels
{
    public class ClusterPanelViewModel : INotifyPropertyChanged
    {
        public ICommand ExecuteDrawCommand { get; }
        public ICommand LoadPreset1Command { get; }
        public ICommand LoadPreset2Command { get; }
        public ICommand LoadPreset3Command { get; }

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

        public ClusterPanelViewModel()
        {
            ExecuteDrawCommand = new RelayCommand(ExecuteDraw);
            LoadPreset1Command = new RelayCommand(() => SelectedPreset = 1);
            LoadPreset2Command = new RelayCommand(() => SelectedPreset = 2);
            LoadPreset3Command = new RelayCommand(() => SelectedPreset = 3);
            ApplyPreset(1); // 默认方案1
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

      

        private void ExecuteDraw()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            try
            {
                using (doc.LockDocument())
                {
                    var dpa = DimPointsAndAxis.GetInput();
                    if (dpa == null)
                    {
                        ed.WriteMessage("\n未获取到有效的选择集。\n");
                        return;
                    }

                    var axes = AxisDatas.FromLines(dpa.AxisLines, dpa.SelectPoints);
                    var pointsMap = axes.PointsMap;

                    // 设置聚类参数
                    ClusterConfigX.ExpandMargins = (ExpandMarginLeft, ExpandMarginTop, ExpandMarginRight, ExpandMarginBottom);
                    ClusterConfigY.ExpandMargins = (ExpandMarginLeft, ExpandMarginTop, ExpandMarginRight, ExpandMarginBottom);
                    ClusterConfigX.MinPoints = MinPoints;
                    ClusterConfigY.MinPoints = MinPoints;

                    // ✅ 添加交点（左下点与最接近轴线交点）
                    foreach (var kv in pointsMap)
                    {
                        var regionInfo = kv.Value;
                        var pts = regionInfo.Points;

                        if (pts == null || pts.Count == 0)
                            continue;

                        var minPt = pts.OrderBy(p => p.X).ThenBy(p => p.Y).FirstOrDefault();

                        var xAxis = regionInfo.XAxis;
                        var yAxis = regionInfo.YAxis;
                        
                    }

                    // ⏩ 聚类与标注
                    var helper = DimHelper.BuildByRegion(pointsMap, ClusterConfigX, ClusterConfigY);
                    DrawInCad.Draw(dpa, axes, helper.Clusters, helper.AllDimensions);
                    ed.WriteMessage("\n区域聚类标注完成。\n");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n致命错误：{ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}\n");
            }
        }



        public double Scale { get => _scale; set { _scale = value; OnPropertyChanged(); } }
        private double _scale = 40;

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

        public bool DrawClusterX { get => _drawClusterX; set { _drawClusterX = value; OnPropertyChanged(); } }
        public bool DrawClusterY { get => _drawClusterY; set { _drawClusterY = value; OnPropertyChanged(); } }
        private bool _drawClusterX = true;
        private bool _drawClusterY = false;

        public bool IncludeBPs { get => DimPointsAndAxisConfig.IncludeBPs; set { DimPointsAndAxisConfig.IncludeBPs = value; OnPropertyChanged(); } }
        public bool IncludeAAPs { get => DimPointsAndAxisConfig.IncludeAAPs; set { DimPointsAndAxisConfig.IncludeAAPs = value; OnPropertyChanged(); } }
        public bool IncludeBAPs { get => DimPointsAndAxisConfig.IncludeBAPs; set { DimPointsAndAxisConfig.IncludeBAPs = value; OnPropertyChanged(); } }
        public bool IncludeABs { get => DimPointsAndAxisConfig.IncludeABs; set { DimPointsAndAxisConfig.IncludeABs = value; OnPropertyChanged(); } }
        public bool IncludeSteelPlatePs { get => DimPointsAndAxisConfig.IncludeSteelPlatePs; set { DimPointsAndAxisConfig.IncludeSteelPlatePs = value; OnPropertyChanged(); } }

        public bool DrawBPs { get => DrawInCad.Draw_BPs; set { DrawInCad.Draw_BPs = value; OnPropertyChanged(); } }
        public bool DrawAAPs { get => DrawInCad.Draw_AAPs; set { DrawInCad.Draw_AAPs = value; OnPropertyChanged(); } }
        public bool DrawBAPs { get => DrawInCad.Draw_BAPs; set { DrawInCad.Draw_BAPs = value; OnPropertyChanged(); } }
        public bool DrawABs { get => DrawInCad.Draw_ABs; set { DrawInCad.Draw_ABs = value; OnPropertyChanged(); } }
        public bool DrawSteelPlatePs { get => DrawInCad.Draw_SteelPlPs; set { DrawInCad.Draw_SteelPlPs = value; OnPropertyChanged(); } }

        public bool DrawClusterEnvelopePolyline { get => DrawInCad.Draw_ClusterEnvelopePolyline; set { DrawInCad.Draw_ClusterEnvelopePolyline = value; OnPropertyChanged(); } }
        public bool DrawClusterExpandedEnvelope { get => DrawInCad.Draw_ClusterEnvelopeExpandedPolyline; set { DrawInCad.Draw_ClusterEnvelopeExpandedPolyline = value; OnPropertyChanged(); } }
        public bool DrawClusterHull { get => DrawInCad.Draw_ClusterHull; set { DrawInCad.Draw_ClusterHull = value; OnPropertyChanged(); } }
        public bool DrawClusterPts { get => DrawInCad.Draw_ClusterPts; set { DrawInCad.Draw_ClusterPts = value; OnPropertyChanged(); } }

        public bool DrawDimX { get => DrawInCad.Draw_DimX; set { DrawInCad.Draw_DimX = value; OnPropertyChanged(); } }
        public bool DrawDimY { get => DrawInCad.Draw_DimY; set { DrawInCad.Draw_DimY = value; OnPropertyChanged(); } }

        public bool DrawAxisCircle { get => DrawInCad.Draw_AxisCircle; set { DrawInCad.Draw_AxisCircle = value; OnPropertyChanged(); } }
        public bool DrawAxisText { get => DrawInCad.Draw_AxisText; set { DrawInCad.Draw_AxisText = value; OnPropertyChanged(); } }
        public bool DrawRegionFrame { get => DrawInCad.Draw_RegionFrame; set { DrawInCad.Draw_RegionFrame = value; OnPropertyChanged(); } }
        public bool DrawRegionText { get => DrawInCad.Draw_RegionText; set { DrawInCad.Draw_RegionText = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
