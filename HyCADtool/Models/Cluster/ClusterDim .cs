using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Models.Cluster;
using HyCADTool.Tools;
using System.Collections.Generic;
using static HyCADTool.Tools.EtGpt;

namespace HyCADTool.HelpClass
{
    /// <summary>
    /// 定义聚类标注生成的完整参数
    /// </summary>
    public class ClusterDim
    {
        /// <summary>
        /// 标注使用的聚类结果
        /// </summary>
        public ClusterResult Cluster { get; set; }

        /// <summary>
        /// 标注方向（ForDown = X向下，ForLeft = Y向左）
        /// </summary>
        public DimensionFor Direction { get; set; }

        /// <summary>
        /// 标注的目标图层
        /// </summary>
        public ObjectId LayerId { get; set; }

        /// <summary>
        /// 比例因子（比如40）
        /// </summary>
        public double Scale { get; set; } = 40.0;

        /// <summary>
        /// 过滤重复尺寸的距离容差
        /// </summary>
        public double DistanceThreshold { get; set; } = 6000.0;

        /// <summary>
        /// 是否按照X聚类还是Y聚类进行分组（默认false，兼容扩展）
        /// </summary>
        public bool GroupByX { get; set; } = true;

        // 【未来扩展】可以继续增加更多配置，例如：
        // public bool UseExtendedOffset { get; set; }
        // public double CustomArrowSize { get; set; }

        public ClusterDim()
        {
        }

        public ClusterDim(ClusterResult cluster, DimensionFor direction, ObjectId layerId, double scale, double distanceThreshold)
        {
            Cluster = cluster;
            Direction = direction;
            LayerId = layerId;
            Scale = scale;
            DistanceThreshold = distanceThreshold;
        }

        //public static List<RotatedDimension> CreateDimensionsForCluster(ClusterDim dimConfig)
        //{
        //    var dims = new List<RotatedDimension>();

        //    if (dimConfig?.Cluster?.Points == null || dimConfig.Cluster.Points.Count < 2)
        //        return dims;

        //    var points = dimConfig.Direction == DimensionFor.ForDown
        //        ? GroupPointsForX(dimConfig.Cluster.Points)
        //        : GroupPointsForY(dimConfig.Cluster.Points);

        //    if (points.Count < 2) return dims;

        //    double offsetBase = 5 * dimConfig.Scale;
        //    double minDist = 3 * dimConfig.Scale;

        //    for (int i = 0; i < points.Count - 1; i++)
        //    {
        //        var p1 = points[i];
        //        var p2 = points[i + 1];
        //        double dist = dimConfig.Direction == DimensionFor.ForDown
        //            ? System.Math.Abs(p2.X - p1.X)
        //            : System.Math.Abs(p2.Y - p1.Y);

        //        double offset = dist < minDist ? 2 * offsetBase : offsetBase;

        //        var dim = GetDimByTwoPoints(p1, p2, offset, dimConfig.Direction, true);
        //        dim.LayerId = dimConfig.LayerId;
        //        dims.Add(dim);
        //    }

        //    return dims;
        //}

    }
    

}
