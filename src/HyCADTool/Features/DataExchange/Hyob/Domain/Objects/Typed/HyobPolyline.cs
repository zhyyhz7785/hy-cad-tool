using System;
using System.Collections.Generic;
using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>Polyline 顶点：(x, y, bulge)。lightweight polyline 是 2D + bulge。</summary>
    public readonly struct HyobPolylineVertex
    {
        public double X { get; }
        public double Y { get; }
        public double Bulge { get; }
        public HyobPolylineVertex(double x, double y, double bulge) { X = x; Y = y; Bulge = bulge; }
    }

    /// <summary>
    /// hyob TypedObject - 轻量多段线（type_id = 0x0002）。设计：02 §5.3。
    /// 仅支持 AutoCAD <c>Polyline</c>（lightweight 2D + bulge）；Polyline2d / Polyline3d 走 Opaque 兜底。
    ///
    /// payload schema v1：
    /// <code>
    ///   [ utf8   layer ]
    ///   [ utf8   handle_hex ]
    ///   [ byte   flags ]                    bit0 = closed
    ///   [ double elevation ]
    ///   [ double normal_x, normal_y, normal_z ]
    ///   [ double const_width ]              全局线宽（0 表示按段不同，本期简化只存全局值）
    ///   [ uint32 vertex_count ]
    ///   loop vertex_count:
    ///     [ double x, y, bulge ]
    /// </code>
    /// </summary>
    public sealed class HyobPolyline : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;
        private const byte FlagClosed = 0x01;

        public string Layer { get; }
        public string HandleHex { get; }
        public bool Closed { get; }
        public double Elevation { get; }
        public double NormalX { get; }
        public double NormalY { get; }
        public double NormalZ { get; }
        public double ConstantWidth { get; }
        public IReadOnlyList<HyobPolylineVertex> Vertices { get; }

        public HyobObjectKind TypeId => HyobObjectKind.Polyline;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobPolyline(string layer, string handleHex, bool closed,
                            double elevation,
                            double nx, double ny, double nz,
                            double constantWidth,
                            IList<HyobPolylineVertex> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            Layer = layer ?? string.Empty;
            HandleHex = handleHex ?? string.Empty;
            Closed = closed;
            Elevation = elevation;
            NormalX = nx; NormalY = ny; NormalZ = nz;
            ConstantWidth = constantWidth;
            Vertices = new List<HyobPolylineVertex>(vertices);
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Layer);
                HyobBinary.WriteUtf8(w, HandleHex);
                byte flags = 0;
                if (Closed) flags |= FlagClosed;
                w.Write(flags);
                w.Write(Elevation);
                w.Write(NormalX); w.Write(NormalY); w.Write(NormalZ);
                w.Write(ConstantWidth);
                w.Write((uint)Vertices.Count);
                foreach (var v in Vertices)
                {
                    w.Write(v.X); w.Write(v.Y); w.Write(v.Bulge);
                }
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobPolyline Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.Polyline)
                throw new InvalidDataException($"期望 Polyline（0x0002），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"Polyline schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var layer = HyobBinary.ReadUtf8(r);
                var hh = HyobBinary.ReadUtf8(r);
                byte flags = r.ReadByte();
                bool closed = (flags & FlagClosed) != 0;
                double elev = r.ReadDouble();
                double nx = r.ReadDouble(), ny = r.ReadDouble(), nz = r.ReadDouble();
                double cw = r.ReadDouble();
                uint count = r.ReadUInt32();
                var verts = new List<HyobPolylineVertex>((int)count);
                for (uint i = 0; i < count; i++)
                {
                    double x = r.ReadDouble(), y = r.ReadDouble(), b = r.ReadDouble();
                    verts.Add(new HyobPolylineVertex(x, y, b));
                }
                return new HyobPolyline(layer, hh, closed, elev, nx, ny, nz, cw, verts);
            }
        }
    }
}
