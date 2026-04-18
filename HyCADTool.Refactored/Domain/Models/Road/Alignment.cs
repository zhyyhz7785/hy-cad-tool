using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 平面线位（道路中心线）。
    ///
    /// v1 仅作为"点集 + 派生几何"的载体，负责在 JSON 中持久化；
    /// 复杂参数化几何元素（直线 / 圆曲线 / 缓和曲线）放入 <see cref="Elements"/> 以便 v2 精确重建。
    ///
    /// 设计约束：
    /// - <see cref="Id"/> 稳定 GUID，对应 DWG Xdata 中的 <c>HY_ROAD_ID</c>，生命周期与 Domain 对象一致。
    /// - 领域层不引用 AutoCAD 类型；所有几何数据通过 <see cref="Polyline3D"/> / <see cref="Point3D"/> 描述。
    /// </summary>
    public sealed class Alignment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>图纸上的显示名称（用户可编辑）。例如 "K0 - K1.2 主线"。</summary>
        public string Name { get; set; }

        /// <summary>起始桩号，米。</summary>
        public double StartStation { get; set; }

        /// <summary>
        /// 几何采样点（3D，Z 一般为零，纵断面在 <see cref="Profile"/> 中叠加）。
        /// v1 用于直接驱动 AutoCAD 绘图；v2 Blender 可基于此建 NURBS。
        /// </summary>
        /// <remarks>
        /// <see cref="JsonPropertyAttribute.ObjectCreationHandling"/> 必须为 <see cref="ObjectCreationHandling.Replace"/>：
        /// 默认 <see cref="ObjectCreationHandling.Auto"/> 下，Newtonsoft 会 reuse 构造函数预先创建的空 <see cref="Polyline3D"/>
        /// 实例并尝试填充其属性；但 <see cref="Polyline3D.Vertices"/> 是 <c>IReadOnlyList</c> 无 public setter，
        /// 导致 JSON 中的顶点被静默丢弃。显式 Replace 强制调用 <c>[JsonConstructor]</c> 构造新实例。
        /// </remarks>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Polyline3D Centerline { get; set; } = new Polyline3D();

        /// <summary>
        /// 参数化几何元素链（v1 预留 + 可逐步填充）：直线 / 圆曲线 / 缓和曲线。
        /// 对 Civil 3D 等软件的 Alignment Entity 链的概念对应。
        /// </summary>
        public List<AlignmentElement> Elements { get; } = new List<AlignmentElement>();

        /// <summary>
        /// 纵断面集合（一个平面线位可以有多个纵断面方案，默认一个）。
        /// </summary>
        public List<Profile> Profiles { get; } = new List<Profile>();

        /// <summary>
        /// 输入来源快照；按 PI 创建 / 编辑链路写入，hyRoadA 拾取保持为 null。
        /// </summary>
        public AlignmentSource Source { get; set; }

        public override string ToString() => $"Alignment[{Name}, Id={Id:N}, Points={Centerline.VertexCount}]";

        /// <summary>
        /// 连续性自检（v1 轻量版）：
        /// 1. 中心线顶点数 ≥ 2；
        /// 2. 平面长度 &gt; 容差；
        /// 3. 顶点不重合（相邻点距离 &gt; 容差）。
        ///
        /// 返回 (ok, errors)：errors 为空表示通过；非空时每条对应一处问题，命令层可逐条 WriteMessage。
        /// </summary>
        public AlignmentValidationResult Validate(double tolerance = 1e-6)
        {
            var errors = new List<string>();

            if (Centerline == null || Centerline.VertexCount < 2)
            {
                errors.Add("中心线顶点不足 2 个，无法构成平面线位。");
                return new AlignmentValidationResult(false, errors);
            }

            double planar = Centerline.GetPlanarLength();
            if (planar <= tolerance)
            {
                errors.Add($"平面长度过短（{planar:E2} m ≤ 容差 {tolerance:E2} m），疑似 PI 全部重合。");
            }

            for (int i = 1; i < Centerline.VertexCount; i++)
            {
                var a = Centerline.GetPointAt(i - 1);
                var b = Centerline.GetPointAt(i);
                double dx = b.X - a.X;
                double dy = b.Y - a.Y;
                if (System.Math.Sqrt(dx * dx + dy * dy) < tolerance)
                {
                    errors.Add($"顶点[{i - 1}]→[{i}] 在 XY 平面内重合（距离 < {tolerance:E2} m）。");
                }
            }

            return new AlignmentValidationResult(errors.Count == 0, errors);
        }
    }

    /// <summary>
    /// <see cref="Alignment.Validate"/> 的结果。
    /// </summary>
    public readonly struct AlignmentValidationResult
    {
        public bool Ok { get; }
        public IReadOnlyList<string> Errors { get; }

        public AlignmentValidationResult(bool ok, IReadOnlyList<string> errors)
        {
            Ok = ok;
            Errors = errors ?? System.Array.Empty<string>();
        }
    }

    /// <summary>
    /// 平面几何元素类型。v1 先列出常见三种 + 未识别。
    /// </summary>
    public enum AlignmentElementKind
    {
        Unknown = 0,
        Line = 1,
        CircularArc = 2,
        Spiral = 3
    }

    /// <summary>
    /// 平面几何元素（直线 / 圆曲线 / 缓和曲线）。
    ///
    /// v1 仅存原始参数（起点 / 终点 / 半径 / 缓和曲线参数 A），v2 解析成精确数学模型供 Blender 曲线使用。
    /// </summary>
    public sealed class AlignmentElement
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public AlignmentElementKind Kind { get; set; } = AlignmentElementKind.Unknown;

        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }

        /// <summary>圆曲线半径（m）；Line / Spiral 为 <see cref="double.NaN"/>。</summary>
        public double Radius { get; set; } = double.NaN;

        /// <summary>缓和曲线参数 A（m）；非缓和曲线为 <see cref="double.NaN"/>。</summary>
        public double SpiralParameterA { get; set; } = double.NaN;

        /// <summary>本元素起点的累计桩号（m）。</summary>
        public double StartStation { get; set; }

        /// <summary>本元素长度（m）。</summary>
        public double Length { get; set; }
    }
}
