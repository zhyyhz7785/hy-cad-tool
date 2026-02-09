using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.ValueObjects.Geometry
{
    /// <summary>
    /// 二维多段线值对象（平台无关）
    /// 支持开放和闭合多段线，适用于钢筋等非封闭几何
    /// </summary>
    public class Polyline2D
    {
        private readonly List<Point2D> _vertices;

        public IReadOnlyList<Point2D> Vertices => _vertices.AsReadOnly();
        public bool IsClosed { get; set; }
        public int VertexCount => _vertices.Count;

        #region 构造

        public Polyline2D(IEnumerable<Point2D> vertices, bool isClosed = false)
        {
            _vertices = vertices != null ? new List<Point2D>(vertices) : throw new ArgumentNullException(nameof(vertices));
            IsClosed = isClosed;
        }

        public Polyline2D(bool isClosed = false)
        {
            _vertices = new List<Point2D>();
            IsClosed = isClosed;
        }

        #endregion

        #region 顶点操作

        public void AddVertexAt(int index, Point2D point)
        {
            if (index < 0 || index > _vertices.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            _vertices.Insert(index, point);
        }

        public void AddVertex(Point2D point) => _vertices.Add(point);

        public Point2D GetPointAt(int index) => _vertices[index];

        public void RemoveDuplicateVertices(double tolerance = 1e-6)
        {
            if (_vertices.Count < 2) return;
            var cleaned = new List<Point2D> { _vertices[0] };
            for (int i = 1; i < _vertices.Count; i++)
            {
                if (!_vertices[i].IsEqualTo(cleaned[cleaned.Count - 1], tolerance))
                    cleaned.Add(_vertices[i]);
            }
            _vertices.Clear();
            _vertices.AddRange(cleaned);
        }

        #endregion

        #region 线段操作

        public int SegmentCount
        {
            get
            {
                if (_vertices.Count < 2) return 0;
                return IsClosed ? _vertices.Count : _vertices.Count - 1;
            }
        }

        public Line2D GetSegmentAt(int index)
        {
            int maxIndex = IsClosed ? _vertices.Count - 1 : _vertices.Count - 2;
            if (index < 0 || index > maxIndex)
                throw new ArgumentOutOfRangeException(nameof(index));
            int nextIndex = (index + 1) % _vertices.Count;
            return new Line2D(_vertices[index], _vertices[nextIndex]);
        }

        public Line2D[] GetSegments()
        {
            int count = SegmentCount;
            var segments = new Line2D[count];
            for (int i = 0; i < count; i++)
                segments[i] = GetSegmentAt(i);
            return segments;
        }

        #endregion

        #region 角度计算

        /// <summary>
        /// 获取每个顶点处相邻线段的转折角度（弧度）
        /// 用于判断是否需要在该点断开钢筋（角度 >= PI 表示大转折）
        /// </summary>
        /// <summary>
        /// 计算每个顶点处的转弯角（带方向，0 ~ 2*PI）
        /// 与旧代码 GetPolySegmentAngle + GetPolylineShape 一致：
        ///   v1 = seg[i-1] 方向, v2 = seg[i] 方向
        ///   角度 = Atan2(cross, dot)，映射到 [0, 2*PI)
        /// 对于逆时针多段线的凸角（左转）：angle 约 PI/2（&lt; PI，继续同一根钢筋）
        /// 对于凹角（右转或尖角）：angle 约 3*PI/2（&gt; PI，断开钢筋）
        /// </summary>
        public double[] GetVertexTurnAngles()
        {
            int n = _vertices.Count;
            var angles = new double[n];
            if (n < 3) return angles;

            int segCount = SegmentCount; // 闭合: n, 不闭合: n-1

            for (int i = 0; i < n; i++)
            {
                if (!IsClosed && (i == 0 || i == n - 1))
                {
                    angles[i] = 0;
                    continue;
                }

                // 旧代码：angles[i] 是 seg[i-1] 与 seg[i] 之间的角度
                // 闭合多段线: angles[0] 是最后线段与第一条线段的角度
                int prevSegIdx = ((i - 1) + segCount) % segCount;
                int curSegIdx = i % segCount;

                var seg1 = GetSegmentAt(prevSegIdx);
                var seg2 = GetSegmentAt(curSegIdx);

                Vector2D v1 = seg1.Direction; // EndPoint - StartPoint
                Vector2D v2 = seg2.Direction;

                // 对应 AutoCAD Vector3d.GetAngleTo(v2, normal)
                // normal = (0,0,1) 时等价于 Atan2(cross, dot)，映射到 [0, 2*PI)
                double cross = v1.Cross(v2); // v1.X * v2.Y - v1.Y * v2.X
                double dot = v1.Dot(v2);
                double angle = Math.Atan2(cross, dot); // [-PI, PI]
                if (angle < 0) angle += 2.0 * Math.PI;  // 映射到 [0, 2*PI)
                angles[i] = angle;
            }
            return angles;
        }

        #endregion

        #region 几何查询

        /// <summary>
        /// 根据点查找所在线段及方向（点更靠近哪端，方向朝向较远端）
        /// 对应旧代码 GetSegmentInPolyLineByPoint
        /// </summary>
        public (Line2D segment, Vector2D direction) GetSegmentAtPoint(Point2D point, double tolerance = 1e-6)
        {
            int segCount = SegmentCount;
            for (int i = 0; i < segCount; i++)
            {
                var seg = GetSegmentAt(i);
                if (!seg.IsPointOnSegment(point, tolerance))
                    continue;

                double distFromStart = point.DistanceTo(seg.StartPoint);
                double ratio = seg.Length > tolerance ? distFromStart / seg.Length : 0;

                Vector2D direction = ratio < 0.5
                    ? seg.StartPoint.VectorTo(seg.EndPoint).Normalize()
                    : seg.EndPoint.VectorTo(seg.StartPoint).Normalize();

                return (seg, direction);
            }
            throw new ArgumentException("点不在多段线的任何线段上");
        }

        public double GetLength()
        {
            double length = 0;
            for (int i = 0; i < SegmentCount; i++)
                length += GetSegmentAt(i).Length;
            return length;
        }

        public double GetSignedArea()
        {
            double area = 0.0;
            int n = _vertices.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += _vertices[i].X * _vertices[j].Y;
                area -= _vertices[j].X * _vertices[i].Y;
            }
            return area / 2.0;
        }

        #endregion

        #region 拓扑操作

        /// <summary>
        /// 连接另一条多段线（顶点追加到末尾，首尾重合则跳过重复点）
        /// </summary>
        public void Join(Polyline2D other)
        {
            if (other == null || other.VertexCount == 0) return;
            int start = (_vertices.Count > 0 && _vertices[_vertices.Count - 1].IsEqualTo(other._vertices[0])) ? 1 : 0;
            for (int i = start; i < other._vertices.Count; i++)
                _vertices.Add(other._vertices[i]);
        }

        /// <summary>
        /// 确保闭合多段线为顺时针方向
        /// </summary>
        public void SetClockwise()
        {
            if (!IsClosed || _vertices.Count < 3) return;
            if (GetSignedArea() > 0) _vertices.Reverse();
        }

        /// <summary>
        /// 确保闭合多段线为逆时针方向
        /// 旧代码 SetPolyLineClockWise 实际行为是确保逆时针（area &lt; 0 时 ReverseCurve）
        /// </summary>
        public void SetCounterClockwise()
        {
            if (!IsClosed || _vertices.Count < 3) return;
            if (GetSignedArea() < 0) _vertices.Reverse();
        }

        /// <summary>
        /// 按角度阈值将闭合多段线切分为多段开放多段线
        /// 角度 >= threshold 的顶点处断开
        /// 对应旧代码 GetSubReinforcements 的核心逻辑
        /// </summary>
        public Polyline2D[] SplitByAngleThreshold(double threshold = Math.PI)
        {
            if (VertexCount < 3 || SegmentCount < 2)
                return new[] { Clone() };

            var angles = GetVertexTurnAngles();
            var result = new List<Polyline2D>();
            int n = VertexCount;
            int i = 0;

            while (i < n)
            {
                var poly = new Polyline2D();
                do
                {
                    if (i >= SegmentCount) break;
                    var seg = GetSegmentAt(i);
                    poly.AddVertex(seg.StartPoint);
                    i++;
                } while (i < n && angles[i] < threshold);

                // 添加最后一段终点
                if (poly.VertexCount > 0)
                {
                    int lastSegIdx = Math.Min(i - 1, SegmentCount - 1);
                    if (lastSegIdx >= 0)
                        poly.AddVertex(GetSegmentAt(lastSegIdx).EndPoint);
                }

                if (poly.VertexCount >= 2)
                    result.Add(poly);

                // i-- + i++ = 不跳过（与旧代码逻辑一致）
            }

            // 起点角度 < threshold 时，末段与首段合并
            if (result.Count > 2 && angles[0] < threshold)
            {
                var first = result[0];
                var last = result[result.Count - 1];
                last.Join(first);
                result.RemoveAt(0);
            }

            return result.ToArray();
        }

        /// <summary>
        /// 条件连接多段线数组：平行且接近的相邻钢筋合并
        /// 对应旧代码 ConnectReinByCondition
        /// </summary>
        public static Polyline2D[] ConnectByCondition(Polyline2D[] polys, double anchorageJoinLength, double tolerance = 1e-6)
        {
            if (polys == null || polys.Length == 0) return polys;

            var list = polys.ToList();
            int i = 0;

            while (i < list.Count)
            {
                var current = list[i];
                if (current.VertexCount < 2) { i++; continue; }

                var segStart = current.GetSegmentAt(current.VertexCount - 2);
                var pointS = segStart.EndPoint;
                int j = i + 1;

                while (j < list.Count)
                {
                    var next = list[j];
                    if (next.VertexCount < 2) { j++; continue; }

                    var segEnd = next.GetSegmentAt(0);
                    var pointE = segEnd.StartPoint;

                    if (segStart.IsParallelTo(segEnd, tolerance))
                    {
                        double distanceV = ParallelLineDistance(segStart, segEnd);
                        double distance = pointS.DistanceTo(pointE);
                        double distanceH = Math.Sqrt(Math.Max(0, distance * distance - distanceV * distanceV));

                        double rate = distanceV > tolerance ? distanceV / distanceH : 0;

                        if (rate < 1.0 / 6 && distance < anchorageJoinLength)
                        {
                            current.AddVertex(pointE);
                            current.Join(next);

                            // 检查是否形成闭合
                            if (current.GetPointAt(0).IsEqualTo(
                                next.GetPointAt(next.VertexCount - 1), tolerance))
                            {
                                current.IsClosed = false;
                                current.AddVertex(segStart.StartPoint);
                            }

                            list.RemoveAt(j);
                            break;
                        }
                    }
                    j++;
                }
                i++;
            }
            return list.ToArray();
        }

        #endregion

        #region 辅助

        public Polyline2D Clone()
        {
            return new Polyline2D(new List<Point2D>(_vertices), IsClosed);
        }

        public override string ToString()
        {
            return $"Polyline2D[{VertexCount} vertices, Closed={IsClosed}]";
        }

        private static double ParallelLineDistance(Line2D seg1, Line2D seg2)
        {
            Vector2D dir = seg1.Direction;
            double len = dir.Length;
            if (len < 1e-10) return 0;
            Vector2D normal = new Vector2D(-dir.Y, dir.X) / len;
            Vector2D diff = seg1.StartPoint.VectorTo(seg2.StartPoint);
            return Math.Abs(normal.Dot(diff));
        }

        #endregion
    }
}
