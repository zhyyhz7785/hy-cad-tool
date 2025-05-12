//using HyCADTool.HelpClass;
//using System.ComponentModel;
//using System.Runtime.CompilerServices;
//using System.Windows.Input;
//namespace HyCADTool.ViewModels
//{
//    public class PointsClusterPanelViewModel : INotifyPropertyChanged
//    {
//        private double _scale = 40;
//        private double _epsilonX = 1500;
//        private double _epsilonY = 1500;
//        private int _minPoints = 1;
//        private double _inputDistanceFromContour = 300; // 整体轮廓距离
//        // 四边界（同步内部逻辑，暂不暴露）
//        private double _marginUp = 300;
//        private double _marginDown = 300;
//        private double _marginLeft = 300;
//        private double _marginRight = 300;
//        public double Scale { get => _scale; set { _scale = value; OnPropertyChanged(); } }
//        public double EpsilonX { get => _epsilonX; set { _epsilonX = value; OnPropertyChanged(); } }
//        public double EpsilonY { get => _epsilonY; set { _epsilonY = value; OnPropertyChanged(); } }
//        public int MinPoints { get => _minPoints; set { _minPoints = value; OnPropertyChanged(); } }
//        public double InputDistanceFromContour
//        {
//            get => _inputDistanceFromContour;
//            set
//            {
//                _inputDistanceFromContour = value;
//                MarginUp = MarginDown = MarginLeft = MarginRight = value;
//                OnPropertyChanged();
//            }
//        }
//        public double MarginUp { get => _marginUp; private set { _marginUp = value; OnPropertyChanged(); } }
//        public double MarginDown { get => _marginDown; private set { _marginDown = value; OnPropertyChanged(); } }
//        public double MarginLeft { get => _marginLeft; private set { _marginLeft = value; OnPropertyChanged(); } }
//        public double MarginRight { get => _marginRight; private set { _marginRight = value; OnPropertyChanged(); } }
//        public ICommand DrawExpandedRectanglesCommand { get; }
//        public ICommand DrawConvexHullsCommand { get; }
//        public ICommand MarkOnlyEnvelopeCommand { get; } // ★ 新增命令
//        public PointsClusterPanelViewModel()
//        {
//            DrawExpandedRectanglesCommand = new RelayCommand(DrawExpandedRectangles);
//            DrawConvexHullsCommand = new RelayCommand(DrawConvexHulls);
//            MarkOnlyEnvelopeCommand = new RelayCommand(MarkOnlyEnvelope); // 预留
//        }
//        private void DrawExpandedRectangles()
//        {
//            var points = PointClusterHelper.SelectPointsOrCircles();
//            if (points.Count == 0) return;
//            var helper = PointClusterHelper.Create(points, EpsilonX, EpsilonY, MinPoints);
//            helper.Expand = (MarginLeft, MarginUp, MarginRight, MarginDown);
//            helper.DrawExpandedEnvelopes();
//        }
//        private void DrawConvexHulls()
//        {
//            var points = PointClusterHelper.SelectPointsOrCircles();
//            if (points.Count == 0) return;
//            var helper = PointClusterHelper.Create(points, EpsilonX, EpsilonY, MinPoints);
//            helper.DrawClusterConvexHulls();
//        }
//        private void MarkOnlyEnvelope()
//        {
//           // PointClusterHelper.GenerateABDims();
//        }
//        public event PropertyChangedEventHandler PropertyChanged;
//        protected void OnPropertyChanged([CallerMemberName] string name = null)
//            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
//    }
//}
