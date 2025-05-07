using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace HyCADTool.HelpClass.DCEL
{
    public static class DCELFactory
    {
        public static double ToleranceDouble { get; } = 1e-2;
        private static readonly object dcelLock = new object(); // 用于同步 DCEL 修改
        public static DCEL CreateFromCurves(List<Curve> curves)
        {
            if (curves == null)
                throw new ArgumentNullException(nameof(curves), "曲线列表为 null。");
            if (curves.Count == 0)
            {
                SimpleLogger.LogWarning("曲线列表为空，返回空的 DCEL。");
                return new DCEL();
            }
            try
            {
                // 缓存当前文档、数据库和编辑器
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                {
                    var exx = new Exception();
                    SimpleLogger.LogCritical("无法获取当前活动文档。", exx);
                    throw new InvalidOperationException("无法获取当前活动文档。");
                }
                var db = doc.Database;
                var ed = doc.Editor;
                var dcel = new DCEL();
                var vertexMap = new ConcurrentDictionary<Point3d, Vertex>(new Point3dEqualityComparer(ToleranceDouble));
                SimpleLogger.LogInfo($"开始处理 {curves.Count} 条曲线。");
                // 并行处理每条曲线，提高性能
                SimpleLogger.LogElapsedTime("处理每个曲线", () =>
                {
                    ParallelOptions parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    };
                    Parallel.ForEach(curves, parallelOptions, (curve, state, index) =>
                     {
                         try
                         {
                             if (curve == null)
                                 throw new ArgumentNullException(nameof(curve), $"曲线索引 {index} 为 null。");
                             var start = curve.StartPoint;
                             var end = curve.EndPoint;
                             Vertex startVertex = GetOrCreateVertexCached(dcel, vertexMap, start);
                             Vertex endVertex = GetOrCreateVertexCached(dcel, vertexMap, end);
                             lock (dcelLock) // 确保 DCEL 修改的线程安全
                             {
                                 dcel.AddEdgePair(startVertex, endVertex);
                             }
                             if (index % 1000 == 0 && index > 0) // 每处理1000条曲线记录一次日志
                             {
                                 SimpleLogger.LogInfo($"已处理 {index} 条曲线。");
                             }
                         }
                         catch (Exception ex)
                         {
                             SimpleLogger.LogError($"处理曲线索引 {index} 时出错: {ex.Message}", ex);
                             // 根据需求，决定是否中断并停止处理
                             // state.Stop();
                         }
                     });
                });
                SimpleLogger.LogInfo("完成曲线处理。");
                // 删除关联半边数小于 1 的顶点
                SimpleLogger.LogElapsedTime("删除关联半边数小于 1 的顶点", () =>
                {
                    RemoveVerticesWithLessThanOneHalfEdge(dcel);
                });
                // 设置半边的 Next 和 Prev 属性
                SimpleLogger.LogElapsedTime("设置半边的 Next 和 Prev 属性", () =>
                {
                    SetNextAndPrevForNewEdges(dcel);
                });
                // 分类面
                ClassifyFaces(dcel);
                SimpleLogger.LogInfo("DCEL 创建完成。");
                return dcel;
            }
            catch (Exception ex)
            {
                SimpleLogger.LogCritical($"创建 DCEL 失败: {ex.Message}", ex);
                throw; // 根据需求，可以选择重新抛出异常或返回一个默认值
            }
        }
        /// <summary>
        /// 删除关联半边数小于 1 的顶点和相关半边
        /// </summary>
        private static void RemoveVerticesWithLessThanOneHalfEdge(DCEL dcel)
        {
            const int MaxIterations = 1000; // 设置一个合理的最大迭代次数
            int iteration = 0;
            bool hasRemoved = true;
            while (hasRemoved && iteration < MaxIterations)
            {
                hasRemoved = false;
                var halfEdgesToRemove = new HashSet<HalfEdge>();
                // 收集需要删除的半边及其对偶半边
                foreach (var he in dcel.HalfEdges.ToList()) // 使用 ToList() 以避免在遍历时修改集合
                {
                    if (he.StartVertex.OutgoingHalfedges.Count <= 1)
                    {
                        halfEdgesToRemove.Add(he);
                        he.Twin?.Let(t => halfEdgesToRemove.Add(t));
                    }
                }
                if (halfEdgesToRemove.Count > 0)
                {
                    hasRemoved = true;
                    foreach (var he in halfEdgesToRemove)
                    {
                        he.StartVertex.OutgoingHalfedges.Remove(he);
                        dcel.HalfEdges.Remove(he);
                    }
                    // 删除关联半边数量为 0 的顶点
                    var verticesToRemove = dcel.Vertices.Where(v => v.OutgoingHalfedges.Count == 0).ToList();
                    foreach (var vertex in verticesToRemove)
                    {
                        dcel.Vertices.Remove(vertex);
                    }
                }
                iteration++;
            }
            if (iteration >= MaxIterations)
            {
                SimpleLogger.LogWarning("删除顶点时达到最大迭代次数，可能存在无限循环的风险。");
            }
        }
        /// <summary>
        /// 使用局部缓存字典加速顶点创建
        /// </summary>
        private static Vertex GetOrCreateVertexCached(DCEL dcel, ConcurrentDictionary<Point3d, Vertex> vertexMap, Point3d position)
        {
            return vertexMap.GetOrAdd(position, pos =>
            {
                lock (dcelLock) // 确保线程安全地修改 DCEL
                {
                    return dcel.AddVertex(pos);
                }
            });
        }
        /// <summary>
        /// 设置新边的 Next 和 Prev 属性
        /// </summary>
        private static void SetNextAndPrevForNewEdges(DCEL dcel)
        {
            var ex1 = new Exception();
            var halfEdgeSet = dcel.HalfEdges.ToList();
            if (dcel.HalfEdges == null || !dcel.HalfEdges.Any())
            {
                SimpleLogger.LogError("HalfEdges is null or empty.", ex1);
                return;
            }
            halfEdgeSet.Sort((he1, he2) =>
            {
                int cmp = he1.StartVertex.Position.X.CompareTo(he2.StartVertex.Position.X);
                if (cmp != 0) return cmp;
                cmp = he1.StartVertex.Position.Y.CompareTo(he2.StartVertex.Position.Y);
                if (cmp != 0) return cmp;
                return he1.Twin.StartVertex.Position.Y.CompareTo(he2.Twin.StartVertex.Position.Y);
            });
            while (halfEdgeSet.Any())
            {
                var startEdge = halfEdgeSet.First();
                startEdge.IsInitialized = true;
                var currentEdge = startEdge;
                var faceEdges = new List<HalfEdge>();
                int maxIterations = 1000;  // 设置一个合理的最大迭代次数
                int iterationCount = 0;
                do
                {
                    faceEdges.Add(currentEdge);
                    halfEdgeSet.Remove(currentEdge);
                    currentEdge.IsInitialized = true;
                    try
                    {
                        var nextEdges = FindNextEdges(currentEdge);
                        if (nextEdges.minHalfEdge == null)
                            break; // 无法继续，可能是不闭合的
                        currentEdge = nextEdges.minHalfEdge;
                        iterationCount++;
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.LogError($"查找下一条边时出错: {ex.Message}", ex);
                        break;
                    }
                    if (iterationCount > maxIterations)
                    {
                        SimpleLogger.LogError("Max iterations exceeded while processing face.", ex1);
                        break;
                    }
                }
                while (currentEdge != null && currentEdge != startEdge);
                if (currentEdge == startEdge)
                {
                    try
                    {
                        dcel.CreateFace(faceEdges);
                    }
                    catch (Exception ex)
                    {
                        SimpleLogger.LogError($"创建面时出错: {ex.Message}", ex);
                    }
                }
            }
        }
        /// <summary>
        /// 使用左侧法则找到下一条半边
        /// </summary>
        private static (HalfEdge minHalfEdge, HalfEdge maxHalfEdge) FindNextEdges(HalfEdge currentEdge)
        {
            var vertex1 = currentEdge.StartVertex;
            var vertex2 = currentEdge.Twin.StartVertex;
            var vector = vertex1.Position - vertex2.Position;
            HalfEdge minHalfEdge = null;
            double minAngle = double.MaxValue;
            foreach (var edge in vertex2.OutgoingHalfedges)
            {
                if (edge == currentEdge.Twin)
                    continue;
                var currentVector = edge.Twin.StartVertex.Position - vertex2.Position;
                double angle = vector.GetAngleBetweenVectors(currentVector);
                if (angle < minAngle)
                {
                    minAngle = angle;
                    minHalfEdge = edge;
                }
            }
            if (minHalfEdge == null)
            {
                throw new InvalidOperationException("未找到可处理的边，请重新选择封闭图案。");
            }
            return (minHalfEdge, null); // 如果需要 maxHalfEdge，可以按需添加
        }
        /// <summary>
        /// 分类面为外面或内部
        /// </summary>
        public static void ClassifyFaces(DCEL dcel)
        {
            foreach (var face in dcel.Faces)
            {
                try
                {
                    if (face.Components.IsOuterContour())
                    {
                        dcel.OuterFaces.Add(face);
                    }
                    else
                    {
                        dcel.InterFaces.Add(face);
                    }
                }
                catch (Exception ex)
                {
                    SimpleLogger.LogError($"分类面时出错: {ex.Message}", ex);
                }
            }
        }
        /// <summary>
        /// 点比较器，考虑容差
        /// </summary>
        public class Point3dEqualityComparer : IEqualityComparer<Point3d>
        {
            private readonly double _tolerance;
            public Point3dEqualityComparer(double tolerance)
            {
                _tolerance = tolerance;
            }
            public bool Equals(Point3d p1, Point3d p2)
            {
                return p1.IsEqualTo(p2, new Tolerance(_tolerance, _tolerance));
            }
            public int GetHashCode(Point3d point)
            {
                int hashX = Math.Round(point.X / _tolerance).GetHashCode();
                int hashY = Math.Round(point.Y / _tolerance).GetHashCode();
                int hashZ = Math.Round(point.Z / _tolerance).GetHashCode();
                return hashX ^ hashY ^ hashZ;
            }
        }
        /// <summary>
        /// 计算向量叉积的Z分量
        /// </summary>
        public static double CrossProduct(Vector3d v1, Vector3d v2)
        {
            return (v1.X * v2.Y) - (v1.Y * v2.X);
        }
        /// <summary>
        /// 判断是否为外轮廓的方法
        /// </summary>
        private static bool IsOuterContour(this List<HalfEdge> faceEdges)
        {
            double area = 0.0;
            foreach (var he in faceEdges)
            {
                var current = he.StartVertex.Position;
                var next = he.Next.StartVertex.Position;
                area += (current.X * next.Y) - (next.X * current.Y);
            }
            area *= 0.5;
            // 如果面积为正，则为逆时针方向（外轮廓）
            return area > 0;
        }
        /// <summary>
        /// 扩展方法：计算两个向量之间的夹角，范围 [0, 2π]
        /// </summary>
        public static double GetAngleBetweenVectors(this Vector3d v1, Vector3d v2)
        {
            double dot = v1.DotProduct(v2);
            double crossZ = v1.CrossProduct(v2).Z;
            double angle = Math.Atan2(crossZ, dot);
            return angle >= 0 ? angle : (2 * Math.PI + angle);
        }
    }
    // 扩展方法帮助
    public static class ExtensionMethods
    {
        /// <summary>
        /// 如果对象不为空，执行操作
        /// </summary>
        public static void Let<T>(this T obj, Action<T> action) where T : class
        {
            if (obj != null)
                action(obj);
        }
    }
}
