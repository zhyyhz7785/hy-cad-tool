using System;
using System.Collections.Generic;
using HyCAD.Geometry;

namespace HYFEA.Hosting;

/// <summary>内存轴线（独立壳测试用；默认水平梁）。</summary>
public sealed class InMemoryAxisGeometry : IFemHostGeometry
{
    private readonly IReadOnlyList<Point2D> _nodes;

    public InMemoryAxisGeometry(IReadOnlyList<Point2D> nodes)
    {
        if (nodes == null || nodes.Count < 2)
            throw new ArgumentException("至少需要 2 个节点。", nameof(nodes));
        _nodes = nodes;
    }

    /// <param name="length">梁长（与单位制一致，默认 MmN 下为 mm）。</param>
    /// <param name="segments">单元段数（节点数 = segments + 1）。</param>
    public static InMemoryAxisGeometry Horizontal(double length, int segments, double y = 0)
    {
        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
        if (segments < 1) throw new ArgumentOutOfRangeException(nameof(segments));

        var nodes = new Point2D[segments + 1];
        for (int i = 0; i <= segments; i++)
            nodes[i] = new Point2D(length * i / segments, y);
        return new InMemoryAxisGeometry(nodes);
    }

    public IReadOnlyList<Point2D> GetAxisNodes() => _nodes;
}
