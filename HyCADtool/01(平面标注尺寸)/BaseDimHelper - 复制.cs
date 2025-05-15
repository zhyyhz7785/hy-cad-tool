//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using HyCADTool.Models.Cluster;
//using HyCADTool.Tools;
//using HyCADTool.Models.Annotation;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using static HyCADTool.Tools.EtGpt;

//namespace HyCADTool.HelpClass
//{
//    public partial class BaseDimHelper
//    {
//        #region 配置属性
//        public double Scale { get; set; } = 40.0;
//        public double DistanceThreshold { get; set; } = 6000.0;

//        public ClusterConfig ClusterConfigX { get; set; } = new ClusterConfig { EpsilonX = 9000, EpsilonY = 600, MinPoints = 1 };
//        public ClusterConfig ClusterConfigY { get; set; } = new ClusterConfig { EpsilonX = 600, EpsilonY = 9000, MinPoints = 1 };

//        public bool DrawClusterX { get; set; } = true;
//        public bool DrawClusterY { get; set; } = true;
//        public bool DrawInputPoints { get; set; } = true;

//        public List<Point3d> Points { get; private set; }
//        public List<ClusterResult> ClusterResultX { get; private set; }
//        public List<ClusterResult> ClusterResultY { get; private set; }
//        #endregion

//        #region 私有字段
//        private readonly Database _db;
//        private readonly Editor _ed;
//        private DimPointsAndAxis _input;
//        #endregion

//        #region 构造
//        public static BaseDimHelper Create()
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument
//                ?? throw new InvalidOperationException("未检测到有效AutoCAD文档。");
//            return new BaseDimHelper(doc.Database, doc.Editor);
//        }

//        private BaseDimHelper(Database db, Editor ed)
//        {
//            _db = db;
//            _ed = ed;
//        }
//        #endregion

//        #region 主流程
//        public void RunAll()
//        {
//            InitializePoints();
//            GenerateClusters();
//            GenerateDimensionsForClusters();
//        }
//        #endregion

//        #region 输入点集初始化
//        private void InitializePoints()
//        {
//            _input = DimPointsAndAxis.GetInput();

//            if (DrawInputPoints)
//            {
//                DimPointDrawer.Draw(_input);
//            }

//            Points = _input.BPs
//                .Concat(_input.A_APs)
//                .Concat(_input.B_APs)
//                .Concat(_input.ABs)
//                .Concat(_input.SteelPlatePs)
//                .Distinct(new Point3dEqualityComparer(0.0001))
//                .ToList();
//        }
//        #endregion

//        #region 聚类生成
//        private void GenerateClusters()
//        {
//            ClusterResultX = ClusterFactory.Create(Points, ClusterConfigX);
//            ClusterResultY = ClusterFactory.Create(Points, ClusterConfigY);
//            AppendAxisIntersectionsToClusters(ClusterResultX);
//            AppendAxisIntersectionsToClusters(ClusterResultY);
//        }

//        private void AppendAxisIntersectionsToClusters(List<ClusterResult> clusters)
//        {
//            foreach (var cluster in clusters)
//            {
//                if (cluster.EnvelopePolyline == null) continue;

//                foreach (var axis in _input.AxisLines)
//                {
//                    var pts = new Point3dCollection();
//                    axis.IntersectWith(cluster.EnvelopePolyline, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);
//                    cluster.Points.AddRange(pts.Cast<Point3d>());
//                }

//                cluster.EnsureUniquePoints(0.0001);
//            }
//        }
//        #endregion

//        #region 标注生成
//        public void GenerateDimensionsForClusters()
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;

//            var layerIdX = EtGpt.CreateLayer("00_hy_3公共_标注2_内x", 93, db);
//            var layerIdY = EtGpt.CreateLayer("00_hy_3公共_标注2_内y", 45, db);

//            var options = new ClusterDimOptions
//            {
//                Scale = Scale,
//                DistanceThreshold = DistanceThreshold,
//                XDirectionIsUp = false,
//                YDirectionIsRight = false
//            };

//            using (doc.LockDocument())
//            using (var tr = db.TransactionManager.StartTransaction())
//            {
//                var allDims = new List<RotatedDimension>();
//                foreach (var cluster in ClusterResultX)
//                    allDims.AddRange(EtGpt.CreateDimensionsForClusterSingleSide(cluster.Points, true, layerIdX, options));

//                foreach (var cluster in ClusterResultY)
//                    allDims.AddRange(EtGpt.CreateDimensionsForClusterSingleSide(cluster.Points, false, layerIdY, options));

//                allDims = FilterDuplicateDimensions(allDims, DistanceThreshold);
//                allDims.ToSpace(db);
//                tr.Commit();
//            }
//        }
//        #endregion

//        #region 重复标注过滤
//        private List<RotatedDimension> FilterDuplicateDimensions(List<RotatedDimension> dims, double threshold)
//        {
//            const double tol = 0.1;
//            var xGroups = dims
//                .Where(d => Math.Abs(d.Rotation) < 1e-6)
//                .GroupBy(d => new { X1 = Math.Round(d.XLine1Point.X / tol), X2 = Math.Round(d.XLine2Point.X / tol) });

//            var yGroups = dims
//                .Where(d => Math.Abs(Math.Abs(d.Rotation) - Math.PI / 2) < 1e-6)
//                .GroupBy(d => new { Y1 = Math.Round(d.XLine1Point.Y / tol), Y2 = Math.Round(d.XLine2Point.Y / tol) });

//            return FilterGroup(xGroups, d => d.DimLinePoint.Y, threshold)
//                .Concat(FilterGroup(yGroups, d => d.DimLinePoint.X, threshold))
//                .ToList();
//        }

//        private List<RotatedDimension> FilterGroup(
//            IEnumerable<IGrouping<object, RotatedDimension>> groups,
//            Func<RotatedDimension, double> keySelector,
//            double threshold)
//        {
//            var result = new List<RotatedDimension>();
//            foreach (var group in groups)
//            {
//                var list = group.OrderBy(keySelector).ToList();
//                int current = 0;
//                while (current < list.Count)
//                {
//                    var sameLine = new List<RotatedDimension> { list[current] };
//                    int next = current + 1;
//                    while (next < list.Count &&
//                           Math.Abs(keySelector(list[next]) - keySelector(list[current])) <= threshold)
//                    {
//                        sameLine.Add(list[next]);
//                        next++;
//                    }
//                    result.Add(sameLine.First());
//                    current = next;
//                }
//            }
//            return result;
//        }
//        #endregion
//    }
//}
