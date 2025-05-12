using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.HelpClass;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
namespace HyCADTool.ViewModels
{
    public class PointsClusterPanelViewModel : INotifyPropertyChanged
    {
        public double Scale
        {
            get => PointClusterHelper.GlobalScale;
            set { PointClusterHelper.GlobalScale = value; OnPropertyChanged(); }
        }
        public double EpsilonX
        {
            get => PointClusterHelper.GlobalEpsilonX;
            set { PointClusterHelper.GlobalEpsilonX = value; OnPropertyChanged(); }
        }
        public double EpsilonY
        {
            get => PointClusterHelper.GlobalEpsilonY;
            set { PointClusterHelper.GlobalEpsilonY = value; OnPropertyChanged(); }
        }
        public int MinPoints
        {
            get => PointClusterHelper.GlobalMinPoints;
            set { PointClusterHelper.GlobalMinPoints = value; OnPropertyChanged(); }
        }
        private double _inputDistanceFromContour = 300;
        public double InputDistanceFromContour
        {
            get => _inputDistanceFromContour;
            set
            {
                _inputDistanceFromContour = value;
                PointClusterHelper.GlobalMargin = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MarginUp));
                OnPropertyChanged(nameof(MarginDown));
                OnPropertyChanged(nameof(MarginLeft));
                OnPropertyChanged(nameof(MarginRight));
            }
        }
        public double MarginUp => PointClusterHelper.GlobalMargin;
        public double MarginDown => PointClusterHelper.GlobalMargin;
        public double MarginLeft => PointClusterHelper.GlobalMargin;
        public double MarginRight => PointClusterHelper.GlobalMargin;
        public ICommand DrawExpandedRectanglesCommand { get; }
        public ICommand DrawConvexHullsCommand { get; }
        public ICommand GenerateBoltDimensionsCommand { get; }
        public PointsClusterPanelViewModel()
        {
            DrawExpandedRectanglesCommand = new RelayCommand(DrawExpandedRectangles);
            DrawConvexHullsCommand = new RelayCommand(DrawConvexHulls);
            GenerateBoltDimensionsCommand = new RelayCommand(GenerateBoltDimensions);
        }
        private void DrawExpandedRectangles()
        {
            var points = PointClusterHelper.SelectPointsOrCircles();
            if (points.Count == 0) return;
            var helper = PointClusterHelper.CreateWithStaticConfig(points);
            helper.DrawExpandedEnvelopes();
        }
        private void DrawConvexHulls()
        {
            var points = PointClusterHelper.SelectPointsOrCircles();
            if (points.Count == 0) return;
            var helper = PointClusterHelper.CreateWithStaticConfig(points);
            helper.DrawClusterConvexHulls();
        }
        private void GenerateBoltDimensions()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            using (doc.LockDocument())
            {
                PointClusterHelper.GenerateBoltDimensions();
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
