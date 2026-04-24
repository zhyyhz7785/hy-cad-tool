using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.Global;
using AcRuntime = Autodesk.AutoCAD.Runtime;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 从 <see cref="Database"/> 线型表读取定义并生成 .lin 与目录 DTO。
    /// 所有读属性/分段的调用都单独 try/catch —— AutoCAD 对某些记录
    /// （XREF 依赖、残留/保留条目、特殊字符线型）访问会抛 <c>eNotApplicable</c>，
    /// 一条坏记录不应打断全表导出。
    /// </summary>
    internal static class LinetypeTableExporter
    {
        private static readonly string[] ReservedNames = { "ByLayer", "ByBlock" };

        public static List<LinetypeCatalogItem> Collect(Transaction tr, Database db)
        {
            var list = new List<LinetypeCatalogItem>();
            var ltTable = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            foreach (ObjectId id in ltTable)
            {
                try
                {
                    var item = TryReadRecord(tr, id);
                    if (item != null)
                        list.Add(item);
                }
                catch (AcRuntime.Exception)
                {
                    // 单条坏记录（eNotApplicable / eWrongObjectType 等）跳过
                }
                catch (System.Exception)
                {
                    // 其它异常同样跳过，避免一次失败拖垮整表
                }
            }

            list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            return list;
        }

        private static LinetypeCatalogItem TryReadRecord(Transaction tr, ObjectId id)
        {
            var rec = tr.GetObject(id, OpenMode.ForRead) as LinetypeTableRecord;
            if (rec == null) return null;
            if (rec.IsErased) return null;

            string name = SafeGet(() => rec.Name, string.Empty);
            if (string.IsNullOrEmpty(name)) return null;
            if (ReservedNames.Contains(name, StringComparer.OrdinalIgnoreCase)) return null;

            string comments = SafeGet(() => rec.Comments, string.Empty);
            double patternLen = SafeGet(() => rec.PatternLength, 0.0);
            int numDashes = SafeGet(() => rec.NumDashes, 0);
            bool isDependent = SafeGet(() => rec.IsDependent, false);

            var pattern = BuildPatternLine(tr, rec, numDashes, out bool hasComplex, out var dashLens);

            return new LinetypeCatalogItem
            {
                Name = name,
                Comments = comments ?? string.Empty,
                PatternLength = patternLen,
                NumDashes = numDashes,
                IsDependent = isDependent,
                HasComplexSegments = hasComplex,
                DashLengths = dashLens,
                LinPatternLine = pattern
            };
        }

        public static void WriteLin(IReadOnlyList<LinetypeCatalogItem> items, TextWriter writer)
        {
            writer.WriteLine("; HyCADTool — linetypes exported from DWG");
            writer.WriteLine("; " + DateTime.UtcNow.ToString("u", CultureInfo.InvariantCulture));
            writer.WriteLine();

            foreach (var it in items)
            {
                writer.WriteLine($"*{it.Name},{EscapeComment(it.Comments)}");
                writer.WriteLine(it.LinPatternLine ?? "A,");
                writer.WriteLine();
            }
        }

        private static string BuildPatternLine(Transaction tr, LinetypeTableRecord lt, int numDashes, out bool hasComplex, out List<double> dashLens)
        {
            hasComplex = false;
            dashLens = new List<double>(Math.Max(0, numDashes));
            if (numDashes <= 0)
                return "A,";

            var sb = new StringBuilder("A");
            for (int i = 0; i < numDashes; i++)
            {
                double len = SafeGet(() => lt.DashLengthAt(i), 0.0);
                dashLens.Add(len);

                string segment;
                try
                {
                    segment = BuildSegment(tr, lt, i, len, ref hasComplex);
                }
                catch (AcRuntime.Exception)
                {
                    segment = FormatNum(len); // 段内任何 API 异常都降级为纯虚线段
                }
                catch (System.Exception)
                {
                    segment = FormatNum(len);
                }

                sb.Append(',').Append(segment);
            }

            return sb.ToString();
        }

        private static string BuildSegment(Transaction tr, LinetypeTableRecord lt, int i, double len, ref bool hasComplex)
        {
            string text = SafeGet(() => lt.TextAt(i), string.Empty) ?? string.Empty;
            ObjectId styleId = SafeGet(() => lt.ShapeStyleAt(i), ObjectId.Null);

            // 文字段：dash 长度后接 [TEXT,...]
            if (!string.IsNullOrEmpty(text))
            {
                string styleName = "STANDARD";
                if (!styleId.IsNull)
                {
                    try
                    {
                        if (tr.GetObject(styleId, OpenMode.ForRead) is TextStyleTableRecord ts
                            && !string.IsNullOrWhiteSpace(ts.Name))
                            styleName = ts.Name;
                    }
                    catch { /* 名字取不到就用 STANDARD 兜底 */ }
                }

                hasComplex = true;
                double s = SafeGet(() => lt.ShapeScaleAt(i), 1.0);
                double r = SafeGet(() => lt.ShapeRotationAt(i), 0.0);
                bool u = SafeGet(() => lt.ShapeIsUcsOrientedAt(i), false);
                double offX = 0, offY = 0;
                try { var off = lt.ShapeOffsetAt(i); offX = off.X; offY = off.Y; } catch { }

                var textBlock = "[TEXT," + EscapeLinetypeToken(text) + "," + EscapeLinetypeToken(styleName)
                                + ",S=" + FormatNum(s) + ",R=" + FormatNum(r) + ",U=" + (u ? "1.0" : "0.0")
                                + ",X=" + FormatNum(offX) + ",Y=" + FormatNum(offY) + "]";
                return FormatNum(len) + "," + textBlock;
            }

            // 形文件段：dash 长度后接 [SHAPE,...]
            if (!styleId.IsNull)
            {
                DBObject o = null;
                try { o = tr.GetObject(styleId, OpenMode.ForRead); } catch { }
                if (o != null && !(o is TextStyleTableRecord))
                {
                    hasComplex = true;
                    string file = TryGetShapeFileName(o);
                    int num = SafeGet(() => lt.ShapeNumberAt(i), 0);
                    double sc = SafeGet(() => lt.ShapeScaleAt(i), 1.0);
                    double rot = SafeGet(() => lt.ShapeRotationAt(i), 0.0);
                    double offX = 0, offY = 0;
                    try { var off = lt.ShapeOffsetAt(i); offX = off.X; offY = off.Y; } catch { }

                    var shapeBlock = "[SHAPE," + EscapeLinetypeToken(file) + "," + num
                                     + ",S=" + FormatNum(sc) + ",R=" + FormatNum(rot)
                                     + ",X=" + FormatNum(offX) + ",Y=" + FormatNum(offY) + "]";
                    return FormatNum(len) + "," + shapeBlock;
                }
            }

            return FormatNum(len);
        }

        private static string TryGetShapeFileName(DBObject o)
        {
            var sym = o as SymbolTableRecord;
            if (sym != null)
            {
                string n = SafeGet(() => sym.Name, string.Empty);
                if (!string.IsNullOrWhiteSpace(n))
                {
                    n = n.Trim();
                    if (n.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
                        return n;
                    return n + ".shx";
                }
            }

            return "ltypeshp.shx";
        }

        private static T SafeGet<T>(Func<T> getter, T fallback)
        {
            try { return getter(); }
            catch { return fallback; }
        }

        private static string FormatNum(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            return v.ToString("0.################", CultureInfo.InvariantCulture);
        }

        private static string EscapeComment(string s)
        {
            if (string.IsNullOrEmpty(s)) return " ";
            return s.Replace("\r", " ").Replace("\n", " ");
        }

        /// <summary>线型定义中逗号、方括号等需转义时前缀反斜杠（与 AutoCAD 自定义线型说明一致）。</summary>
        private static string EscapeLinetypeToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return " ";
            var sb = new StringBuilder(s.Length + 4);
            foreach (char c in s)
            {
                if (c == ',' || c == '\\' || c == '[' || c == ']')
                    sb.Append('\\');
                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}
