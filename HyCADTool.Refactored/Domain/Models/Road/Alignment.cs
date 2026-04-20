using System;
using System.Collections.Generic;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
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
        /// 输入来源快照；<c>hyRoadAlnByPi</c> 写入完整 PI 表。
        /// <c>hyRoadA</c> 对纯直线多段线（无 bulge）会按顶点自动生成折线 PI 表；含弧段且无表时为 null。
        /// </summary>
        public AlignmentSource Source { get; set; }

        /// <summary>
        /// 桩号方程（Station Equations）列表，沿中心线从 BP 向 EP 按 <see cref="StationEquation.BeforeRaw"/>
        /// <b>严格升序</b> 排列。无方程时为空列表，显示桩号 = <see cref="StartStation"/> + rawFromBp。
        ///
        /// 新增 / 删除 / 清空由 <c>hyRoadAlnStaEq</c> 命令负责；<see cref="Domain.Services.Road.StationConverter"/>
        /// 负责 raw → display 的换算，所有下游（Sub-Entity 表 / 几何点标注 / 桩号标注 / PI 与 Frame 导出）
        /// 统一走换算后的显示桩号。
        /// </summary>
        [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<StationEquation> StationEquations { get; set; } = new List<StationEquation>();

        public override string ToString() => $"Alignment[{Name}, Id={Id:N}, Points={Centerline.VertexCount}]";

        /// <summary>
        /// 连续性自检（v1 轻量版，向后兼容入口）。
        /// 内部转发到 <see cref="ValidateContinuity"/>，仅暴露 <c>tolerance</c> 一个参数；
        /// 历史调用者（含 <c>RoadAlignmentService</c>、单元测试）保持原签名。
        ///
        /// 检查项：
        /// 1. 中心线顶点数 ≥ 2；
        /// 2. 平面长度 &gt; 容差；
        /// 3. 顶点不重合（相邻点距离 &gt; 容差）；
        /// 4. PI 偏转角合理（背折 / 共线 PI）；
        /// 5. <see cref="Elements"/> 一致性（单调桩号 / 端点连续 / 半径与缓和参数有效）；
        /// 6. <see cref="Elements"/> 累计末端桩号与中心线推算一致。
        ///
        /// 返回 (ok, errors)：errors 为空表示通过；非空时每条对应一处问题，命令层可逐条 WriteMessage。
        /// </summary>
        public AlignmentValidationResult Validate(double tolerance = 1e-6)
            => ValidateContinuity(new AlignmentValidationOptions { Tolerance = tolerance });

        /// <summary>
        /// 完整连续性自检（v1.1 升级版，A4 看板）。
        /// 与 <see cref="Validate"/> 共享前 3 项；额外覆盖：
        /// <list type="bullet">
        ///   <item>PI 偏转角检查：背折（&gt; <see cref="AlignmentValidationOptions.MaxPiDeflectionDeg"/>）/ 几乎共线（&lt; <see cref="AlignmentValidationOptions.MinPiDeflectionDeg"/>，可省略 PI 提示）。</item>
        ///   <item><see cref="Elements"/> 几何字段：<c>Length &gt; 0</c>、圆曲线 <c>Radius &gt; 0</c>、缓和曲线 <c>SpiralParameterA &gt; 0</c>。</item>
        ///   <item><see cref="Elements"/> 桩号链：<c>StartStation</c> 单调升序；相邻 <c>StartStation</c> 与上一段 <c>EndStation = StartStation + Length</c> 在 <see cref="AlignmentValidationOptions.StationConsistencyTolerance"/> 内一致。</item>
        ///   <item>桩号闭合：<see cref="StartStation"/> + 平面长度 ≈ 末段累计末端桩号（仅当 <see cref="Elements"/> 非空时才校核）。</item>
        /// </list>
        ///
        /// 设计原则：检查项为"读不变量 / 不写状态 / 不抛异常"；任何错误都堆到 <see cref="AlignmentValidationResult.Errors"/>。
        /// </summary>
        public AlignmentValidationResult ValidateContinuity(AlignmentValidationOptions options = null)
        {
            options = options ?? AlignmentValidationOptions.Default;
            double tolerance = options.Tolerance;
            var errors = new List<string>();

            // ---------- 1) 顶点数 ----------
            if (Centerline == null || Centerline.VertexCount < 2)
            {
                errors.Add("中心线顶点不足 2 个，无法构成平面线位。");
                return new AlignmentValidationResult(false, errors);
            }

            // ---------- 2) 平面长度 ----------
            double planar = Centerline.GetPlanarLength();
            if (planar <= tolerance)
            {
                errors.Add($"平面长度过短（{planar:E2} m ≤ 容差 {tolerance:E2} m），疑似 PI 全部重合。");
            }

            // ---------- 3) 相邻顶点不重合 ----------
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

            // ---------- 4) PI 偏转角 ----------
            // 偏转角 α = <入边方向 V1, 出边方向 V2> 之夹角，∈ [0°, 180°]：
            //   α ≈ 0°    → PI 几乎在直线上，可省略；
            //   α ≈ 90°   → 90° 急弯（默认仍允许，由 MaxPiDeflectionDeg 决定）；
            //   α ≈ 180°  → 背折（V1 与 V2 反向，几乎是反向折回原路）。
            for (int i = 1; i < Centerline.VertexCount - 1; i++)
            {
                var p0 = Centerline.GetPointAt(i - 1);
                var p1 = Centerline.GetPointAt(i);
                var p2 = Centerline.GetPointAt(i + 1);

                double v1x = p1.X - p0.X, v1y = p1.Y - p0.Y;
                double v2x = p2.X - p1.X, v2y = p2.Y - p1.Y;
                double n1 = System.Math.Sqrt(v1x * v1x + v1y * v1y);
                double n2 = System.Math.Sqrt(v2x * v2x + v2y * v2y);

                if (n1 < tolerance || n2 < tolerance) continue; // 已在 3) 报错

                double cosTheta = (v1x * v2x + v1y * v2y) / (n1 * n2);
                if (cosTheta > 1.0) cosTheta = 1.0;
                else if (cosTheta < -1.0) cosTheta = -1.0;
                double deflectionDeg = System.Math.Acos(cosTheta) * 180.0 / System.Math.PI;

                if (deflectionDeg > options.MaxPiDeflectionDeg)
                {
                    errors.Add($"PI[{i}] 偏转角 {deflectionDeg:F2}° 超过最大允许 {options.MaxPiDeflectionDeg:F2}°（疑似背折 / 急弯，请检查 PI 顺序）。");
                }
                else if (deflectionDeg < options.MinPiDeflectionDeg)
                {
                    errors.Add($"PI[{i}] 偏转角 {deflectionDeg:F2}° 小于最小推荐 {options.MinPiDeflectionDeg:F2}°（PI 几乎共线，可考虑省略此 PI）。");
                }
            }

            // ---------- 5) Elements 字段合法性 ----------
            if (Elements != null && Elements.Count > 0)
            {
                for (int i = 0; i < Elements.Count; i++)
                {
                    var el = Elements[i];
                    if (el == null)
                    {
                        errors.Add($"Element[{i}] 为 null。");
                        continue;
                    }

                    if (double.IsNaN(el.Length) || double.IsInfinity(el.Length) || el.Length <= 0)
                    {
                        errors.Add($"Element[{i}]({el.Kind}) Length = {el.Length}，必须 > 0 的有限值。");
                    }

                    if (el.Kind == AlignmentElementKind.CircularArc)
                    {
                        if (double.IsNaN(el.Radius) || double.IsInfinity(el.Radius) || el.Radius <= 0)
                        {
                            errors.Add($"Element[{i}](CircularArc) Radius = {el.Radius}，必须 > 0 的有限值。");
                        }
                    }
                    else if (el.Kind == AlignmentElementKind.Spiral)
                    {
                        if (double.IsNaN(el.SpiralParameterA) || double.IsInfinity(el.SpiralParameterA) || el.SpiralParameterA <= 0)
                        {
                            errors.Add($"Element[{i}](Spiral) SpiralParameterA = {el.SpiralParameterA}，必须 > 0 的有限值。");
                        }
                    }
                }

                // ---------- 5b) Elements 桩号链 ----------
                for (int i = 1; i < Elements.Count; i++)
                {
                    var prev = Elements[i - 1];
                    var cur = Elements[i];
                    if (prev == null || cur == null) continue;

                    if (cur.StartStation < prev.StartStation - options.StationConsistencyTolerance)
                    {
                        errors.Add($"Element[{i}] StartStation = {cur.StartStation:F3} m 小于上一段 StartStation = {prev.StartStation:F3} m（应单调升序）。");
                    }

                    if (prev.Length > 0)
                    {
                        double prevEnd = prev.StartStation + prev.Length;
                        if (System.Math.Abs(cur.StartStation - prevEnd) > options.StationConsistencyTolerance)
                        {
                            errors.Add($"Element[{i}] StartStation = {cur.StartStation:F3} m 与上一段 EndStation = {prevEnd:F3} m 不连续（差 {cur.StartStation - prevEnd:F3} m）。");
                        }
                    }
                }

                // ---------- 6) Elements 累计末端 vs 中心线 ----------
                var last = Elements[Elements.Count - 1];
                if (last != null && last.Length > 0)
                {
                    double expectedEnd = StartStation + planar;
                    double actualEnd = last.StartStation + last.Length;
                    if (System.Math.Abs(actualEnd - expectedEnd) > options.StationConsistencyTolerance)
                    {
                        errors.Add($"Elements 累计末端桩号 {actualEnd:F3} m 与中心线推算桩号 {expectedEnd:F3} m 不一致（差 {actualEnd - expectedEnd:F3} m）。");
                    }
                }
            }

            return new AlignmentValidationResult(errors.Count == 0, errors);
        }
    }

    /// <summary>
    /// <see cref="Alignment.ValidateContinuity"/> 的可调阈值集合。
    /// 字段全部走带默认值的可写属性，便于命令层 / 测试以"对象初始化器"语法定制。
    ///
    /// 默认值的取值依据：
    /// <list type="bullet">
    ///   <item><see cref="Tolerance"/> = 1e-6 m，对应 1 微米，足够区分 hyRoadA 拾取的 PI 重合误差。</item>
    ///   <item><see cref="MinPiDeflectionDeg"/> = 1°，与 CJJ 37-2012 推荐"PI 偏转角小于 1° 时可视为直线"一致。</item>
    ///   <item><see cref="MaxPiDeflectionDeg"/> = 175°，留 5° 容差以避开浮点误差，足以拦住典型"反向折回"误操作。</item>
    ///   <item><see cref="StationConsistencyTolerance"/> = 0.01 m，对应 1 cm，与 hyRoadAlnByPi 的桩号舍入精度一致。</item>
    /// </list>
    /// </summary>
    public sealed class AlignmentValidationOptions
    {
        public double Tolerance { get; set; } = 1e-6;
        public double MinPiDeflectionDeg { get; set; } = 1.0;
        public double MaxPiDeflectionDeg { get; set; } = 175.0;
        public double StationConsistencyTolerance { get; set; } = 0.01;

        /// <summary>每次新建一个独立实例，避免共享可变默认值。</summary>
        public static AlignmentValidationOptions Default => new AlignmentValidationOptions();
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
