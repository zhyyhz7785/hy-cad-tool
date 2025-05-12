using Autodesk.AutoCAD.DatabaseServices;
namespace EquipmentFoundation.Models
{
    public class BoundaryCondition
    {
        public Line Edge { get; } // 边界边，使用 AutoCAD 的 Line
        public bool IsSoilBoundary { get; } // 是否为土壤边界
        public Polyline AdjacentPolygon { get; } // 相邻多边形，可能为 null
        public bool IsWall { get; } // 是否为墙体
        public Line CoincidentEdge { get; } // 新增属性，表示重合边
        public BoundaryCondition(Line edge, bool isSoilBoundary, Polyline adjacentPolygon, bool isWall, Line coincidentEdge = null)
        {
            if (edge == null)
                throw new ArgumentNullException(nameof(edge));
            if (edge.StartPoint == edge.EndPoint)
                throw new ArgumentException("Edge 必须是由两个不同点构成的直线", nameof(edge));
            Edge = edge;
            IsSoilBoundary = isSoilBoundary;
            AdjacentPolygon = adjacentPolygon;
            IsWall = isWall;
            CoincidentEdge = coincidentEdge; // 可为 null
        }
        // 判断是否为墙体，基于 Polyline 的标高
        public static bool DetermineIsWall(Polyline ownerPolygon, Dictionary<Polyline, double> elevationsDic)
        {
            if (!elevationsDic.TryGetValue(ownerPolygon, out double elevation))
                return false; // 如果没有标高数据，默认不是墙体
            if (elevation < 0) // 标高 < 0，所有 Edge 都是墙体
                return true;
            if (elevation == 0) // 标高 = 0，整个 Polygon 是墙体(后期开发)
                return false;
            else return false; // 标高 > 0，不是墙体
        }
    }
    public class WallData
    {
        public Line Edge { get; set; } // 墙体的边，与 BoundaryCondition 的 Edge 一致
        public double InnerElevation { get; set; } // 内侧标高
        public double OuterElevation { get; set; } // 外侧标高
        public double Thickness { get; set; } // 墙体厚度，若不是墙体可为 0
        public BoundaryCondition Boundary { get; } // 对应的边界条件
        public bool IsWall { get; } // 是否为墙体，直接存储
        public EndType EndType { get; set; } // 新增属性，表示端点处理方式，默认 Polygon
        public WallData(Line edge, double innerElevation, double outerElevation, double thickness,
            BoundaryCondition boundary, Line coincidentEdge = null, EndType endType = EndType.Polygon)
        {
            if (edge == null)
                throw new ArgumentNullException(nameof(edge));
            if (edge.StartPoint == edge.EndPoint)
                throw new ArgumentException("Edge 必须是由两个不同点构成的直线", nameof(edge));
            if (boundary == null)
                throw new ArgumentNullException(nameof(boundary));
            if (!edge.StartPoint.Equals(boundary.Edge.StartPoint) || !edge.EndPoint.Equals(boundary.Edge.EndPoint))
                throw new ArgumentException("WallData 的 Edge 必须与 BoundaryCondition 的 Edge 一致", nameof(edge));
            Edge = edge;
            InnerElevation = innerElevation;
            OuterElevation = outerElevation;
            Thickness = thickness;
            Boundary = boundary;
            // 计算 IsWall：基于 Boundary 的初始值并应用 ShouldSkipConnection 逻辑
            bool initialIsWall = boundary.IsWall;
            IsWall = initialIsWall && !ShouldSkipConnection(this);
            EndType = endType; // 设置端点处理方式，默认 Polygon
            if (Thickness < 0)
                throw new ArgumentException("围墙厚度必须大于 0", nameof(thickness));
            // 可选：如果需要验证 CoincidentEdge
            if (coincidentEdge != null && coincidentEdge.StartPoint == coincidentEdge.EndPoint)
                throw new ArgumentException("CoincidentEdge 必须是由两个不同点构成的直线", nameof(coincidentEdge));
        }
        private static bool ShouldSkipConnection(WallData wall)
        {
            return wall.Boundary.CoincidentEdge != null && wall.InnerElevation > wall.OuterElevation;
        }
    }
    public enum WallSegmentType
    {
        None,               // 无墙体段
        ClosedCurve,        // 单一封闭曲线
        SingleOpenCurve,    // 单一非封闭曲线
        MultipleOpenCurves  // 多个非封闭曲线
    }
    public class GeometryData
    {
        public Polyline Polygon { get; set; } // 对应的多边形
        public double Elevation { get; set; } // 对应的多边形标高
        public List<WallData> Walls { get; set; } // 墙体数据列表，包含所有边（即使不是墙体）
        public List<Polyline> Buffers { get; set; } // 墙体数据列表，包含所有边（即使不是墙体）
        public double BaseThickness { get; set; } // 底板厚度
        private Dictionary<Line, WallData> _wallIndex; // 墙体索引，便于快速查询
        // 新增属性：分别存储不同类型的墙体段
        public List<WallData> ClosedCurve { get; private set; }        // 单一封闭曲线
        public List<WallData> SingleOpenCurve { get; private set; }    // 单一非封闭曲线
        public List<List<WallData>> MultipleOpenCurves { get; private set; } // 多个非封闭曲线
        // 墙体段的类型
        public WallSegmentType SegmentType { get; private set; }
        public GeometryData(Polyline polygon, double baseThickness)
        {
            Polygon = polygon ?? throw new ArgumentNullException(nameof(polygon));
            Walls = new List<WallData>();
            BaseThickness = baseThickness;
            _wallIndex = new Dictionary<Line, WallData>(new LineEqualityComparer());
            ClosedCurve = new List<WallData>();
            SingleOpenCurve = new List<WallData>();
            MultipleOpenCurves = new List<List<WallData>>();
            SegmentType = WallSegmentType.None; // 默认无墙体段
            // 在构造函数中直接调用 GetWallSegments 来初始化墙体段
            GetWallSegments();
        }
        // 添加墙体数据时，自动更新索引和墙体段
        public void AddWallData(WallData wallData)
        {
            if (wallData == null)
                throw new ArgumentNullException(nameof(wallData));
            Walls.Add(wallData);
            if (wallData.Boundary.IsWall && wallData.Thickness > 0) // 只索引真正的墙体
            {
                _wallIndex[wallData.Edge] = wallData;
            }
            // 添加墙体后重新计算墙体段
            GetWallSegments();
        }
        // 快速查询所有墙体的 Edge 和 Thickness
        public IEnumerable<(Line Edge, double Thickness)> GetWallEdgesAndThickness()
        {
            foreach (var wall in _wallIndex.Values)
            {
                yield return (wall.Edge, wall.Thickness);
            }
        }
        // 根据 Edge 查询对应的墙体厚度，若不是墙体返回 null
        public double? GetWallThickness(Line edge)
        {
            if (_wallIndex.TryGetValue(edge, out var wallData))
            {
                return wallData.Thickness;
            }
            return null;
        }
        // 更新墙体索引
        public void UpdateWallIndex()
        {
            _wallIndex.Clear(); // 清空旧索引
            foreach (var wall in Walls)
            {
                if (wall.Boundary.IsWall && wall.Thickness > 0)
                {
                    _wallIndex[wall.Edge] = wall;
                }
            }
            // 更新索引后重新计算墙体段
            GetWallSegments();
        }
        // 修改后的 GetWallSegments 方法
        public void GetWallSegments()
        {
            // 清空现有墙体段数据
            ClosedCurve.Clear();
            SingleOpenCurve.Clear();
            MultipleOpenCurves.Clear();
            if (Polygon == null || Walls == null || Walls.Count == 0)
            {
                SegmentType = WallSegmentType.None;
                return;
            }
            var currentSegment = new List<WallData>();
            int vertexCount = Polygon.NumberOfVertices;
            bool isClosed = Polygon.Closed; // 检查多边形是否闭合
            var tempSegments = new List<List<WallData>>(); // 临时存储所有段
            // 假设 Walls 列表与 Polyline 的边顺序一致
            for (int i = 0; i < Walls.Count; i++)
            {
                var wall = Walls[i];
                if (wall.IsWall)
                {
                    // 是墙体，添加到当前段
                    currentSegment.Add(wall);
                }
                else if (currentSegment.Count > 0)
                {
                    // 遇到非墙体且当前段不为空，结束当前段
                    tempSegments.Add(new List<WallData>(currentSegment));
                    currentSegment.Clear();
                }
                // 处理闭环的首尾连接
                if (i == Walls.Count - 1 && currentSegment.Count > 0)
                {
                    if (isClosed && tempSegments.Count > 0 && Walls[0].IsWall)
                    {
                        // 如果多边形闭合且首尾都是墙体，合并到第一个段
                        tempSegments[0].InsertRange(0, currentSegment);
                    }
                    else
                    {
                        // 否则作为独立段添加
                        tempSegments.Add(new List<WallData>(currentSegment));
                    }
                    currentSegment.Clear();
                }
            }
            // 如果整个 Polyline 都是墙体，且未分段，则添加唯一段
            if (currentSegment.Count > 0)
            {
                tempSegments.Add(new List<WallData>(currentSegment));
            }
            // 根据分段结果更新属性
            if (tempSegments.Count == 0)
            {
                SegmentType = WallSegmentType.None;
            }
            else if (tempSegments.Count == 1)
            {
                var segment = tempSegments[0];
                if (IsSegmentClosed(segment))
                {
                    ClosedCurve.AddRange(segment);
                    SegmentType = WallSegmentType.ClosedCurve;
                }
                else
                {
                    SingleOpenCurve.AddRange(segment);
                    SegmentType = WallSegmentType.SingleOpenCurve;
                }
            }
            else
            {
                MultipleOpenCurves.AddRange(tempSegments);
                SegmentType = WallSegmentType.MultipleOpenCurves;
            }
        }
        // 判断单个墙体段是否封闭
        private bool IsSegmentClosed(List<WallData> segment)
        {
            if (segment.Count < 2) return false; // 少于2条边无法封闭
            var firstWall = segment[0];
            var lastWall = segment[segment.Count - 1];
            return firstWall.Edge.StartPoint.Equals(lastWall.Edge.EndPoint);
        }
        public class LineEqualityComparer : IEqualityComparer<Line>
        {
            public bool Equals(Line x, Line y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x == null || y == null) return false;
                return x.StartPoint.Equals(y.StartPoint) && x.EndPoint.Equals(y.EndPoint);
            }
            public int GetHashCode(Line obj)
            {
                if (obj == null) return 0;
                return obj.StartPoint.GetHashCode() ^ obj.EndPoint.GetHashCode();
            }
        }
    }
}
