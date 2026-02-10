using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Cluster;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Cluster
{
    /// <summary>
    /// 轴线分析服务
    /// 替代旧 AxisDatas：轴线排序、区域划分、点分区、生成辅助实体
    /// </summary>
    public class AxisAnalysisService
    {
        /// <summary>轴线外扩范围</summary>
        public (double X, double Y) OuterExtension { get; set; } = (15000.0, 15000.0);

        /// <summary>
        /// 分析轴线，划分区域，将点分配到区域
        /// </summary>
        public AxisAnalysisResult AnalyzeAxes(List<Line> lines, List<Point3d> allPoints, double scale)
        {
            var result = new AxisAnalysisResult { Scale = scale };

            var axes = lines?.Where(l => l != null)
                             .Select(l => new AxisData(l))
                             .ToList() ?? new List<AxisData>();

            // 垂直轴线 = Y向，水平轴线 = X向
            result.YAxes = axes.Where(a => a.IsVertical).OrderBy(a => a.Position).ToList();
            result.XAxes = axes.Where(a => !a.IsVertical).OrderBy(a => a.Position).ToList();

            double minX = result.YAxes.Any() ? result.YAxes.Min(a => a.Position) - OuterExtension.X : -OuterExtension.X;
            double maxX = result.YAxes.Any() ? result.YAxes.Max(a => a.Position) + OuterExtension.X : OuterExtension.X;
            double minY = result.XAxes.Any() ? result.XAxes.Min(a => a.Position) - OuterExtension.Y : -OuterExtension.Y;
            double maxY = result.XAxes.Any() ? result.XAxes.Max(a => a.Position) + OuterExtension.Y : OuterExtension.Y;

            double d = 8.0 * scale; // 轴线圆直径

            // X向轴线标注为字母
            for (int i = 0; i < result.XAxes.Count; i++)
            {
                var ax = result.XAxes[i];
                ax.SerialNumber = i + 1;
                ax.Name = GetAlphabeticLabel(i);
                ax.CircleCenter = ComputeAxisCircleCenter(ax, true, d);

                result.AxisCircles.Add(new Circle(ax.CircleCenter, Vector3d.ZAxis, d / 2.0));
                result.AxisTexts.Add(CreateText(ax.Name, ax.CircleCenter, scale));
            }

            // Y向轴线标注为数字
            for (int j = 0; j < result.YAxes.Count; j++)
            {
                var ay = result.YAxes[j];
                ay.SerialNumber = j + 1;
                ay.Name = ay.SerialNumber.ToString();
                ay.CircleCenter = ComputeAxisCircleCenter(ay, false, d);

                result.AxisCircles.Add(new Circle(ay.CircleCenter, Vector3d.ZAxis, d / 2.0));
                result.AxisTexts.Add(CreateText(ay.Name, ay.CircleCenter, scale));
            }

            // 区域划分
            var yRegs = CenteredRegions(result.YAxes, minY, maxY, true);
            var xRegs = CenteredRegions(result.XAxes, minX, maxX, false);

            for (int i = 0; i < yRegs.Count; i++)
            {
                for (int j = 0; j < xRegs.Count; j++)
                {
                    string name = $"Region_{result.YAxes[i].Name}_{result.XAxes[j].Name}";
                    var ext = new Extents3d(
                        new Point3d(yRegs[i].MinPoint.X, xRegs[j].MinPoint.Y, 0),
                        new Point3d(yRegs[i].MaxPoint.X, xRegs[j].MaxPoint.Y, 0));

                    result.RegionMap[name] = ext;

                    var labelPt = new Point3d(
                        (ext.MinPoint.X + ext.MaxPoint.X) / 2,
                        (ext.MinPoint.Y + ext.MaxPoint.Y) / 2, 0);
                    result.RegionLabels[name] = labelPt;

                    result.RegionFrames.Add(CreateRectPolyline(ext));
                    result.RegionTexts.Add(CreateText(name, labelPt, scale));
                }
            }

            // 点分区
            if (allPoints?.Count > 0)
                result.PointsMap = GroupPointsByRegion(allPoints, result);

            return result;
        }

        #region 私有工具

        private List<Extents3d> CenteredRegions(List<AxisData> axes, double fMin, double fMax, bool vertical)
        {
            var list = new List<Extents3d>();
            if (axes == null || axes.Count == 0) return list;

            for (int i = 0; i < axes.Count; i++)
            {
                double p = axes[i].Position;
                double min, max;

                if (axes.Count == 1)
                {
                    double ext = vertical ? OuterExtension.Y : OuterExtension.X;
                    min = p - ext; max = p + ext;
                }
                else if (i == 0)
                {
                    double next = axes[i + 1].Position;
                    min = p - (vertical ? OuterExtension.Y : OuterExtension.X);
                    max = (p + next) / 2.0;
                }
                else if (i == axes.Count - 1)
                {
                    double prev = axes[i - 1].Position;
                    min = (prev + p) / 2.0;
                    max = p + (vertical ? OuterExtension.Y : OuterExtension.X);
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

        private static Point3d ComputeAxisCircleCenter(AxisData axis, bool horizontal, double diameter)
        {
            var pt = axis.Line.StartPoint;
            if ((horizontal && pt.X > axis.Line.EndPoint.X) ||
                (!horizontal && pt.Y > axis.Line.EndPoint.Y))
                pt = axis.Line.EndPoint;

            double off = diameter / 2.0;
            return horizontal
                ? new Point3d(pt.X - off, pt.Y, 0)
                : new Point3d(pt.X, pt.Y - off, 0);
        }

        private static DBText CreateText(string txt, Point3d pos, double scale)
        {
            return new DBText
            {
                TextString = txt,
                Position = pos,
                Height = 5 * scale,
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

        private Dictionary<Extents3d, RegionPointInfo> GroupPointsByRegion(
            List<Point3d> pts, AxisAnalysisResult result)
        {
            const double tol = 0.001;
            var map = new Dictionary<Extents3d, RegionPointInfo>();

            foreach (var p in pts)
            {
                Extents3d? best = null;
                double minDist = double.MaxValue;

                foreach (var kv in result.RegionMap)
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
                    if (!map.ContainsKey(best.Value))
                    {
                        var regionName = result.RegionMap
                            .FirstOrDefault(kv => kv.Value.Equals(best.Value)).Key;
                        var parts = regionName?.Split('_');
                        var yName = parts?.Length > 1 ? parts[1] : null;
                        var xName = parts?.Length > 2 ? parts[2] : null;

                        var yAxis = result.YAxes.FirstOrDefault(a => a.Name == yName);
                        var xAxis = result.XAxes.FirstOrDefault(a => a.Name == xName);

                        map[best.Value] = new RegionPointInfo
                        {
                            XAxis = xAxis,
                            YAxis = yAxis,
                            Points = new List<Point3d>()
                        };
                    }
                    map[best.Value].Points.Add(p);
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

        #endregion
    }

    #region 数据类型

    /// <summary>轴线数据</summary>
    public class AxisData
    {
        public Line Line { get; private set; }
        public string Name { get; set; } = string.Empty;
        public Point3d CircleCenter { get; set; }
        public int SerialNumber { get; set; }

        public bool IsVertical => Math.Abs(Line.StartPoint.X - Line.EndPoint.X) < 1e-3;
        public double Position => IsVertical ? Line.StartPoint.X : Line.StartPoint.Y;

        public AxisData(Line line)
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

    /// <summary>区域点信息</summary>
    public class RegionPointInfo
    {
        public List<Point3d> Points { get; set; } = new List<Point3d>();
        public AxisData XAxis { get; set; }
        public AxisData YAxis { get; set; }
    }

    /// <summary>轴线分析结果 DTO</summary>
    public class AxisAnalysisResult
    {
        public double Scale { get; set; }

        /// <summary>垂直轴线（Y向）集合</summary>
        public List<AxisData> YAxes { get; set; } = new List<AxisData>();

        /// <summary>水平轴线（X向）集合</summary>
        public List<AxisData> XAxes { get; set; } = new List<AxisData>();

        public Dictionary<string, Extents3d> RegionMap { get; } = new Dictionary<string, Extents3d>();
        public Dictionary<string, Point3d> RegionLabels { get; } = new Dictionary<string, Point3d>();
        public Dictionary<Extents3d, RegionPointInfo> PointsMap { get; set; } = new Dictionary<Extents3d, RegionPointInfo>();

        public List<Circle> AxisCircles { get; set; } = new List<Circle>();
        public List<DBText> AxisTexts { get; set; } = new List<DBText>();
        public List<Polyline> RegionFrames { get; set; } = new List<Polyline>();
        public List<DBText> RegionTexts { get; set; } = new List<DBText>();
    }

    #endregion
}
