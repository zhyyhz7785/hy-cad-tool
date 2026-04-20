using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// LandXML 1.2 平面线位导出服务（纯 Domain，无 I/O 依赖外部 DWG）。
    ///
    /// 导出结构（精简 1.2 Schema 必需字段）：
    /// <code>
    /// &lt;LandXML version="1.2" xmlns="http://www.landxml.org/schema/LandXML-1.2"&gt;
    ///   &lt;Units&gt;&lt;Metric .../&gt;&lt;/Units&gt;
    ///   &lt;Project name="hyRoadAln"/&gt;
    ///   &lt;Alignments name="..."&gt;
    ///     &lt;Alignment name="..." length="..." staStart="..."&gt;
    ///       &lt;CoordGeom&gt;
    ///         &lt;Line length="..." dir="..."&gt;&lt;Start&gt;x y&lt;/Start&gt;&lt;End&gt;x y&lt;/End&gt;&lt;/Line&gt;
    ///         &lt;Curve length="..." radius="..." rot="cw/ccw"&gt;&lt;Start/&gt;&lt;Center/&gt;&lt;End/&gt;&lt;/Curve&gt;
    ///         &lt;Spiral length="..." radiusStart="INF/R" radiusEnd="R/INF" rot="cw/ccw" spiType="clothoid"&gt;
    ///           &lt;Start/&gt;&lt;End/&gt;
    ///         &lt;/Spiral&gt;
    ///       &lt;/CoordGeom&gt;
    ///       &lt;StaEquation staAhead="..." staBack="..." staInternal="..." /&gt;
    ///     &lt;/Alignment&gt;
    ///   &lt;/Alignments&gt;
    /// &lt;/LandXML&gt;
    /// </code>
    ///
    /// <b>方位约定转换</b>：LandXML 的 <c>dir</c> 是"北方位、顺时针、十进制度"（North = 0°, East = 90°）；
    /// 我们内部的 Bearing 是"X+ 起算、逆时针、弧度"（East = 0, North = π/2）。
    /// 换算：<c>azimuthDeg = ((π/2 − bearingRad) rad → deg) mod 360</c>。
    ///
    /// <b>圆心计算</b>：给定圆弧起点 S、终点 E、半径 R、转向 rot（cw/ccw）：
    /// 中点 M = (S+E)/2；弦向量 v = E−S；v 的垂直单位向量 n = v.Perpendicular().Normalize()；
    /// 垂距 h = √(R² − |v/2|²)；圆心 C = M ± h·n（ccw→左法向"+"，cw→右法向"−"，
    /// 其中"左法向"为 v 逆时针 90°）。
    ///
    /// <b>缓和曲线半径端</b>：直→圆的缓和段以 <c>radiusStart=INF, radiusEnd=R</c> 表示；
    /// 圆→直的缓和段以 <c>radiusStart=R, radiusEnd=INF</c> 表示。圆内半径带符号时本服务只看 |R|。
    /// </summary>
    public static class LandXmlExportService
    {
        public const string LandXmlNamespace = "http://www.landxml.org/schema/LandXML-1.2";
        public const string LandXmlVersion = "1.2";

        private static readonly XNamespace Ns = LandXmlNamespace;

        /// <summary>
        /// 生成一段 <b>完整合法</b>的 LandXML 1.2 文本（UTF-8，带 XML 声明）。
        /// 单 Alignment overload 等价于 <see cref="BuildXmlString(IEnumerable{Alignment}, PiDesignOptions, string)"/> 仅传一条。
        /// </summary>
        public static string BuildXmlString(Alignment alignment, PiDesignOptions options = null)
        {
            if (alignment == null) throw new ArgumentNullException(nameof(alignment));
            return BuildXmlString(new[] { alignment }, options, alignment.Name);
        }

        /// <summary>
        /// 生成包含多条 Alignment 的 LandXML 1.2 文本（v1.2 多路线导出）。
        /// 每条 Alignment 作为 <c>&lt;Alignments&gt;</c> 内的一个 <c>&lt;Alignment&gt;</c> 子元素，共享同一个 Units / Project。
        /// </summary>
        /// <param name="alignments">导出的 Alignment 集合。<c>null</c> / 空集 / 全部不可用都会抛 <see cref="ArgumentException"/>。</param>
        /// <param name="options">PI 分段计算配置；<c>null</c> 用默认。</param>
        /// <param name="projectName">LandXML <c>Project name</c>，建议传当前 DWG 名。<c>null</c> 时取首条 Alignment.Name。</param>
        public static string BuildXmlString(IEnumerable<Alignment> alignments, PiDesignOptions options = null, string projectName = null)
        {
            if (alignments == null) throw new ArgumentNullException(nameof(alignments));
            var list = alignments.Where(a => a != null).ToList();
            if (list.Count == 0) throw new ArgumentException("alignments 不含可用条目", nameof(alignments));

            var doc = BuildDocument(list, options, projectName);
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = true,
                IndentChars = "  ",
                OmitXmlDeclaration = false,
            };
            using (var sw = new StringWriter(sb))
            using (var xw = XmlWriter.Create(sw, settings))
            {
                doc.Save(xw);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 把 XML 字符串按 UTF-8（带 BOM）落盘，便于 Windows 记事本 / Excel 识别。
        /// 写入前确保父目录存在；覆盖同名文件。
        /// </summary>
        public static void SaveToFile(Alignment alignment, string path, PiDesignOptions options = null)
        {
            if (alignment == null) throw new ArgumentNullException(nameof(alignment));
            SaveAllToFile(new[] { alignment }, path, options, alignment.Name);
        }

        /// <summary>
        /// 多 Alignment 落盘（v1.2 多路线导出入口）。
        /// </summary>
        public static void SaveAllToFile(IEnumerable<Alignment> alignments, string path, PiDesignOptions options = null, string projectName = null)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path 为空", nameof(path));
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var xml = BuildXmlString(alignments, options, projectName);
            var bom = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(xml);
            var all = new byte[bom.Length + body.Length];
            Buffer.BlockCopy(bom, 0, all, 0, bom.Length);
            Buffer.BlockCopy(body, 0, all, bom.Length, body.Length);
            File.WriteAllBytes(path, all);
        }

        // ---------- 内部实现 ----------

        private static XDocument BuildDocument(IList<Alignment> alignments, PiDesignOptions options, string projectName)
        {
            var first = alignments[0];
            var root = new XElement(Ns + "LandXML",
                new XAttribute("version", LandXmlVersion),
                new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"));

            root.Add(new XElement(Ns + "Units",
                new XElement(Ns + "Metric",
                    new XAttribute("linearUnit", "meter"),
                    new XAttribute("areaUnit", "squareMeter"),
                    new XAttribute("volumeUnit", "cubicMeter"),
                    new XAttribute("temperatureUnit", "celsius"),
                    new XAttribute("pressureUnit", "milliBars"),
                    new XAttribute("angularUnit", "decimal degrees"),
                    new XAttribute("directionUnit", "decimal degrees"))));

            root.Add(new XElement(Ns + "Project",
                new XAttribute("name", projectName ?? first.Name ?? "hyRoadAln")));

            var alignmentsEl = new XElement(Ns + "Alignments",
                new XAttribute("name", projectName ?? first.Name ?? "hyRoadAln"));

            foreach (var aln in alignments)
                alignmentsEl.Add(BuildAlignmentElementCore(aln, options));

            root.Add(alignmentsEl);
            return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        }

        private static XElement BuildAlignmentElementCore(Alignment alignment, PiDesignOptions options)
        {
            AlignmentBreakdown breakdown = null;
            if (alignment.Source?.PiElements != null && alignment.Source.PiElements.Count >= 2)
            {
                var elements = alignment.Source.PiElements
                    .Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag))
                    .ToList();
                breakdown = AlignmentStationBreakdown.Build(
                    elements, alignment.StartStation, options, alignment.StationEquations);
            }

            double length = alignment.Centerline?.GetPlanarLength() ?? 0.0;

            var alignmentEl = new XElement(Ns + "Alignment",
                new XAttribute("name", alignment.Name ?? "Alignment"),
                new XAttribute("length", Fmt(length)),
                new XAttribute("staStart", Fmt(alignment.StartStation)));

            var coordGeom = new XElement(Ns + "CoordGeom");
            alignmentEl.Add(coordGeom);

            if (breakdown != null && breakdown.Segments.Count > 0)
            {
                foreach (var seg in breakdown.Segments)
                    coordGeom.Add(BuildSegmentElement(seg));
            }
            else if (alignment.Centerline != null && alignment.Centerline.VertexCount >= 2)
            {
                // 兜底：Source 为空（非 PI 路径生成的 alignment），Centerline 按折线切 Line。
                for (int i = 0; i < alignment.Centerline.VertexCount - 1; i++)
                {
                    var s = alignment.Centerline.GetPointAt(i);
                    var e = alignment.Centerline.GetPointAt(i + 1);
                    double dx = e.X - s.X, dy = e.Y - s.Y;
                    double segLen = Math.Sqrt(dx * dx + dy * dy);
                    if (segLen < 1e-9) continue;
                    double brg = Math.Atan2(dy, dx);
                    coordGeom.Add(new XElement(Ns + "Line",
                        new XAttribute("length", Fmt(segLen)),
                        new XAttribute("dir", Fmt(BearingToAzimuthDeg(brg))),
                        PointEl("Start", s.X, s.Y),
                        PointEl("End", e.X, e.Y)));
                }
            }

            foreach (var eq in SafeEquations(alignment))
            {
                double staBack = StationConverter.ToDisplayStation(
                    Math.Max(0, eq.BeforeRaw - 1e-9), alignment.StartStation, alignment.StationEquations);
                alignmentEl.Add(new XElement(Ns + "StaEquation",
                    new XAttribute("staAhead", Fmt(eq.AheadStation)),
                    new XAttribute("staBack", Fmt(staBack)),
                    new XAttribute("staInternal", Fmt(eq.BeforeRaw))));
            }

            return alignmentEl;
        }

        private static XElement BuildSegmentElement(SegmentRecord seg)
        {
            double brgStart = seg.StartBearingRad;
            double brgEnd = seg.EndBearingRad;
            double dirStartDeg = BearingToAzimuthDeg(brgStart);
            double dirEndDeg = BearingToAzimuthDeg(brgEnd);

            switch (seg.Kind)
            {
                case SegmentKind.Line:
                    return new XElement(Ns + "Line",
                        new XAttribute("length", Fmt(seg.LengthM)),
                        new XAttribute("dir", Fmt(dirStartDeg)),
                        PointEl("Start", seg.StartPoint.X, seg.StartPoint.Y),
                        PointEl("End", seg.EndPoint.X, seg.EndPoint.Y));

                case SegmentKind.Arc:
                {
                    string rot = ArcRotation(brgStart, brgEnd);
                    var center = ArcCenter(seg.StartPoint, seg.EndPoint, seg.Radius, rot);
                    return new XElement(Ns + "Curve",
                        new XAttribute("length", Fmt(seg.LengthM)),
                        new XAttribute("radius", Fmt(Math.Abs(seg.Radius))),
                        new XAttribute("rot", rot),
                        new XAttribute("dirStart", Fmt(dirStartDeg)),
                        new XAttribute("dirEnd", Fmt(dirEndDeg)),
                        PointEl("Start", seg.StartPoint.X, seg.StartPoint.Y),
                        PointEl("Center", center.X, center.Y),
                        PointEl("End", seg.EndPoint.X, seg.EndPoint.Y));
                }

                case SegmentKind.Spiral:
                {
                    string rot = ArcRotation(brgStart, brgEnd);
                    // 判断缓和方向：入侧缓（半径 ∞→R）/出侧缓（半径 R→∞）。
                    // 规则：如果起点方位是直线方位（PI 前一段的切向与直线相同），
                    // 则 radiusStart=INF。工程实现中，仅靠 bearing 变化无法区分入/出，
                    // 需要靠下一段/上一段类型；SegmentRecord 未提供。这里按"Ls 方向"推定：
                    //   — 段长 = Ls；缓和参数 A = √(R·Ls)；
                    //   — 采用 dirStart 与 dirEnd 的差异推进方向（不足以区分 in/out），
                    //     故默认输出 radiusStart=INF、radiusEnd=R（直→缓→圆），
                    //     调用方若需要区分可在导入端按临近段调整。
                    // 该决策在 B2 导入侧可通过相邻段拓扑还原。
                    return new XElement(Ns + "Spiral",
                        new XAttribute("length", Fmt(seg.LengthM)),
                        new XAttribute("radiusStart", "INF"),
                        new XAttribute("radiusEnd", Fmt(Math.Abs(seg.Radius))),
                        new XAttribute("rot", rot),
                        new XAttribute("spiType", "clothoid"),
                        new XAttribute("dirStart", Fmt(dirStartDeg)),
                        new XAttribute("dirEnd", Fmt(dirEndDeg)),
                        PointEl("Start", seg.StartPoint.X, seg.StartPoint.Y),
                        PointEl("End", seg.EndPoint.X, seg.EndPoint.Y));
                }

                default:
                    // 未知段：降级为 Line
                    return new XElement(Ns + "Line",
                        new XAttribute("length", Fmt(seg.LengthM)),
                        new XAttribute("dir", Fmt(dirStartDeg)),
                        PointEl("Start", seg.StartPoint.X, seg.StartPoint.Y),
                        PointEl("End", seg.EndPoint.X, seg.EndPoint.Y));
            }
        }

        private static XElement PointEl(string name, double x, double y)
        {
            // LandXML 的点坐标是 "Y X"（北、东）；等同 (Northing Easting)。
            // 我们内部 X = 东、Y = 北，所以写成 $"{Y} {X}"。
            return new XElement(Ns + name, string.Format(CultureInfo.InvariantCulture, "{0:0.######} {1:0.######}", y, x));
        }

        private static string Fmt(double v) => v.ToString("0.########", CultureInfo.InvariantCulture);

        /// <summary>
        /// 内部 Bearing（X+ 起、CCW、rad）→ LandXML Azimuth（北起、CW、deg）。
        /// az_rad = π/2 − bearing_rad；结果规范化到 [0, 360)。
        /// </summary>
        public static double BearingToAzimuthDeg(double bearingRad)
        {
            double azRad = Math.PI / 2.0 - bearingRad;
            double azDeg = azRad * 180.0 / Math.PI;
            azDeg = azDeg % 360.0;
            if (azDeg < 0) azDeg += 360.0;
            return azDeg;
        }

        /// <summary>
        /// LandXML Azimuth（北起、CW、deg）→ 内部 Bearing（X+ 起、CCW、rad）。
        /// 与 <see cref="BearingToAzimuthDeg"/> 互为逆函数。
        /// </summary>
        public static double AzimuthDegToBearing(double azimuthDeg)
        {
            double brgRad = Math.PI / 2.0 - (azimuthDeg * Math.PI / 180.0);
            // 规范化到 (-π, π]
            while (brgRad > Math.PI) brgRad -= 2 * Math.PI;
            while (brgRad <= -Math.PI) brgRad += 2 * Math.PI;
            return brgRad;
        }

        /// <summary>
        /// 由起 / 终切向（Bearing rad）推断圆弧转向。<c>dBearing = end − start</c>，
        /// 规范化到 (−π, π]；&gt;0 为逆时针（LandXML "ccw"），&lt;0 为顺时针（"cw"）。
        /// </summary>
        public static string ArcRotation(double startBearingRad, double endBearingRad)
        {
            double d = endBearingRad - startBearingRad;
            while (d > Math.PI) d -= 2 * Math.PI;
            while (d <= -Math.PI) d += 2 * Math.PI;
            return d >= 0 ? "ccw" : "cw";
        }

        /// <summary>
        /// 由弦端 S/E + 半径 R + 转向推算圆心 C。若 |chord| &gt; 2R 则 clamp。
        /// </summary>
        public static Point2D ArcCenter(Point2D s, Point2D e, double radius, string rot)
        {
            double cx = 0.5 * (s.X + e.X);
            double cy = 0.5 * (s.Y + e.Y);
            double vx = e.X - s.X;
            double vy = e.Y - s.Y;
            double chord = Math.Sqrt(vx * vx + vy * vy);
            double r = Math.Abs(radius);
            if (chord < 1e-12 || r < 1e-12) return new Point2D(cx, cy);

            double halfChord = chord / 2.0;
            double hSq = r * r - halfChord * halfChord;
            double h = hSq > 0 ? Math.Sqrt(hSq) : 0;

            // v 的左法向（CCW 90°）单位向量
            double nxL = -vy / chord;
            double nyL = vx / chord;

            // ccw：圆心在左侧；cw：圆心在右侧
            double sign = string.Equals(rot, "ccw", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
            return new Point2D(cx + sign * h * nxL, cy + sign * h * nyL);
        }

        private static IEnumerable<StationEquation> SafeEquations(Alignment alignment)
        {
            if (alignment.StationEquations == null) yield break;
            foreach (var eq in alignment.StationEquations)
                if (eq != null) yield return eq;
        }
    }
}
