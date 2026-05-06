using System;
using System.Collections.Generic;
using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>2D 边界点（仅 X/Y）。</summary>
    public readonly struct HyobPoint2d
    {
        public double X { get; }
        public double Y { get; }
        public HyobPoint2d(double x, double y) { X = x; Y = y; }
    }

    /// <summary>
    /// hyob TypedObject - 遮罩 Wipeout（type_id = 0x000C）。设计：02 §5.3 / 04 §5。
    ///
    /// Wipeout = Image 派生的多边形遮罩。本期存：
    ///   - 锚点 / 宽高 / 旋转 / 颜色索引
    ///   - 边界 polygon（2D 顶点列表，OCS 平面）
    ///
    /// 与 Hatch 不同，Wipeout 边界点数有限（一般 4-12）且总是 polyline，可以全存。
    ///
    /// payload schema v1：
    /// <code>
    ///   [ utf8   layer ]
    ///   [ utf8   handle_hex ]
    ///   [ double pos_x, pos_y, pos_z ]
    ///   [ double width ]
    ///   [ double height ]
    ///   [ double rotation_rad ]
    ///   [ int32  color_index ]
    ///   [ uint32 boundary_count ]
    ///   loop boundary_count: [ double x, y ]
    /// </code>
    /// </summary>
    public sealed class HyobWipeout : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public string Layer { get; }
        public string HandleHex { get; }
        public double PosX { get; }
        public double PosY { get; }
        public double PosZ { get; }
        public double Width { get; }
        public double Height { get; }
        public double RotationRad { get; }
        public int ColorIndex { get; }
        public IReadOnlyList<HyobPoint2d> Boundary { get; }

        public HyobObjectKind TypeId => HyobObjectKind.Wipeout;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobWipeout(
            string layer, string handleHex,
            double px, double py, double pz,
            double width, double height, double rotationRad,
            int colorIndex,
            IList<HyobPoint2d> boundary)
        {
            if (boundary == null) throw new ArgumentNullException(nameof(boundary));
            Layer = layer ?? string.Empty;
            HandleHex = handleHex ?? string.Empty;
            PosX = px; PosY = py; PosZ = pz;
            Width = width;
            Height = height;
            RotationRad = rotationRad;
            ColorIndex = colorIndex;
            Boundary = new List<HyobPoint2d>(boundary);
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Layer);
                HyobBinary.WriteUtf8(w, HandleHex);
                w.Write(PosX); w.Write(PosY); w.Write(PosZ);
                w.Write(Width);
                w.Write(Height);
                w.Write(RotationRad);
                w.Write(ColorIndex);
                w.Write((uint)Boundary.Count);
                foreach (var p in Boundary) { w.Write(p.X); w.Write(p.Y); }
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobWipeout Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.Wipeout)
                throw new InvalidDataException($"期望 Wipeout（0x000C），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"Wipeout schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var layer = HyobBinary.ReadUtf8(r);
                var hh = HyobBinary.ReadUtf8(r);
                double px = r.ReadDouble(), py = r.ReadDouble(), pz = r.ReadDouble();
                double width = r.ReadDouble();
                double height = r.ReadDouble();
                double rot = r.ReadDouble();
                int color = r.ReadInt32();
                uint cnt = r.ReadUInt32();
                var boundary = new List<HyobPoint2d>((int)cnt);
                for (uint i = 0; i < cnt; i++)
                {
                    double x = r.ReadDouble(), y = r.ReadDouble();
                    boundary.Add(new HyobPoint2d(x, y));
                }
                return new HyobWipeout(layer, hh, px, py, pz, width, height, rot, color, boundary);
            }
        }
    }
}
