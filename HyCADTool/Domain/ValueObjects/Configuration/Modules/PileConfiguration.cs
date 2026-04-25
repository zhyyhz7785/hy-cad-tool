using System;
using HyCADTool.Domain.Entities.Pile;
using HyCADTool.Domain.Enums;

namespace HyCADTool.Domain.ValueObjects.Configuration.Modules
{
    /// <summary>
    /// 桩基配置值对象
    /// 对应旧代码中的 PileConfig
    /// </summary>
    public class PileConfiguration
    {
        /// <summary>
        /// 桩截面类型
        /// </summary>
        public PileSectionType Section { get; }

        /// <summary>
        /// 桩直径或边长 (mm)
        /// </summary>
        public double DiameterOrEdge { get; }

        /// <summary>
        /// 布置类型
        /// </summary>
        public PileArrangementType ArrangementType { get; }

        /// <summary>
        /// 桩布置率
        /// </summary>
        public double PileArrangeRate { get; }

        /// <summary>
        /// 边距 (上, 下, 左, 右) mm
        /// </summary>
        public (double Up, double Down, double Left, double Right) Margin { get; }

        /// <summary>
        /// 最小桩中心距 (mm)
        /// </summary>
        public double MinPileCenterDistance { get; }

        /// <summary>
        /// 输入位移率
        /// </summary>
        public double InputDisplacementRate { get; }

        /// <summary>
        /// 距轮廓距离 (mm)
        /// </summary>
        public double InputDistanceFromContour { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public PileConfiguration(
            PileSectionType section,
            double diameterOrEdge,
            PileArrangementType arrangementType,
            double pileArrangeRate,
            (double Up, double Down, double Left, double Right) margin,
            double minPileCenterDistance,
            double inputDisplacementRate,
            double inputDistanceFromContour)
        {
            if (diameterOrEdge < 200 || diameterOrEdge > 1200)
                throw new ArgumentException("桩直径或边长应在 200-1200mm 范围内", nameof(diameterOrEdge));

            if (pileArrangeRate < 0 || pileArrangeRate > 1)
                throw new ArgumentException("桩布置率应在 0-1 范围内", nameof(pileArrangeRate));

            if (minPileCenterDistance < 500 || minPileCenterDistance > 5000)
                throw new ArgumentException("最小桩中心距应在 500-5000mm 范围内", nameof(minPileCenterDistance));

            if (inputDisplacementRate < 0.01 || inputDisplacementRate > 0.1)
                throw new ArgumentException("输入位移率应在 0.01-0.1 范围内", nameof(inputDisplacementRate));

            Section = section;
            DiameterOrEdge = diameterOrEdge;
            ArrangementType = arrangementType;
            PileArrangeRate = pileArrangeRate;
            Margin = margin;
            MinPileCenterDistance = minPileCenterDistance;
            InputDisplacementRate = inputDisplacementRate;
            InputDistanceFromContour = inputDistanceFromContour;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static PileConfiguration CreateDefault()
        {
            return new PileConfiguration(
                section: PileSectionType.Circle,
                diameterOrEdge: 400.0,
                arrangementType: PileArrangementType.Rectangle,
                pileArrangeRate: 0.5,
                margin: (400, 400, 400, 400),
                minPileCenterDistance: 1200.0,
                inputDisplacementRate: 0.02,
                inputDistanceFromContour: 400.0
            );
        }

        /// <summary>
        /// 使用新的桩直径创建新配置
        /// </summary>
        public PileConfiguration WithDiameterOrEdge(double newDiameter)
        {
            return new PileConfiguration(
                Section, newDiameter, ArrangementType, PileArrangeRate,
                Margin, MinPileCenterDistance, InputDisplacementRate, InputDistanceFromContour);
        }
    }
}

