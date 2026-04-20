using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
{
    /// <summary>
    /// 三维多段线值对象（平台无关）。
    /// 用于道路平面线位 / 纵断面 / 走廊拟合线等 3D 连续曲线。
    /// 对 AutoCAD 的 Polyline3d 不产生直接依赖，便于后续 Blender / Lumion 流水线消费。
    ///
    /// 弧段支持（P1.b 扩展）：
    /// - 每个顶点挂一个 <see cref="Bulges"/> 值，表示"该顶点 → 下一顶点"那段的凸度，
    ///   语义与 AutoCAD 2D <c>Polyline.GetBulgeAt(i)</c> 完全一致：bulge = tan(θ/4)，
    ///   θ 为弧段圆心角，顺时针为负。
    /// - 开放 polyline 的末端顶点 bulge 无意义，约定存 0。
    /// - 闭合 polyline 的末端 bulge 表示"顶点[N-1] → 顶点[0]"那段。
    /// - <see cref="Bulges"/> 长度恒等于 <see cref="VertexCount"/>；构造时若传入长度不匹配会自动补 0 / 截断。
    /// - 全 0 时 JSON 序列化会省略该字段（<see cref="ShouldSerializeBulges"/>），向后兼容老 roaddesign.json。
    /// </summary>
    public class Polyline3D
    {
        private readonly List<Point3D> _vertices;
        private readonly List<double> _bulges;

        public IReadOnlyList<Point3D> Vertices => _vertices.AsReadOnly();

        /// <summary>
        /// 每个顶点的 bulge 值（tan(θ/4)）；长度 = <see cref="VertexCount"/>。
        /// 0 表示该顶点到下一顶点之间是直线段。
        /// </summary>
        public IReadOnlyList<double> Bulges => _bulges.AsReadOnly();

        public bool IsClosed { get; set; }

        [JsonIgnore]
        public int VertexCount => _vertices.Count;

        /// <summary>
        /// 对第 <paramref name="segmentIndex"/> 段圆弧（bulge≠0），求两侧切线无限延长线的交点（市政导线 PI），
        /// 以及该段圆弧半径（米，正值）。
        /// 用于从 LWPOLYLINE 反推道路 PI 表；直线段返回 false。
        /// </summary>
        public bool TryGetArcTangentIntersectionPi(int segmentIndex, out Point2D pi, out double radiusAbs)
        {
            pi = default;
            radiusAbs = 0;
            if (segmentIndex < 0 || segmentIndex >= SegmentCount) return false;
            double bulge = _bulges[segmentIndex];
            if (Math.Abs(bulge) < BulgeEpsilon) return false;

            var seg = GetSegmentAt(segmentIndex);
            ComputeArcGeometry(seg.Start, seg.End, bulge,
                out _, out _, out double radius,
                out double startAngle, out double sweep);

            radiusAbs = Math.Abs(radius);
            if (radiusAbs < BulgeEpsilon) return false;

            // 与 TangentOnSegment(segmentIndex, t) 一致：切向 = 半径方向按 sweep 旋转 90°。
            double a0 = startAngle;
            double a1 = startAngle + sweep;
            var radial0 = new Vector2D(Math.Cos(a0), Math.Sin(a0));
            var radial1 = new Vector2D(Math.Cos(a1), Math.Sin(a1));
            var t0 = sweep >= 0 ? radial0.Perpendicular() : -radial0.Perpendicular();
            var t1 = sweep >= 0 ? radial1.Perpendicular() : -radial1.Perpendicular();
            if (!t0.TryNormalize(out var d0) || !t1.TryNormalize(out var d1))
                return false;

            var p0 = new Point2D(seg.Start.X, seg.Start.Y);
            var p1 = new Point2D(seg.End.X, seg.End.Y);
            return TryIntersectLines2D(p0, d0, p1, d1, out pi);
        }

        /// <summary>
        /// 任一 bulge 非零即视为"含弧段"。</summary>
        [JsonIgnore]
        public bool HasArcs
        {
            get
            {
                for (int i = 0; i < _bulges.Count; i++)
                {
                    if (Math.Abs(_bulges[i]) > BulgeEpsilon) return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 主构造函数 + JSON 反序列化入口（<see cref="JsonConstructorAttribute"/>）。
        /// Newtonsoft 需要参数名（大小写不敏感）与属性名一致：
        /// <c>vertices</c> ↔ <c>Vertices</c>，<c>bulges</c> ↔ <c>Bulges</c>。
        /// 老 JSON 没有 bulges 字段时会传入 null，内部自动补齐为全 0。
        /// </summary>
        [JsonConstructor]
        public Polyline3D(IEnumerable<Point3D> vertices, bool isClosed = false, IEnumerable<double> bulges = null)
        {
            _vertices = vertices != null
                ? new List<Point3D>(vertices)
                : throw new ArgumentNullException(nameof(vertices));

            _bulges = bulges != null
                ? new List<double>(bulges)
                : new List<double>();

            NormalizeBulges();

            IsClosed = isClosed;
        }

        public Polyline3D(bool isClosed = false)
        {
            _vertices = new List<Point3D>();
            _bulges = new List<double>();
            IsClosed = isClosed;
        }

        public void AddVertex(Point3D point, double bulge = 0)
        {
            _vertices.Add(point);
            _bulges.Add(bulge);
        }

        public void AddVertexAt(int index, Point3D point, double bulge = 0)
        {
            if (index < 0 || index > _vertices.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            _vertices.Insert(index, point);
            _bulges.Insert(index, bulge);
        }

        public Point3D GetPointAt(int index) => _vertices[index];

        public double GetBulgeAt(int index) => _bulges[index];

        [JsonIgnore]
        public int SegmentCount
        {
            get
            {
                if (_vertices.Count < 2) return 0;
                return IsClosed ? _vertices.Count : _vertices.Count - 1;
            }
        }

        public (Point3D Start, Point3D End) GetSegmentAt(int index)
        {
            int segCount = SegmentCount;
            if (index < 0 || index >= segCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            int nextIndex = (index + 1) % _vertices.Count;
            return (_vertices[index], _vertices[nextIndex]);
        }

        /// <summary>
        /// 3D 总长度。对于含弧段的分段，采用"Z 沿弧长均匀变化"近似：
        /// L = sqrt(arcXY² + ΔZ²)。
        /// 道路中心线 ΔZ ≪ 弧长时误差可忽略（工程够用，P2 桩号精化时再替换为精确积分）。
        /// </summary>
        public double GetTotalLength()
        {
            double total = 0;
            for (int i = 0; i < SegmentCount; i++)
            {
                var seg = GetSegmentAt(i);
                double dx = seg.End.X - seg.Start.X;
                double dy = seg.End.Y - seg.Start.Y;
                double dz = seg.End.Z - seg.Start.Z;
                double chordXY = Math.Sqrt(dx * dx + dy * dy);
                double arcXY = ArcLengthFromChord(chordXY, _bulges[i]);
                total += Math.Sqrt(arcXY * arcXY + dz * dz);
            }
            return total;
        }

        public Polyline3D Clone()
        {
            return new Polyline3D(new List<Point3D>(_vertices), IsClosed, new List<double>(_bulges));
        }

        /// <summary>
        /// 仅投影到 XY 平面的总长度（忽略 Z 分量），用于按桩号换算。
        /// 含弧段时按 bulge 计算平面弧长。
        /// </summary>
        public double GetPlanarLength()
        {
            double total = 0;
            for (int i = 0; i < SegmentCount; i++)
            {
                var seg = GetSegmentAt(i);
                double dx = seg.End.X - seg.Start.X;
                double dy = seg.End.Y - seg.Start.Y;
                double chord = Math.Sqrt(dx * dx + dy * dy);
                total += ArcLengthFromChord(chord, _bulges[i]);
            }
            return total;
        }

        // ========================================================
        //  桩号采样（P1 hyRoadAlnStation 使用）
        //  约定：station 参数为"沿 XY 平面累计弧长"，与 AutoCAD/Civil 3D 桩号语义一致，
        //  Z 值在命中分段内按弦长比例线性插值（道路中心线纵坡极小，足够工程精度；
        //  精确纵断面由 Profile 模块接管）。
        // ========================================================

        /// <summary>
        /// 给定平面桩号，返回对应的三维点。
        /// - <paramref name="station"/> 超出 [0, GetPlanarLength()] 时自动 clamp 到端点，不抛异常（符合现场"略微越界"容忍）。
        /// - 含弧段时在圆弧上精确采样；直线段直接按比例插值。
        /// </summary>
        public Point3D PointAtPlanarStation(double station)
        {
            ResolvePlanarStation(station, out int segIndex, out double t, out _);
            return InterpolatePointOnSegment(segIndex, t);
        }

        /// <summary>
        /// 给定平面桩号，返回对应点处的 XY 平面单位切线方向（行进方向）。
        /// - 直线段切线 = 弦方向。
        /// - 圆弧段切线 = 起点切向绕圆心按已走过的圆心角旋转后的方向，顺/逆时针由 bulge 符号决定。
        /// - 退化情形（重合点 / 零向量）返回 <see cref="Vector2D.UnitX"/>，调用方可按需处理。
        /// </summary>
        public Vector2D TangentAtPlanarStation(double station)
        {
            ResolvePlanarStation(station, out int segIndex, out double t, out _);
            return TangentOnSegment(segIndex, t);
        }

        /// <summary>
        /// 沿平面桩号等间距采样。返回的桩号序列形如：
        /// <paramref name="startOffset"/>, startOffset + <paramref name="interval"/>, startOffset + 2·interval, ... 直到 GetPlanarLength()；
        /// 末尾是否包含恰好等于长度的样点由 <paramref name="includeEnd"/> 决定。
        ///
        /// - <paramref name="interval"/> &lt;= 0 抛 <see cref="ArgumentOutOfRangeException"/>。
        /// - <paramref name="startOffset"/> 常用 0（首桩 K0+000）或"整二十米取整"偏移。
        /// - 不要求 interval 与总长整除。
        /// </summary>
        public IEnumerable<StationSample> SamplePlanarStations(
            double interval,
            double startOffset = 0,
            bool includeEnd = false)
        {
            if (interval <= 0)
                throw new ArgumentOutOfRangeException(nameof(interval), "interval 必须为正数。");
            if (SegmentCount == 0) yield break;

            double total = GetPlanarLength();
            if (total <= BulgeEpsilon) yield break;

            // startOffset < 0 的负偏移视为 0；允许 startOffset > total 时直接不产出。
            double s = Math.Max(0, startOffset);
            const double eps = 1e-9;
            bool emittedEnd = false;
            while (s <= total + eps)
            {
                double clamped = Math.Min(s, total);
                yield return new StationSample(
                    clamped,
                    PointAtPlanarStation(clamped),
                    TangentAtPlanarStation(clamped));
                if (Math.Abs(clamped - total) < eps) emittedEnd = true;
                s += interval;
            }
            if (includeEnd && !emittedEnd)
            {
                yield return new StationSample(
                    total,
                    PointAtPlanarStation(total),
                    TangentAtPlanarStation(total));
            }
        }

        /// <summary>
        /// 给定平面桩号，定位所在分段索引与段内比例参数 t ∈ [0, 1]。
        /// clamp 到两端；空 polyline 抛异常由调用方保证前置校验。
        /// </summary>
        private void ResolvePlanarStation(
            double station,
            out int segIndex,
            out double t,
            out double segPlanarLength)
        {
            int segCount = SegmentCount;
            if (segCount == 0)
                throw new InvalidOperationException("Polyline3D 顶点不足，无法按桩号定位。");

            double remaining = Math.Max(0, station);
            for (int i = 0; i < segCount; i++)
            {
                double len = GetPlanarSegmentLength(i);
                if (len <= BulgeEpsilon) continue;

                if (remaining <= len || i == segCount - 1)
                {
                    segIndex = i;
                    t = Math.Min(1.0, remaining / len);
                    segPlanarLength = len;
                    return;
                }
                remaining -= len;
            }

            // 理论不可达；兜底：末段终点。
            segIndex = segCount - 1;
            t = 1.0;
            segPlanarLength = GetPlanarSegmentLength(segIndex);
        }

        private double GetPlanarSegmentLength(int segIndex)
        {
            var seg = GetSegmentAt(segIndex);
            double dx = seg.End.X - seg.Start.X;
            double dy = seg.End.Y - seg.Start.Y;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            return ArcLengthFromChord(chord, _bulges[segIndex]);
        }

        /// <summary>分段内按比例 t 求 3D 点。含弧段在 XY 上走圆弧，Z 按 t 线性插值。</summary>
        private Point3D InterpolatePointOnSegment(int segIndex, double t)
        {
            var seg = GetSegmentAt(segIndex);
            double bulge = _bulges[segIndex];
            double z = seg.Start.Z + (seg.End.Z - seg.Start.Z) * t;

            if (Math.Abs(bulge) < BulgeEpsilon)
            {
                double x = seg.Start.X + (seg.End.X - seg.Start.X) * t;
                double y = seg.Start.Y + (seg.End.Y - seg.Start.Y) * t;
                return new Point3D(x, y, z);
            }

            ComputeArcGeometry(seg.Start, seg.End, bulge,
                out double cx, out double cy, out double radius,
                out double startAngle, out double sweep);

            double angle = startAngle + sweep * t;
            double ax = cx + radius * Math.Cos(angle);
            double ay = cy + radius * Math.Sin(angle);
            return new Point3D(ax, ay, z);
        }

        /// <summary>分段内按比例 t 求 XY 单位切线。弧段通过圆心-点连线旋转 90°（按 sweep 符号）得到。</summary>
        private Vector2D TangentOnSegment(int segIndex, double t)
        {
            var seg = GetSegmentAt(segIndex);
            double bulge = _bulges[segIndex];

            if (Math.Abs(bulge) < BulgeEpsilon)
            {
                var chord = new Vector2D(seg.End.X - seg.Start.X, seg.End.Y - seg.Start.Y);
                return chord.TryNormalize(out var unit) ? unit : Vector2D.UnitX;
            }

            ComputeArcGeometry(seg.Start, seg.End, bulge,
                out double cx, out double cy, out double radius,
                out double startAngle, out double sweep);

            double angle = startAngle + sweep * t;
            // 半径方向（中心 → 当前点）
            var radial = new Vector2D(Math.Cos(angle), Math.Sin(angle));
            // 切线 = 半径方向按 sweep 方向旋转 90°：sweep>0 逆时针行进 → 切线 = Perpendicular()；
            // sweep<0 顺时针 → 切线 = -Perpendicular()。
            var tangent = sweep >= 0 ? radial.Perpendicular() : -radial.Perpendicular();
            return tangent.TryNormalize(out var unit2) ? unit2 : Vector2D.UnitX;
        }

        /// <summary>
        /// 由"起点 + 终点 + bulge"还原圆弧的中心、半径、起始角度、扫掠角度（带符号）。
        /// bulge = tan(θ/4)，θ 为圆心角（顺时针为负）。
        /// 算法：
        /// 1. θ = 4·atan(bulge)（带符号）
        /// 2. 弦中点 M = (Start + End) / 2；弦向量 C = End - Start
        /// 3. 弦到弧顶的垂距 h = bulge * |C| / 2（带符号）
        /// 4. 圆心 = M + n̂ * (R·cos(θ/2) 的补偿方向)
        ///    更稳定的写法：center = M + perpLeft(C) * d；d = R·cos(θ/2)，与 sign(θ) 相关。
        /// 本实现用"bulge 符号决定中心在弦哪一侧"：
        ///   bulge > 0（逆时针小弧 / 大弧）→ 圆心在弦左侧（垂直向左偏移）
        ///   bulge < 0（顺时针）→ 圆心在弦右侧
        /// </summary>
        private static void ComputeArcGeometry(
            Point3D start, Point3D end, double bulge,
            out double cx, out double cy,
            out double radius,
            out double startAngle,
            out double sweep)
        {
            double theta = 4.0 * Math.Atan(bulge); // 带符号
            sweep = theta;

            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            double sinHalf = Math.Sin(theta / 2.0);

            // bulge != 0 时 sinHalf 不会接近 0；退化场景在上游已按直线处理。
            radius = (chord / 2.0) / sinHalf; // 带符号的 radius，下面用 Abs 即可
            double absR = Math.Abs(radius);

            double mx = (start.X + end.X) / 2.0;
            double my = (start.Y + end.Y) / 2.0;

            // 弦左侧垂直单位向量（逆时针旋转 90°）
            double chordLen = chord > BulgeEpsilon ? chord : 1.0;
            double nx = -dy / chordLen;
            double ny = dx / chordLen;

            // 圆心到弦中点的距离 d = R·cos(θ/2)（带符号）
            // 按 bulge 符号决定方向：bulge>0（逆时针）→ 中心在弦左侧，d 为正；bulge<0 → 中心在右侧。
            double d = absR * Math.Cos(theta / 2.0) * Math.Sign(bulge);
            cx = mx + nx * d;
            cy = my + ny * d;

            radius = absR;
            startAngle = Math.Atan2(start.Y - cy, start.X - cx);
        }

        /// <summary>
        /// Newtonsoft 回调：全 0 bulges 时不输出 JSON 字段，减小文件体积并与老数据兼容。
        /// </summary>
        public bool ShouldSerializeBulges() => HasArcs;

        public override string ToString()
        {
            return HasArcs
                ? $"Polyline3D[{VertexCount} vertices, Closed={IsClosed}, Length={GetTotalLength():F3}, Arcs]"
                : $"Polyline3D[{VertexCount} vertices, Closed={IsClosed}, Length={GetTotalLength():F3}]";
        }

        // ===== private helpers =====

        private const double BulgeEpsilon = 1e-12;

        /// <summary>
        /// 保证 <see cref="_bulges"/> 长度恰好等于 <see cref="_vertices"/> 数量：
        /// 不足补 0（老 JSON 升级 / 部分手工构造），多出截断（Blender 往返时长度不匹配的容错）。
        /// </summary>
        private void NormalizeBulges()
        {
            while (_bulges.Count < _vertices.Count) _bulges.Add(0);
            if (_bulges.Count > _vertices.Count)
                _bulges.RemoveRange(_vertices.Count, _bulges.Count - _vertices.Count);
        }

        /// <summary>
        /// 无限长直线 p0 + s·d0 与 p1 + t·d1 的交点（d0、d1 已单位化）。
        /// </summary>
        private static bool TryIntersectLines2D(
            Point2D p0, Vector2D d0, Point2D p1, Vector2D d1, out Point2D hit)
        {
            hit = default;
            var w = p0.VectorTo(p1);
            double det = d0.Cross(d1);
            if (Math.Abs(det) < 1e-12) return false;
            double s = w.Cross(d1) / det;
            hit = p0.Add(d0 * s);
            return true;
        }

        /// <summary>
        /// 由弦长和 bulge 反推弧长。
        /// bulge = tan(θ/4) ⇒ θ = 4·atan(|bulge|)；R = chord/(2·sin(θ/2))；L = R·θ。
        /// bulge ≈ 0 退化为直线（返回弦长），避免除零。
        /// </summary>
        private static double ArcLengthFromChord(double chord, double bulge)
        {
            if (Math.Abs(bulge) < BulgeEpsilon) return chord;
            double theta = 4.0 * Math.Atan(Math.Abs(bulge));
            double sinHalf = Math.Sin(theta / 2.0);
            if (sinHalf < BulgeEpsilon) return chord;
            double radius = (chord / 2.0) / sinHalf;
            return radius * theta;
        }
    }
}
