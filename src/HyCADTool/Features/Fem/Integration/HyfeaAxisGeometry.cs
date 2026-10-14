using System;
using System.Collections.Generic;
using HyCAD.Geometry;
using HYFEA.Core.Model;
using HYFEA.Hosting;

namespace HyCADTool.Features.Fem.Integration
{
    /// <summary>
    /// 已采样梁轴线，实现 <see cref="IFemHostGeometry"/>。
    /// 不引用 AutoCAD 类型；CAD 选线结果与内存折线共用这一层。
    /// </summary>
    public sealed class HyfeaAxisGeometry : IFemHostGeometry
    {
        private readonly IReadOnlyList<Point2D> _nodes;

        public HyfeaAxisGeometry(IReadOnlyList<Point2D> nodes)
        {
            if (nodes == null || nodes.Count < 2)
                throw new ArgumentException("至少需要 2 个节点。", nameof(nodes));
            _nodes = nodes;
        }

        public IReadOnlyList<Point2D> GetAxisNodes() => _nodes;

        /// <summary>
        /// 按弧长均匀采样。输入为 CAD 毫米坐标；<see cref="UnitSystem.SI"/> 时缩放到米。
        /// </summary>
        public static IReadOnlyList<Point2D> SampleByLength(
            IReadOnlyList<Point2D> verticesMm,
            int segmentCount,
            UnitSystem units,
            bool closed = false)
        {
            if (verticesMm == null || verticesMm.Count < 2)
                throw new ArgumentException("至少需要 2 个顶点。", nameof(verticesMm));
            if (segmentCount < 1)
                segmentCount = 1;

            int nc = verticesMm.Count;
            int segCount = closed ? nc : nc - 1;
            var lens = new double[segCount];
            double totalLen = 0;
            for (int i = 0; i < segCount; i++)
            {
                var p0 = verticesMm[i];
                var p1 = verticesMm[(i + 1) % nc];
                double dx = p1.X - p0.X;
                double dy = p1.Y - p0.Y;
                double dl = Math.Sqrt(dx * dx + dy * dy);
                lens[i] = dl;
                totalLen += dl;
            }

            if (totalLen < 1e-9)
                throw new InvalidOperationException("线长为 0。");

            double s = units == UnitSystem.SI ? 1e-3 : 1.0;
            var list = new List<Point2D>(segmentCount + 1);
            for (int k = 0; k <= segmentCount; k++)
            {
                double target = totalLen * k / segmentCount;
                double walk = 0;
                for (int i = 0; i < segCount; i++)
                {
                    if (target <= walk + lens[i] + 1e-12 || i == segCount - 1)
                    {
                        var p0 = verticesMm[i];
                        var p1 = verticesMm[(i + 1) % nc];
                        double t = lens[i] < 1e-12 ? 0 : (target - walk) / lens[i];
                        if (t < 0) t = 0;
                        if (t > 1) t = 1;
                        list.Add(new Point2D(
                            (p0.X + t * (p1.X - p0.X)) * s,
                            (p0.Y + t * (p1.Y - p0.Y)) * s));
                        break;
                    }

                    walk += lens[i];
                }
            }

            return list;
        }
    }
}
