using Autodesk.AutoCAD.Geometry;
using HyCADTool.Models.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Annotation
{
    /// <summary>
    /// 根据间距和区域高度/宽度判断是否多层级标注
    /// </summary>
    public class DimensionLayerGrouper
    {
        private readonly double _scale;
        private readonly Direction _direction;
        private readonly double _minSpacing;
        private readonly double _offsetThreshold;
        public DimensionLayerGrouper(double scale, Direction direction)
        {
            _scale = scale;
            _direction = direction;
            _minSpacing = 3 * _scale;
            _offsetThreshold = 3 * 5 * _scale;
        }
        public List<List<Point3d>> GroupPoints(List<Point3d> points)
        {
            var ordered = (_direction == Direction.X)
                ? points.OrderBy(p => p.X).ToList()
                : points.OrderBy(p => p.Y).ToList();
            bool needMultiLayer = CheckIfNeedMultiLayer(ordered);
            if (!needMultiLayer)
                return new List<List<Point3d>> { ordered };
            return SliceIntoBatches(ordered);
        }
        private bool CheckIfNeedMultiLayer(List<Point3d> pts)
        {
            int consecutiveClose = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                double delta = (_direction == Direction.X)
                    ? Math.Abs(pts[i].X - pts[i - 1].X)
                    : Math.Abs(pts[i].Y - pts[i - 1].Y);
                if (delta < _minSpacing) consecutiveClose++;
                else consecutiveClose = 0;
                if (consecutiveClose >= 3)
                    return true;
            }
            return false;
        }
        private List<List<Point3d>> SliceIntoBatches(List<Point3d> pts)
        {
            double minVal = (_direction == Direction.X) ? pts.Min(p => p.Y) : pts.Min(p => p.X);
            double maxVal = (_direction == Direction.X) ? pts.Max(p => p.Y) : pts.Max(p => p.X);
            double height = maxVal - minVal;
            int layers = (int)Math.Ceiling(height / _offsetThreshold);
            var batches = Enumerable.Range(0, layers).Select(_ => new List<Point3d>()).ToList();
            foreach (var pt in pts)
            {
                double coord = (_direction == Direction.X) ? pt.Y : pt.X;
                int index = (int)((coord - minVal) / _offsetThreshold);
                index = Math.Min(index, layers - 1);
                batches[index].Add(pt);
            }
            return batches.Where(b => b.Count > 0).ToList();
        }
    }
}
