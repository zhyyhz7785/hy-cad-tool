using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.HelpClass;
using HyCADTool.Models.Cluster;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
namespace HyCADTool.ViewModels
{
    public class BaseDimensionPanelViewModel : INotifyPropertyChanged
    {
        private BaseDimHelper _helper;
        private double _scale = 40;
        private double _clusterConfigX_EpsilonX = 9000;
        private double _clusterConfigX_EpsilonY = 600;
        private double _clusterConfigY_EpsilonX = 600;
        private double _clusterConfigY_EpsilonY = 9000;
        private double _distanceThreshold = 6000;
        private bool _drawInputPoints = false;
        private bool _drawClusterX = true;
        private bool _drawClusterY = true;
        public bool DrawInputPoints
        {
            get => _drawInputPoints;
            set
            {
                if (_drawInputPoints != value)
                {
                    _drawInputPoints = value;
                    OnPropertyChanged();
                }
            }
        }
        public bool DrawClusterX
        {
            get => _drawClusterX;
            set
            {
                if (_drawClusterX != value)
                {
                    _drawClusterX = value;
                    OnPropertyChanged();
                    if (_helper != null) _helper.DrawClusterX = value;
                }
            }
        }
        public bool DrawClusterY
        {
            get => _drawClusterY;
            set
            {
                if (_drawClusterY != value)
                {
                    _drawClusterY = value;
                    OnPropertyChanged();
                    if (_helper != null) _helper.DrawClusterY = value;
                }
            }
        }
        
        public double Scale { get => _scale; set { _scale = value; OnPropertyChanged(); } }
        public double ClusterConfigX_EpsilonX { get => _clusterConfigX_EpsilonX; set { _clusterConfigX_EpsilonX = value; OnPropertyChanged(); } }
        public double ClusterConfigX_EpsilonY { get => _clusterConfigX_EpsilonY; set { _clusterConfigX_EpsilonY = value; OnPropertyChanged(); } }
        public double ClusterConfigY_EpsilonX { get => _clusterConfigY_EpsilonX; set { _clusterConfigY_EpsilonX = value; OnPropertyChanged(); } }
        public double ClusterConfigY_EpsilonY { get => _clusterConfigY_EpsilonY; set { _clusterConfigY_EpsilonY = value; OnPropertyChanged(); } }
        public double DistanceThreshold { get => _distanceThreshold; set { _distanceThreshold = value; OnPropertyChanged(); } }
        public ICommand GenerateBaseDimensionsCommand { get; }
        public BaseDimensionPanelViewModel()
        {
            GenerateBaseDimensionsCommand = new RelayCommand(GenerateBaseDimensions);
        }
        private void GenerateBaseDimensions()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            try
            {
                using (doc.LockDocument())
                {
                    _helper = BaseDimHelper.Create(); // ★ 初始化并赋值到字段
                    _helper.Scale = Scale;
                    _helper.ClusterConfigX = new ClusterConfig
                    {
                        EpsilonX = ClusterConfigX_EpsilonX,
                        EpsilonY = ClusterConfigX_EpsilonY,
                        MinPoints = 1
                    };
                    _helper.ClusterConfigY = new ClusterConfig
                    {
                        EpsilonX = ClusterConfigY_EpsilonX,
                        EpsilonY = ClusterConfigY_EpsilonY,
                        MinPoints = 1
                    };
                    _helper.DistanceThreshold = DistanceThreshold;
                    
                    _helper.DrawClusterX = DrawClusterX;
                    _helper.DrawClusterY = DrawClusterY;
                    _helper.RunAll();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n生成基础标注时出错: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
