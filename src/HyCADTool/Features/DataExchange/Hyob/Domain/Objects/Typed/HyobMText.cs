using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>
    /// hyob TypedObject - 多行文字 MText（type_id = 0x0006）。设计：02 §5.3 / 04 §5。
    /// payload schema v1：
    /// <code>
    ///   [ utf8   layer ]
    ///   [ utf8   handle_hex ]
    ///   [ utf8   contents ]              含 AutoCAD MText 控制码（\\f / \\C / \\P 等）
    ///   [ utf8   text_style_name ]
    ///   [ double loc_x, loc_y, loc_z ]
    ///   [ double text_height ]
    ///   [ double width ]                 0 = 不限宽
    ///   [ double rotation_rad ]
    ///   [ double normal_x, normal_y, normal_z ]
    ///   [ double dir_x,    dir_y,    dir_z    ]
    ///   [ byte   attachment ]            AttachmentPoint
    ///   [ byte   draw_direction ]        DrawingDirection
    ///   [ byte   line_spacing_style ]
    ///   [ double line_spacing_factor ]
    ///   [ byte   background_fill ]       0=none, 1=on
    ///   [ uint32 background_color_argb ] AutoCAD Color → ARGB
    ///   [ double background_scale_factor ]
    /// </code>
    /// </summary>
    public sealed class HyobMText : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public string Layer { get; }
        public string HandleHex { get; }
        public string Contents { get; }
        public string TextStyleName { get; }
        public double LocX { get; }
        public double LocY { get; }
        public double LocZ { get; }
        public double TextHeight { get; }
        public double Width { get; }
        public double RotationRad { get; }
        public double NormalX { get; }
        public double NormalY { get; }
        public double NormalZ { get; }
        public double DirX { get; }
        public double DirY { get; }
        public double DirZ { get; }
        public byte Attachment { get; }
        public byte DrawDirection { get; }
        public byte LineSpacingStyle { get; }
        public double LineSpacingFactor { get; }
        public byte BackgroundFill { get; }
        public uint BackgroundColorArgb { get; }
        public double BackgroundScaleFactor { get; }

        public HyobObjectKind TypeId => HyobObjectKind.MText;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobMText(
            string layer, string handleHex,
            string contents, string textStyleName,
            double lx, double ly, double lz,
            double textHeight, double width, double rotationRad,
            double nx, double ny, double nz,
            double dx, double dy, double dz,
            byte attachment, byte drawDirection, byte lineSpacingStyle,
            double lineSpacingFactor,
            byte backgroundFill, uint backgroundColorArgb, double backgroundScaleFactor)
        {
            Layer = layer ?? string.Empty;
            HandleHex = handleHex ?? string.Empty;
            Contents = contents ?? string.Empty;
            TextStyleName = textStyleName ?? string.Empty;
            LocX = lx; LocY = ly; LocZ = lz;
            TextHeight = textHeight;
            Width = width;
            RotationRad = rotationRad;
            NormalX = nx; NormalY = ny; NormalZ = nz;
            DirX = dx; DirY = dy; DirZ = dz;
            Attachment = attachment;
            DrawDirection = drawDirection;
            LineSpacingStyle = lineSpacingStyle;
            LineSpacingFactor = lineSpacingFactor;
            BackgroundFill = backgroundFill;
            BackgroundColorArgb = backgroundColorArgb;
            BackgroundScaleFactor = backgroundScaleFactor;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Layer);
                HyobBinary.WriteUtf8(w, HandleHex);
                HyobBinary.WriteUtf8(w, Contents);
                HyobBinary.WriteUtf8(w, TextStyleName);
                w.Write(LocX); w.Write(LocY); w.Write(LocZ);
                w.Write(TextHeight);
                w.Write(Width);
                w.Write(RotationRad);
                w.Write(NormalX); w.Write(NormalY); w.Write(NormalZ);
                w.Write(DirX); w.Write(DirY); w.Write(DirZ);
                w.Write(Attachment);
                w.Write(DrawDirection);
                w.Write(LineSpacingStyle);
                w.Write(LineSpacingFactor);
                w.Write(BackgroundFill);
                w.Write(BackgroundColorArgb);
                w.Write(BackgroundScaleFactor);
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobMText Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.MText)
                throw new InvalidDataException($"期望 MText（0x0006），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"MText schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var layer = HyobBinary.ReadUtf8(r);
                var hh = HyobBinary.ReadUtf8(r);
                var contents = HyobBinary.ReadUtf8(r);
                var style = HyobBinary.ReadUtf8(r);
                double lx = r.ReadDouble(), ly = r.ReadDouble(), lz = r.ReadDouble();
                double th = r.ReadDouble();
                double width = r.ReadDouble();
                double rot = r.ReadDouble();
                double nx = r.ReadDouble(), ny = r.ReadDouble(), nz = r.ReadDouble();
                double dx = r.ReadDouble(), dy = r.ReadDouble(), dz = r.ReadDouble();
                byte att = r.ReadByte();
                byte drawDir = r.ReadByte();
                byte lss = r.ReadByte();
                double lsf = r.ReadDouble();
                byte bf = r.ReadByte();
                uint bc = r.ReadUInt32();
                double bsf = r.ReadDouble();
                return new HyobMText(layer, hh, contents, style, lx, ly, lz, th, width, rot,
                    nx, ny, nz, dx, dy, dz, att, drawDir, lss, lsf, bf, bc, bsf);
            }
        }
    }
}
