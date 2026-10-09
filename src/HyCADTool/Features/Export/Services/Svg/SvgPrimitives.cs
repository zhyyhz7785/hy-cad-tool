using System.Collections.Generic;

namespace HyCADTool.Features.Export.Services.Svg
{
    internal enum SvgPrimitiveKind
    {
        Line,
        Circle,
        Ellipse,
        Path,
        Polygon,
        Text
    }

    internal enum SvgPathOp
    {
        Move,
        Line,
        Arc,
        Close
    }

    internal struct SvgPathSeg
    {
        public SvgPathOp Op;
        public double X;
        public double Y;
        public double Rx;
        public double Ry;
        public double XRotDeg;
        public bool Large;
        /// <summary>true = AutoCAD 坐标系下逆时针（写出时 Y 翻转会取反）。</summary>
        public bool SweepCcw;

        public static SvgPathSeg Move(double x, double y)
        {
            return new SvgPathSeg { Op = SvgPathOp.Move, X = x, Y = y };
        }

        public static SvgPathSeg LineTo(double x, double y)
        {
            return new SvgPathSeg { Op = SvgPathOp.Line, X = x, Y = y };
        }

        public static SvgPathSeg ArcTo(double rx, double ry, double xRotDeg, bool large, bool sweepCcw, double x, double y)
        {
            return new SvgPathSeg
            {
                Op = SvgPathOp.Arc,
                Rx = rx,
                Ry = ry,
                XRotDeg = xRotDeg,
                Large = large,
                SweepCcw = sweepCcw,
                X = x,
                Y = y
            };
        }

        public static SvgPathSeg Close()
        {
            return new SvgPathSeg { Op = SvgPathOp.Close };
        }
    }

    internal struct SvgPoint
    {
        public double X;
        public double Y;

        public SvgPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>展平后的二维图元，坐标为 DWG（Y 向上）；写出器负责翻转。</summary>
    internal sealed class SvgPrimitive
    {
        public SvgPrimitiveKind Kind { get; set; }
        public string Handle { get; set; }
        public string ParentHandle { get; set; }
        public string Layer { get; set; }
        public string Stroke { get; set; }
        public string Fill { get; set; }
        public double StrokeWidthMm { get; set; }
        /// <summary>true = StrokeWidthMm 实际是图形单位（多段线宽度），写出时不再乘单位/mm。</summary>
        public bool StrokeWidthInDrawingUnits { get; set; }

        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }

        public double Cx { get; set; }
        public double Cy { get; set; }
        public double R { get; set; }
        public double Rx { get; set; }
        public double Ry { get; set; }
        public double RotationDeg { get; set; }

        public List<SvgPathSeg> Path { get; set; }
        public List<SvgPoint> Points { get; set; }

        public string Text { get; set; }
        public double FontSize { get; set; }
        public string FontFamily { get; set; }
        public string TextAnchor { get; set; }
        public string DominantBaseline { get; set; }
        public double WidthFactor { get; set; }
        /// <summary>CAD 文字包围盒宽度（图形单位）。&gt;0 时写出 textLength，不再乘宽度因子。</summary>
        public double BoxWidth { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
    }

    internal sealed class SvgExportStats
    {
        public int SourceEntityCount { get; set; }
        public int PrimitiveCount { get; set; }
        public int ExplodedCount { get; set; }
        public Dictionary<string, int> Skipped { get; } = new Dictionary<string, int>();
        public string OutputPath { get; set; }
        public long ElapsedMs { get; set; }

        public void AddSkip(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                typeName = "?";
            if (Skipped.TryGetValue(typeName, out int n))
                Skipped[typeName] = n + 1;
            else
                Skipped[typeName] = 1;
        }
    }
}
