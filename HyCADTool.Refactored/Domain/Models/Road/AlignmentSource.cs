using System.Collections.Generic;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 平面线位的"输入来源"快照。仅用于按 PI 创建 / 编辑链路（hyRoadAlnByPi / hyRoadAlnEditPi）；
    /// 拾取已有 Polyline 创建的 Alignment（hyRoadA）保持 <c>Source = null</c>，避免误导。
    ///
    /// 持久化用途：
    /// - 让 hyRoadAlnEditPi 命令拿到原始 PI 表（含每点 R / Ls_in / Ls_out / Tag），无需从几何反解；
    /// - 后续 v2 Blender 联调可直接用此结构在 Blender 内重建参数化曲线。
    /// </summary>
    public sealed class AlignmentSource
    {
        /// <summary>
        /// 来源种类（v1 仅区分 PI 与未知；为后续 LandXML / 曲线法 留扩展位）。
        /// </summary>
        public AlignmentSourceKind Kind { get; set; } = AlignmentSourceKind.PiTable;

        /// <summary>PI 元素列表（含首尾点）。</summary>
        public List<AlignmentPiInput> PiElements { get; set; } = new List<AlignmentPiInput>();
    }

    public enum AlignmentSourceKind
    {
        Unknown = 0,
        PiTable = 1,
    }

    /// <summary>
    /// 单个 PI 输入项的 JSON 持久化形式（与 <c>Domain.Services.Road.PiElement</c> 同构，但适合 Newtonsoft 序列化）。
    /// 命名故意区分以避免 Domain.Services 层的内部 readonly struct 暴露给 JSON。
    /// </summary>
    public sealed class AlignmentPiInput
    {
        public Point2D P { get; set; }
        public double Radius { get; set; }
        public double SpiralIn { get; set; }
        public double SpiralOut { get; set; }
        public string Tag { get; set; }
    }
}
