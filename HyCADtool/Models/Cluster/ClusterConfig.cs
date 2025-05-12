using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace HyCADTool.Models.Cluster
{
    /// <summary>
    /// 聚类配置类
    /// </summary>
    public class ClusterConfig
    {
        /// <summary>X方向上的聚类距离</summary>
        public double EpsilonX { get; set; } = 2000.0;
        /// <summary>Y方向上的聚类距离</summary>
        public double EpsilonY { get; set; } = 800.0;
        /// <summary>聚类最小点数</summary>
        public int MinPoints { get; set; } = 3;
        /// <summary>聚类轮廓图层名称</summary>
        public string ClusterLayerName { get; set; } = "00_hy_基础_聚类轮廓";
        /// <summary>聚类轮廓图层ID</summary>
        public ObjectId ClusterLayerId { get; set; } = ObjectId.Null;
    }
    /// <summary>
    /// 标注参数配置项，用于聚类标注控制
    /// </summary>
    public class ClusterDimOptions
    {       
        /// <summary>最小标注间距（默认 3 × scale）</summary>
        public double MinSpacing => 3 * Scale;
        /// <summary>标注线偏移高度（默认 5 × scale）</summary>
        public double Offset => 5 * Scale;
        /// <summary>Y 向最大标注批次偏移（默认 3 层 × Offset）</summary>
        public double MaxOffsetY => 3 * Offset;
        /// <summary>X 向最大标注批次偏移</summary>
        public double MaxOffsetX => 3 * Offset;
        /// <summary>
        /// X方向标注是否转为上方（默认false：下方）
        /// </summary>
        public bool XDirectionIsUp { get; set; } = false;
        /// <summary>
        /// Y方向标注是否转为右侧（默认false：左侧）
        /// </summary>
        public bool YDirectionIsRight { get; set; } = false;
        /// <summary>
        /// 比例因子（控制偏移量）
        /// </summary>
        public double Scale { get; set; } = BaseConfig.Scale;
        /// <summary>
        /// 过滤重复标注的距离容差
        /// </summary>
        public double DistanceThreshold { get; set; } = 6000.0;
    }
}
