using NetTopologySuite.Geometries;
using System;
namespace HyCADTool.HelpClass
{
    #region 枚举
    // 枚举：截面类型
    public enum PileSectionType
    {
        Circle,
        Square
    }
    // 枚举：桩类型
    public enum PileType
    {
        Corner,
        Edge,
        Middle
    }
    // 枚举：桩布置方式
    public enum PileArrangementType
    {
        Rectangle,  // 长方形布置
        Circular    // 梅花型布置
    }
    #endregion
    // 基础桩类
    public class Pile
    {
        public int Id { get; set; }
        public Point Center { get; set; }  // 形心坐标, 使用 NetTopologySuite 的 Point
        public PileSectionType Section { get; set; } // 截面形式
        public double DiameterOrEdge { get; set; } // 圆形直径
        public PileType PileType { get; set; } // 角桩、边桩、中桩类型
        public bool IsInitialized { get; set; } = false;  // 是否初始化
        // 计算得到的桩面积
        public double PileArea { get; set; }
        // 构造函数，用户输入截面类型、边长或直径和桩类型
        public Pile(PileSectionType section, double diameterOrEdge)
        {
            // 初始化用户输入的值
            Section = section;
            DiameterOrEdge = diameterOrEdge;
            // 根据桩的截面类型和桩类型自动计算面积
            CalculatePileArea();
        }
        // 计算桩的面积
        private void CalculatePileArea()
        {
            if (Section == PileSectionType.Circle)  // 如果是圆形截面
            {
                // 圆形面积计算：π * (直径/2)^2
                PileArea = Math.PI * Math.Pow(DiameterOrEdge / 2, 2);
            }
            else if (Section == PileSectionType.Square)  // 如果是方形截面（X方向和Y方向的边长）
            {
                // 如果XEdge和YEdge都不为0，计算方形的面积
                if (DiameterOrEdge > 0)
                {
                    PileArea = DiameterOrEdge * DiameterOrEdge;
                }
                else
                {
                    throw new InvalidOperationException("For square section, both XEdge and YEdge must be specified.");
                }
            }
        }
    }
}
