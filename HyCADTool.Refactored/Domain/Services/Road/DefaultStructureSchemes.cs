using System.Collections.Generic;
using HyCADTool.Refactored.Domain.Models.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 按 <see cref="TemplateComponentKind"/> 返回默认「面层 / 基层 / 垫层」结构层方案。
    ///
    /// <para>
    /// rCs（横断面绘制）面板 v2：当用户把条带 Kind 切到"承载结构"类型（机动车道 / 非机动车道 / 人行道）
    /// 时，<c>BandRowViewModel</c> 在首次无方案时从本工厂注入一份默认 <see cref="StructureLayerScheme"/>；
    /// 切到绿化带 / 中分带 / 缘石等"不承载结构"类型时方案保持为 null（UI 上也不显示"路面结构层"Expander）。
    /// </para>
    ///
    /// <para>
    /// 各 Kind 的"默认厚度 / 填料 / 填充符号"仅作为首次创建的便利起点，用户可在属性面板里随意覆写。
    /// 数值参考《CJJ 37-2012 城市道路设计规范》与现行常用工程做法，**非规范强制**。
    /// </para>
    /// </summary>
    public static class DefaultStructureSchemes
    {
        /// <summary>
        /// 返回给定 Kind 的默认结构层方案；<see cref="TemplateComponentKind.GreenStrip"/> /
        /// <see cref="TemplateComponentKind.MedianStrip"/> / <see cref="TemplateComponentKind.Kerb"/> /
        /// <see cref="TemplateComponentKind.Shoulder"/> / <see cref="TemplateComponentKind.Slope"/>
        /// 返回 <c>null</c>（不承载结构）。
        /// </summary>
        public static StructureLayerScheme For(TemplateComponentKind kind)
        {
            switch (kind)
            {
                case TemplateComponentKind.Pavement:
                    return CreatePavement();
                case TemplateComponentKind.NonMotorized:
                    return CreateNonMotor();
                case TemplateComponentKind.Sidewalk:
                    return CreateSidewalk();
                default:
                    return null;
            }
        }

        /// <summary>机动车道默认：面层×1(4cm) + 基层×2(20+20cm) + 垫层×1(15cm)。</summary>
        public static StructureLayerScheme CreatePavement()
            => new StructureLayerScheme
            {
                Name = "机动车道(默认)",
                IsBuiltIn = true,
                Layers = new List<StructureLayer>
                {
                    new StructureLayer { Name = "细粒式沥青混凝土", LayerKind = StructureLayerKind.Surface, ThicknessCm = 4, FillMaterial = "细粒式沥青混凝土", PatternName = "ANSI37" },
                    new StructureLayer { Name = "水泥稳定碎石基层", LayerKind = StructureLayerKind.Base, ThicknessCm = 20, FillMaterial = "水泥稳定碎石", PatternName = "ANSI31" },
                    new StructureLayer { Name = "水泥稳定碎石下基层", LayerKind = StructureLayerKind.Base, ThicknessCm = 20, FillMaterial = "水泥稳定碎石", PatternName = "ANSI31" },
                    new StructureLayer { Name = "天然砂砾垫层", LayerKind = StructureLayerKind.Subbase, ThicknessCm = 15, FillMaterial = "天然砂砾", PatternName = "AR-SAND" },
                },
            };

        /// <summary>非机动车道默认：面层×1(3cm) + 基层×1(15cm) + 垫层×1(10cm)。</summary>
        public static StructureLayerScheme CreateNonMotor()
            => new StructureLayerScheme
            {
                Name = "非机动车道(默认)",
                IsBuiltIn = true,
                Layers = new List<StructureLayer>
                {
                    new StructureLayer { Name = "细粒式沥青混凝土", LayerKind = StructureLayerKind.Surface, ThicknessCm = 3, FillMaterial = "细粒式沥青混凝土", PatternName = "ANSI37" },
                    new StructureLayer { Name = "水泥稳定碎石", LayerKind = StructureLayerKind.Base, ThicknessCm = 15, FillMaterial = "水泥稳定碎石", PatternName = "ANSI31" },
                    new StructureLayer { Name = "天然砂砾垫层", LayerKind = StructureLayerKind.Subbase, ThicknessCm = 10, FillMaterial = "天然砂砾", PatternName = "AR-SAND" },
                },
            };

        /// <summary>人行道默认：面层×1(6cm 人行道砖) + 基层×1(10cm) + 垫层×1(5cm)。</summary>
        public static StructureLayerScheme CreateSidewalk()
            => new StructureLayerScheme
            {
                Name = "人行道(默认)",
                IsBuiltIn = true,
                Layers = new List<StructureLayer>
                {
                    new StructureLayer { Name = "人行道砖", LayerKind = StructureLayerKind.Surface, ThicknessCm = 6, FillMaterial = "人行道砖", PatternName = "AR-BRELM" },
                    new StructureLayer { Name = "水泥砂浆找平", LayerKind = StructureLayerKind.Base, ThicknessCm = 10, FillMaterial = "水泥砂浆", PatternName = "AR-CONC" },
                    new StructureLayer { Name = "天然砂砾垫层", LayerKind = StructureLayerKind.Subbase, ThicknessCm = 5, FillMaterial = "天然砂砾", PatternName = "AR-SAND" },
                },
            };
    }
}
