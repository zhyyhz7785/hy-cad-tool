using System;
using System.Collections.Generic;
using HYFEA.Core.Dofs;
using HYFEA.Core.Model;
using HYFEA.Core.Results;

namespace HYFEA.Viz;

/// <summary>将 Euler 梁模型 + 位移场映射为线单元 <see cref="ResultMesh"/>。</summary>
public static class BeamResultMeshBuilder
{
    public static ResultMesh From(FemProblem problem, FemResult result)
    {
        if (problem == null) throw new ArgumentNullException(nameof(problem));
        if (result == null) throw new ArgumentNullException(nameof(result));
        if (!result.Success)
            throw new InvalidOperationException("求解未成功，无法构建结果网格。");

        var nodes = problem.Nodes;
        int n = nodes.Count;
        if (n < 2)
            throw new ArgumentException("至少需要 2 个节点。", nameof(problem));

        var idToIndex = new Dictionary<int, int>(n);
        var points = new double[n * 3];
        for (int i = 0; i < n; i++)
        {
            var node = nodes[i];
            idToIndex[node.Id.Value] = i;
            points[i * 3] = node.Position.X;
            points[i * 3 + 1] = node.Position.Y;
            points[i * 3 + 2] = 0;
        }

        var lines = new List<int>();
        foreach (var el in problem.Elements)
        {
            if (el is not EulerBeam2DElementDef beam)
                continue;
            if (!idToIndex.TryGetValue(beam.NodeA.Value, out int ia) ||
                !idToIndex.TryGetValue(beam.NodeB.Value, out int ib))
                throw new InvalidOperationException($"单元 {beam.Id.Value} 引用了未知节点。");
            lines.Add(ia);
            lines.Add(ib);
        }

        if (lines.Count == 0)
            throw new InvalidOperationException("模型中没有 EulerBeam2D 单元。");

        var ux = new double[n];
        var uy = new double[n];
        var umag = new double[n];
        var uVec = new double[n * 3];
        var disp = result.Displacements;
        for (int i = 0; i < n; i++)
        {
            var id = nodes[i].Id;
            double x = 0, y = 0;
            if (disp != null)
            {
                disp.TryGet(id, DofType.UX, out x);
                disp.TryGet(id, DofType.UY, out y);
            }

            ux[i] = x;
            uy[i] = y;
            umag[i] = Math.Sqrt(x * x + y * y);
            uVec[i * 3] = x;
            uVec[i * 3 + 1] = y;
            uVec[i * 3 + 2] = 0;
        }

        return new ResultMesh
        {
            Points = points,
            LineConnectivity = lines.ToArray(),
            PointData = new Dictionary<string, double[]>
            {
                ["UX"] = ux,
                ["UY"] = uy,
                ["U_mag"] = umag,
            },
            PointVectors = new Dictionary<string, double[]>
            {
                ["U"] = uVec,
            },
        };
    }
}
