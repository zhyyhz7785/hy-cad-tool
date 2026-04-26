using System;
using System.Collections.Generic;

namespace HyCADTool.Shared.Geometry
{
    /// <summary>
    /// 三角网格值对象（平台无关）。
    ///
    /// P0 阶段仅作为 <c>ICorridorMeshBuilder</c> 的结果类型占位，v1 不产生实际三角网格；
    /// v2 Blender 流水线启动后（P7），用于承载 Corridor 三角带、路面 / 侧面 / 缘石组。
    ///
    /// 约束：
    /// - 顶点数组为紧凑索引（0 基），<see cref="Indices"/> 每三个整数构成一个三角形。
    /// - 法线可选（v1 不填，v2 填），由 Blender 重建。
    /// - 材质键（<see cref="MaterialGroups"/>）映射到 <c>blender-material-mapping.json</c>。
    /// </summary>
    public class Mesh3D
    {
        public IReadOnlyList<Point3D> Vertices { get; }
        public IReadOnlyList<int> Indices { get; }
        public IReadOnlyList<Vector3D> Normals { get; }

        /// <summary>
        /// 按材质分组的三角形范围。
        /// Key：material_key（如 "asphalt_base"、"kerb_stone"），Value：三角形索引段 [startTriIdx, triCount]。
        /// </summary>
        public IReadOnlyDictionary<string, MaterialRange> MaterialGroups { get; }

        public Mesh3D(
            IReadOnlyList<Point3D> vertices,
            IReadOnlyList<int> indices,
            IReadOnlyList<Vector3D> normals = null,
            IReadOnlyDictionary<string, MaterialRange> materialGroups = null)
        {
            Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            Indices = indices ?? throw new ArgumentNullException(nameof(indices));
            if (indices.Count % 3 != 0)
                throw new ArgumentException("Indices 数量必须是 3 的倍数（按三角形组织）", nameof(indices));
            Normals = normals;
            MaterialGroups = materialGroups ?? new Dictionary<string, MaterialRange>();
        }

        public int TriangleCount => Indices.Count / 3;

        public static Mesh3D Empty { get; } = new Mesh3D(new Point3D[0], new int[0]);

        public override string ToString()
        {
            return $"Mesh3D[V={Vertices.Count}, T={TriangleCount}, M={MaterialGroups.Count}]";
        }
    }

    /// <summary>
    /// 三角形材质分组范围（连续三角形索引段）。
    /// </summary>
    public readonly struct MaterialRange
    {
        public int StartTriangleIndex { get; }
        public int TriangleCount { get; }

        public MaterialRange(int startTriangleIndex, int triangleCount)
        {
            if (startTriangleIndex < 0) throw new ArgumentOutOfRangeException(nameof(startTriangleIndex));
            if (triangleCount < 0) throw new ArgumentOutOfRangeException(nameof(triangleCount));
            StartTriangleIndex = startTriangleIndex;
            TriangleCount = triangleCount;
        }

        public override string ToString() => $"[{StartTriangleIndex}..{StartTriangleIndex + TriangleCount - 1}]";
    }
}
