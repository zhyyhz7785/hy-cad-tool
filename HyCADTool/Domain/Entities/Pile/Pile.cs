using HyCADTool.Shared.Geometry;
using System;

namespace HyCADTool.Domain.Entities.Pile
{
    /// <summary>
    /// 桩实体（Pile Entity）
    /// 表示基础工程中的桩
    /// </summary>
    public class Pile
    {
        /// <summary>
        /// 桩 ID（Pile Identifier）
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 桩中心坐标（Pile Center Point）
        /// </summary>
        public Point2D Center { get; set; }

        /// <summary>
        /// 截面类型（Section Type）
        /// </summary>
        public PileSectionType Section { get; set; }

        /// <summary>
        /// 圆形直径或方形边长（Diameter or Edge Length）
        /// </summary>
        public double DiameterOrEdge { get; set; }

        /// <summary>
        /// 桩类型（Pile Type）- 角桩、边桩、中桩
        /// </summary>
        public PileType PileType { get; set; }

        /// <summary>
        /// 是否已初始化（Is Initialized）
        /// </summary>
        public bool IsInitialized { get; set; }

        /// <summary>
        /// 桩面积（Pile Area）
        /// 根据截面类型和尺寸自动计算
        /// </summary>
        public double PileArea { get; private set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="section">截面类型</param>
        /// <param name="diameterOrEdge">圆形直径或方形边长</param>
        public Pile(PileSectionType section, double diameterOrEdge)
        {
            Section = section;
            DiameterOrEdge = diameterOrEdge;
            CalculatePileArea();
        }

        /// <summary>
        /// 计算桩面积（Calculate Pile Area）
        /// </summary>
        private void CalculatePileArea()
        {
            if (Section == PileSectionType.Circle)
            {
                // 圆形面积：π * r²
                PileArea = Math.PI * Math.Pow(DiameterOrEdge / 2, 2);
            }
            else if (Section == PileSectionType.Square)
            {
                if (DiameterOrEdge > 0)
                {
                    // 方形面积：边长²
                    PileArea = DiameterOrEdge * DiameterOrEdge;
                }
                else
                {
                    throw new ArgumentException("方形桩的边长必须大于 0");
                }
            }
        }

        /// <summary>
        /// 更新桩尺寸并重新计算面积
        /// </summary>
        /// <param name="newDiameterOrEdge">新的直径或边长</param>
        public void UpdateSize(double newDiameterOrEdge)
        {
            if (newDiameterOrEdge <= 0)
                throw new ArgumentException("桩尺寸必须大于 0");

            DiameterOrEdge = newDiameterOrEdge;
            CalculatePileArea();
        }

        /// <summary>
        /// 转换为字符串（用于调试）
        /// </summary>
        public override string ToString()
        {
            // Point2D 是 struct（值类型），不会为 null
            return $"Pile {Id}: {Section} at ({Center.X:F2}, {Center.Y:F2}), Area = {PileArea:F2}";
        }
    }
}

