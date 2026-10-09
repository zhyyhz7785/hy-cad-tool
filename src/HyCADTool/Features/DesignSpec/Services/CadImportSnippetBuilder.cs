using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Tables.Data;
using HyCAD.Tables.Inference;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Operations;
using HyCAD.Tables.Structure;
using HyCADTool.Features.DesignSpec.Domain.Models;
using HyCADTool.Features.Tables.Infrastructure.AutoCad;
using HyCADTool.Features.TextEdit.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace HyCADTool.Features.DesignSpec.Services
{
    /// <summary>
    /// 把本批 AutoCAD 选择分拣成 cad-import schema 1 JSON。
    /// </summary>
    public static class CadImportSnippetBuilder
    {
        private static readonly Regex FallbackMtext = new Regex(
            @"\\[A-Za-z][^;\\{}]*;|[{}]",
            RegexOptions.Compiled);

        public static JObject Build(
            Transaction tr,
            Database db,
            Editor ed,
            SelectionSet set,
            double scale)
        {
            double s = scale > 1e-9 ? scale : 1.0;
            var items = new JArray();
            var consumed = new HashSet<ObjectId>();
            var seenMarkdown = new HashSet<string>(StringComparer.Ordinal);
            var lineSegs = new List<Seg>();
            var texts = new List<TextEnt>();

            var ids = new ObjectId[set.Count];
            for (int i = 0; i < set.Count; i++)
                ids[i] = set[i].ObjectId;

            foreach (var id in ids)
            {
                if (id.IsNull || !id.IsValid)
                    continue;
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null)
                    continue;

                if (ent is BlockReference || ent is AttributeDefinition || ent is AttributeReference
                    || ent is MLeader || ent is Dimension)
                {
                    ed?.WriteMessage("\n已跳过块参照/属性/引线/标注。");
                    consumed.Add(id);
                    continue;
                }

                string markdown;
                DesignSpecConfig config;
                if (DesignSpecService.TryReadHymdSource(tr, db, ent, out markdown, out config))
                {
                    string key = (markdown ?? "") + "\n" + (config != null ? config.SourcePath ?? "" : "");
                    if (seenMarkdown.Add(key) && (!string.IsNullOrWhiteSpace(markdown) || (config != null && !string.IsNullOrWhiteSpace(config.SourcePath))))
                    {
                        Extents3d ext;
                        try { ext = ent.GeometricExtents; }
                        catch { ext = new Extents3d(); }
                        items.Add(new JObject
                        {
                            ["kind"] = "markdown",
                            ["x"] = Mm(ext.MinPoint.X, s),
                            ["y"] = Mm(ext.MaxPoint.Y, s),
                            ["markdown"] = markdown ?? ""
                        });
                    }
                    consumed.Add(id);
                    continue;
                }

                if (ent is Table table)
                {
                    items.Add(ReadAcadTable(table, s));
                    consumed.Add(id);
                    continue;
                }

                CollectGeometry(ent, tr, id, lineSegs, texts);
            }

            InferLineTables(tr, ed, lineSegs, texts, consumed, items, s);

            foreach (var t in texts)
            {
                if (consumed.Contains(t.Id))
                    continue;
                items.Add(ReadText(tr, t, s));
            }

            return new JObject
            {
                ["schema"] = 1,
                ["scale"] = s,
                ["items"] = items
            };
        }

        private static JObject ReadAcadTable(Table table, double s)
        {
            int rows = table.Rows.Count;
            int cols = table.Columns.Count;
            var cells = new JArray();
            var used = new bool[Math.Max(rows, 1), Math.Max(cols, 1)];
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (used[r, c])
                        continue;
                    int rs = 1;
                    int cs = 1;
                    try
                    {
                        var range = table.Cells[r, c].GetMergeRange();
                        rs = Math.Max(1, range.BottomRow - range.TopRow + 1);
                        cs = Math.Max(1, range.RightColumn - range.LeftColumn + 1);
                        if (range.TopRow != r || range.LeftColumn != c)
                            continue;
                    }
                    catch { }

                    for (int rr = r; rr < r + rs && rr < rows; rr++)
                    {
                        for (int cc = c; cc < c + cs && cc < cols; cc++)
                            used[rr, cc] = true;
                    }

                    string text = "";
                    try { text = table.Cells[r, c].TextString ?? ""; }
                    catch { }
                    cells.Add(new JObject
                    {
                        ["r"] = r,
                        ["c"] = c,
                        ["rs"] = rs,
                        ["cs"] = cs,
                        ["text"] = StripMtextLite(text)
                    });
                }
            }

            Point3d pos = table.Position;
            return new JObject
            {
                ["kind"] = "table",
                ["src"] = "ACAD_TABLE",
                ["x"] = Mm(pos.X, s),
                ["y"] = Mm(pos.Y, s),
                ["rows"] = rows,
                ["cols"] = cols,
                ["cells"] = cells
            };
        }

        private static void InferLineTables(
            Transaction tr,
            Editor ed,
            List<Seg> lineSegs,
            List<TextEnt> texts,
            HashSet<ObjectId> consumed,
            JArray items,
            double s)
        {
            var live = new List<Seg>();
            foreach (var seg in lineSegs)
            {
                if (!consumed.Contains(seg.Id))
                    live.Add(seg);
            }
            if (live.Count == 0)
                return;

            double snap = TableInferOptions.Default.EndpointSnapMm * s;
            int n = live.Count;
            var parent = new int[n];
            for (int i = 0; i < n; i++)
                parent[i] = i;
            Func<int, int> find = null;
            find = x => parent[x] == x ? x : (parent[x] = find(parent[x]));
            Action<int, int> unite = (a, b) =>
            {
                a = find(a);
                b = find(b);
                if (a != b)
                    parent[b] = a;
            };

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (Connected(live[i], live[j], snap))
                        unite(i, j);
                }
            }

            var clusters = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int root = find(i);
                List<int> list;
                if (!clusters.TryGetValue(root, out list))
                {
                    list = new List<int>();
                    clusters[root] = list;
                }
                list.Add(i);
            }

            var collector = new AcadTableEntityCollector();
            var engine = new TableInferEngine();
            foreach (var kv in clusters)
            {
                var idxs = kv.Value;
                var clusterIds = new List<ObjectId>();
                var seen = new HashSet<ObjectId>();
                double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
                foreach (int i in idxs)
                {
                    var seg = live[i];
                    minX = Math.Min(minX, Math.Min(seg.X1, seg.X2));
                    maxX = Math.Max(maxX, Math.Max(seg.X1, seg.X2));
                    minY = Math.Min(minY, Math.Min(seg.Y1, seg.Y2));
                    maxY = Math.Max(maxY, Math.Max(seg.Y1, seg.Y2));
                    if (seen.Add(seg.Id))
                        clusterIds.Add(seg.Id);
                }

                double margin = TableInferOptions.Default.TextInCellMarginMm * s;
                var textIds = new List<ObjectId>();
                foreach (var t in texts)
                {
                    if (consumed.Contains(t.Id))
                        continue;
                    if (t.MaxX < minX - margin || t.MinX > maxX + margin || t.MaxY < minY - margin || t.MinY > maxY + margin)
                        continue;
                    if (seen.Add(t.Id))
                    {
                        clusterIds.Add(t.Id);
                        textIds.Add(t.Id);
                    }
                }

                var input = collector.Collect(clusterIds.ToArray(), tr);
                input = ToMm(input, s);
                var result = engine.Infer(input);
                var grid = result != null ? result.Grid : null;
                int rows = grid != null ? grid.Structure.Topology.RowCount : 0;
                int cols = grid != null ? grid.Structure.Topology.ColCount : 0;
                if (result == null || result.Status != InferStatus.Success || grid == null || rows < 2 || cols < 2)
                {
                    if (result != null && result.Status != InferStatus.Success)
                        ed?.WriteMessage($"\n线框表识别失败：{result.Status}，文字按普通文字导入。");
                    continue;
                }

                foreach (var id in clusterIds)
                    consumed.Add(id);

                var cells = new JArray();
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        var addr = new CellAddr(r, c);
                        if (grid.Structure.IsHidden(addr))
                            continue;
                        int rs = 1;
                        int cs = 1;
                        MergeRegion region;
                        if (grid.Structure.TryGetMergeAt(addr, out region) && region.Anchor.Equals(addr))
                        {
                            rs = region.RowSpan;
                            cs = region.ColSpan;
                        }
                        var val = GridEditor.GetValue(grid, addr);
                        cells.Add(new JObject
                        {
                            ["r"] = r,
                            ["c"] = c,
                            ["rs"] = rs,
                            ["cs"] = cs,
                            ["text"] = CellText(val)
                        });
                    }
                }

                var uncertain = new JArray();
                if (result.UncertainCells != null)
                {
                    foreach (var cell in result.UncertainCells)
                        uncertain.Add(new JArray { cell.Row, cell.Col });
                }

                var table = new JObject
                {
                    ["kind"] = "table",
                    ["src"] = "LINES",
                    ["x"] = Mm(minX, s),
                    ["y"] = Mm(maxY, s),
                    ["rows"] = rows,
                    ["cols"] = cols,
                    ["cells"] = cells,
                    ["confidence"] = result.OverallConfidence
                };
                if (uncertain.Count > 0)
                    table["uncertain"] = uncertain;
                items.Add(table);

                ed?.WriteMessage(
                    $"\n线框表 {rows}×{cols}，置信度 {result.OverallConfidence:F2}。");
                if (uncertain.Count > 0)
                    ed?.WriteMessage($" 存疑格 {uncertain.Count} 个。");
            }
        }

        private static JObject ReadText(Transaction tr, TextEnt t, double s)
        {
            var ent = tr.GetObject(t.Id, OpenMode.ForRead);
            if (ent is MText mtext)
                return ReadMtext(mtext, s);
            if (ent is DBText db)
                return ReadDbText(db, s);
            return new JObject
            {
                ["kind"] = "text",
                ["src"] = "TEXT",
                ["x"] = Mm(t.MinX, s),
                ["y"] = Mm(t.MaxY, s),
                ["heightMm"] = Mm(Math.Max(0.01, t.MaxY - t.MinY), s),
                ["widthMm"] = Mm(Math.Max(0.01, t.MaxX - t.MinX), s),
                ["rotation"] = 0,
                ["lines"] = new JArray { t.Content ?? "" }
            };
        }

        private static JObject ReadMtext(MText mtext, double s)
        {
            Extents3d ext;
            try { ext = mtext.GeometricExtents; }
            catch { ext = new Extents3d(mtext.Location, mtext.Location); }
            string editable;
            MTextWrap wrap;
            MTextCodec.Decode(mtext.Contents ?? "", out editable, out wrap);
            bool bold = (mtext.Contents ?? "").IndexOf("|b1", StringComparison.OrdinalIgnoreCase) >= 0;
            if (wrap != null && wrap.RawFallback)
                editable = FallbackMtext.Replace(mtext.Contents ?? "", "");
            editable = editable.Replace("\\P", "\n");
            var lines = new JArray();
            foreach (var line in editable.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                lines.Add(line);
            return new JObject
            {
                ["kind"] = "text",
                ["src"] = "MTEXT",
                ["x"] = Mm(ext.MinPoint.X, s),
                ["y"] = Mm(ext.MaxPoint.Y, s),
                ["heightMm"] = Mm(mtext.TextHeight, s),
                ["widthMm"] = Mm(Math.Max(0.01, ext.MaxPoint.X - ext.MinPoint.X), s),
                ["rotation"] = mtext.Rotation,
                ["lines"] = lines,
                ["bold"] = bold
            };
        }

        private static JObject ReadDbText(DBText db, double s)
        {
            Extents3d ext;
            try { ext = db.GeometricExtents; }
            catch { ext = new Extents3d(db.Position, db.Position); }
            return new JObject
            {
                ["kind"] = "text",
                ["src"] = "TEXT",
                ["x"] = Mm(ext.MinPoint.X, s),
                ["y"] = Mm(ext.MaxPoint.Y, s),
                ["heightMm"] = Mm(db.Height, s),
                ["widthMm"] = Mm(Math.Max(0.01, ext.MaxPoint.X - ext.MinPoint.X), s),
                ["rotation"] = db.Rotation,
                ["lines"] = new JArray { DecodeDtext(db.TextString ?? "") }
            };
        }

        private static string DecodeDtext(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";
            s = s.Replace("%%d", "°").Replace("%%D", "°")
                .Replace("%%p", "±").Replace("%%P", "±")
                .Replace("%%c", "Ø").Replace("%%C", "Ø");
            return Regex.Replace(s, "%%[uUoO]", "");
        }

        private static string StripMtextLite(string contents)
        {
            if (string.IsNullOrEmpty(contents))
                return "";
            string editable;
            MTextWrap wrap;
            MTextCodec.Decode(contents, out editable, out wrap);
            if (wrap != null && wrap.RawFallback)
                editable = FallbackMtext.Replace(contents, "").Replace("\\P", "\n");
            return editable ?? "";
        }

        private static string CellText(CellValue value)
        {
            if (value.Runs != null && value.Runs.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var run in value.Runs)
                    sb.Append(run.Text);
                return sb.ToString();
            }
            return value.Text ?? "";
        }

        private static TableInferInput ToMm(TableInferInput input, double s)
        {
            var segs = new List<HyCAD.Tables.Inference.LineSegment2d>();
            foreach (var seg in input.Segments)
            {
                segs.Add(new HyCAD.Tables.Inference.LineSegment2d(
                    Mm(seg.X1, s), Mm(seg.Y1, s), Mm(seg.X2, s), Mm(seg.Y2, s),
                    seg.Orientation));
            }
            var boxes = new List<TextBox2d>();
            foreach (var t in input.Texts)
            {
                var b = t.Bounds;
                boxes.Add(new TextBox2d(
                    new LayoutRect(Mm(b.Left, s), Mm(b.Top, s), Mm(b.Right, s), Mm(b.Bottom, s)),
                    t.Content,
                    Mm(t.HeightMm, s)));
            }
            return new TableInferInput(segs, boxes, input.ClipBounds);
        }

        private static void CollectGeometry(
            Entity entity,
            Transaction tr,
            ObjectId id,
            List<Seg> segs,
            List<TextEnt> texts)
        {
            if (entity is Line line)
            {
                segs.Add(new Seg(id, line.StartPoint.X, line.StartPoint.Y, line.EndPoint.X, line.EndPoint.Y));
                return;
            }
            if (entity is Polyline pl)
            {
                int count = pl.NumberOfVertices;
                if (count < 2)
                    return;
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    if (!pl.Closed && next == 0)
                        break;
                    if (Math.Abs(pl.GetBulgeAt(i)) > 1e-6)
                        continue;
                    var p0 = pl.GetPoint2dAt(i);
                    var p1 = pl.GetPoint2dAt(next);
                    segs.Add(new Seg(id, p0.X, p0.Y, p1.X, p1.Y));
                }
                return;
            }
            if (entity is Polyline2d p2)
            {
                var pts = new List<Point3d>();
                foreach (ObjectId vid in p2)
                {
                    var v = tr.GetObject(vid, OpenMode.ForRead) as Vertex2d;
                    if (v != null)
                        pts.Add(v.Position);
                }
                int count = pts.Count;
                if (count < 2)
                    return;
                int last = p2.Closed ? count : count - 1;
                for (int i = 0; i < last; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % count];
                    segs.Add(new Seg(id, a.X, a.Y, b.X, b.Y));
                }
                return;
            }
            if (entity is DBText db)
            {
                Extents3d ext;
                try { ext = db.GeometricExtents; }
                catch { return; }
                texts.Add(new TextEnt(id, ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y, db.TextString));
                return;
            }
            if (entity is MText mt)
            {
                Extents3d ext;
                try { ext = mt.GeometricExtents; }
                catch { return; }
                texts.Add(new TextEnt(id, ext.MinPoint.X, ext.MinPoint.Y, ext.MaxPoint.X, ext.MaxPoint.Y, mt.Contents));
            }
        }

        private static bool Connected(Seg a, Seg b, double snap)
        {
            if (Dist(a.X1, a.Y1, b.X1, b.Y1) <= snap) return true;
            if (Dist(a.X1, a.Y1, b.X2, b.Y2) <= snap) return true;
            if (Dist(a.X2, a.Y2, b.X1, b.Y1) <= snap) return true;
            if (Dist(a.X2, a.Y2, b.X2, b.Y2) <= snap) return true;
            double ax0 = Math.Min(a.X1, a.X2) - snap, ax1 = Math.Max(a.X1, a.X2) + snap;
            double ay0 = Math.Min(a.Y1, a.Y2) - snap, ay1 = Math.Max(a.Y1, a.Y2) + snap;
            double bx0 = Math.Min(b.X1, b.X2), bx1 = Math.Max(b.X1, b.X2);
            double by0 = Math.Min(b.Y1, b.Y2), by1 = Math.Max(b.Y1, b.Y2);
            return ax0 <= bx1 && ax1 >= bx0 && ay0 <= by1 && ay1 >= by0;
        }

        private static double Dist(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2, dy = y1 - y2;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double Mm(double drawing, double scale)
        {
            return drawing / scale;
        }

        private struct Seg
        {
            public ObjectId Id;
            public double X1, Y1, X2, Y2;
            public Seg(ObjectId id, double x1, double y1, double x2, double y2)
            {
                Id = id;
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
            }
        }

        private struct TextEnt
        {
            public ObjectId Id;
            public double MinX, MinY, MaxX, MaxY;
            public string Content;
            public TextEnt(ObjectId id, double minX, double minY, double maxX, double maxY, string content)
            {
                Id = id;
                MinX = minX;
                MinY = minY;
                MaxX = maxX;
                MaxY = maxY;
                Content = content;
            }
        }
    }
}
