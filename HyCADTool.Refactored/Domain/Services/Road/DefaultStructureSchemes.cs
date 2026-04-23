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

        /// <summary>机动车道默认：多面层 + 多基层 + 单垫层（rCs v2 用户约定）。</summary>
        public static StructureLayerScheme CreatePavement() => CreateMotorizedFamily("机动车道(默认)");

        /// <summary>非机动车道默认：与机动车道同构。</summary>
        public static StructureLayerScheme CreateNonMotor() => CreateMotorizedFamily("非机动车道(默认)");

        /// <summary>人行道默认：与机动车道同构（用户要求「同机动车道」）。</summary>
        public static StructureLayerScheme CreateSidewalk() => CreateMotorizedFamily("人行道(默认)");

        private static StructureLayerScheme CreateMotorizedFamily(string schemeName)
            => new StructureLayerScheme
            {
                Name = schemeName,
                IsBuiltIn = true,
                Layers = new List<StructureLayer>
                {
                    S("细粒式SBS改性沥青混凝土(AC-13C)", StructureLayerKind.Surface, 4, "ANSI37"),
                    S("粘层油(PC-3)", StructureLayerKind.Surface, 0, "ANSI36"),
                    S("中粒式沥青混凝土(AC-16C)", StructureLayerKind.Surface, 8, "ANSI37"),
                    S("粗粒式沥青混凝土AC-25C", StructureLayerKind.Surface, 8, "ANSI37"),
                    S("乳化沥青稀浆封层(ES-3)", StructureLayerKind.Surface, 1, "AR-CONC"),
                    S("透层油(AL(M)-2)", StructureLayerKind.Surface, 0, "AR-CONC"),
                    B("水泥稳定碎石(5.0%)", 18, "ANSI31"),
                    B("水泥稳定碎石(4.5%)", 18, "ANSI31"),
                    B("14%灰土", 15, "AR-SAND"),
                    B("12%灰土", 15, "AR-SAND"),
                    new StructureLayer
                    {
                        Name = "路基处理8%灰土",
                        LayerKind = StructureLayerKind.Subbase,
                        ThicknessCm = 15,
                        FillMaterial = "路基处理8%灰土",
                        PatternName = "AR-SAND",
                    },
                },
            };

        private static StructureLayer S(string name, StructureLayerKind kind, double cm, string pattern)
        {
            return new StructureLayer
            {
                Name = name,
                LayerKind = kind,
                ThicknessCm = cm,
                FillMaterial = name,
                PatternName = pattern,
            };
        }

        private static StructureLayer B(string name, double cm, string pattern) =>
            new StructureLayer
            {
                Name = name,
                LayerKind = StructureLayerKind.Base,
                ThicknessCm = cm,
                FillMaterial = name,
                PatternName = pattern,
            };
    }
}
