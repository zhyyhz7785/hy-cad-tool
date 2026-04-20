using System;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 结构层类型（面层 / 基层 / 垫层 / 自定义）。
    /// <para>对应用户图 7「结构层方案」TreeView 的二级分组。</para>
    /// </summary>
    public enum StructureLayerKind
    {
        /// <summary>自定义，默认。</summary>
        Custom = 0,

        /// <summary>面层（直接与车辆接触的层：细粒式沥青 / 水泥混凝土面层）。</summary>
        Surface = 1,

        /// <summary>基层（水泥稳定碎石 / 二灰碎石）。</summary>
        Base = 2,

        /// <summary>垫层（砂垫层 / 石灰土）。</summary>
        Subbase = 3,
    }

    /// <summary>
    /// 结构层（Structure Layer）—— 道路板块的分层构造单元（M6/M7）。
    ///
    /// <para><b>对应图 7</b></para>
    /// 每条层条目含字段：名称 / 描述 / 类型 / 厚度 / 左右加宽 / 左右坡度 / 填料 / 填充符号。
    ///
    /// <para><b>M6 阶段</b></para>
    /// 本类仅作为 JSON Schema v1.2.0 的字段预留。完整 UI 驱动由 M7 落实。
    /// </summary>
    public sealed class StructureLayer : IHyEntity
    {
        public const string KindConstant = "StructureLayer";

        public Guid Id { get; set; } = Guid.NewGuid();

        string IHyEntity.Kind => KindConstant;

        /// <summary>层名称（例："细粒式沥青混凝土"）。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>类型（面层 / 基层 / 垫层 / 自定义）。</summary>
        public StructureLayerKind LayerKind { get; set; } = StructureLayerKind.Custom;

        /// <summary>厚度（cm）。必须 &gt; 0。</summary>
        public double ThicknessCm { get; set; }

        /// <summary>左侧加宽（cm）。默认 0。</summary>
        public double LeftWidenCm { get; set; }

        /// <summary>右侧加宽（cm）。默认 0。</summary>
        public double RightWidenCm { get; set; }

        /// <summary>
        /// 左侧坡度（1:n 中的 n）。默认 0 表示垂直边。
        /// 图 7 的输入形式是 "1:n"。
        /// </summary>
        public double LeftSlope { get; set; }

        /// <summary>右侧坡度（1:n 中的 n）。默认 0 表示垂直边。</summary>
        public double RightSlope { get; set; }

        /// <summary>填料名称（例："细粒式沥青混凝土"、"水泥稳定碎石"）。</summary>
        public string FillMaterial { get; set; }

        /// <summary>AutoCAD 填充符号模式名（对应 Hatch PatternName，例："ANSI31"、"AR-CONC"）。</summary>
        public string PatternName { get; set; }

        /// <summary>可选的描述。</summary>
        public string Description { get; set; }

        public override string ToString()
            => $"StructureLayer[{Name}({LayerKind}) h={ThicknessCm}cm]";
    }
}
