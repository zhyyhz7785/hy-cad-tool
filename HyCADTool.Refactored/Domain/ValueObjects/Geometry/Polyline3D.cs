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

        /// <summary>任一 bulge 非零即视为"含弧段"。</summary>
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
