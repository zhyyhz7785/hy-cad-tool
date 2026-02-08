using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.ValueObjects.Reinforcement
{
    /// <summary>
    /// 配筋生成的完整结果（平台无关）
    /// 封装线钢筋、点钢筋、标注位置等全部中间和最终结果
    /// </summary>
    public class ReinforcementResult
    {
        /// <summary>
        /// 混凝土边界（闭合多段线）
        /// </summary>
        public Polyline2D Boundary { get; set; }

        /// <summary>
        /// 分段后的钢筋（按轮廓角度分段）
        /// </summary>
        public Polyline2D[] SubReinforcements { get; set; }

        /// <summary>
        /// 添加锚固后的钢筋
        /// </summary>
        public Polyline2D[] SubReinforcementWithAnchors { get; set; }

        /// <summary>
        /// 添加锚固和弯钩后的最终钢筋
        /// </summary>
        public Polyline2D[] FinalReinforcements { get; set; }

        /// <summary>
        /// 每段钢筋端头是否弯折的标记
        /// Key: 1=起点, 2=终点; Value: true=弯折
        /// </summary>
        public List<Dictionary<int, bool>> BendingFlags { get; set; }

        /// <summary>
        /// 点钢筋中心偏移多段线
        /// </summary>
        public Polyline2D DotReinCenterPoly { get; set; }

        /// <summary>
        /// 点钢筋位置
        /// </summary>
        public Point2D[] DotReinPoints { get; set; }

        /// <summary>
        /// 点钢筋圆的多段线表示
        /// </summary>
        public Polyline2D[] DotReinPolys { get; set; }

        /// <summary>
        /// 减少数量的点钢筋位置
        /// </summary>
        public Point2D[] ReduceDotReinPoints { get; set; }

        /// <summary>
        /// 减少数量的点钢筋圆
        /// </summary>
        public Polyline2D[] ReduceDotReinPolys { get; set; }

        /// <summary>
        /// 标注数据（每个标注的多点位置和内容）
        /// </summary>
        public MLeaderData[] MLeaders { get; set; }
    }

    /// <summary>
    /// 标注数据（平台无关）
    /// 包含标注所需的点位置和文字内容
    /// </summary>
    public class MLeaderData
    {
        /// <summary>
        /// 标注锚点（钢筋上的点）
        /// </summary>
        public Point2D[] AnchorPoints { get; set; }

        /// <summary>
        /// 引线终点偏移距离
        /// </summary>
        public double LeaderDistance { get; set; }

        /// <summary>
        /// 标注文字内容
        /// </summary>
        public string Content { get; set; }
    }
}
