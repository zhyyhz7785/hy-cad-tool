using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Config;
using HyCADTool.Models.Cluster;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.HelpClass
{
    public partial class PointClusterHelper
    {
        #region 🔧 静态共享参数（面板和命令统一配置使用）
        public static double GlobalScale { get; set; } = 40;
        public static double GlobalEpsilonX { get; set; } = 1500;
        public static double GlobalEpsilonY { get; set; } = 1500;
        public static int GlobalMinPoints { get; set; } = 1;
        public static double GlobalMargin { get; set; } = 300;
        public static (double Left, double Top, double Right, double Bottom) GlobalExpand =>
            (GlobalMargin, GlobalMargin, GlobalMargin, GlobalMargin);
        #endregion

        #region 📦 属性
        public double Scale { get; set; } = BaseConfig.Scale;
        public List<Point3d> InputPoints { get; private set; }            // 输入的原始点集合
        public List<ClusterResult> ClusterResults { get; private set; }   // 聚类结果
        public List<Extents3d> EnvelopeRects { get; private set; }        // 每组聚类的原始外包矩形
        public List<Extents3d> ExpandedRects { get; private set; }        // 每组扩大后的外包矩形
        public List<Line> AxisLines { get; private set; }
        public (double Left, double Top, double Right, double Bottom) Expand { get; set; } = (300.0, 300.0, 300.0, 300.0);

        private readonly double _epsilonX;
        private readonly double _epsilonY;
        private readonly int _minPoints;
        #endregion

        #region 🏗️ 工厂方法

        // 保留原有参数版本（用于兼容已有调用）
        public static PointClusterHelper Create(List<Point3d> points, double epsilonX = 1500, double epsilonY = 1500, int minPoints = 1, double scale = 40.0)
        {
            var helper = new PointClusterHelper(points, epsilonX, epsilonY, minPoints)
            {
                Scale = scale
            };
            return helper;
        }

        // 新增：使用静态配置参数构造
        public static PointClusterHelper CreateWithStaticConfig(List<Point3d> points)
        {
            var helper = new PointClusterHelper(points, GlobalEpsilonX, GlobalEpsilonY, GlobalMinPoints)
            {
                Scale = GlobalScale,
                Expand = GlobalExpand
            };
            return helper;
        }

        #endregion

        #region 🚧 构造函数

        private PointClusterHelper(List<Point3d> points, double epsilonX = 9000.0, double epsilonY = 500.0, int minPoints = 1)
        {
            if (points == null || !points.Any())
                throw new ArgumentException("输入点集合不能为空。", nameof(points));

            InputPoints = points;
            _epsilonX = epsilonX;
            _epsilonY = epsilonY;
            _minPoints = minPoints;

            PerformClustering();
            GenerateEnvelopes();
        }

        #endregion

        #region 🧠 聚类处理

        private void PerformClustering()
        {
            var envelopeCluster = new EnvelopeCluster();
            var config = new ClusterConfig
            {
                EpsilonX = _epsilonX,
                EpsilonY = _epsilonY,
                MinPoints = _minPoints
            };
            ClusterResults = envelopeCluster.PerformDBSCAN(InputPoints, config);
        }

        #endregion
    }
}
