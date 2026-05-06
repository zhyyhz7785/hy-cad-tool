using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Tables
{
    /// <summary>
    /// hyob 表定义 - 文字样式 TextStyleDef（type_id = 0x0102）。
    /// payload schema v1：
    /// <code>
    ///   [ utf8   name ]
    ///   [ utf8   file_name ]              // SHX/TTF 文件名
    ///   [ utf8   big_font_file_name ]
    ///   [ double text_size ]              // 0 表示运行时指定
    ///   [ double width_factor ]
    ///   [ double oblique_rad ]
    ///   [ byte   is_vertical ]
    ///   [ byte   is_shape ]
    ///   [ utf8   font_typeface ]          // TrueType typeface name (e.g. Arial)
    ///   [ int32  font_pitch_family ]      // PitchAndFamily 枚举打包
    ///   [ int32  font_charset ]
    ///   [ byte   font_bold ]
    ///   [ byte   font_italic ]
    /// </code>
    /// </summary>
    public sealed class HyobTextStyleDef : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public string Name { get; }
        public string FileName { get; }
        public string BigFontFileName { get; }
        public double TextSize { get; }
        public double WidthFactor { get; }
        public double ObliqueRad { get; }
        public byte IsVertical { get; }
        public byte IsShape { get; }
        public string FontTypeface { get; }
        public int FontPitchFamily { get; }
        public int FontCharset { get; }
        public byte FontBold { get; }
        public byte FontItalic { get; }

        public HyobObjectKind TypeId => HyobObjectKind.TextStyleDef;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobTextStyleDef(
            string name, string fileName, string bigFontFileName,
            double textSize, double widthFactor, double obliqueRad,
            byte isVertical, byte isShape,
            string fontTypeface, int fontPitchFamily, int fontCharset,
            byte fontBold, byte fontItalic)
        {
            Name = name ?? string.Empty;
            FileName = fileName ?? string.Empty;
            BigFontFileName = bigFontFileName ?? string.Empty;
            TextSize = textSize;
            WidthFactor = widthFactor;
            ObliqueRad = obliqueRad;
            IsVertical = isVertical;
            IsShape = isShape;
            FontTypeface = fontTypeface ?? string.Empty;
            FontPitchFamily = fontPitchFamily;
            FontCharset = fontCharset;
            FontBold = fontBold;
            FontItalic = fontItalic;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Name);
                HyobBinary.WriteUtf8(w, FileName);
                HyobBinary.WriteUtf8(w, BigFontFileName);
                w.Write(TextSize);
                w.Write(WidthFactor);
                w.Write(ObliqueRad);
                w.Write(IsVertical);
                w.Write(IsShape);
                HyobBinary.WriteUtf8(w, FontTypeface);
                w.Write(FontPitchFamily);
                w.Write(FontCharset);
                w.Write(FontBold);
                w.Write(FontItalic);
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobTextStyleDef Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.TextStyleDef)
                throw new InvalidDataException($"期望 TextStyleDef（0x0102），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"TextStyleDef schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var name = HyobBinary.ReadUtf8(r);
                var fn = HyobBinary.ReadUtf8(r);
                var bigFn = HyobBinary.ReadUtf8(r);
                double size = r.ReadDouble();
                double wf = r.ReadDouble();
                double obl = r.ReadDouble();
                byte vert = r.ReadByte();
                byte shape = r.ReadByte();
                var face = HyobBinary.ReadUtf8(r);
                int pitch = r.ReadInt32();
                int cs = r.ReadInt32();
                byte bold = r.ReadByte();
                byte italic = r.ReadByte();
                return new HyobTextStyleDef(name, fn, bigFn, size, wf, obl, vert, shape,
                    face, pitch, cs, bold, italic);
            }
        }
    }
}
