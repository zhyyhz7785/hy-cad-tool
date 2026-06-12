using System.Collections.Generic;

namespace HyCADTool.Features.G101.Domain.Geometry
{
    public struct SketchPoint2
    {
        public double X { get; }
        public double Y { get; }

        public SketchPoint2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static SketchPoint2 operator +(SketchPoint2 a, SketchPoint2 b)
            => new SketchPoint2(a.X + b.X, a.Y + b.Y);

        public static SketchPoint2 operator -(SketchPoint2 a, SketchPoint2 b)
            => new SketchPoint2(a.X - b.X, a.Y - b.Y);
    }

    public enum SketchLayerKind
    {
        Concrete,
        Rebar,
        Dimension,
        Text,
        Hatch
    }

    public enum SketchLineKind
    {
        Solid,
        Rebar,
        Dimension,
        Leader
    }

    public sealed class SketchPolyline
    {
        public List<SketchPoint2> Points { get; } = new List<SketchPoint2>();
        public bool Closed { get; set; }
        public SketchLayerKind Layer { get; set; } = SketchLayerKind.Concrete;
        public SketchLineKind LineKind { get; set; } = SketchLineKind.Solid;
        public double LineWeight { get; set; } = 0.25;
    }

    public sealed class SketchArc
    {
        public SketchPoint2 Center { get; set; }
        public double Radius { get; set; }
        public double StartAngleDeg { get; set; }
        public double EndAngleDeg { get; set; }
        public SketchLayerKind Layer { get; set; } = SketchLayerKind.Rebar;
    }

    public sealed class SketchText
    {
        public SketchPoint2 Position { get; set; }
        public string Content { get; set; } = string.Empty;
        public double Height { get; set; } = 2.5;
        public double RotationDeg { get; set; }
        public SketchLayerKind Layer { get; set; } = SketchLayerKind.Text;
    }

    public sealed class SketchDimension
    {
        public SketchPoint2 P1 { get; set; }
        public SketchPoint2 P2 { get; set; }
        public SketchPoint2 DimLinePoint { get; set; }
        public string OverrideText { get; set; }
    }

    /// <summary>平台无关构造大样图元集合。</summary>
    public sealed class DetailSketch
    {
        public List<SketchPolyline> Polylines { get; } = new List<SketchPolyline>();
        public List<SketchArc> Arcs { get; } = new List<SketchArc>();
        public List<SketchText> Texts { get; } = new List<SketchText>();
        public List<SketchDimension> Dimensions { get; } = new List<SketchDimension>();
        public SketchPoint2 Origin { get; set; }
        public string Title { get; set; }

        public void AddRect(double x, double y, double w, double h, SketchLayerKind layer = SketchLayerKind.Concrete)
        {
            Polylines.Add(new SketchPolyline
            {
                Closed = true,
                Layer = layer,
                Points =
                {
                    new SketchPoint2(x, y),
                    new SketchPoint2(x + w, y),
                    new SketchPoint2(x + w, y + h),
                    new SketchPoint2(x, y + h),
                }
            });
        }

        public void AddLine(double x1, double y1, double x2, double y2,
            SketchLayerKind layer = SketchLayerKind.Rebar, SketchLineKind kind = SketchLineKind.Rebar)
        {
            Polylines.Add(new SketchPolyline
            {
                Layer = layer,
                LineKind = kind,
                Points = { new SketchPoint2(x1, y1), new SketchPoint2(x2, y2) }
            });
        }

        public void AddText(double x, double y, string text, double height = 2.5)
        {
            Texts.Add(new SketchText { Position = new SketchPoint2(x, y), Content = text, Height = height });
        }

        public void AddHorizontalDim(double x1, double x2, double y, string text = null)
        {
            Dimensions.Add(new SketchDimension
            {
                P1 = new SketchPoint2(x1, y),
                P2 = new SketchPoint2(x2, y),
                DimLinePoint = new SketchPoint2((x1 + x2) / 2, y + 8),
                OverrideText = text
            });
        }

        public void AddVerticalDim(double x, double y1, double y2, string text = null)
        {
            Dimensions.Add(new SketchDimension
            {
                P1 = new SketchPoint2(x, y1),
                P2 = new SketchPoint2(x, y2),
                DimLinePoint = new SketchPoint2(x + 8, (y1 + y2) / 2),
                OverrideText = text
            });
        }
    }
}
