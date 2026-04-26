using System;
using HyCADTool.Shared.Geometry;
using Newtonsoft.Json;

namespace HyCADTool.Domain.Models.Road.ControlElements
{
    /// <summary>
    /// 参考线（ReferenceLine）—— 一种 <see cref="IHyControl"/>。
    ///
    /// <para><b>用途</b></para>
    /// 标记设计过程中的"轴 / 中心线投影 / 桩号虚线 / 限界线"；不参与 3D 导出。
    /// </summary>
    public sealed class ReferenceLine : IHyControl
    {
        /// <summary>Kind 常量。</summary>
        public const string KindConstant = "Control.ReferenceLine";

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Kind { get; set; } = KindConstant;

        public string Name { get; set; }

        /// <summary>
        /// 参考线的折线几何（无弧段）。Polyline3D 的多态 JSON 处理已成熟，本字段复用。
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Polyline3D Polyline { get; set; } = new Polyline3D();

        public bool IsTransient { get; set; }

        /// <summary>线型标签（"Dashed" / "Dotted" / "Solid"），绘图时映射到 AutoCAD 线型。</summary>
        public string LineStyle { get; set; } = "Dashed";

        public override string ToString()
            => $"ReferenceLine[{Name}, Id={Id:N}, V={Polyline?.VertexCount ?? 0}]";
    }
}
