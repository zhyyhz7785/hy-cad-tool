using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Models
{
    /*─────────────────────────────  Axis  ─────────────────────────────*/
    public class Axis
    {
        /// <summary>内部比例因子（默认取全局 <see cref="BaseConfig.Scale"/>）。</summary>
        public static double Scale { get; set; } = BaseConfig.Scale;

        /// <summary>用户可调整的“基准直径”。程序实际直径 D = <c>BaseDiameter × Scale</c>。</summary>
        public static double BaseDiameter { get; set; } = 8.0;

        /// <summary>用于绘制圆的实际直径。</summary>
        public static double D => BaseDiameter * Scale;

        public Line Line { get; private set; }
        public string Name { get; set; } = string.Empty;
        public Point3d CircleCenter { get; set; }
        public int SerialNumber { get; set; }
        public (string Label, Circle Circle)? Annotation { get; set; }

        public bool IsVertical => Math.Abs(Line.StartPoint.X - Line.EndPoint.X) < 1e-3;
        public double Position => IsVertical ? Line.StartPoint.X : Line.StartPoint.Y;

        public Axis(Line line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            Line = EnsureDirection(line);
        }

        private Line EnsureDirection(Line line)
        {
            bool v = Math.Abs(line.StartPoint.X - line.EndPoint.X) < 1e-3;
            if (v && line.StartPoint.Y > line.EndPoint.Y) return new Line(line.EndPoint, line.StartPoint);
            if (!v && line.StartPoint.X > line.EndPoint.X) return new Line(line.EndPoint, line.StartPoint);
            return line;
        }
    }

    /*───────────────────────────  AxisDatas  ──────────────────────────*/
    public class AxisDatas
    {
        /// <summary>实例级比例（文字高度等随之变化）。</summary>
        public double Scale { get; }

        public List<Axis> XAxes { get; } = new List<Axis>();
        public List<Axis> YAxes { get; } = new List<Axis>();

        public Dictionary<string, Extents3d> RegionMap { get; } = new Dictionary<string, Extents3d>();
        public Dictionary<string, Point3d> RegionLabels { get; } = new Dictionary<string, Point3d>();
        public Dictionary<Extents3d, List<Point3d>> PointsMap { get; } = new Dictionary<Extents3d, List<Point3d>>();

        public List<Circle> AxisCircles { get; } = new List<Circle>();
        public List<DBText> AxisTexts { get; } = new List<DBText>();
        public List<Polyline> RegionFrames { get; } = new List<Polyline>();
        public List<DBText> RegionTexts { get; } = new List<DBText>();

        public static (double XExtend, double YExtend) OuterExtension { get; set; } = (15000.0, 15000.0);

        /*――― 工厂入口 ―――*/
        public static AxisDatas FromLines(IEnumerable<Line> lines, double scale = -1)
            => new AxisDatas(lines, null, scale < 0 ? BaseConfig.Scale : scale);

        public static AxisDatas FromLines(IEnumerable<Line> lines, List<Point3d> pts, double scale = -1)
            => new AxisDatas(lines, pts, scale < 0 ? BaseConfig.Scale : scale);

        /*――― 构造 ―――*/
        private AxisDatas(IEnumerable<Line> lines, List<Point3d> allPoints, double scale)
        {
            Scale = scale;
            Axis.Scale = scale;                 // 同步轴线比例

            var axes = lines?.Where(l => l != null)
                             .Select(l => new Axis(l))
                             .ToList() ?? new List<Axis>();

            XAxes = axes.Where(a => a.IsVertical).OrderBy(a => a.Position).ToList();
            YAxes = axes.Where(a => !a.IsVertical).OrderBy(a => a.Position).ToList();

            double minX = XAxes.Any() ? XAxes.Min(a => a.Position) - OuterExtension.XExtend : -OuterExtension.XExtend;
            double maxX = XAxes.Any() ? XAxes.Max(a => a.Position) + OuterExtension.XExtend : OuterExtension.XExtend;
            double minY = YAxes.Any() ? YAxes.Min(a => a.Position) - OuterExtension.YExtend : -OuterExtension.YExtend;
            double maxY = YAxes.Any() ? YAxes.Max(a => a.Position) + OuterExtension.YExtend : OuterExtension.YExtend;

            /*—— 生成圆与文字 ——*/
            for (int i = 0; i < XAxes.Count; i++)
            {
                var ax = XAxes[i];
                ax.SerialNumber = i + 1;
                ax.Name = ax.SerialNumber.ToString();
                ax.CircleCenter = ComputeAxisCircleCenter(ax, false);
                ax.Annotation = (ax.Name, new Circle(ax.CircleCenter, Vector3d.ZAxis, Axis.D / 2.0));

                AxisCircles.Add(ax.Annotation.Value.Circle);
                AxisTexts.Add(CreateText(ax.Name, ax.CircleCenter));
            }

            for (int j = 0; j < YAxes.Count; j++)
            {
                var ay = YAxes[j];
                ay.SerialNumber = j + 1;
                ay.Name = GetAlphabeticLabel(j);
                ay.CircleCenter = ComputeAxisCircleCenter(ay, true);
                ay.Annotation = (ay.Name, new Circle(ay.CircleCenter, Vector3d.ZAxis, Axis.D / 2.0));

                AxisCircles.Add(ay.Annotation.Value.Circle);
                AxisTexts.Add(CreateText(ay.Name, ay.CircleCenter));
            }

            /*—— 区域 ——*/
            var xRegs = CenteredRegions(XAxes, minY, maxY, true);
            var yRegs = CenteredRegions(YAxes, minX, maxX, false);

            for (int i = 0; i < xRegs.Count; i++)
                for (int j = 0; j < yRegs.Count; j++)
                {
                    string name = $"Region_{XAxes[i].Name}_{YAxes[j].Name}";
                    var ext = new Extents3d(
                        new Point3d(xRegs[i].MinPoint.X, yRegs[j].MinPoint.Y, 0),
                        new Point3d(xRegs[i].MaxPoint.X, yRegs[j].MaxPoint.Y, 0));

                    RegionMap[name] = ext;
                    RegionLabels[name] = new Point3d(
                        (ext.MinPoint.X + ext.MaxPoint.X) / 2,
                        (ext.MinPoint.Y + ext.MaxPoint.Y) / 2, 0);

                    RegionFrames.Add(CreateRectPolyline(ext));
                    RegionTexts.Add(CreateText(name, RegionLabels[name]));
                }

            if (allPoints?.Count > 0)
                PointsMap = GroupPointsByRegion(allPoints);
        }

        /*――― 私有工具 ―――*/
        private static List<Extents3d> CenteredRegions(List<Axis> axes, double fMin, double fMax, bool vertical)
        {
            var list = new List<Extents3d>();
            if (axes == null || axes.Count == 0) return list;

            for (int i = 0; i < axes.Count; i++)
            {
                double p = axes[i].Position;
                double min, max;

                if (axes.Count == 1)
                {
                    double ext = vertical ? OuterExtension.XExtend : OuterExtension.YExtend;
                    min = p - ext; max = p + ext;
                }
                else if (i == 0)
                {
                    double next = axes[i + 1].Position;
                    min = p - (vertical ? OuterExtension.XExtend : OuterExtension.YExtend);
                    max = (p + next) / 2.0;
                }
                else if (i == axes.Count - 1)
                {
                    double prev = axes[i - 1].Position;
                    min = (prev + p) / 2.0;
                    max = p + (vertical ? OuterExtension.XExtend : OuterExtension.YExtend);
                }
                else
                {
                    double prev = axes[i - 1].Position;
                    double next = axes[i + 1].Position;
                    min = (prev + p) / 2.0;
                    max = (p + next) / 2.0;
                }

                list.Add(vertical
                    ? new Extents3d(new Point3d(min, fMin, 0), new Point3d(max, fMax, 0))
                    : new Extents3d(new Point3d(fMin, min, 0), new Point3d(fMax, max, 0)));
            }
            return list;
        }

        private static Point3d ComputeAxisCircleCenter(Axis axis, bool horizontal)
        {
            var pt = axis.Line.StartPoint;
            if ((horizontal && pt.X > axis.Line.EndPoint.X) ||
                (!horizontal && pt.Y > axis.Line.EndPoint.Y))
                pt = axis.Line.EndPoint;

            double off = Axis.D / 2.0;
            return horizontal ? new Point3d(pt.X - off, pt.Y, 0)
                              : new Point3d(pt.X, pt.Y - off, 0);
        }

        private DBText CreateText(string txt, Point3d pos)
        {
            return new DBText
            {
                TextString = txt,
                Position = pos,
                Height = 5 * Scale,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = pos
            };
        }

        private static Polyline CreateRectPolyline(Extents3d ext)
        {
            var pl = new Polyline(4) { Closed = true };
            pl.AddVertexAt(0, new Point2d(ext.MinPoint.X, ext.MinPoint.Y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(ext.MaxPoint.X, ext.MinPoint.Y), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(ext.MinPoint.X, ext.MaxPoint.Y), 0, 0, 0);
            return pl;
        }

        private static string GetAlphabeticLabel(int idx)
        {
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            string label = "";
            do
            {
                label = letters[idx % 26] + label;
                idx = idx / 26 - 1;
            } while (idx >= 0);
            return label;
        }

        /*――― 点分区逻辑（与原实现一致） ―――*/
        public Dictionary<Extents3d, List<Point3d>> GroupPointsByRegion(List<Point3d> pts)
        {
            const double tol = 0.001;
            var map = new Dictionary<Extents3d, List<Point3d>>();
            var sorted = RegionMap.OrderBy(kv => kv.Key).ToList();

            foreach (var p in pts)
            {
                Extents3d? best = null;
                double minDist = double.MaxValue;

                foreach (var kv in sorted)
                {
                    var reg = kv.Value;
                    if (IsInside(reg, p))
                    {
                        double d = DistToBoundary(reg, p);
                        if (d < tol) { best = reg; break; }
                        if (d < minDist) { minDist = d; best = reg; }
                    }
                }

                if (best.HasValue)
                {
                    if (!map.ContainsKey(best.Value)) map[best.Value] = new List<Point3d>();
                    map[best.Value].Add(p);
                }
            }
            return map;
        }

        private static bool IsInside(Extents3d e, Point3d p)
            => p.X >= e.MinPoint.X && p.X <= e.MaxPoint.X
            && p.Y >= e.MinPoint.Y && p.Y <= e.MaxPoint.Y;

        private static double DistToBoundary(Extents3d e, Point3d p)
        {
            double dx = Math.Min(Math.Abs(p.X - e.MinPoint.X), Math.Abs(p.X - e.MaxPoint.X));
            double dy = Math.Min(Math.Abs(p.Y - e.MinPoint.Y), Math.Abs(p.Y - e.MaxPoint.Y));
            return Math.Min(dx, dy);
        }

    }
}
