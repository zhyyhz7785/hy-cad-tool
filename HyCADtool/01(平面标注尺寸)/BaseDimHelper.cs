//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Clipper2Lib;
//using HyCADTool.Config;
//using HyCADTool.Tools;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using static HyCADTool.Tools.EtGpt;

//namespace HyCADTool.HelpClass
//{
//    public partial class BaseDimHelper
//    {
//        #region 属性字段
//        public bool IsPointsToSpace
//        {
//            get => _baseDimension.IsPointsToSpace;
//            set => _baseDimension.IsPointsToSpace = value;
//        }

//        // 【新增】比例
//        public double Scale { get; set; } = 40.0;

//        // 【新增】X方向聚类参数（允许外部传递）
//        public ClusterConfig ClusterConfigX { get; set; } = new ClusterConfig { EpsilonX = 9000, EpsilonY = 600, MinPoints = 1 };

//        // 【新增】Y方向聚类参数（允许外部传递）
//        public ClusterConfig ClusterConfigY { get; set; } = new ClusterConfig { EpsilonX = 600, EpsilonY = 9000, MinPoints = 1 };    

//        // 【新增】过滤重复标注容差
//        public double DistanceThreshold { get; set; } = 6000.0;


//        public List<Point3d> Points { get; private set; }
//        public List<List<Point3d>> ClusterPointsX { get; private set; }
//        public List<List<Point3d>> ClusterPointsY { get; private set; }
//        public List<ClusterResult> ClusterResultX { get; private set; }
//        public List<ClusterResult> ClusterResultY { get; private set; }

//        private readonly BaseDimension _baseDimension;
//        private readonly EnvelopeCluster _envelopeCluster;
//        private readonly Database _db;
//        private readonly Editor _ed;

//        private const double MergeDistance = 12000.0;
//        #endregion

//        #region 构造方法
//        public static BaseDimHelper Create()
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            if (doc == null) throw new InvalidOperationException("未检测到有效AutoCAD文档。");
//            return new BaseDimHelper(doc.Database, doc.Editor);
//        }

//        private BaseDimHelper(Database db, Editor ed)
//        {
//            _db = db;
//            _ed = ed;

//            _baseDimension = new BaseDimension();
//            _envelopeCluster = new EnvelopeCluster();

//            InitializePoints();
//            GenerateClusters();
//            GenerateDimensionsForClusters();
//        }
//        #endregion

//        #region 初始化点集合
//        private void InitializePoints()
//        {
//            Points = new List<Point3d>();
//            Points.AddRange(_baseDimension.BPs ?? new List<Point3d>());
//            Points.AddRange(_baseDimension.A_APs ?? new List<Point3d>());
//            Points.AddRange(_baseDimension.B_APs ?? new List<Point3d>());
//            Points = Points.Distinct(new Point3dEqualityComparer(0.0001)).ToList();
//        }
//        #endregion

//        #region 聚类逻辑（修改版）
//        private void GenerateClusters()
//        {
//            var configX = ClusterConfigX ?? new ClusterConfig { EpsilonX = 9000, EpsilonY = 600, MinPoints = 1 };
//            var configY = ClusterConfigY ?? new ClusterConfig { EpsilonX = 600, EpsilonY = 9000, MinPoints = 1 };

//            configX.ClusterLayerId = EtGpt.CreateLayer("00_hy_基础_聚类轮廓_X", 123, _db);
//            configY.ClusterLayerId = EtGpt.CreateLayer("00_hy_基础_聚类轮廓_Y", 124, _db);

//            ClusterResultX = _envelopeCluster.ProcessClustersAndGenerateEnvelopes(Points, configX, _db) > 0
//                ? _envelopeCluster.ClusterResults
//                : new List<ClusterResult>();

//            ClusterResultY = _envelopeCluster.ProcessClustersAndGenerateEnvelopes(Points, configY, _db) > 0
//                ? _envelopeCluster.ClusterResults
//                : new List<ClusterResult>();           
//                SupplementClusterPoints(ClusterResultX);
//                SupplementClusterPoints(ClusterResultY);
            
//        }     
    

//        private void SupplementClusterPoints(List<ClusterResult> clusters)
//        {
//            foreach (var cluster in clusters)
//            {
//                if (cluster.EnvelopePolyline == null) continue;

//                foreach (var axis in _baseDimension.AxisLines)
//                {
//                    Point3dCollection points = new Point3dCollection();
//                    axis.IntersectWith(cluster.EnvelopePolyline, Intersect.OnBothOperands, points, IntPtr.Zero, IntPtr.Zero);
//                    cluster.Points.AddRange(points.Cast<Point3d>());
//                }

//                // ✨ 聚类内部去重（避免交点重复）
//                cluster.Points = cluster.Points
//                    .Distinct(new Point3dEqualityComparer(0.0001))
//                    .ToList();
//            }
//        }

//        #endregion

//        #region 单聚类处理（改为X向和Y向分开）
//        private List<RotatedDimension> ProcessClusterSingleX(ClusterResult cluster, ObjectId layerId, DimensionFor dimDirection)
//        {
//            var dims = new List<RotatedDimension>();
//            var points = GroupPointsForX(cluster.Points);


//            if (points.Count < 2) return dims;

//            double baseY = points.Min(p => p.Y);
//            points = points.Select(p => new Point3d(p.X, baseY, 0)).ToList();

//            double defaultOffset = 5 * BaseConfig.Scale;
//            double distanceThreshold = 3 * BaseConfig.Scale;

//            for (int i = 0; i < points.Count - 1; i++)
//            {
//                var p1 = points[i];
//                var p2 = points[i + 1];
//                double dx = Math.Abs(p2.X - p1.X);
//                double effectiveOffset = defaultOffset;
//                if (dx < distanceThreshold) effectiveOffset = 2 * defaultOffset;

//                var dim = EtGpt.GetDimByTwoPoints(p1, p2, effectiveOffset, dimDirection, true);
//                dim.LayerId = layerId;
//                dims.Add(dim);
//            }

//            return dims;
//        }

//        private List<RotatedDimension> ProcessClusterSingleY(ClusterResult cluster, ObjectId layerId, DimensionFor dimDirection)
//        {
//            var dims = new List<RotatedDimension>();
//            var points = GroupPointsForY(cluster.Points);

//            if (points.Count < 2) return dims;

//            double baseX = points.Min(p => p.X);
//            points = points.Select(p => new Point3d(baseX, p.Y, 0)).ToList();

//            double defaultOffset = 5 * BaseConfig.Scale;
//            double distanceThreshold = 3 * BaseConfig.Scale;

//            for (int i = 0; i < points.Count - 1; i++)
//            {
//                var p1 = points[i];
//                var p2 = points[i + 1];
//                double dy = Math.Abs(p2.Y - p1.Y);
//                double effectiveOffset = defaultOffset;
//                if (dy < distanceThreshold) effectiveOffset = 2 * defaultOffset;

//                var dim = EtGpt.GetDimByTwoPoints(p1, p2, effectiveOffset, dimDirection, true);
//                dim.LayerId = layerId;
//                dims.Add(dim);
//            }

//            return dims;
//        }
//        #endregion

//        #region Group方法（分开优化）
//        private List<Point3d> GroupPointsForX(List<Point3d> points)
//        {
//            if (points == null || points.Count == 0) return new List<Point3d>();

//            var groupedByX = points.GroupBy(p => Math.Round(p.X, 4));
//            var result = new List<Point3d>();

//            foreach (var group in groupedByX)
//            {
//                var minYPoint = group.OrderBy(p => p.Y).First();
//                result.Add(minYPoint);
//            }
//            return result.OrderBy(p => p.X).ToList();
//        }

//        private List<Point3d> GroupPointsForY(List<Point3d> points)
//        {
//            if (points == null || points.Count == 0) return new List<Point3d>();

//            var groupedByY = points.GroupBy(p => Math.Round(p.Y, 4));
//            var result = new List<Point3d>();

//            foreach (var group in groupedByY)
//            {
//                var minXPoint = group.OrderBy(p => p.X).First();
//                result.Add(minXPoint);
//            }
//            return result.OrderBy(p => p.Y).ToList();
//        }
//        #endregion

//        #region 最后统一生成标注
//        /// <summary>
//        /// 生成全部聚类区域的标注（包含X向和Y向），统一输出到模型空间
//        /// </summary>
//        public void GenerateDimensionsForClusters()
//        {
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var db = doc.Database;

//            var layerIdX = EtGpt.CreateLayer("00_hy_3公共_标注2_内x", 93, db);
//            var layerIdY = EtGpt.CreateLayer("00_hy_3公共_标注2_内y", 45, db);

//            using (doc.LockDocument())
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                var allDimensions = new List<RotatedDimension>();
//                var dimsX = new List<RotatedDimension>();
//                var dimsY = new List<RotatedDimension>();

//                // ✨ 处理每个 X向 聚类（ClusterResultX） → 只做 X向标注
//                foreach (var cluster in ClusterResultX)
//                {
//                    var dims = EtGpt.CreateDimensionsForCluster(cluster, DimensionFor.ForDown, layerIdX, Scale, DistanceThreshold);
//                    allDimensions.AddRange(dims);
//                }
//                foreach (var cluster in ClusterResultY)
//                {
//                    var dims = EtGpt.CreateDimensionsForCluster(cluster, DimensionFor.ForLeft, layerIdY, Scale, DistanceThreshold);
//                    allDimensions.AddRange(dims);
//                }


//                // ✨ 插入清理逻辑
//                //double distanceThreshold = 5 * BaseConfig.Scale;
//                allDimensions = FilterDuplicateDimensions(allDimensions, DistanceThreshold);
//                // ✨ 统一输出到模型空间
//                allDimensions.ToSpace(db);

//                tr.Commit();
//            }
//        }

//        /// <summary>
//        /// 使用带容差的逻辑，过滤重复尺寸的标注
//        /// </summary>
//        private List<RotatedDimension> FilterDuplicateDimensions(List<RotatedDimension> dims, double distanceThreshold)
//        {
//            const double Tolerance = 0.1; // 容差，按实际需求可以放大或改成 BaseConfig.Scale * 0.001
//            var filteredDims = new List<RotatedDimension>(dims);

//            // 处理X向标注
//            var xDims = filteredDims
//                .Where(d => Math.Abs(d.Rotation) < 1e-6)
//                .OrderBy(d => d.DimLinePoint.Y)
//                .ToList();

//            var xGroups = xDims
//                .GroupBy(d => new
//                {
//                    StartX = Math.Round(d.XLine1Point.X / Tolerance),
//                    EndX = Math.Round(d.XLine2Point.X / Tolerance)
//                });

//            var newXDims = new List<RotatedDimension>();
//            foreach (var group in xGroups)
//            {
//                var list = group.OrderBy(d => d.DimLinePoint.Y).ToList();
//                if (list.Count == 1)
//                {
//                    newXDims.Add(list[0]);
//                    continue;
//                }

//                int current = 0;
//                while (current < list.Count)
//                {
//                    var candidates = new List<RotatedDimension> { list[current] };
//                    int next = current + 1;
//                    while (next < list.Count &&
//                           Math.Abs(list[next].DimLinePoint.Y - list[current].DimLinePoint.Y) <= distanceThreshold)
//                    {
//                        candidates.Add(list[next]);
//                        next++;
//                    }

//                    var keep = candidates.OrderBy(d => d.DimLinePoint.Y).First();
//                    newXDims.Add(keep);

//                    current = next;
//                }
//            }

//            // 处理Y向标注
//            var yDims = filteredDims
//                .Where(d => Math.Abs(Math.Abs(d.Rotation) - Math.PI / 2) < 1e-6)
//                .OrderBy(d => d.DimLinePoint.X)
//                .ToList();

//            var yGroups = yDims
//                .GroupBy(d => new
//                {
//                    StartY = Math.Round(d.XLine1Point.Y / Tolerance),
//                    EndY = Math.Round(d.XLine2Point.Y / Tolerance)
//                });

//            var newYDims = new List<RotatedDimension>();
//            foreach (var group in yGroups)
//            {
//                var list = group.OrderBy(d => d.DimLinePoint.X).ToList();
//                if (list.Count == 1)
//                {
//                    newYDims.Add(list[0]);
//                    continue;
//                }

//                int current = 0;
//                while (current < list.Count)
//                {
//                    var candidates = new List<RotatedDimension> { list[current] };
//                    int next = current + 1;
//                    while (next < list.Count &&
//                           Math.Abs(list[next].DimLinePoint.X - list[current].DimLinePoint.X) <= distanceThreshold)
//                    {
//                        candidates.Add(list[next]);
//                        next++;
//                    }

//                    var keep = candidates.OrderBy(d => d.DimLinePoint.X).First();
//                    newYDims.Add(keep);

//                    current = next;
//                }
//            }

//            return newXDims.Concat(newYDims).ToList();
//        }


//        #endregion

//        #region 辅助
//        private class Point3dEqualityComparer : IEqualityComparer<Point3d>
//    {
//        private readonly double _tolerance;
//        public Point3dEqualityComparer(double tolerance) => _tolerance = tolerance;
//        public bool Equals(Point3d p1, Point3d p2) => Math.Abs(p1.X - p2.X) <= _tolerance && Math.Abs(p1.Y - p2.Y) <= _tolerance;
//        public int GetHashCode(Point3d p) => HashCode.Combine((int)(p.X / _tolerance), (int)(p.Y / _tolerance));
//    }
//    #endregion
//}
//}
