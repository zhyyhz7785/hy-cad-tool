using System;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.Models.Road.ControlElements
{
    /// <summary>
    /// 参考面（ReferencePlane）—— 一种 <see cref="IHyControl"/>。
    ///
    /// <para><b>用途</b></para>
    /// 标注"标高面 / 设计参考面 / 预留施工面"；v1 平面图上仅画出该面与地平面的交线（2 点），
    /// 并用旋转角表示面的水平朝向。不参与 3D 导出。
    ///
    /// <para><b>数据</b></para>
    /// 用"基准点 + 法向向量"（面的最小数学定义，7 个浮点）；
    /// 平面绘制时根据 <see cref="Origin"/>/<see cref="Normal"/> 计算出交线区段。
    /// </summary>
    public sealed class ReferencePlane : IHyControl
    {
        /// <summary>Kind 常量。</summary>
        public const string KindConstant = "Control.ReferencePlane";

        public Guid Id { get; set; } = Guid.NewGuid();

        public string Kind { get; set; } = KindConstant;

        public string Name { get; set; }

        /// <summary>面上的一个基准点。</summary>
        public Point3D Origin { get; set; }

        /// <summary>
        /// 面法向（世界坐标，单位向量建议；若传非单位向量，使用方自己归一化）。
        /// 水平参考面取 (0,0,1)；沿 Alignment 方向的参考面取相应的水平法向。
        /// </summary>
        public Vector3D Normal { get; set; } = new Vector3D(0, 0, 1);

        /// <summary>
        /// 面的显示尺寸（米），用于平面图上画出该面在 AutoCAD 视图上的投影范围。
        /// 默认 10m × 10m。
        /// </summary>
        public double DisplaySizeMeters { get; set; } = 10.0;

        public bool IsTransient { get; set; }

        public override string ToString()
            => $"ReferencePlane[{Name}, Id={Id:N}, Origin=({Origin.X:F2},{Origin.Y:F2},{Origin.Z:F2})]";
    }
}
