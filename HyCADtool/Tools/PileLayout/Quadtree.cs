using System;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    /// <summary>
    /// 节点与多边形相交关系
    /// </summary>
    public enum IntersectionType
    {
        Inside,
        Outside,
        Partial
    }
    public class QuadtreeNode
    {
        public double XMin { get; }
        public double XMax { get; }
        public double YMin { get; }
        public double YMax { get; }
        public IntersectionType Intersection { get; set; }
        public QuadtreeNode[] Children { get; set; }
        public bool IsLeaf => Children == null;
        public QuadtreeNode(double xMin, double xMax, double yMin, double yMax)
        {
            XMin = xMin;
            XMax = xMax;
            YMin = yMin;
            YMax = yMax;
            Children = null;
        }
    }
    public class SimpleQuadtree
    {
        private readonly int _maxDepth;
        private readonly double _minCellSize;
        private readonly Func<QuadtreeNode, IntersectionType> _intersectFunc;
        public QuadtreeNode Root { get; }
        public SimpleQuadtree(
             double xMin, double xMax,
             double yMin, double yMax,
             int maxDepth,
             double minCellSize,
             Func<QuadtreeNode, IntersectionType> intersectFunc
         )
        {
            _maxDepth = maxDepth;
            _minCellSize = minCellSize;
            _intersectFunc = intersectFunc;
            Root = new QuadtreeNode(xMin, xMax, yMin, yMax);
            Subdivide(Root, 0);
        }
        private void Subdivide(QuadtreeNode node, int depth)
        {
            // 1. 判断关系
            node.Intersection = _intersectFunc(node);
            // 2. 若 Outside 或 达到细分极限 -> 不再下分
            if (node.Intersection == IntersectionType.Outside) return;
            double width = node.XMax - node.XMin;
            double height = node.YMax - node.YMin;
            bool canSubdivide = (depth < _maxDepth)
                && (width > _minCellSize)
                && (height > _minCellSize);
            // 3. 若 Inside 但可以继续细分(可根据需求决定),
            //    或 Partial 就要再细分
            if ((node.Intersection == IntersectionType.Partial && canSubdivide)
                || (node.Intersection == IntersectionType.Inside && canSubdivide))
            {
                node.Children = new QuadtreeNode[4];
                double midX = (node.XMin + node.XMax) / 2.0;
                double midY = (node.YMin + node.YMax) / 2.0;
                node.Children[0] = new QuadtreeNode(node.XMin, midX, node.YMin, midY);
                node.Children[1] = new QuadtreeNode(midX, node.XMax, node.YMin, midY);
                node.Children[2] = new QuadtreeNode(node.XMin, midX, midY, node.YMax);
                node.Children[3] = new QuadtreeNode(midX, node.XMax, midY, node.YMax);
                for (int i = 0; i < 4; i++)
                {
                    Subdivide(node.Children[i], depth + 1);
                }
            }
        }
        public List<QuadtreeNode> GetLeafNodes()
        {
            var leaves = new List<QuadtreeNode>();
            CollectLeaves(Root, leaves);
            return leaves;
        }
        private void CollectLeaves(QuadtreeNode node, List<QuadtreeNode> leaves)
        {
            if (node.IsLeaf)
            {
                leaves.Add(node);
            }
            else
            {
                foreach (var child in node.Children)
                {
                    CollectLeaves(child, leaves);
                }
            }
        }
    }
}
