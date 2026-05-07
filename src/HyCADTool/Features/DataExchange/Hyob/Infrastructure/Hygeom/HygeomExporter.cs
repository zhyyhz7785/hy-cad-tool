using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Opaque;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Tables;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;
using Newtonsoft.Json;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.Hygeom
{
    /// <summary>
    /// 把 hyob commit 导出为 hygeom JSON（结构化文本，AI 友好）。
    /// 设计：04 §12（M12）；用户最初诉求：把 AutoCAD 几何结构化传给 AI 分析。
    ///
    /// 输出 schema：<c>hyob.snapshot/v4 → hygeom.export/v1</c>
    /// 流式 JsonTextWriter，5800 entity ≈ 5MB JSON，10 万 entity 也不会 OOM。
    /// </summary>
    /// <summary>
    /// hygeom 导出过滤器（M12 v2）。所有字段 null/empty 视为不过滤。
    /// 多字段同时设置时取交集（AND）。
    /// </summary>
    public sealed class ExportFilter
    {
        /// <summary>仅导出 layer 等于此名（精确匹配）的 entity。null = 不过滤。</summary>
        public string LayerName { get; set; }
        /// <summary>仅导出 type 名（"Line" / "Dimension" / ...）匹配的 entity。null = 不过滤。</summary>
        public string TypeName { get; set; }
        /// <summary>仅导出 handle 前缀匹配（hex，大小写不敏感）的 entity。null = 不过滤。</summary>
        public string HandlePrefix { get; set; }

        public bool IsEmpty =>
            string.IsNullOrEmpty(LayerName) &&
            string.IsNullOrEmpty(TypeName) &&
            string.IsNullOrEmpty(HandlePrefix);

        public string Describe()
        {
            if (IsEmpty) return "(全部)";
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(LayerName))    parts.Add($"layer={LayerName}");
            if (!string.IsNullOrEmpty(TypeName))     parts.Add($"type={TypeName}");
            if (!string.IsNullOrEmpty(HandlePrefix)) parts.Add($"handle^={HandlePrefix}");
            return string.Join(" & ", parts);
        }
    }

    public sealed class HygeomExporter
    {
        public sealed class ExportStats
        {
            public int EntityCount { get; internal set; }
            public int XDataCount { get; internal set; }
            public int ExtDictCount { get; internal set; }
            public int LayerCount { get; internal set; }
            public int TextStyleCount { get; internal set; }
            public int DimStyleCount { get; internal set; }
            public int BlockDefCount { get; internal set; }
            public int EntitiesFiltered { get; internal set; }
            public long FileBytes { get; internal set; }
        }

        public const string SchemaVersion = "hyob.snapshot/v4 -> hygeom.export/v1";

        private readonly HyobObjectStore _objects;

        public HygeomExporter(HyobObjectStore objects)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        /// <summary>把 commitHash 整个快照导出到 outputPath。可选 filter 限定 entity 范围（不影响 tables）。</summary>
        public ExportStats Export(Hash commitHash, string outputPath, ExportFilter filter = null)
        {
            if (outputPath == null) throw new ArgumentNullException(nameof(outputPath));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var stats = new ExportStats();

            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            using (var sw = new StreamWriter(fs, new UTF8Encoding(false)))
            using (var w = new JsonTextWriter(sw)
            {
                Formatting = Formatting.Indented,
                Indentation = 2,
                IndentChar = ' ',
            })
            {
                w.WriteStartObject();

                w.WritePropertyName("$schema");
                w.WriteValue(SchemaVersion);
                w.WritePropertyName("exported_at");
                w.WriteValue(DateTime.UtcNow.ToString("o"));

                if (filter != null && !filter.IsEmpty)
                {
                    w.WritePropertyName("filter");
                    w.WriteStartObject();
                    if (!string.IsNullOrEmpty(filter.LayerName))    { w.WritePropertyName("layer");  w.WriteValue(filter.LayerName); }
                    if (!string.IsNullOrEmpty(filter.TypeName))     { w.WritePropertyName("type");   w.WriteValue(filter.TypeName); }
                    if (!string.IsNullOrEmpty(filter.HandlePrefix)) { w.WritePropertyName("handle_prefix"); w.WriteValue(filter.HandlePrefix); }
                    w.WriteEndObject();
                }

                if (!_objects.TryRead(commitHash, out var commitBlob))
                    throw new InvalidOperationException($"commit 不存在：{commitHash.ToHex().Substring(0, 12)}");
                var commit = HyobCommit.Decode(commitBlob);

                w.WritePropertyName("commit");
                WriteCommitInfo(w, commitHash, commit);

                if (!_objects.TryRead(commit.Tree, out var rootBlob))
                    throw new InvalidOperationException("root tree 不存在");
                var rootTree = HyobTree.Decode(rootBlob);

                w.WritePropertyName("tables");
                WriteTablesSection(w, rootTree, stats);

                w.WritePropertyName("entities");
                WriteEntitiesSection(w, rootTree, stats, filter);

                w.WriteEndObject();
                w.Flush();
            }

            stats.FileBytes = new FileInfo(outputPath).Length;
            return stats;
        }

        // ============================================================
        // commit info
        // ============================================================

        private static void WriteCommitInfo(JsonWriter w, Hash hash, HyobCommit commit)
        {
            w.WriteStartObject();
            w.WritePropertyName("hash");    w.WriteValue(hash.ToHex());
            w.WritePropertyName("short");   w.WriteValue(hash.ToHex().Substring(0, 12));
            w.WritePropertyName("author");  w.WriteValue(commit.Author);
            w.WritePropertyName("email");   w.WriteValue(commit.Email);
            w.WritePropertyName("time");    w.WriteValue(commit.Time.ToString("o"));
            w.WritePropertyName("message"); w.WriteValue(commit.Message);
            w.WritePropertyName("command"); w.WriteValue(commit.Command);

            w.WritePropertyName("parents");
            w.WriteStartArray();
            foreach (var p in commit.Parents) w.WriteValue(p.ToHex());
            w.WriteEndArray();

            w.WritePropertyName("meta");
            w.WriteStartObject();
            foreach (var kv in commit.Meta)
            {
                w.WritePropertyName(kv.Key);
                w.WriteValue(kv.Value);
            }
            w.WriteEndObject();

            w.WriteEndObject();
        }

        // ============================================================
        // tables
        // ============================================================

        private void WriteTablesSection(JsonWriter w, HyobTree rootTree, ExportStats stats)
        {
            w.WriteStartObject();

            if (rootTree.TryFind("tables", out var tablesEntry) &&
                tablesEntry.Kind == HyobTreeEntryKind.Tree &&
                _objects.TryRead(tablesEntry.Hash, out var tablesBlob))
            {
                var tablesTree = HyobTree.Decode(tablesBlob);

                stats.LayerCount     = WriteTableArray(w, tablesTree, "layers",      WriteLayerDef);
                stats.TextStyleCount = WriteTableArray(w, tablesTree, "text_styles", WriteTextStyleDef);
                stats.DimStyleCount  = WriteTableArray(w, tablesTree, "dim_styles",  WriteDimStyleDef);
                stats.BlockDefCount  = WriteTableArray(w, tablesTree, "blocks",      WriteBlockDef);
            }
            else
            {
                w.WritePropertyName("layers");      w.WriteStartArray(); w.WriteEndArray();
                w.WritePropertyName("text_styles"); w.WriteStartArray(); w.WriteEndArray();
                w.WritePropertyName("dim_styles");  w.WriteStartArray(); w.WriteEndArray();
                w.WritePropertyName("blocks");      w.WriteStartArray(); w.WriteEndArray();
            }

            w.WriteEndObject();
        }

        private int WriteTableArray(JsonWriter w, HyobTree tablesTree, string name, Action<JsonWriter, byte[]> writer)
        {
            int count = 0;
            w.WritePropertyName(name);
            w.WriteStartArray();
            if (tablesTree.TryFind(name, out var sub) && sub.Kind == HyobTreeEntryKind.Tree &&
                _objects.TryRead(sub.Hash, out var blob))
            {
                var subTree = HyobTree.Decode(blob);
                foreach (var e in subTree.Entries)
                {
                    if (e.Kind != HyobTreeEntryKind.Object) continue;
                    if (!_objects.TryRead(e.Hash, out var b)) continue;
                    try { writer(w, b); count++; }
                    catch { /* 单条错误不阻断整个导出 */ }
                }
            }
            w.WriteEndArray();
            return count;
        }

        // ============================================================
        // entities
        // ============================================================

        private void WriteEntitiesSection(JsonWriter w, HyobTree rootTree, ExportStats stats, ExportFilter filter)
        {
            w.WriteStartArray();

            if (!rootTree.TryFind("entities", out var entitiesEntry) ||
                entitiesEntry.Kind != HyobTreeEntryKind.Tree ||
                !_objects.TryRead(entitiesEntry.Hash, out var entitiesBlob))
            {
                w.WriteEndArray();
                return;
            }
            var entitiesTree = HyobTree.Decode(entitiesBlob);

            if (!entitiesTree.TryFind("by-handle", out var byHandleEntry) ||
                byHandleEntry.Kind != HyobTreeEntryKind.Tree ||
                !_objects.TryRead(byHandleEntry.Hash, out var byHandleBlob))
            {
                w.WriteEndArray();
                return;
            }
            var byHandleTree = HyobTree.Decode(byHandleBlob);

            string currentBase = null;
            bool entityOpen = false;
            bool currentSkipped = false;

            foreach (var entry in byHandleTree.Entries)
            {
                string name = entry.Name;
                string baseHandle; string suffix;
                if (name.EndsWith(".x", StringComparison.Ordinal))
                {
                    baseHandle = name.Substring(0, name.Length - 2);
                    suffix = ".x";
                }
                else if (name.EndsWith(".d", StringComparison.Ordinal))
                {
                    baseHandle = name.Substring(0, name.Length - 2);
                    suffix = ".d";
                }
                else
                {
                    baseHandle = name;
                    suffix = string.Empty;
                }

                if (suffix.Length == 0)
                {
                    if (entityOpen) { w.WriteEndObject(); entityOpen = false; }
                    currentBase = baseHandle;
                    currentSkipped = false;

                    if (!_objects.TryRead(entry.Hash, out var blob)) { currentSkipped = true; continue; }
                    HyobObjectHeader header;
                    try { var (h, _) = HyobObjectHeader.Decode(blob); header = h; }
                    catch { currentSkipped = true; continue; }

                    string typeName = TypeName(header.TypeId);
                    if (!PassFilter(filter, baseHandle, typeName, blob, header.TypeId))
                    {
                        currentSkipped = true;
                        stats.EntitiesFiltered++;
                        continue;
                    }

                    w.WriteStartObject();
                    w.WritePropertyName("handle"); w.WriteValue(baseHandle);
                    w.WritePropertyName("type");   w.WriteValue(typeName);
                    w.WritePropertyName("fields");
                    WriteFieldsByType(w, header.TypeId, blob);

                    entityOpen = true;
                    stats.EntityCount++;
                }
                else if (suffix == ".x")
                {
                    if (!entityOpen || currentSkipped ||
                        !string.Equals(currentBase, baseHandle, StringComparison.Ordinal)) continue;
                    if (!_objects.TryRead(entry.Hash, out var blob)) continue;
                    w.WritePropertyName("xdata");
                    WriteFieldsByType(w, HyobObjectKind.XDataAttachment, blob);
                    stats.XDataCount++;
                }
                else if (suffix == ".d")
                {
                    if (!entityOpen || currentSkipped ||
                        !string.Equals(currentBase, baseHandle, StringComparison.Ordinal)) continue;
                    if (!_objects.TryRead(entry.Hash, out var blob)) continue;
                    w.WritePropertyName("extdict");
                    WriteFieldsByType(w, HyobObjectKind.ExtensionDictionary, blob);
                    stats.ExtDictCount++;
                }
            }
            if (entityOpen) { w.WriteEndObject(); entityOpen = false; }

            w.WriteEndArray();
        }

        /// <summary>
        /// 应用过滤器：handle_prefix → type → layer，AND 取交集。layer 提取走 FieldExtractor 第一项的 "layer" 字段。
        /// 任一规则不命中即返回 false。
        /// </summary>
        private static bool PassFilter(ExportFilter f, string handle, string typeName, byte[] blob, HyobObjectKind kind)
        {
            if (f == null || f.IsEmpty) return true;

            if (!string.IsNullOrEmpty(f.HandlePrefix))
            {
                if (!handle.StartsWith(f.HandlePrefix, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (!string.IsNullOrEmpty(f.TypeName))
            {
                if (!string.Equals(typeName, f.TypeName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            if (!string.IsNullOrEmpty(f.LayerName))
            {
                string layer = ExtractLayerSafe(blob);
                if (layer == null) return false;
                if (!string.Equals(layer, f.LayerName, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        /// <summary>从 entity blob 提取 layer 字段；非 entity 类型（XData / 表）返回 null。</summary>
        private static string ExtractLayerSafe(byte[] blob)
        {
            try
            {
                var fields = Diff.HyobFieldExtractor.Extract(blob);
                foreach (var kv in fields)
                {
                    if (string.Equals(kv.Key, "layer", StringComparison.Ordinal))
                    {
                        var v = kv.Value;
                        if (v != null && v.Length >= 2 && v[0] == '"' && v[v.Length - 1] == '"')
                            return v.Substring(1, v.Length - 2);
                        return v;
                    }
                }
            }
            catch { }
            return null;
        }

        // ============================================================
        // type 名 & fields dispatcher
        // ============================================================

        private static string TypeName(HyobObjectKind k)
        {
            switch (k)
            {
                case HyobObjectKind.Line:                return "Line";
                case HyobObjectKind.Polyline:            return "Polyline";
                case HyobObjectKind.Arc:                 return "Arc";
                case HyobObjectKind.Circle:              return "Circle";
                case HyobObjectKind.DBText:              return "DBText";
                case HyobObjectKind.MText:               return "MText";
                case HyobObjectKind.BlockReference:      return "BlockReference";
                case HyobObjectKind.Dimension:           return "Dimension";
                case HyobObjectKind.MLeader:             return "MLeader";
                case HyobObjectKind.HatchBoundary:       return "Hatch";
                case HyobObjectKind.Wipeout:             return "Wipeout";
                case HyobObjectKind.XDataAttachment:     return "XData";
                case HyobObjectKind.ExtensionDictionary: return "ExtensionDictionary";
                case HyobObjectKind.Opaque:              return "Opaque";
                default: return $"0x{(ushort)k:X4}";
            }
        }

        private static void WriteFieldsByType(JsonWriter w, HyobObjectKind kind, byte[] blob)
        {
            try
            {
                switch (kind)
                {
                    case HyobObjectKind.Line:                WriteLine(w, blob);           return;
                    case HyobObjectKind.Polyline:            WritePolyline(w, blob);       return;
                    case HyobObjectKind.Arc:                 WriteArc(w, blob);            return;
                    case HyobObjectKind.Circle:              WriteCircle(w, blob);         return;
                    case HyobObjectKind.DBText:              WriteDBText(w, blob);         return;
                    case HyobObjectKind.MText:               WriteMText(w, blob);          return;
                    case HyobObjectKind.BlockReference:      WriteBlockRef(w, blob);       return;
                    case HyobObjectKind.Dimension:           WriteDimension(w, blob);      return;
                    case HyobObjectKind.MLeader:             WriteMLeader(w, blob);        return;
                    case HyobObjectKind.HatchBoundary:       WriteHatch(w, blob);          return;
                    case HyobObjectKind.Wipeout:             WriteWipeout(w, blob);        return;
                    case HyobObjectKind.XDataAttachment:     WriteXData(w, blob);          return;
                    case HyobObjectKind.ExtensionDictionary: WriteExtDict(w, blob);        return;
                    case HyobObjectKind.Opaque:              WriteOpaque(w, blob);         return;
                }
            }
            catch
            {
                w.WriteStartObject();
                w.WritePropertyName("_error"); w.WriteValue("decode failed");
                w.WriteEndObject();
                return;
            }
            w.WriteStartObject();
            w.WritePropertyName("_unknown_type"); w.WriteValue($"0x{(ushort)kind:X4}");
            w.WriteEndObject();
        }

        // ============================================================
        // helpers
        // ============================================================

        private static void WritePoint(JsonWriter w, double x, double y, double z)
        {
            w.WriteStartArray();
            w.WriteValue(x); w.WriteValue(y); w.WriteValue(z);
            w.WriteEndArray();
        }

        // ============================================================
        // typed writers
        // ============================================================

        private static void WriteLine(JsonWriter w, byte[] blob)
        {
            var o = HyobLine.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("layer"); w.WriteValue(o.Layer);
            w.WritePropertyName("start"); WritePoint(w, o.StartX, o.StartY, o.StartZ);
            w.WritePropertyName("end");   WritePoint(w, o.EndX, o.EndY, o.EndZ);
            w.WriteEndObject();
        }

        private static void WriteCircle(JsonWriter w, byte[] blob)
        {
            var o = HyobCircle.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("layer");  w.WriteValue(o.Layer);
            w.WritePropertyName("center"); WritePoint(w, o.CenterX, o.CenterY, o.CenterZ);
            w.WritePropertyName("radius"); w.WriteValue(o.Radius);
            w.WritePropertyName("normal"); WritePoint(w, o.NormalX, o.NormalY, o.NormalZ);
            w.WriteEndObject();
        }

        private static void WriteArc(JsonWriter w, byte[] blob)
        {
            var o = HyobArc.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("layer");           w.WriteValue(o.Layer);
            w.WritePropertyName("center");          WritePoint(w, o.CenterX, o.CenterY, o.CenterZ);
            w.WritePropertyName("radius");          w.WriteValue(o.Radius);
            w.WritePropertyName("start_angle_rad"); w.WriteValue(o.StartAngleRad);
            w.WritePropertyName("end_angle_rad");   w.WriteValue(o.EndAngleRad);
            w.WritePropertyName("normal");          WritePoint(w, o.NormalX, o.NormalY, o.NormalZ);
            w.WriteEndObject();
        }

        private static void WritePolyline(JsonWriter w, byte[] blob)
        {
            var o = HyobPolyline.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("layer");          w.WriteValue(o.Layer);
            w.WritePropertyName("closed");         w.WriteValue(o.Closed);
            w.WritePropertyName("elevation");      w.WriteValue(o.Elevation);
            w.WritePropertyName("constant_width"); w.WriteValue(o.ConstantWidth);
            w.WritePropertyName("normal");         WritePoint(w, o.NormalX, o.NormalY, o.NormalZ);
            w.WritePropertyName("vertices");
            w.WriteStartArray();
            foreach (var v in o.Vertices)
            {
                w.WriteStartObject();
                w.WritePropertyName("x");     w.WriteValue(v.X);
                w.WritePropertyName("y");     w.WriteValue(v.Y);
                w.WritePropertyName("bulge"); w.WriteValue(v.Bulge);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }

        private static void WriteDBText(JsonWriter w, byte[] blob)
        {
            var o = HyobDBText.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("layer");         w.WriteValue(o.Layer);
            w.WritePropertyName("text");          w.WriteValue(o.TextString);
            w.WritePropertyName("style");         w.WriteValue(o.TextStyleName);
            w.WritePropertyName("position");      WritePoint(w, o.PosX, o.PosY, o.PosZ);
            w.WritePropertyName("height");        w.WriteValue(o.Height);
            w.WritePropertyName("rotation_rad");  w.WriteValue(o.RotationRad);
            w.WritePropertyName("width_factor");  w.WriteValue(o.WidthFactor);
            w.WritePropertyName("oblique_rad");   w.WriteValue(o.ObliqueRad);
            w.WritePropertyName("thickness");     w.WriteValue(o.Thickness);
            w.WritePropertyName("h_mode");        w.WriteValue(o.HorizontalMode);
            w.WritePropertyName("v_mode");        w.WriteValue(o.VerticalMode);
            w.WritePropertyName("align");         WritePoint(w, o.AlignX, o.AlignY, o.AlignZ);
            w.WritePropertyName("flags");         w.WriteValue(o.Flags);
            w.WritePropertyName("normal");        WritePoint(w, o.NormalX, o.NormalY, o.NormalZ);
            w.WriteEndObject();
        }

        private static void WriteMText(JsonWriter w, byte[] blob)
        {
            var o = HyobMText.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("layer");        w.WriteValue(o.Layer);
            w.WritePropertyName("contents");     w.WriteValue(o.Contents);
            w.WritePropertyName("style");        w.WriteValue(o.TextStyleName);
            w.WritePropertyName("location");     WritePoint(w, o.LocX, o.LocY, o.LocZ);
            w.WritePropertyName("text_height");  w.WriteValue(o.TextHeight);
            w.WritePropertyName("width");        w.WriteValue(o.Width);
            w.WritePropertyName("rotation_rad"); w.WriteValue(o.RotationRad);
            w.WritePropertyName("attachment");   w.WriteValue(o.Attachment);
            w.WritePropertyName("line_spacing_style");  w.WriteValue(o.LineSpacingStyle);
            w.WritePropertyName("line_spacing_factor"); w.WriteValue(o.LineSpacingFactor);
            w.WritePropertyName("background_fill");     w.WriteValue(o.BackgroundFill);
            w.WritePropertyName("bg_color_argb");       w.WriteValue(o.BackgroundColorArgb);
            w.WritePropertyName("bg_scale");            w.WriteValue(o.BackgroundScaleFactor);
            w.WritePropertyName("direction");           WritePoint(w, o.DirX, o.DirY, o.DirZ);
            w.WritePropertyName("normal");              WritePoint(w, o.NormalX, o.NormalY, o.NormalZ);
            w.WriteEndObject();
        }

        private static void WriteBlockRef(JsonWriter w, byte[] blob)
        {
            var o = HyobBlockReference.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("layer");        w.WriteValue(o.Layer);
            w.WritePropertyName("block_name");   w.WriteValue(o.BlockName);
            w.WritePropertyName("position");     WritePoint(w, o.PosX, o.PosY, o.PosZ);
            w.WritePropertyName("scale");        WritePoint(w, o.ScaleX, o.ScaleY, o.ScaleZ);
            w.WritePropertyName("rotation_rad"); w.WriteValue(o.RotationRad);
            w.WritePropertyName("normal");       WritePoint(w, o.NormalX, o.NormalY, o.NormalZ);
            w.WritePropertyName("attributes");
            w.WriteStartArray();
            foreach (var a in o.Attributes)
            {
                w.WriteStartObject();
                w.WritePropertyName("tag");   w.WriteValue(a.Tag);
                w.WritePropertyName("value"); w.WriteValue(a.TextString);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }

        private static void WriteDimension(JsonWriter w, byte[] blob)
        {
            var o = HyobDimension.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("subtype");     w.WriteValue(o.DimType.ToString());
            w.WritePropertyName("layer");       w.WriteValue(o.Layer);
            w.WritePropertyName("text");        w.WriteValue(o.DimensionText);
            w.WritePropertyName("style");       w.WriteValue(o.DimStyleName);
            w.WritePropertyName("block_name");  w.WriteValue(o.DimBlockName);
            w.WritePropertyName("text_pos");    WritePoint(w, o.TextX, o.TextY, o.TextZ);
            w.WritePropertyName("measurement"); w.WriteValue(o.Measurement);
            w.WritePropertyName("normal");      WritePoint(w, o.NormalX, o.NormalY, o.NormalZ);
            w.WritePropertyName("defining_points");
            w.WriteStartArray();
            foreach (var p in o.DefiningPoints) WritePoint(w, p.X, p.Y, p.Z);
            w.WriteEndArray();
            w.WritePropertyName("extras");
            w.WriteStartArray();
            foreach (var v in o.Extras) w.WriteValue(v);
            w.WriteEndArray();
            w.WriteEndObject();
        }

        private static void WriteMLeader(JsonWriter w, byte[] blob)
        {
            var o = HyobMLeader.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("layer");          w.WriteValue(o.Layer);
            w.WritePropertyName("content_type");   w.WriteValue(o.ContentType);
            w.WritePropertyName("mtext_contents"); w.WriteValue(o.MTextContents);
            w.WritePropertyName("block_name");     w.WriteValue(o.BlockName);
            w.WritePropertyName("style");          w.WriteValue(o.MLeaderStyleName);
            w.WritePropertyName("text_pos");       WritePoint(w, o.TextX, o.TextY, o.TextZ);
            w.WritePropertyName("text_height");    w.WriteValue(o.TextHeight);
            w.WritePropertyName("arrow_size");     w.WriteValue(o.ArrowSize);
            w.WritePropertyName("dogleg_length");  w.WriteValue(o.DoglegLength);
            w.WritePropertyName("landing_gap");    w.WriteValue(o.LandingGap);
            w.WritePropertyName("scale");          w.WriteValue(o.Scale);
            w.WritePropertyName("block_rotation_rad"); w.WriteValue(o.BlockRotation);
            w.WritePropertyName("leader_count");   w.WriteValue(o.LeaderCount);
            w.WritePropertyName("leader_lines");   w.WriteValue(o.LeaderLineCount);
            w.WriteEndObject();
        }

        private static void WriteHatch(JsonWriter w, byte[] blob)
        {
            var o = HyobHatch.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("layer");             w.WriteValue(o.Layer);
            w.WritePropertyName("pattern_type");      w.WriteValue(o.PatternType);
            w.WritePropertyName("pattern_name");      w.WriteValue(o.PatternName);
            w.WritePropertyName("pattern_scale");     w.WriteValue(o.PatternScale);
            w.WritePropertyName("pattern_angle_rad"); w.WriteValue(o.PatternAngleRad);
            w.WritePropertyName("pattern_space");     w.WriteValue(o.PatternSpace);
            w.WritePropertyName("hatch_style");       w.WriteValue(o.HatchStyle);
            w.WritePropertyName("elevation");         w.WriteValue(o.Elevation);
            w.WritePropertyName("loop_count");        w.WriteValue(o.NumberOfLoops);
            w.WritePropertyName("pattern_defs");      w.WriteValue(o.NumberOfPatternDefinitions);
            w.WritePropertyName("area");              w.WriteValue(o.Area);
            w.WritePropertyName("associative");       w.WriteValue(o.Associative != 0);
            w.WritePropertyName("normal");            WritePoint(w, o.NormalX, o.NormalY, o.NormalZ);
            w.WriteEndObject();
        }

        private static void WriteWipeout(JsonWriter w, byte[] blob)
        {
            var o = HyobWipeout.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("layer");        w.WriteValue(o.Layer);
            w.WritePropertyName("position");     WritePoint(w, o.PosX, o.PosY, o.PosZ);
            w.WritePropertyName("width");        w.WriteValue(o.Width);
            w.WritePropertyName("height");       w.WriteValue(o.Height);
            w.WritePropertyName("rotation_rad"); w.WriteValue(o.RotationRad);
            w.WritePropertyName("color_index");  w.WriteValue(o.ColorIndex);
            w.WritePropertyName("boundary");
            w.WriteStartArray();
            foreach (var p in o.Boundary)
            {
                w.WriteStartArray();
                w.WriteValue(p.X); w.WriteValue(p.Y);
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }

        private static void WriteXData(JsonWriter w, byte[] blob)
        {
            var o = HyobXDataAttachment.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("apps");
            w.WriteStartArray();
            foreach (var g in o.Groups)
            {
                w.WriteStartObject();
                w.WritePropertyName("app"); w.WriteValue(g.AppName);
                w.WritePropertyName("entries");
                w.WriteStartArray();
                foreach (var e in g.Entries)
                {
                    w.WriteStartObject();
                    w.WritePropertyName("dxf_code"); w.WriteValue(e.DxfCode);
                    w.WritePropertyName("value");    w.WriteValue(e.ValueStr);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }

        private static void WriteExtDict(JsonWriter w, byte[] blob)
        {
            var o = HyobExtensionDictionary.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("entries");
            w.WriteStartArray();
            foreach (var e in o.Entries)
            {
                w.WriteStartObject();
                w.WritePropertyName("key");     w.WriteValue(e.KeyName);
                w.WritePropertyName("kind");    w.WriteValue(e.Kind);
                w.WritePropertyName("summary"); w.WriteValue(e.Summary);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }

        private static void WriteOpaque(JsonWriter w, byte[] blob)
        {
            var o = HyobOpaqueObject.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("dwg_class");        w.WriteValue(o.DwgClassName);
            w.WritePropertyName("dwg_app");          w.WriteValue(o.DwgAppName);
            w.WritePropertyName("raw_dxf_size");     w.WriteValue(o.RawDxf?.Length ?? 0);
            w.WritePropertyName("raw_xdata_size");   w.WriteValue(o.RawXData?.Length ?? 0);
            w.WritePropertyName("raw_extdict_size"); w.WriteValue(o.RawExtensionDictionary?.Length ?? 0);
            w.WriteEndObject();
        }

        // ============================================================
        // table writers
        // ============================================================

        private static void WriteLayerDef(JsonWriter w, byte[] blob)
        {
            var o = HyobLayerDef.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("name");          w.WriteValue(o.Name);
            w.WritePropertyName("color_index");   w.WriteValue(o.ColorIndex);
            w.WritePropertyName("linetype");      w.WriteValue(o.LinetypeName);
            w.WritePropertyName("lineweight_mm"); w.WriteValue(o.LineweightMm);
            w.WritePropertyName("is_off");        w.WriteValue(o.IsOff != 0);
            w.WritePropertyName("is_frozen");     w.WriteValue(o.IsFrozen != 0);
            w.WritePropertyName("is_locked");     w.WriteValue(o.IsLocked != 0);
            w.WritePropertyName("is_plottable");  w.WriteValue(o.IsPlottable != 0);
            w.WritePropertyName("is_used");       w.WriteValue(o.IsUsed != 0);
            w.WritePropertyName("description");   w.WriteValue(o.Description);
            w.WriteEndObject();
        }

        private static void WriteTextStyleDef(JsonWriter w, byte[] blob)
        {
            var o = HyobTextStyleDef.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("name");           w.WriteValue(o.Name);
            w.WritePropertyName("file_name");      w.WriteValue(o.FileName);
            w.WritePropertyName("big_font");       w.WriteValue(o.BigFontFileName);
            w.WritePropertyName("text_size");      w.WriteValue(o.TextSize);
            w.WritePropertyName("width_factor");   w.WriteValue(o.WidthFactor);
            w.WritePropertyName("oblique_rad");    w.WriteValue(o.ObliqueRad);
            w.WritePropertyName("is_vertical");    w.WriteValue(o.IsVertical != 0);
            w.WritePropertyName("is_shape");       w.WriteValue(o.IsShape != 0);
            w.WritePropertyName("font_typeface");  w.WriteValue(o.FontTypeface);
            w.WritePropertyName("font_pitch");     w.WriteValue(o.FontPitchFamily);
            w.WritePropertyName("font_charset");   w.WriteValue(o.FontCharset);
            w.WritePropertyName("font_bold");      w.WriteValue(o.FontBold != 0);
            w.WritePropertyName("font_italic");    w.WriteValue(o.FontItalic != 0);
            w.WriteEndObject();
        }

        private static void WriteDimStyleDef(JsonWriter w, byte[] blob)
        {
            var o = HyobDimStyleDef.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("name");                  w.WriteValue(o.Name);
            w.WritePropertyName("dim_scale");             w.WriteValue(o.DimScale);
            w.WritePropertyName("dim_text_height");       w.WriteValue(o.DimTextHeight);
            w.WritePropertyName("dim_arrow_size");        w.WriteValue(o.DimArrowSize);
            w.WritePropertyName("dim_ext_offset");        w.WriteValue(o.DimExtOffset);
            w.WritePropertyName("dim_ext_extension");     w.WriteValue(o.DimExtExtension);
            w.WritePropertyName("dim_baseline_distance"); w.WriteValue(o.DimBaselineDistance);
            w.WritePropertyName("text_style");            w.WriteValue(o.TextStyleName);
            w.WritePropertyName("ldr_arrow_block");       w.WriteValue(o.LdrArrowBlockName);
            w.WritePropertyName("text_color");            w.WriteValue(o.TextColorIndex);
            w.WritePropertyName("dim_line_color");        w.WriteValue(o.DimLineColorIndex);
            w.WritePropertyName("ext_line_color");        w.WriteValue(o.ExtLineColorIndex);
            w.WritePropertyName("linear_scale");          w.WriteValue(o.LinearScaleFactor);
            w.WritePropertyName("decimal_places");        w.WriteValue(o.DecimalPlaces);
            w.WritePropertyName("tofl");                  w.WriteValue(o.Tofl != 0);
            w.WritePropertyName("tih");                   w.WriteValue(o.Tih != 0);
            w.WritePropertyName("toh");                   w.WriteValue(o.Toh != 0);
            w.WriteEndObject();
        }

        private static void WriteBlockDef(JsonWriter w, byte[] blob)
        {
            var o = HyobBlockDef.Decode(blob);
            w.WriteStartObject();
            w.WritePropertyName("name");          w.WriteValue(o.Name);
            w.WritePropertyName("description");   w.WriteValue(o.Description);
            w.WritePropertyName("is_layout");     w.WriteValue(o.IsLayout != 0);
            w.WritePropertyName("is_xref");       w.WriteValue(o.IsFromExternalReference != 0);
            w.WritePropertyName("is_anonymous");  w.WriteValue(o.IsAnonymous != 0);
            w.WritePropertyName("origin");        WritePoint(w, o.OriginX, o.OriginY, o.OriginZ);
            w.WritePropertyName("units");         w.WriteValue(o.Units);
            w.WritePropertyName("entity_count");  w.WriteValue(o.EntityCount);
            w.WritePropertyName("path_name");     w.WriteValue(o.PathName);
            w.WriteEndObject();
        }
    }
}
