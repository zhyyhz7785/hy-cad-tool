using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>
    /// hyob TypedObject - 直线（type_id = 0x0001）。设计：02 §5.3。
    /// payload schema v1：
    /// <code>
    ///   [ utf8   layer ]
    ///   [ utf8   handle_hex ]
    ///   [ double sx, sy, sz, ex, ey, ez ]
    /// </code>
    /// </summary>
    public sealed class HyobLine : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public string Layer { get; }
        public string HandleHex { get; }
        public double StartX { get; }
        public double StartY { get; }
        public double StartZ { get; }
        public double EndX { get; }
        public double EndY { get; }
        public double EndZ { get; }

        public HyobObjectKind TypeId => HyobObjectKind.Line;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobLine(string layer, string handleHex,
                        double sx, double sy, double sz,
                        double ex, double ey, double ez)
        {
            Layer = layer ?? string.Empty;
            HandleHex = handleHex ?? string.Empty;
            StartX = sx; StartY = sy; StartZ = sz;
            EndX = ex; EndY = ey; EndZ = ez;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Layer);
                HyobBinary.WriteUtf8(w, HandleHex);
                w.Write(StartX); w.Write(StartY); w.Write(StartZ);
                w.Write(EndX);   w.Write(EndY);   w.Write(EndZ);
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobLine Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.Line)
                throw new InvalidDataException($"期望 Line（0x0001），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"Line schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var layer = HyobBinary.ReadUtf8(r);
                var hh = HyobBinary.ReadUtf8(r);
                double sx = r.ReadDouble(), sy = r.ReadDouble(), sz = r.ReadDouble();
                double ex = r.ReadDouble(), ey = r.ReadDouble(), ez = r.ReadDouble();
                return new HyobLine(layer, hh, sx, sy, sz, ex, ey, ez);
            }
        }
    }
}
