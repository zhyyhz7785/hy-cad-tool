using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>
    /// hyob TypedObject - 圆弧（type_id = 0x0003）。设计：02 §5.3。
    /// payload schema v1：
    /// <code>
    ///   [ utf8   layer ]
    ///   [ utf8   handle_hex ]
    ///   [ double cx, cy, cz ]
    ///   [ double radius ]
    ///   [ double start_angle_rad ]
    ///   [ double end_angle_rad ]
    ///   [ double nx, ny, nz ]
    /// </code>
    /// </summary>
    public sealed class HyobArc : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public string Layer { get; }
        public string HandleHex { get; }
        public double CenterX { get; }
        public double CenterY { get; }
        public double CenterZ { get; }
        public double Radius { get; }
        public double StartAngleRad { get; }
        public double EndAngleRad { get; }
        public double NormalX { get; }
        public double NormalY { get; }
        public double NormalZ { get; }

        public HyobObjectKind TypeId => HyobObjectKind.Arc;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobArc(string layer, string handleHex,
                       double cx, double cy, double cz,
                       double radius,
                       double startAngleRad, double endAngleRad,
                       double nx, double ny, double nz)
        {
            Layer = layer ?? string.Empty;
            HandleHex = handleHex ?? string.Empty;
            CenterX = cx; CenterY = cy; CenterZ = cz;
            Radius = radius;
            StartAngleRad = startAngleRad;
            EndAngleRad = endAngleRad;
            NormalX = nx; NormalY = ny; NormalZ = nz;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Layer);
                HyobBinary.WriteUtf8(w, HandleHex);
                w.Write(CenterX); w.Write(CenterY); w.Write(CenterZ);
                w.Write(Radius);
                w.Write(StartAngleRad);
                w.Write(EndAngleRad);
                w.Write(NormalX); w.Write(NormalY); w.Write(NormalZ);
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobArc Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.Arc)
                throw new InvalidDataException($"期望 Arc（0x0003），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"Arc schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var layer = HyobBinary.ReadUtf8(r);
                var hh = HyobBinary.ReadUtf8(r);
                double cx = r.ReadDouble(), cy = r.ReadDouble(), cz = r.ReadDouble();
                double radius = r.ReadDouble();
                double sa = r.ReadDouble(), ea = r.ReadDouble();
                double nx = r.ReadDouble(), ny = r.ReadDouble(), nz = r.ReadDouble();
                return new HyobArc(layer, hh, cx, cy, cz, radius, sa, ea, nx, ny, nz);
            }
        }
    }
}
