using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>
    /// hyob TypedObject - 单行文字 DBText（type_id = 0x0005）。设计：02 §5.3 / 04 §5。
    /// payload schema v1：
    /// <code>
    ///   [ utf8   layer ]
    ///   [ utf8   handle_hex ]
    ///   [ utf8   text_string ]
    ///   [ utf8   text_style_name ]
    ///   [ double pos_x, pos_y, pos_z ]
    ///   [ double height ]
    ///   [ double rotation_rad ]
    ///   [ double width_factor ]
    ///   [ double oblique_rad ]
    ///   [ double thickness ]
    ///   [ double normal_x, normal_y, normal_z ]
    ///   [ byte   horizontal_mode ]      AutoCAD TextHorizontalMode
    ///   [ byte   vertical_mode ]        AutoCAD TextVerticalMode
    ///   [ double align_x, align_y, align_z ]
    ///   [ byte   flags ]                bit0=mirroredX, bit1=mirroredY, bit2=verticallyDrawn
    /// </code>
    /// </summary>
    public sealed class HyobDBText : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public const byte FlagMirroredX = 0x01;
        public const byte FlagMirroredY = 0x02;
        public const byte FlagVertical  = 0x04;

        public string Layer { get; }
        public string HandleHex { get; }
        public string TextString { get; }
        public string TextStyleName { get; }
        public double PosX { get; }
        public double PosY { get; }
        public double PosZ { get; }
        public double Height { get; }
        public double RotationRad { get; }
        public double WidthFactor { get; }
        public double ObliqueRad { get; }
        public double Thickness { get; }
        public double NormalX { get; }
        public double NormalY { get; }
        public double NormalZ { get; }
        public byte HorizontalMode { get; }
        public byte VerticalMode { get; }
        public double AlignX { get; }
        public double AlignY { get; }
        public double AlignZ { get; }
        public byte Flags { get; }

        public HyobObjectKind TypeId => HyobObjectKind.DBText;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobDBText(
            string layer, string handleHex,
            string textString, string textStyleName,
            double px, double py, double pz,
            double height, double rotationRad,
            double widthFactor, double obliqueRad, double thickness,
            double nx, double ny, double nz,
            byte horizontalMode, byte verticalMode,
            double ax, double ay, double az,
            byte flags)
        {
            Layer = layer ?? string.Empty;
            HandleHex = handleHex ?? string.Empty;
            TextString = textString ?? string.Empty;
            TextStyleName = textStyleName ?? string.Empty;
            PosX = px; PosY = py; PosZ = pz;
            Height = height;
            RotationRad = rotationRad;
            WidthFactor = widthFactor;
            ObliqueRad = obliqueRad;
            Thickness = thickness;
            NormalX = nx; NormalY = ny; NormalZ = nz;
            HorizontalMode = horizontalMode;
            VerticalMode = verticalMode;
            AlignX = ax; AlignY = ay; AlignZ = az;
            Flags = flags;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Layer);
                HyobBinary.WriteUtf8(w, HandleHex);
                HyobBinary.WriteUtf8(w, TextString);
                HyobBinary.WriteUtf8(w, TextStyleName);
                w.Write(PosX); w.Write(PosY); w.Write(PosZ);
                w.Write(Height);
                w.Write(RotationRad);
                w.Write(WidthFactor);
                w.Write(ObliqueRad);
                w.Write(Thickness);
                w.Write(NormalX); w.Write(NormalY); w.Write(NormalZ);
                w.Write(HorizontalMode);
                w.Write(VerticalMode);
                w.Write(AlignX); w.Write(AlignY); w.Write(AlignZ);
                w.Write(Flags);
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobDBText Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.DBText)
                throw new InvalidDataException($"期望 DBText（0x0005），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"DBText schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var layer = HyobBinary.ReadUtf8(r);
                var hh = HyobBinary.ReadUtf8(r);
                var text = HyobBinary.ReadUtf8(r);
                var style = HyobBinary.ReadUtf8(r);
                double px = r.ReadDouble(), py = r.ReadDouble(), pz = r.ReadDouble();
                double height = r.ReadDouble();
                double rot = r.ReadDouble();
                double wf = r.ReadDouble();
                double obl = r.ReadDouble();
                double thk = r.ReadDouble();
                double nx = r.ReadDouble(), ny = r.ReadDouble(), nz = r.ReadDouble();
                byte hm = r.ReadByte();
                byte vm = r.ReadByte();
                double ax = r.ReadDouble(), ay = r.ReadDouble(), az = r.ReadDouble();
                byte flags = r.ReadByte();
                return new HyobDBText(layer, hh, text, style, px, py, pz, height, rot,
                    wf, obl, thk, nx, ny, nz, hm, vm, ax, ay, az, flags);
            }
        }
    }
}
