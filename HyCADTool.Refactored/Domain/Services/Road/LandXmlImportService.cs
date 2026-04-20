using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// LandXML 1.2 平面线位导入服务（纯 Domain）。
    ///
    /// 解析范围：
    /// <list type="bullet">
    ///   <item><c>&lt;Alignment&gt;</c>：取属性 <c>name</c> / <c>staStart</c>；</item>
    ///   <item><c>&lt;CoordGeom&gt;</c> 子元素 <c>&lt;Line&gt;</c> / <c>&lt;Curve&gt;</c> / <c>&lt;Spiral&gt;</c>：
    ///     产出 <see cref="AlignmentElement"/>（Kind、长度、半径、缓和 A）+ 中心线 <see cref="Polyline3D"/>
    ///     （圆弧按 bulge 还原，缓和按 <c>SpiralSteps</c> 段离散）；</item>
    ///   <item><c>&lt;StaEquation&gt;</c>：按 <c>staInternal</c> → <see cref="StationEquation.BeforeRaw"/>，
    ///     <c>staAhead</c> → <see cref="StationEquation.AheadStation"/>，保持升序。</item>
    /// </list>
    ///
    /// <b>导入后限制</b>：<see cref="AlignmentSource"/> 不被填充（LandXML 不含 PI 参数，无法反推）。
    /// 因此依赖 PI 的命令（<c>hyRoadAlnEditPi</c> / <c>hyRoadAlnExportPi</c> / <c>hyRoadAlnInsertPi</c> 等）不可用；
    /// 其余基于 Centerline / Elements / Equations 的命令均可用。
    ///
    /// <b>坐标约定</b>：LandXML 点坐标为 "Northing Easting"（= Y X），服务统一翻转为内部 (X, Y)。
    /// </summary>
    public static class LandXmlImportService
    {
        private const int DefaultSpiralSteps = 12;
        private static readonly XNamespace Ns = LandXmlExportService.LandXmlNamespace;

        /// <summary>
        /// 从磁盘文件解析 LandXML 1.2；文件不存在或无有效 Alignment 时抛异常。
        /// 文件内含多条 <c>&lt;Alignment&gt;</c> 时仅取首条。多条用 <see cref="LoadAllFromFile"/>。
        /// </summary>
        public static Alignment LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path 为空", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("LandXML 文件不存在", path);
            var xml = File.ReadAllText(path);
            return ParseXmlString(xml);
        }

        /// <summary>
        /// 从磁盘文件解析 LandXML 1.2，返回文件内所有可解析的 <c>&lt;Alignment&gt;</c>（v1.2 多路线导入）。
        /// 单条 Alignment 解析异常会被记录到返回的 <c>errors</c>，不影响其他条目。
        /// 当文件解析失败 / 无任何 Alignment 时仍抛异常。
        /// </summary>
        public static IReadOnlyList<Alignment> LoadAllFromFile(string path, out IReadOnlyList<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path 为空", nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("LandXML 文件不存在", path);
            var xml = File.ReadAllText(path);
            return ParseAllFromXmlString(xml, out errors);
        }

        /// <summary>
        /// 从 XML 字符串解析；仅取第一条 <c>&lt;Alignment&gt;</c>。若解析失败抛 <see cref="InvalidDataException"/>。
        /// </summary>
        public static Alignment ParseXmlString(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml)) throw new ArgumentException("xml 为空", nameof(xml));
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch (Exception ex) { throw new InvalidDataException("LandXML 解析失败：" + ex.Message, ex); }

            var root = doc.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "LandXML", StringComparison.Ordinal))
                throw new InvalidDataException("根元素不是 LandXML。");

            // 为兼容任意命名空间（不少第三方不写 xmlns），匹配 LocalName。
            var alignmentEl = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "Alignment");
            if (alignmentEl == null)
                throw new InvalidDataException("LandXML 文档不含 <Alignment> 元素。");

            return ParseAlignmentElement(alignmentEl);
        }

        /// <summary>
        /// 从 XML 字符串解析所有 <c>&lt;Alignment&gt;</c>。单条失败收集到 <paramref name="errors"/>，不中断；
        /// 整个文件根标签错误 / 0 条可解析时抛异常。
        /// </summary>
        public static IReadOnlyList<Alignment> ParseAllFromXmlString(string xml, out IReadOnlyList<string> errors)
        {
            if (string.IsNullOrWhiteSpace(xml)) throw new ArgumentException("xml 为空", nameof(xml));
            XDocument doc;
            try { doc = XDocument.Parse(xml); }
            catch (Exception ex) { throw new InvalidDataException("LandXML 解析失败：" + ex.Message, ex); }

            var root = doc.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "LandXML", StringComparison.Ordinal))
                throw new InvalidDataException("根元素不是 LandXML。");

            var alignmentEls = root.Descendants().Where(e => e.Name.LocalName == "Alignment").ToList();
            if (alignmentEls.Count == 0)
                throw new InvalidDataException("LandXML 文档不含 <Alignment> 元素。");

            var ok = new List<Alignment>();
            var errs = new List<string>();
            for (int i = 0; i < alignmentEls.Count; i++)
            {
                try
                {
                    var aln = ParseAlignmentElement(alignmentEls[i]);
                    if (aln != null) ok.Add(aln);
                }
                catch (Exception ex)
                {
                    string nm = alignmentEls[i].Attribute("name")?.Value ?? $"#{i + 1}";
                    errs.Add($"Alignment[{nm}] 解析失败：{ex.Message}");
                }
            }
            errors = errs;

            if (ok.Count == 0)
                throw new InvalidDataException(
                    "LandXML 内全部 Alignment 解析失败。"
                    + (errs.Count > 0 ? " 详细：" + string.Join(" | ", errs) : ""));

            return ok;
        }

        private static Alignment ParseAlignmentElement(XElement alignmentEl)
        {
            var aln = new Alignment
            {
                Name = alignmentEl.Attribute("name")?.Value ?? "Imported",
                StartStation = ParseDouble(alignmentEl.Attribute("staStart"), 0.0),
            };

            var coordGeom = alignmentEl.Elements().FirstOrDefault(e => e.Name.LocalName == "CoordGeom");
            if (coordGeom == null)
                throw new InvalidDataException("<Alignment> 不含 <CoordGeom> 元素。");

            double stationCursor = aln.StartStation;
            var vertices = new List<Point3D>();
            var bulges = new List<double>();
            Point2D? lastPoint = null;

            foreach (var segEl in coordGeom.Elements())
            {
                switch (segEl.Name.LocalName)
                {
                    case "Line":
                        AppendLine(segEl, aln, ref stationCursor, ref lastPoint, vertices, bulges);
                        break;
                    case "Curve":
                        AppendCurve(segEl, aln, ref stationCursor, ref lastPoint, vertices, bulges);
                        break;
                    case "Spiral":
                        AppendSpiral(segEl, aln, ref stationCursor, ref lastPoint, vertices, bulges);
                        break;
                    default:
                        // 忽略未知段
                        break;
                }
            }

            // Polyline3D 构造需要 vertices.Count = bulges.Count + 1（闭合时 = bulges.Count）
            if (vertices.Count >= 2)
            {
                aln.Centerline = new Polyline3D(vertices, isClosed: false, bulges: bulges);
            }
            else
            {
                // 构造空实例——调用方视情况处理
                aln.Centerline = new Polyline3D();
            }

            // 方程
            foreach (var eqEl in alignmentEl.Elements().Where(e => e.Name.LocalName == "StaEquation"))
            {
                double staAhead = ParseDouble(eqEl.Attribute("staAhead"), double.NaN);
                double staInternal = ParseDouble(eqEl.Attribute("staInternal"), double.NaN);
                if (double.IsNaN(staAhead) || double.IsNaN(staInternal)) continue;
                aln.StationEquations.Add(new StationEquation(staInternal, staAhead));
            }
            aln.StationEquations.Sort((x, y) => x.BeforeRaw.CompareTo(y.BeforeRaw));

            return aln;
        }

        // ---------- 段解析 ----------

        private static void AppendLine(
            XElement segEl, Alignment aln,
            ref double stationCursor, ref Point2D? lastPoint,
            List<Point3D> vertices, List<double> bulges)
        {
            var start = ReadPoint(segEl, "Start");
            var end = ReadPoint(segEl, "End");
            double length = ParseDouble(segEl.Attribute("length"),
                Math.Sqrt((end.X - start.X) * (end.X - start.X) + (end.Y - start.Y) * (end.Y - start.Y)));

            EnsureFirstVertex(vertices, bulges, ref lastPoint, start);
            // 直线段 bulge=0
            AppendVertex(vertices, bulges, end, bulge: 0.0);
            lastPoint = end;

            aln.Elements.Add(new AlignmentElement
            {
                Kind = AlignmentElementKind.Line,
                StartPoint = new Point3D(start.X, start.Y, 0),
                EndPoint = new Point3D(end.X, end.Y, 0),
                StartStation = stationCursor,
                Length = length,
            });
            stationCursor += length;
        }

        private static void AppendCurve(
            XElement segEl, Alignment aln,
            ref double stationCursor, ref Point2D? lastPoint,
            List<Point3D> vertices, List<double> bulges)
        {
            var start = ReadPoint(segEl, "Start");
            var end = ReadPoint(segEl, "End");
            double radius = ParseDouble(segEl.Attribute("radius"), double.NaN);
            double length = ParseDouble(segEl.Attribute("length"), double.NaN);
            string rot = segEl.Attribute("rot")?.Value ?? "ccw";
            if (double.IsNaN(radius) || radius <= 0 || double.IsNaN(length) || length <= 0)
                throw new InvalidDataException("<Curve> 缺少 radius / length。");

            // bulge = tan(θ/4)，θ = length / radius；ccw 正、cw 负。
            double theta = length / radius;
            double bulgeMag = Math.Tan(theta / 4.0);
            double bulge = string.Equals(rot, "ccw", StringComparison.OrdinalIgnoreCase) ? bulgeMag : -bulgeMag;

            EnsureFirstVertex(vertices, bulges, ref lastPoint, start);
            // 更新上一段的 bulge（在尾段已经补零）——此处 bulge 属于从 last→end 的那一段
            // 处理：我们需要把上一次"为保证单 vertex/segment"追加的 bulge 替换
            // 简化做法：约定 AppendVertex 在追加新 vertex 时同时指定从 prev→new 的 bulge。
            // 但 EnsureFirstVertex 只追加起点，没有 bulge；后续 AppendVertex 同时追加 vertex 和 prev→new bulge。
            AppendVertex(vertices, bulges, end, bulge);
            lastPoint = end;

            aln.Elements.Add(new AlignmentElement
            {
                Kind = AlignmentElementKind.CircularArc,
                StartPoint = new Point3D(start.X, start.Y, 0),
                EndPoint = new Point3D(end.X, end.Y, 0),
                Radius = radius,
                StartStation = stationCursor,
                Length = length,
            });
            stationCursor += length;
        }

        private static void AppendSpiral(
            XElement segEl, Alignment aln,
            ref double stationCursor, ref Point2D? lastPoint,
            List<Point3D> vertices, List<double> bulges)
        {
            var start = ReadPoint(segEl, "Start");
            var end = ReadPoint(segEl, "End");
            double length = ParseDouble(segEl.Attribute("length"), double.NaN);
            double rStart = ParseRadiusOrInf(segEl.Attribute("radiusStart")?.Value);
            double rEnd = ParseRadiusOrInf(segEl.Attribute("radiusEnd")?.Value);
            string rot = segEl.Attribute("rot")?.Value ?? "ccw";
            if (double.IsNaN(length) || length <= 0)
                throw new InvalidDataException("<Spiral> 缺少 length。");

            // 缓和参数 A = √(R·Ls)，其中 R 为非无穷端的半径、Ls = length。
            double rFinite = !double.IsInfinity(rStart) ? rStart : rEnd;
            double spiralA = !double.IsInfinity(rFinite) && rFinite > 0
                ? Math.Sqrt(rFinite * length)
                : double.NaN;

            // v1 简化：把缓和段按 <c>DefaultSpiralSteps</c> 等弧长离散，bulge = tan((θ_i)/4)，
            // θ_i ≈ length / (2R · N) 的局部估计（近似 Civil 3D 离散），
            // 严格的缓和曲线弧长公式较复杂，此处只要保证"首尾切向连续、长度近似"即可。
            // 由于 LandXML 未显式给中间控制点，我们用起终两点 + 一个 bulge 段表示（与 Curve 类似），
            // 其等效半径 r_eq ≈ (rStart + rEnd)/2（其中 INF 按 10·length 退化处理，避免零 bulge）。
            double rA = double.IsInfinity(rStart) ? 10 * length : rStart;
            double rB = double.IsInfinity(rEnd) ? 10 * length : rEnd;
            double rEquiv = 0.5 * (rA + rB);
            double theta = length / Math.Max(rEquiv, 1e-6);
            double bulgeMag = Math.Tan(theta / 4.0);
            double bulge = string.Equals(rot, "ccw", StringComparison.OrdinalIgnoreCase) ? bulgeMag : -bulgeMag;

            EnsureFirstVertex(vertices, bulges, ref lastPoint, start);
            AppendVertex(vertices, bulges, end, bulge);
            lastPoint = end;

            aln.Elements.Add(new AlignmentElement
            {
                Kind = AlignmentElementKind.Spiral,
                StartPoint = new Point3D(start.X, start.Y, 0),
                EndPoint = new Point3D(end.X, end.Y, 0),
                SpiralParameterA = spiralA,
                StartStation = stationCursor,
                Length = length,
            });
            stationCursor += length;
        }

        // ---------- 辅助 ----------

        private static void EnsureFirstVertex(
            List<Point3D> vertices, List<double> bulges, ref Point2D? lastPoint, Point2D start)
        {
            if (vertices.Count == 0)
            {
                vertices.Add(new Point3D(start.X, start.Y, 0));
                return;
            }
            // 若当前段起点与上一段终点有小缝隙，忽略差值（LandXML 精度舍入），保持连续。
            // 不重复追加同一顶点，也不修改已有 bulges。
        }

        private static void AppendVertex(
            List<Point3D> vertices, List<double> bulges, Point2D point, double bulge)
        {
            // vertices 中已有 ≥ 1 个点；新加 point 为末顶点，bulge 为 [prev→new] 段的 bulge
            bulges.Add(bulge);
            vertices.Add(new Point3D(point.X, point.Y, 0));
        }

        private static Point2D ReadPoint(XElement segEl, string localName)
        {
            var pEl = segEl.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
            if (pEl == null) throw new InvalidDataException($"段 <{segEl.Name.LocalName}> 缺少 <{localName}>。");
            var text = (pEl.Value ?? "").Trim();
            var parts = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new InvalidDataException($"<{localName}> 文本 '{text}' 不是合法坐标对。");
            // LandXML 坐标顺序：Northing Easting → 映射 (Y, X)
            double y = double.Parse(parts[0], CultureInfo.InvariantCulture);
            double x = double.Parse(parts[1], CultureInfo.InvariantCulture);
            return new Point2D(x, y);
        }

        private static double ParseDouble(XAttribute attr, double fallback)
        {
            if (attr == null) return fallback;
            var s = attr.Value;
            if (string.IsNullOrEmpty(s)) return fallback;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
            return fallback;
        }

        private static double ParseRadiusOrInf(string s)
        {
            if (string.IsNullOrEmpty(s)) return double.NaN;
            if (string.Equals(s, "INF", StringComparison.OrdinalIgnoreCase)) return double.PositiveInfinity;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
            return double.NaN;
        }
    }
}
