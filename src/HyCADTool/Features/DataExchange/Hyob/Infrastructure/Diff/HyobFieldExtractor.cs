using System;
using System.Collections.Generic;
using System.Globalization;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Opaque;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Tables;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.Diff
{
    /// <summary>
    /// 把任意 hyob object blob → 有序的 (字段名, 显示值) 列表，用于 hyobD 字段级 diff。
    /// 设计：04 §11（M11-B）。同一种 type_id 的两个 blob 字段名集合永远相同（schema 决定），
    /// 因此 diff = 按相同 index 比较 oldValue / newValue 字符串。
    ///
    /// 数值显示规则：
    /// <list type="bullet">
    ///   <item>double 普通数：F3 InvariantCulture（mm 精度足够）</item>
    ///   <item>double 角度（弧度）：转度 + F2，附 °</item>
    ///   <item>byte 0/1（is_xxx）：true / false</item>
    ///   <item>string：直接（带引号便于辨识空白）</item>
    ///   <item>3D 点：(x, y, z)</item>
    ///   <item>列表：仅显示 count（避免输出爆炸）</item>
    /// </list>
    /// </summary>
    public static class HyobFieldExtractor
    {
        /// <summary>提取一个 hyob object 的字段列表。无法识别的 type_id 返回空列表（diff 退化到 hash 级）。</summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Extract(byte[] blob)
        {
            if (blob == null || blob.Length == 0) return Empty;
            HyobObjectKind kind;
            try
            {
                var (header, _) = HyobObjectHeader.Decode(blob);
                kind = header.TypeId;
            }
            catch { return Empty; }

            try
            {
                switch (kind)
                {
                    case HyobObjectKind.Line:                return ExtractLine(blob);
                    case HyobObjectKind.Circle:              return ExtractCircle(blob);
                    case HyobObjectKind.Arc:                 return ExtractArc(blob);
                    case HyobObjectKind.Polyline:            return ExtractPolyline(blob);
                    case HyobObjectKind.DBText:              return ExtractDBText(blob);
                    case HyobObjectKind.MText:               return ExtractMText(blob);
                    case HyobObjectKind.BlockReference:      return ExtractBlockReference(blob);
                    case HyobObjectKind.Dimension:           return ExtractDimension(blob);
                    case HyobObjectKind.MLeader:             return ExtractMLeader(blob);
                    case HyobObjectKind.HatchBoundary:       return ExtractHatch(blob);
                    case HyobObjectKind.Wipeout:             return ExtractWipeout(blob);
                    case HyobObjectKind.XDataAttachment:     return ExtractXData(blob);
                    case HyobObjectKind.ExtensionDictionary: return ExtractExtDict(blob);
                    case HyobObjectKind.LayerDef:            return ExtractLayerDef(blob);
                    case HyobObjectKind.TextStyleDef:        return ExtractTextStyleDef(blob);
                    case HyobObjectKind.DimStyleDef:         return ExtractDimStyleDef(blob);
                    case HyobObjectKind.BlockDef:            return ExtractBlockDef(blob);
                    case HyobObjectKind.Opaque:              return ExtractOpaque(blob);
                    default: return Empty;
                }
            }
            catch
            {
                return Empty;
            }
        }

        private static readonly IReadOnlyList<KeyValuePair<string, string>> Empty
            = Array.Empty<KeyValuePair<string, string>>();

        private static List<KeyValuePair<string, string>> New() => new List<KeyValuePair<string, string>>(16);
        private static void Add(List<KeyValuePair<string, string>> l, string k, string v)
            => l.Add(new KeyValuePair<string, string>(k, v));

        private static string F(double v) => v.ToString("F3", CultureInfo.InvariantCulture);
        private static string Deg(double rad) => (rad * 180.0 / Math.PI).ToString("F2", CultureInfo.InvariantCulture) + "°";
        private static string Q(string s) => s == null ? "\"\"" : "\"" + s.Replace("\"", "\\\"") + "\"";
        private static string B(byte v) => v == 0 ? "false" : "true";
        private static string Pt(double x, double y, double z) => $"({F(x)}, {F(y)}, {F(z)})";

        private static List<KeyValuePair<string, string>> ExtractLine(byte[] blob)
        {
            var o = HyobLine.Decode(blob);
            var l = New();
            Add(l, "layer", Q(o.Layer));
            Add(l, "start", Pt(o.StartX, o.StartY, o.StartZ));
            Add(l, "end",   Pt(o.EndX, o.EndY, o.EndZ));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractCircle(byte[] blob)
        {
            var o = HyobCircle.Decode(blob);
            var l = New();
            Add(l, "layer",  Q(o.Layer));
            Add(l, "center", Pt(o.CenterX, o.CenterY, o.CenterZ));
            Add(l, "radius", F(o.Radius));
            Add(l, "normal", Pt(o.NormalX, o.NormalY, o.NormalZ));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractArc(byte[] blob)
        {
            var o = HyobArc.Decode(blob);
            var l = New();
            Add(l, "layer",       Q(o.Layer));
            Add(l, "center",      Pt(o.CenterX, o.CenterY, o.CenterZ));
            Add(l, "radius",      F(o.Radius));
            Add(l, "start_angle", Deg(o.StartAngleRad));
            Add(l, "end_angle",   Deg(o.EndAngleRad));
            Add(l, "normal",      Pt(o.NormalX, o.NormalY, o.NormalZ));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractPolyline(byte[] blob)
        {
            var o = HyobPolyline.Decode(blob);
            var l = New();
            Add(l, "layer",          Q(o.Layer));
            Add(l, "closed",         o.Closed ? "true" : "false");
            Add(l, "elevation",      F(o.Elevation));
            Add(l, "constant_width", F(o.ConstantWidth));
            Add(l, "vertex_count",   o.Vertices.Count.ToString(CultureInfo.InvariantCulture));
            Add(l, "normal",         Pt(o.NormalX, o.NormalY, o.NormalZ));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractDBText(byte[] blob)
        {
            var o = HyobDBText.Decode(blob);
            var l = New();
            Add(l, "layer",        Q(o.Layer));
            Add(l, "text",         Q(o.TextString));
            Add(l, "style",        Q(o.TextStyleName));
            Add(l, "position",     Pt(o.PosX, o.PosY, o.PosZ));
            Add(l, "height",       F(o.Height));
            Add(l, "rotation",     Deg(o.RotationRad));
            Add(l, "width_factor", F(o.WidthFactor));
            Add(l, "oblique",      Deg(o.ObliqueRad));
            Add(l, "thickness",    F(o.Thickness));
            Add(l, "h_mode",       o.HorizontalMode.ToString(CultureInfo.InvariantCulture));
            Add(l, "v_mode",       o.VerticalMode.ToString(CultureInfo.InvariantCulture));
            Add(l, "align",        Pt(o.AlignX, o.AlignY, o.AlignZ));
            Add(l, "flags",        o.Flags.ToString(CultureInfo.InvariantCulture));
            Add(l, "normal",       Pt(o.NormalX, o.NormalY, o.NormalZ));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractMText(byte[] blob)
        {
            var o = HyobMText.Decode(blob);
            var l = New();
            Add(l, "layer",            Q(o.Layer));
            Add(l, "contents",         Q(o.Contents));
            Add(l, "style",            Q(o.TextStyleName));
            Add(l, "location",         Pt(o.LocX, o.LocY, o.LocZ));
            Add(l, "text_height",      F(o.TextHeight));
            Add(l, "width",            F(o.Width));
            Add(l, "rotation",         Deg(o.RotationRad));
            Add(l, "attachment",       o.Attachment.ToString(CultureInfo.InvariantCulture));
            Add(l, "line_spacing",     o.LineSpacingStyle.ToString(CultureInfo.InvariantCulture) +
                                       "/" + F(o.LineSpacingFactor));
            Add(l, "background_fill",  o.BackgroundFill.ToString(CultureInfo.InvariantCulture));
            Add(l, "bg_color_argb",    "0x" + o.BackgroundColorArgb.ToString("X8", CultureInfo.InvariantCulture));
            Add(l, "bg_scale",         F(o.BackgroundScaleFactor));
            Add(l, "direction",        Pt(o.DirX, o.DirY, o.DirZ));
            Add(l, "normal",           Pt(o.NormalX, o.NormalY, o.NormalZ));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractBlockReference(byte[] blob)
        {
            var o = HyobBlockReference.Decode(blob);
            var l = New();
            Add(l, "layer",      Q(o.Layer));
            Add(l, "block_name", Q(o.BlockName));
            Add(l, "position",   Pt(o.PosX, o.PosY, o.PosZ));
            Add(l, "scale",      $"({F(o.ScaleX)}, {F(o.ScaleY)}, {F(o.ScaleZ)})");
            Add(l, "rotation",   Deg(o.RotationRad));
            Add(l, "attr_count", o.Attributes.Count.ToString(CultureInfo.InvariantCulture));
            Add(l, "normal",     Pt(o.NormalX, o.NormalY, o.NormalZ));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractDimension(byte[] blob)
        {
            var o = HyobDimension.Decode(blob);
            var l = New();
            Add(l, "subtype",        o.DimType.ToString());
            Add(l, "layer",          Q(o.Layer));
            Add(l, "text",           Q(o.DimensionText));
            Add(l, "style",          Q(o.DimStyleName));
            Add(l, "block_name",     Q(o.DimBlockName));
            Add(l, "text_pos",       Pt(o.TextX, o.TextY, o.TextZ));
            Add(l, "measurement",    F(o.Measurement));
            Add(l, "def_pt_count",   o.DefiningPoints.Count.ToString(CultureInfo.InvariantCulture));
            Add(l, "extra_count",    o.Extras.Count.ToString(CultureInfo.InvariantCulture));
            Add(l, "normal",         Pt(o.NormalX, o.NormalY, o.NormalZ));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractMLeader(byte[] blob)
        {
            var o = HyobMLeader.Decode(blob);
            var l = New();
            Add(l, "layer",          Q(o.Layer));
            Add(l, "content_type",   o.ContentType.ToString(CultureInfo.InvariantCulture));
            Add(l, "mtext_contents", Q(o.MTextContents));
            Add(l, "block_name",     Q(o.BlockName));
            Add(l, "style",          Q(o.MLeaderStyleName));
            Add(l, "text_pos",       Pt(o.TextX, o.TextY, o.TextZ));
            Add(l, "text_height",    F(o.TextHeight));
            Add(l, "arrow_size",     F(o.ArrowSize));
            Add(l, "dogleg_length",  F(o.DoglegLength));
            Add(l, "landing_gap",    F(o.LandingGap));
            Add(l, "scale",          F(o.Scale));
            Add(l, "block_rotation", Deg(o.BlockRotation));
            Add(l, "leader_count",   o.LeaderCount.ToString(CultureInfo.InvariantCulture));
            Add(l, "leader_lines",   o.LeaderLineCount.ToString(CultureInfo.InvariantCulture));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractHatch(byte[] blob)
        {
            var o = HyobHatch.Decode(blob);
            var l = New();
            Add(l, "layer",         Q(o.Layer));
            Add(l, "pattern_type",  o.PatternType.ToString(CultureInfo.InvariantCulture));
            Add(l, "pattern_name",  Q(o.PatternName));
            Add(l, "pattern_scale", F(o.PatternScale));
            Add(l, "pattern_angle", Deg(o.PatternAngleRad));
            Add(l, "pattern_space", F(o.PatternSpace));
            Add(l, "hatch_style",   o.HatchStyle.ToString(CultureInfo.InvariantCulture));
            Add(l, "elevation",     F(o.Elevation));
            Add(l, "loop_count",    o.NumberOfLoops.ToString(CultureInfo.InvariantCulture));
            Add(l, "pattern_defs",  o.NumberOfPatternDefinitions.ToString(CultureInfo.InvariantCulture));
            Add(l, "area",          F(o.Area));
            Add(l, "associative",   B(o.Associative));
            Add(l, "normal",        Pt(o.NormalX, o.NormalY, o.NormalZ));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractWipeout(byte[] blob)
        {
            var o = HyobWipeout.Decode(blob);
            var l = New();
            Add(l, "layer",          Q(o.Layer));
            Add(l, "position",       Pt(o.PosX, o.PosY, o.PosZ));
            Add(l, "width",          F(o.Width));
            Add(l, "height",         F(o.Height));
            Add(l, "rotation",       Deg(o.RotationRad));
            Add(l, "color_index",    o.ColorIndex.ToString(CultureInfo.InvariantCulture));
            Add(l, "boundary_count", o.Boundary.Count.ToString(CultureInfo.InvariantCulture));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractXData(byte[] blob)
        {
            var o = HyobXDataAttachment.Decode(blob);
            var l = New();
            Add(l, "app_count", o.Groups.Count.ToString(CultureInfo.InvariantCulture));
            int total = 0;
            for (int i = 0; i < o.Groups.Count; i++)
            {
                var g = o.Groups[i];
                Add(l, $"app[{i}]", Q(g.AppName));
                Add(l, $"app[{i}].entries", g.Entries.Count.ToString(CultureInfo.InvariantCulture));
                total += g.Entries.Count;
                int max = Math.Min(8, g.Entries.Count);
                for (int j = 0; j < max; j++)
                {
                    var e = g.Entries[j];
                    Add(l, $"app[{i}].e[{j}]", $"({e.DxfCode},{Q(e.ValueStr)})");
                }
                if (g.Entries.Count > max) Add(l, $"app[{i}].e[…]", $"还有 {g.Entries.Count - max} 条");
            }
            Add(l, "total_entries", total.ToString(CultureInfo.InvariantCulture));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractExtDict(byte[] blob)
        {
            var o = HyobExtensionDictionary.Decode(blob);
            var l = New();
            Add(l, "entry_count", o.Entries.Count.ToString(CultureInfo.InvariantCulture));
            int max = Math.Min(16, o.Entries.Count);
            for (int i = 0; i < max; i++)
            {
                var e = o.Entries[i];
                Add(l, $"e[{i}]", $"{Q(e.KeyName)} kind={e.Kind} {Q(e.Summary)}");
            }
            if (o.Entries.Count > max) Add(l, "e[…]", $"还有 {o.Entries.Count - max} 条");
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractLayerDef(byte[] blob)
        {
            var o = HyobLayerDef.Decode(blob);
            var l = New();
            Add(l, "name",          Q(o.Name));
            Add(l, "color_index",   o.ColorIndex.ToString(CultureInfo.InvariantCulture));
            Add(l, "linetype",      Q(o.LinetypeName));
            Add(l, "lineweight_mm", F(o.LineweightMm));
            Add(l, "is_off",        B(o.IsOff));
            Add(l, "is_frozen",     B(o.IsFrozen));
            Add(l, "is_locked",     B(o.IsLocked));
            Add(l, "is_plottable",  B(o.IsPlottable));
            Add(l, "is_used",       B(o.IsUsed));
            Add(l, "description",   Q(o.Description));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractTextStyleDef(byte[] blob)
        {
            var o = HyobTextStyleDef.Decode(blob);
            var l = New();
            Add(l, "name",          Q(o.Name));
            Add(l, "file_name",     Q(o.FileName));
            Add(l, "big_font",      Q(o.BigFontFileName));
            Add(l, "text_size",     F(o.TextSize));
            Add(l, "width_factor",  F(o.WidthFactor));
            Add(l, "oblique",       Deg(o.ObliqueRad));
            Add(l, "is_vertical",   B(o.IsVertical));
            Add(l, "is_shape",      B(o.IsShape));
            Add(l, "font_typeface", Q(o.FontTypeface));
            Add(l, "font_pitch",    o.FontPitchFamily.ToString(CultureInfo.InvariantCulture));
            Add(l, "font_charset",  o.FontCharset.ToString(CultureInfo.InvariantCulture));
            Add(l, "font_bold",     B(o.FontBold));
            Add(l, "font_italic",   B(o.FontItalic));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractDimStyleDef(byte[] blob)
        {
            var o = HyobDimStyleDef.Decode(blob);
            var l = New();
            Add(l, "name",                  Q(o.Name));
            Add(l, "dim_scale",             F(o.DimScale));
            Add(l, "dim_text_height",       F(o.DimTextHeight));
            Add(l, "dim_arrow_size",        F(o.DimArrowSize));
            Add(l, "dim_ext_offset",        F(o.DimExtOffset));
            Add(l, "dim_ext_extension",     F(o.DimExtExtension));
            Add(l, "dim_baseline_distance", F(o.DimBaselineDistance));
            Add(l, "text_style",            Q(o.TextStyleName));
            Add(l, "ldr_arrow_block",       Q(o.LdrArrowBlockName));
            Add(l, "text_color",            o.TextColorIndex.ToString(CultureInfo.InvariantCulture));
            Add(l, "dim_line_color",        o.DimLineColorIndex.ToString(CultureInfo.InvariantCulture));
            Add(l, "ext_line_color",        o.ExtLineColorIndex.ToString(CultureInfo.InvariantCulture));
            Add(l, "linear_scale",          F(o.LinearScaleFactor));
            Add(l, "decimal_places",        o.DecimalPlaces.ToString(CultureInfo.InvariantCulture));
            Add(l, "tofl",                  B(o.Tofl));
            Add(l, "tih",                   B(o.Tih));
            Add(l, "toh",                   B(o.Toh));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractBlockDef(byte[] blob)
        {
            var o = HyobBlockDef.Decode(blob);
            var l = New();
            Add(l, "name",          Q(o.Name));
            Add(l, "description",   Q(o.Description));
            Add(l, "is_layout",     B(o.IsLayout));
            Add(l, "is_xref",       B(o.IsFromExternalReference));
            Add(l, "is_anonymous",  B(o.IsAnonymous));
            Add(l, "origin",        Pt(o.OriginX, o.OriginY, o.OriginZ));
            Add(l, "units",         o.Units.ToString(CultureInfo.InvariantCulture));
            Add(l, "entity_count",  o.EntityCount.ToString(CultureInfo.InvariantCulture));
            Add(l, "path_name",     Q(o.PathName));
            return l;
        }

        private static List<KeyValuePair<string, string>> ExtractOpaque(byte[] blob)
        {
            var o = HyobOpaqueObject.Decode(blob);
            var l = New();
            Add(l, "dwg_class",       Q(o.DwgClassName));
            Add(l, "dwg_app",         Q(o.DwgAppName));
            Add(l, "raw_dxf_size",    (o.RawDxf?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
            Add(l, "raw_xdata_size",  (o.RawXData?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
            Add(l, "raw_extdict_size",(o.RawExtensionDictionary?.Length ?? 0).ToString(CultureInfo.InvariantCulture));
            return l;
        }
    }
}
