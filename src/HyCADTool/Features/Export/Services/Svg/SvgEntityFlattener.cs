using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcColor = Autodesk.AutoCAD.Colors.Color;

namespace HyCADTool.Features.Export.Services.Svg
{
    /// <summary>
    /// 把 AutoCAD 实体展平为 SVG 基元：基元直出，复合实体递归 Explode（深度 8）。
    /// </summary>
    internal sealed class SvgEntityFlattener
    {
        private const int MaxDepth = 8;
        private const double DefaultLineMm = 0.25;
        private const string ShxFallback = "Microsoft YaHei, sans-serif";

        private readonly Transaction _tr;
        private readonly Database _db;
        private readonly Dictionary<string, LayerInfo> _layers = new Dictionary<string, LayerInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<ObjectId, StyleInfo> _styles = new Dictionary<ObjectId, StyleInfo>();

        public SvgEntityFlattener(Transaction tr, Database db)
        {
            _tr = tr;
            _db = db;
        }

        public List<SvgPrimitive> Flatten(IEnumerable<ObjectId> ids, SvgExportStats stats)
        {
            var list = new List<SvgPrimitive>();
            foreach (var id in ids)
            {
                if (id.IsNull || id.IsErased)
                    continue;
                Entity ent;
                try { ent = _tr.GetObject(id, OpenMode.ForRead) as Entity; }
                catch { continue; }
                if (ent == null)
                    continue;
                stats.SourceEntityCount++;
                FlattenEntity(ent, 0, null, null, null, list, stats);
            }
            stats.PrimitiveCount = list.Count;
            return list;
        }

        public static List<ObjectId> FilterIntersecting(Transaction tr, IEnumerable<ObjectId> ids, SvgPlotRect window)
        {
            var result = new List<ObjectId>();
            if (ids == null)
                return result;
            foreach (var id in ids)
            {
                if (id.IsNull || id.IsErased)
                    continue;
                Entity ent;
                try { ent = tr.GetObject(id, OpenMode.ForRead) as Entity; }
                catch { continue; }
                if (ent == null)
                    continue;
                Extents3d ext;
                try { ext = ent.GeometricExtents; }
                catch
                {
                    result.Add(id);
                    continue;
                }
                if (ext.MaxPoint.X < window.MinX || ext.MinPoint.X > window.MaxX
                    || ext.MaxPoint.Y < window.MinY || ext.MinPoint.Y > window.MaxY)
                    continue;
                result.Add(id);
            }
            return result;
        }

        public static bool TryUnionExtents(Transaction tr, IEnumerable<ObjectId> ids, out SvgPlotRect bounds)
        {
            bounds = default;
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            bool any = false;
            if (ids == null)
                return false;
            foreach (var id in ids)
            {
                if (id.IsNull || id.IsErased)
                    continue;
                Entity ent;
                try { ent = tr.GetObject(id, OpenMode.ForRead) as Entity; }
                catch { continue; }
                if (ent == null)
                    continue;
                Extents3d ext;
                try { ext = ent.GeometricExtents; }
                catch { continue; }
                if (ext.MinPoint.X < minX) minX = ext.MinPoint.X;
                if (ext.MinPoint.Y < minY) minY = ext.MinPoint.Y;
                if (ext.MaxPoint.X > maxX) maxX = ext.MaxPoint.X;
                if (ext.MaxPoint.Y > maxY) maxY = ext.MaxPoint.Y;
                any = true;
            }
            if (!any)
                return false;
            bounds = new SvgPlotRect { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
            return bounds.IsValid;
        }

        public static bool TryContentBounds(IReadOnlyList<SvgPrimitive> primitives, out SvgPlotRect bounds)
        {
            bounds = default;
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            if (primitives == null || primitives.Count == 0)
                return false;
            for (int i = 0; i < primitives.Count; i++)
                Acc(primitives[i], ref minX, ref minY, ref maxX, ref maxY);
            if (maxX < minX || maxY < minY)
                return false;
            bounds = new SvgPlotRect { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
            return bounds.IsValid;
        }

        private static void Acc(SvgPrimitive p, ref double minX, ref double minY, ref double maxX, ref double maxY)
        {
            switch (p.Kind)
            {
                case SvgPrimitiveKind.Line:
                    AccPt(p.X1, p.Y1, ref minX, ref minY, ref maxX, ref maxY);
                    AccPt(p.X2, p.Y2, ref minX, ref minY, ref maxX, ref maxY);
                    break;
                case SvgPrimitiveKind.Circle:
                    AccPt(p.Cx - p.R, p.Cy - p.R, ref minX, ref minY, ref maxX, ref maxY);
                    AccPt(p.Cx + p.R, p.Cy + p.R, ref minX, ref minY, ref maxX, ref maxY);
                    break;
                case SvgPrimitiveKind.Ellipse:
                    AccPt(p.Cx - p.Rx, p.Cy - p.Ry, ref minX, ref minY, ref maxX, ref maxY);
                    AccPt(p.Cx + p.Rx, p.Cy + p.Ry, ref minX, ref minY, ref maxX, ref maxY);
                    break;
                case SvgPrimitiveKind.Text:
                    AccPt(p.Cx, p.Cy, ref minX, ref minY, ref maxX, ref maxY);
                    AccPt(p.Cx + p.FontSize, p.Cy + p.FontSize, ref minX, ref minY, ref maxX, ref maxY);
                    break;
                case SvgPrimitiveKind.Path:
                    if (p.Path != null)
                    {
                        for (int i = 0; i < p.Path.Count; i++)
                        {
                            var s = p.Path[i];
                            if (s.Op == SvgPathOp.Close) continue;
                            AccPt(s.X, s.Y, ref minX, ref minY, ref maxX, ref maxY);
                        }
                    }
                    break;
                case SvgPrimitiveKind.Polygon:
                    if (p.Points != null)
                    {
                        for (int i = 0; i < p.Points.Count; i++)
                            AccPt(p.Points[i].X, p.Points[i].Y, ref minX, ref minY, ref maxX, ref maxY);
                    }
                    break;
            }
        }

        private static void AccPt(double x, double y, ref double minX, ref double minY, ref double maxX, ref double maxY)
        {
            if (x < minX) minX = x;
            if (y < minY) minY = y;
            if (x > maxX) maxX = x;
            if (y > maxY) maxY = y;
        }

        private void FlattenEntity(
            Entity ent, int depth, string parentHandle, string byBlockHex, LineWeight? byBlockLw,
            List<SvgPrimitive> outList, SvgExportStats stats)
        {
            if (ent == null)
                return;
            try
            {
                if (!ent.Visible)
                {
                    stats.AddSkip("Invisible");
                    return;
                }
            }
            catch { }

            var layer = GetLayer(ent.Layer);
            if (layer != null && layer.Skip)
            {
                stats.AddSkip("LayerOffOrFrozen");
                return;
            }

            string handle = HandleOf(ent);
            string color = ResolveColor(ent, layer, byBlockHex);
            double strokeMm = ResolveStrokeMm(ent, layer, byBlockLw);

            try
            {
                switch (ent)
                {
                    case Line line:
                        AddLine(outList, line.StartPoint, line.EndPoint, handle, parentHandle, ent.Layer, color, strokeMm);
                        return;
                    case Circle circle:
                        outList.Add(new SvgPrimitive
                        {
                            Kind = SvgPrimitiveKind.Circle,
                            Handle = handle,
                            ParentHandle = parentHandle,
                            Layer = ent.Layer,
                            Stroke = color,
                            Fill = "none",
                            StrokeWidthMm = strokeMm,
                            Cx = circle.Center.X,
                            Cy = circle.Center.Y,
                            R = circle.Radius
                        });
                        return;
                    case Arc arc:
                        AddArc(outList, arc.Center.X, arc.Center.Y, arc.Radius, arc.Radius, 0,
                            arc.StartAngle, arc.EndAngle, true, handle, parentHandle, ent.Layer, color, strokeMm);
                        return;
                    case Ellipse ellipse:
                        AddEllipse(outList, ellipse, handle, parentHandle, color, strokeMm);
                        return;
                    case Polyline pl:
                        AddLwPolyline(outList, pl, handle, parentHandle, color, strokeMm);
                        return;
                    case Polyline2d _:
                    case Polyline3d _:
                    case Spline _:
                        AddSampledCurve(outList, (Curve)ent, handle, parentHandle, color, strokeMm);
                        return;
                    case Solid solid:
                        AddSolid(outList, solid, handle, parentHandle, color);
                        return;
                    case Face face:
                        AddFace(outList, face, handle, parentHandle, color, strokeMm);
                        return;
                    case DBText txt:
                        AddText(outList, txt, handle, parentHandle, color);
                        return;
                    case Hatch hatch:
                        AddHatch(outList, hatch, depth, handle, parentHandle, color, strokeMm, byBlockHex, byBlockLw, stats);
                        return;
                    case Wipeout wo:
                        AddWipeout(outList, wo, handle, parentHandle);
                        return;
                    case Table _:
                    case Dimension _:
                    case MLeader _:
                    case Leader _:
                    case MText _:
                        ExplodeComposite(ent, depth, handle ?? parentHandle, color, ent.LineWeight, outList, stats);
                        return;
                    case BlockReference br:
                        ExplodeComposite(br, depth, handle, color, ent.LineWeight, outList, stats);
                        AddBlockAttributes(br, depth, handle, color, outList, stats);
                        return;
                    case Region _:
                    case Solid3d _:
                    case Xline _:
                    case Ray _:
                        stats.AddSkip(ent.GetType().Name);
                        return;
                    default:
                        if (depth < MaxDepth)
                            ExplodeComposite(ent, depth, handle ?? parentHandle, color, ent.LineWeight, outList, stats);
                        else
                            stats.AddSkip(ent.GetRXClass()?.Name ?? ent.GetType().Name);
                        return;
                }
            }
            catch
            {
                if (depth < MaxDepth)
                {
                    try { ExplodeComposite(ent, depth, handle ?? parentHandle, color, ent.LineWeight, outList, stats); }
                    catch { stats.AddSkip(ent.GetType().Name); }
                }
                else
                    stats.AddSkip(ent.GetType().Name);
            }
        }

        private void ExplodeComposite(
            Entity ent, int depth, string parentHandle, string byBlockHex, LineWeight byBlockLw,
            List<SvgPrimitive> outList, SvgExportStats stats)
        {
            if (depth >= MaxDepth)
            {
                stats.AddSkip(ent.GetRXClass()?.Name ?? ent.GetType().Name);
                return;
            }

            var pieces = new DBObjectCollection();
            try
            {
                ent.Explode(pieces);
            }
            catch
            {
                stats.AddSkip(ent.GetRXClass()?.Name ?? ent.GetType().Name);
                return;
            }

            stats.ExplodedCount++;
            foreach (DBObject obj in pieces)
            {
                try
                {
                    if (obj is Entity child)
                        FlattenEntity(child, depth + 1, parentHandle, byBlockHex, byBlockLw, outList, stats);
                }
                finally
                {
                    if (obj != null && obj.ObjectId.IsNull)
                    {
                        try { obj.Dispose(); } catch { }
                    }
                }
            }
        }

        private void AddBlockAttributes(
            BlockReference br, int depth, string parentHandle, string color,
            List<SvgPrimitive> outList, SvgExportStats stats)
        {
            if (br.AttributeCollection == null)
                return;
            foreach (ObjectId aid in br.AttributeCollection)
            {
                if (aid.IsNull || aid.IsErased)
                    continue;
                AttributeReference ar;
                try { ar = _tr.GetObject(aid, OpenMode.ForRead) as AttributeReference; }
                catch { continue; }
                if (ar == null)
                    continue;
                try
                {
                    if (ar.Invisible)
                        continue;
                }
                catch { }
                FlattenEntity(ar, depth + 1, parentHandle, color, br.LineWeight, outList, stats);
            }
        }

        private static void AddLine(
            List<SvgPrimitive> outList, Point3d a, Point3d b,
            string handle, string parent, string layer, string color, double strokeMm)
        {
            outList.Add(new SvgPrimitive
            {
                Kind = SvgPrimitiveKind.Line,
                Handle = handle,
                ParentHandle = parent,
                Layer = layer,
                Stroke = color,
                Fill = "none",
                StrokeWidthMm = strokeMm,
                X1 = a.X,
                Y1 = a.Y,
                X2 = b.X,
                Y2 = b.Y
            });
        }

        private static void AddArc(
            List<SvgPrimitive> outList,
            double cx, double cy, double rx, double ry, double rotDeg,
            double startRad, double endRad, bool ccw,
            string handle, string parent, string layer, string color, double strokeMm)
        {
            double sweep = endRad - startRad;
            if (!ccw)
                sweep = startRad - endRad;
            while (sweep < 0) sweep += Math.PI * 2;
            while (sweep >= Math.PI * 2) sweep -= Math.PI * 2;
            if (sweep < 1e-12)
                sweep = Math.PI * 2 - 1e-9;

            double sx = cx + rx * Math.Cos(startRad);
            double sy = cy + ry * Math.Sin(startRad);
            double ex = cx + rx * Math.Cos(endRad);
            double ey = cy + ry * Math.Sin(endRad);
            bool large = sweep > Math.PI;

            var path = new List<SvgPathSeg>
            {
                SvgPathSeg.Move(sx, sy),
                SvgPathSeg.ArcTo(rx, ry, rotDeg, large, ccw, ex, ey)
            };
            outList.Add(new SvgPrimitive
            {
                Kind = SvgPrimitiveKind.Path,
                Handle = handle,
                ParentHandle = parent,
                Layer = layer,
                Stroke = color,
                Fill = "none",
                StrokeWidthMm = strokeMm,
                Path = path
            });
        }

        private void AddEllipse(List<SvgPrimitive> outList, Ellipse ell, string handle, string parent, string color, double strokeMm)
        {
            var maj = ell.MajorAxis;
            double rx = maj.Length;
            double ry = rx * ell.RadiusRatio;
            double rotDeg = Math.Atan2(maj.Y, maj.X) * 180.0 / Math.PI;
            double start = ell.StartAngle;
            double end = ell.EndAngle;
            double sweep = end - start;
            while (sweep < 0) sweep += Math.PI * 2;
            bool full = sweep < 1e-8 || Math.Abs(sweep - Math.PI * 2) < 1e-6;

            if (full)
            {
                outList.Add(new SvgPrimitive
                {
                    Kind = SvgPrimitiveKind.Ellipse,
                    Handle = handle,
                    ParentHandle = parent,
                    Layer = ell.Layer,
                    Stroke = color,
                    Fill = "none",
                    StrokeWidthMm = strokeMm,
                    Cx = ell.Center.X,
                    Cy = ell.Center.Y,
                    Rx = rx,
                    Ry = ry,
                    RotationDeg = rotDeg
                });
                return;
            }

            var s = ell.StartPoint;
            var e = ell.EndPoint;
            bool large = sweep > Math.PI;
            var path = new List<SvgPathSeg>
            {
                SvgPathSeg.Move(s.X, s.Y),
                SvgPathSeg.ArcTo(rx, ry, rotDeg, large, true, e.X, e.Y)
            };
            outList.Add(new SvgPrimitive
            {
                Kind = SvgPrimitiveKind.Path,
                Handle = handle,
                ParentHandle = parent,
                Layer = ell.Layer,
                Stroke = color,
                Fill = "none",
                StrokeWidthMm = strokeMm,
                Path = path
            });
        }

        private void AddLwPolyline(List<SvgPrimitive> outList, Polyline pl, string handle, string parent, string color, double strokeMm)
        {
            int n = pl.NumberOfVertices;
            if (n < 2)
                return;
            double width = 0;
            try { width = pl.ConstantWidth; } catch { }
            if (width <= 1e-9)
            {
                for (int i = 0; i < n; i++)
                {
                    double sw = 0, ew = 0;
                    try { sw = pl.GetStartWidthAt(i); } catch { }
                    try { ew = pl.GetEndWidthAt(i); } catch { }
                    if (sw > width) width = sw;
                    if (ew > width) width = ew;
                }
            }
            bool drawingUnits = width > 1e-9;
            if (drawingUnits)
                strokeMm = width;

            var path = new List<SvgPathSeg>();
            var p0 = pl.GetPoint2dAt(0);
            path.Add(SvgPathSeg.Move(p0.X, p0.Y));
            int last = pl.Closed ? n : n - 1;
            for (int i = 0; i < last; i++)
            {
                int j = (i + 1) % n;
                var a = pl.GetPoint2dAt(i);
                var b = pl.GetPoint2dAt(j);
                double bulge = 0;
                try { bulge = pl.GetBulgeAt(i); } catch { }
                AppendBulge(path, a.X, a.Y, b.X, b.Y, bulge);
            }
            if (pl.Closed)
                path.Add(SvgPathSeg.Close());

            outList.Add(new SvgPrimitive
            {
                Kind = SvgPrimitiveKind.Path,
                Handle = handle,
                ParentHandle = parent,
                Layer = pl.Layer,
                Stroke = color,
                Fill = "none",
                StrokeWidthMm = strokeMm,
                StrokeWidthInDrawingUnits = drawingUnits,
                Path = path
            });
        }

        private static void AppendBulge(List<SvgPathSeg> path, double x1, double y1, double x2, double y2, double bulge)
        {
            if (Math.Abs(bulge) < 1e-12)
            {
                path.Add(SvgPathSeg.LineTo(x2, y2));
                return;
            }

            double dx = x2 - x1;
            double dy = y2 - y1;
            double chord = Math.Sqrt(dx * dx + dy * dy);
            if (chord < 1e-12)
                return;

            double included = 4.0 * Math.Atan(bulge);
            double radius = chord / (2.0 * Math.Sin(Math.Abs(included) / 2.0));
            bool large = Math.Abs(included) > Math.PI;
            bool ccw = bulge > 0;
            path.Add(SvgPathSeg.ArcTo(radius, radius, 0, large, ccw, x2, y2));
        }

        private static void AddSampledCurve(
            List<SvgPrimitive> outList, Curve curve,
            string handle, string parent, string color, double strokeMm)
        {
            double start, end;
            try
            {
                start = curve.StartParam;
                end = curve.EndParam;
            }
            catch
            {
                return;
            }

            int n = Math.Max(16, (int)Math.Ceiling(Math.Abs(end - start) * 12));
            if (n > 256) n = 256;
            var path = new List<SvgPathSeg>();
            for (int i = 0; i <= n; i++)
            {
                double t = start + (end - start) * i / n;
                Point3d p;
                try { p = curve.GetPointAtParameter(t); }
                catch { continue; }
                if (path.Count == 0)
                    path.Add(SvgPathSeg.Move(p.X, p.Y));
                else
                    path.Add(SvgPathSeg.LineTo(p.X, p.Y));
            }
            if (path.Count < 2)
                return;
            bool closed = false;
            try { closed = curve.Closed; } catch { }
            if (closed)
                path.Add(SvgPathSeg.Close());

            outList.Add(new SvgPrimitive
            {
                Kind = SvgPrimitiveKind.Path,
                Handle = handle,
                ParentHandle = parent,
                Layer = curve.Layer,
                Stroke = color,
                Fill = "none",
                StrokeWidthMm = strokeMm,
                Path = path
            });
        }

        private static void AddSolid(List<SvgPrimitive> outList, Solid solid, string handle, string parent, string color)
        {
            var pts = new List<SvgPoint>(4);
            for (int i = 0; i < 4; i++)
            {
                Point3d p;
                try { p = solid.GetPointAt((short)i); }
                catch { continue; }
                pts.Add(new SvgPoint(p.X, p.Y));
            }
            if (pts.Count < 3)
                return;
            // AutoCAD Solid 第 3/4 点顺序与多边形交叉，交换最后两点常见。
            if (pts.Count == 4)
            {
                var t = pts[2];
                pts[2] = pts[3];
                pts[3] = t;
            }
            outList.Add(new SvgPrimitive
            {
                Kind = SvgPrimitiveKind.Polygon,
                Handle = handle,
                ParentHandle = parent,
                Layer = solid.Layer,
                Stroke = "none",
                Fill = color,
                Points = pts
            });
        }

        private static void AddFace(List<SvgPrimitive> outList, Face face, string handle, string parent, string color, double strokeMm)
        {
            var pts = new List<SvgPoint>(4);
            for (int i = 0; i < 4; i++)
            {
                Point3d p;
                try { p = face.GetVertexAt((short)i); }
                catch { continue; }
                pts.Add(new SvgPoint(p.X, p.Y));
            }
            if (pts.Count < 3)
                return;
            outList.Add(new SvgPrimitive
            {
                Kind = SvgPrimitiveKind.Polygon,
                Handle = handle,
                ParentHandle = parent,
                Layer = face.Layer,
                Stroke = color,
                Fill = "none",
                StrokeWidthMm = strokeMm,
                Points = pts
            });
        }

        private void AddText(List<SvgPrimitive> outList, DBText txt, string handle, string parent, string color)
        {
            string content;
            try { content = txt.TextString ?? ""; }
            catch { content = ""; }
            if (content.Length == 0)
                return;
            content = DecodeCadText(content);

            double height = 2.5;
            try { height = txt.Height; } catch { }
            double rot = 0;
            try { rot = txt.Rotation * 180.0 / Math.PI; } catch { }
            double wf = 1;
            try { wf = txt.WidthFactor; } catch { }
            if (wf <= 0) wf = 1;

            Point3d pos;
            string anchor;
            string baseline;
            double boxWidth = 0;
            if (!TryTextBox(txt, height, wf, out pos, out boxWidth))
            {
                pos = txt.Position;
                try
                {
                    if (txt.HorizontalMode != TextHorizontalMode.TextLeft
                        || txt.VerticalMode != TextVerticalMode.TextBase)
                        pos = txt.AlignmentPoint;
                }
                catch { }

                anchor = "start";
                try
                {
                    switch (txt.HorizontalMode)
                    {
                        case TextHorizontalMode.TextCenter:
                        case TextHorizontalMode.TextMid:
                            anchor = "middle";
                            break;
                        case TextHorizontalMode.TextRight:
                            anchor = "end";
                            break;
                    }
                }
                catch { }

                baseline = "alphabetic";
                try
                {
                    switch (txt.VerticalMode)
                    {
                        case TextVerticalMode.TextTop:
                            baseline = "hanging";
                            break;
                        case TextVerticalMode.TextVerticalMid:
                            baseline = "central";
                            break;
                        case TextVerticalMode.TextBottom:
                            baseline = "ideographic";
                            break;
                    }
                }
                catch { }
            }
            else
            {
                anchor = "middle";
                baseline = "central";
            }

            var style = GetStyle(txt.TextStyleId);
            outList.Add(new SvgPrimitive
            {
                Kind = SvgPrimitiveKind.Text,
                Handle = handle,
                ParentHandle = parent,
                Layer = txt.Layer,
                Stroke = "none",
                Fill = color,
                Cx = pos.X,
                Cy = pos.Y,
                Text = content,
                FontSize = height,
                FontFamily = style.Family,
                TextAnchor = anchor,
                DominantBaseline = baseline,
                WidthFactor = wf,
                BoxWidth = boxWidth,
                Bold = style.Bold,
                Italic = style.Italic,
                RotationDeg = rot
            });
        }

        private static bool TryTextBox(DBText txt, double height, double widthFactor, out Point3d center, out double boxWidth)
        {
            center = Point3d.Origin;
            boxWidth = 0;
            try
            {
                var ext = txt.GeometricExtents;
                double h = ext.MaxPoint.Y - ext.MinPoint.Y;
                if (h <= 1e-9)
                    return false;
                double w = ext.MaxPoint.X - ext.MinPoint.X;
                double cx = (ext.MinPoint.X + ext.MaxPoint.X) * 0.5;
                if (w <= 1e-9)
                {
                    cx = ext.MinPoint.X;
                    double wf = widthFactor > 0 ? widthFactor : 1;
                    w = height * wf * 0.5;
                }
                center = new Point3d(cx, (ext.MinPoint.Y + ext.MaxPoint.Y) * 0.5, 0);
                boxWidth = w;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>AutoCAD 文字转义：%%c Ø、%%d °、%%p ±、%%nnn 码位、%%% 百分号；%%u/%%o 去掉。</summary>
        private static string DecodeCadText(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('%') < 0)
                return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '%' || i + 1 >= s.Length || s[i + 1] != '%')
                {
                    sb.Append(s[i]);
                    continue;
                }
                if (i + 2 >= s.Length)
                {
                    sb.Append('%');
                    continue;
                }
                char k = s[i + 2];
                if (k == '%')
                {
                    sb.Append('%');
                    i += 2;
                    continue;
                }
                if (k == 'c' || k == 'C')
                {
                    sb.Append('\u00D8');
                    i += 2;
                    continue;
                }
                if (k == 'd' || k == 'D')
                {
                    sb.Append('\u00B0');
                    i += 2;
                    continue;
                }
                if (k == 'p' || k == 'P')
                {
                    sb.Append('\u00B1');
                    i += 2;
                    continue;
                }
                if (k == 'u' || k == 'U' || k == 'o' || k == 'O')
                {
                    i += 2;
                    continue;
                }
                int digits = 0;
                int code = 0;
                int j = i + 2;
                while (j < s.Length && digits < 3 && s[j] >= '0' && s[j] <= '9')
                {
                    code = code * 10 + (s[j] - '0');
                    digits++;
                    j++;
                }
                if (digits == 3)
                {
                    sb.Append((char)code);
                    i = j - 1;
                    continue;
                }
                sb.Append(s[i]);
            }
            return sb.ToString();
        }

        private void AddHatch(
            List<SvgPrimitive> outList, Hatch hatch, int depth,
            string handle, string parent, string color, double strokeMm,
            string byBlockHex, LineWeight? byBlockLw, SvgExportStats stats)
        {
            string pattern = "";
            try { pattern = hatch.PatternName ?? ""; } catch { }
            bool solid = string.Equals(pattern, "SOLID", StringComparison.OrdinalIgnoreCase);

            if (solid)
            {
                var path = new List<SvgPathSeg>();
                int loops = 0;
                try { loops = hatch.NumberOfLoops; } catch { }
                bool any = false;
                for (int i = 0; i < loops; i++)
                {
                    HatchLoop loop;
                    try { loop = hatch.GetLoopAt(i); }
                    catch { continue; }
                    if (AppendHatchLoop(path, loop))
                        any = true;
                }
                if (any)
                {
                    outList.Add(new SvgPrimitive
                    {
                        Kind = SvgPrimitiveKind.Path,
                        Handle = handle,
                        ParentHandle = parent,
                        Layer = hatch.Layer,
                        Stroke = "none",
                        Fill = color,
                        Path = path
                    });
                    return;
                }
            }

            ExplodeComposite(hatch, depth, handle ?? parent, color, hatch.LineWeight, outList, stats);
        }

        private static bool AppendHatchLoop(List<SvgPathSeg> path, HatchLoop loop)
        {
            try
            {
                if ((loop.LoopType & HatchLoopTypes.Polyline) == HatchLoopTypes.Polyline)
                {
                    var bvc = loop.Polyline;
                    if (bvc == null || bvc.Count == 0)
                        return false;
                    var first = bvc[0];
                    path.Add(SvgPathSeg.Move(first.Vertex.X, first.Vertex.Y));
                    for (int i = 0; i < bvc.Count; i++)
                    {
                        var a = bvc[i];
                        var b = bvc[(i + 1) % bvc.Count];
                        AppendBulge(path, a.Vertex.X, a.Vertex.Y, b.Vertex.X, b.Vertex.Y, a.Bulge);
                    }
                    path.Add(SvgPathSeg.Close());
                    return true;
                }

                var curves = loop.Curves;
                if (curves == null || curves.Count == 0)
                    return false;
                bool started = false;
                foreach (Curve2d c in curves)
                {
                    started |= AppendCurve2d(path, c, !started);
                }
                path.Add(SvgPathSeg.Close());
                return started;
            }
            catch
            {
                return false;
            }
        }

        private static bool AppendCurve2d(List<SvgPathSeg> path, Curve2d c, bool move)
        {
            if (c is LineSegment2d ls)
            {
                if (move)
                    path.Add(SvgPathSeg.Move(ls.StartPoint.X, ls.StartPoint.Y));
                path.Add(SvgPathSeg.LineTo(ls.EndPoint.X, ls.EndPoint.Y));
                return true;
            }
            if (c is CircularArc2d arc)
            {
                var s = arc.StartPoint;
                var e = arc.EndPoint;
                if (move)
                    path.Add(SvgPathSeg.Move(s.X, s.Y));
                double sweep = arc.EndAngle - arc.StartAngle;
                if (arc.IsClockWise)
                    sweep = arc.StartAngle - arc.EndAngle;
                while (sweep < 0) sweep += Math.PI * 2;
                bool large = sweep > Math.PI;
                bool ccw = !arc.IsClockWise;
                path.Add(SvgPathSeg.ArcTo(arc.Radius, arc.Radius, 0, large, ccw, e.X, e.Y));
                return true;
            }

            Interval iv;
            try { iv = c.GetInterval(); }
            catch { return false; }
            int n = 16;
            for (int i = 0; i <= n; i++)
            {
                double t = iv.LowerBound + (iv.UpperBound - iv.LowerBound) * i / n;
                var p = c.EvaluatePoint(t);
                if (move && i == 0)
                    path.Add(SvgPathSeg.Move(p.X, p.Y));
                else
                    path.Add(SvgPathSeg.LineTo(p.X, p.Y));
            }
            return true;
        }

        private void AddWipeout(List<SvgPrimitive> outList, Wipeout wo, string handle, string parent)
        {
            var pts = new List<SvgPoint>();
            try
            {
                var ext = wo.GeometricExtents;
                pts.Add(new SvgPoint(ext.MinPoint.X, ext.MinPoint.Y));
                pts.Add(new SvgPoint(ext.MaxPoint.X, ext.MinPoint.Y));
                pts.Add(new SvgPoint(ext.MaxPoint.X, ext.MaxPoint.Y));
                pts.Add(new SvgPoint(ext.MinPoint.X, ext.MaxPoint.Y));
            }
            catch { return; }

            if (pts.Count < 3)
                return;

            outList.Add(new SvgPrimitive
            {
                Kind = SvgPrimitiveKind.Polygon,
                Handle = handle,
                ParentHandle = parent,
                Layer = wo.Layer,
                Stroke = "none",
                Fill = "#ffffff",
                Points = pts
            });
        }

        private string ResolveColor(Entity ent, LayerInfo layer, string byBlockHex)
        {
            AcColor c;
            try { c = ent.Color; }
            catch { return layer != null ? ColorToHex(layer.Color) : "#000000"; }

            if (c.IsByLayer)
                return layer != null ? ColorToHex(layer.Color) : "#000000";
            if (c.IsByBlock)
                return string.IsNullOrEmpty(byBlockHex) ? "#000000" : byBlockHex;
            return ColorToHex(c);
        }

        private static string ColorToHex(AcColor c)
        {
            if (c == null)
                return "#000000";
            try
            {
                if (c.IsByAci || (c.ColorIndex > 0 && c.ColorIndex < 256 && c.ColorMethod != ColorMethod.ByColor))
                {
                    short idx = c.ColorIndex;
                    if (idx == 7)
                        return "#000000";
                    int rgb = EntityColor.LookUpRgb((byte)idx);
                    int r = (int)(rgb & 0xFF);
                    int g = (int)((rgb >> 8) & 0xFF);
                    int b = (int)((rgb >> 16) & 0xFF);
                    if (r > 250 && g > 250 && b > 250)
                        return "#000000";
                    return string.Format("#{0:X2}{1:X2}{2:X2}", r, g, b);
                }

                int cr = c.Red, cg = c.Green, cb = c.Blue;
                if (cr > 250 && cg > 250 && cb > 250)
                    return "#000000";
                return string.Format("#{0:X2}{1:X2}{2:X2}", cr, cg, cb);
            }
            catch
            {
                return "#000000";
            }
        }

        private double ResolveStrokeMm(Entity ent, LayerInfo layer, LineWeight? byBlockLw)
        {
            LineWeight lw;
            try { lw = ent.LineWeight; }
            catch { lw = LineWeight.ByLayer; }

            if (lw == LineWeight.ByLayer)
                lw = layer != null ? layer.LineWeight : LineWeight.ByLineWeightDefault;
            if (lw == LineWeight.ByBlock)
                lw = byBlockLw ?? LineWeight.ByLineWeightDefault;

            double mm = DefaultLineMm;
            int raw = (int)lw;
            if (raw > 0)
                mm = raw / 100.0;
            return mm;
        }

        private LayerInfo GetLayer(string name)
        {
            if (string.IsNullOrEmpty(name))
                name = "0";
            if (_layers.TryGetValue(name, out var cached))
                return cached;

            var info = new LayerInfo();
            try
            {
                var lt = (LayerTable)_tr.GetObject(_db.LayerTableId, OpenMode.ForRead);
                if (lt.Has(name))
                {
                    var rec = (LayerTableRecord)_tr.GetObject(lt[name], OpenMode.ForRead);
                    info.Skip = rec.IsOff || rec.IsFrozen;
                    info.Color = rec.Color;
                    info.LineWeight = rec.LineWeight;
                }
            }
            catch
            {
                info.Color = AcColor.FromColorIndex(ColorMethod.ByAci, 7);
                info.LineWeight = LineWeight.ByLineWeightDefault;
            }
            _layers[name] = info;
            return info;
        }

        private StyleInfo GetStyle(ObjectId styleId)
        {
            if (styleId.IsNull)
                return StyleInfo.Default;
            if (_styles.TryGetValue(styleId, out var cached))
                return cached;

            var info = StyleInfo.Default;
            try
            {
                var rec = _tr.GetObject(styleId, OpenMode.ForRead) as TextStyleTableRecord;
                if (rec != null)
                {
                    string typeFace = "";
                    bool bold = false, italic = false;
                    try
                    {
                        var fd = rec.Font;
                        if (fd != null)
                        {
                            typeFace = fd.TypeFace ?? "";
                            bold = fd.Bold;
                            italic = fd.Italic;
                        }
                    }
                    catch { }

                    string file = rec.FileName ?? "";
                    bool shx = file.EndsWith(".shx", StringComparison.OrdinalIgnoreCase)
                               || string.IsNullOrWhiteSpace(typeFace);
                    info = new StyleInfo
                    {
                        Family = shx ? ShxFallback : typeFace,
                        Bold = bold,
                        Italic = italic
                    };
                }
            }
            catch { }

            _styles[styleId] = info;
            return info;
        }

        private static string HandleOf(Entity ent)
        {
            try
            {
                if (!ent.ObjectId.IsNull)
                    return ent.Handle.Value.ToString("X");
            }
            catch { }
            return null;
        }

        private sealed class LayerInfo
        {
            public bool Skip;
            public AcColor Color;
            public LineWeight LineWeight;
        }

        private sealed class StyleInfo
        {
            public string Family;
            public bool Bold;
            public bool Italic;
            public static readonly StyleInfo Default = new StyleInfo { Family = ShxFallback };
        }
    }
}
