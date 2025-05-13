using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Models
{
    public class Axis
    {
        public Line Line { get; }
        public bool IsVertical => Math.Abs(Line.StartPoint.X - Line.EndPoint.X) < 1e-6;
        public double Position => IsVertical ? Line.StartPoint.X : Line.StartPoint.Y;

        public Axis(Line line)
        {
            Line = line ?? throw new ArgumentNullException(nameof(line));
        }

        public static List<Axis> FromLines(IEnumerable<Line> lines)
        {
            return lines?
                .Where(l => l != null)
                .Select(l => new Axis(l))
                .OrderBy(a => a.Position)
                .ToList();
        }

        /// <summary>
        /// 根据轴线生成以其为中心扩展的区域。
        /// </summary>
        /// <param name="axes">输入轴线集合，须已按位置排序</param>
        /// <param name="fixedMin">垂直方向最小值</param>
        /// <param name="fixedMax">垂直方向最大值</param>
        public static List<Extents3d> GenerateCenteredRegions(List<Axis> axes, double fixedMin, double fixedMax)
        {
            var regions = new List<Extents3d>();
            if (axes == null || axes.Count == 0) return regions;

            for (int i = 0; i < axes.Count; i++)
            {
                double curr = axes[i].Position;

                double minPos = (i == 0)
                    ? curr - (axes[i + 1].Position - curr) / 2.0
                    : (axes[i - 1].Position + curr) / 2.0;

                double maxPos = (i == axes.Count - 1)
                    ? curr + (curr - axes[i - 1].Position) / 2.0
                    : (curr + axes[i + 1].Position) / 2.0;

                Extents3d region;
                if (axes[i].IsVertical)
                {
                    region = new Extents3d(
                        new Point3d(minPos, fixedMin, 0),
                        new Point3d(maxPos, fixedMax, 0));
                }
                else
                {
                    region = new Extents3d(
                        new Point3d(fixedMin, minPos, 0),
                        new Point3d(fixedMax, maxPos, 0));
                }

                regions.Add(region);
            }

            return regions;
        }
    }
}
