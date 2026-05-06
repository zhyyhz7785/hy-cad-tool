using System;
using System.Collections.Generic;
using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>hyob 内部 3D 点（与 AutoCAD Point3d 解耦，平台无关）。</summary>
    public readonly struct HyobPoint3d
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public HyobPoint3d(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    /// <summary>
    /// hyob TypedObject - 标注 Dimension（type_id = 0x0009）。设计：02 §5.3 / 04 §5。
    ///
    /// 9 种 AutoCAD Dimension 子类共享一个 <see cref="HyobObjectKind.Dimension"/> type_id，
    /// 通过 <see cref="DimType"/> 字节区分。<see cref="DefiningPoints"/> 与 <see cref="Extras"/>
    /// 是 subtype 决定的变长数组，协议如下：
    ///
    /// <list type="table">
    /// <item><term>Aligned (1)</term><description>3 points (XL1, XL2, DimLine), 1 extra (Oblique)</description></item>
    /// <item><term>Rotated (2)</term><description>3 points (XL1, XL2, DimLine), 2 extras (Rotation, Oblique)</description></item>
    /// <item><term>Diametric (3)</term><description>2 points (Chord, FarChord), 1 extra (LeaderLength)</description></item>
    /// <item><term>Radial (4)</term><description>2 points (Center, Chord), 1 extra (LeaderLength)</description></item>
    /// <item><term>Arc (5)</term><description>3 points (XL1, XL2, ArcPoint), 2 extras (ArcSymbolType, IsPartial 0/1)</description></item>
    /// <item><term>Ordinate (6)</term><description>2 points (Defining, LeaderEnd), 1 extra (UsingXAxis 0/1)</description></item>
    /// <item><term>Point3Angular (7)</term><description>4 points (XL1, XL2, Center, ArcPoint), 0 extras</description></item>
    /// <item><term>LineAngular (8)</term><description>5 points (XL1S, XL1E, XL2S, XL2E, ArcPoint), 0 extras</description></item>
    /// <item><term>RadialLarge (9)</term><description>4 points (Center, Chord, OverrideCenter, JogPoint), 1 extra (JogAngle)</description></item>
    /// <item><term>Other (0)</term><description>0 points, 0 extras（未识别 subtype 兜底）</description></item>
    /// </list>
    ///
    /// payload schema v1：
    /// <code>
    ///   [ utf8   layer ]
    ///   [ utf8   handle_hex ]
    ///   [ byte   sub_type ]
    ///   [ utf8   dim_block_name ]
    ///   [ utf8   dim_text ]                DimensionText override（"" 表示用 measurement）
    ///   [ utf8   dim_style_name ]
    ///   [ double text_x, text_y, text_z ]
    ///   [ double measurement ]
    ///   [ double normal_x, normal_y, normal_z ]
    ///   [ uint32 point_count ]
    ///   loop point_count: [ double x, y, z ]
    ///   [ uint32 extra_count ]
    ///   loop extra_count: [ double v ]
    /// </code>
    /// </summary>
    public sealed class HyobDimension : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public enum Subtype : byte
        {
            Other          = 0,
            Aligned        = 1,
            Rotated        = 2,
            Diametric      = 3,
            Radial         = 4,
            Arc            = 5,
            Ordinate       = 6,
            Point3Angular  = 7,
            LineAngular    = 8,
            RadialLarge    = 9,
        }

        public Subtype DimType { get; }
        public string Layer { get; }
        public string HandleHex { get; }
        public string DimBlockName { get; }
        public string DimensionText { get; }
        public string DimStyleName { get; }
        public double TextX { get; }
        public double TextY { get; }
        public double TextZ { get; }
        public double Measurement { get; }
        public double NormalX { get; }
        public double NormalY { get; }
        public double NormalZ { get; }
        public IReadOnlyList<HyobPoint3d> DefiningPoints { get; }
        public IReadOnlyList<double> Extras { get; }

        public HyobObjectKind TypeId => HyobObjectKind.Dimension;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobDimension(
            Subtype subType,
            string layer, string handleHex,
            string dimBlockName, string dimensionText, string dimStyleName,
            double textX, double textY, double textZ,
            double measurement,
            double nx, double ny, double nz,
            IList<HyobPoint3d> definingPoints,
            IList<double> extras)
        {
            if (definingPoints == null) throw new ArgumentNullException(nameof(definingPoints));
            if (extras == null) throw new ArgumentNullException(nameof(extras));

            DimType = subType;
            Layer = layer ?? string.Empty;
            HandleHex = handleHex ?? string.Empty;
            DimBlockName = dimBlockName ?? string.Empty;
            DimensionText = dimensionText ?? string.Empty;
            DimStyleName = dimStyleName ?? string.Empty;
            TextX = textX; TextY = textY; TextZ = textZ;
            Measurement = measurement;
            NormalX = nx; NormalY = ny; NormalZ = nz;
            DefiningPoints = new List<HyobPoint3d>(definingPoints);
            Extras = new List<double>(extras);
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Layer);
                HyobBinary.WriteUtf8(w, HandleHex);
                w.Write((byte)DimType);
                HyobBinary.WriteUtf8(w, DimBlockName);
                HyobBinary.WriteUtf8(w, DimensionText);
                HyobBinary.WriteUtf8(w, DimStyleName);
                w.Write(TextX); w.Write(TextY); w.Write(TextZ);
                w.Write(Measurement);
                w.Write(NormalX); w.Write(NormalY); w.Write(NormalZ);
                w.Write((uint)DefiningPoints.Count);
                foreach (var p in DefiningPoints) { w.Write(p.X); w.Write(p.Y); w.Write(p.Z); }
                w.Write((uint)Extras.Count);
                foreach (var v in Extras) w.Write(v);
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobDimension Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.Dimension)
                throw new InvalidDataException($"期望 Dimension（0x0009），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"Dimension schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var layer = HyobBinary.ReadUtf8(r);
                var hh = HyobBinary.ReadUtf8(r);
                var sub = (Subtype)r.ReadByte();
                var blockName = HyobBinary.ReadUtf8(r);
                var dimText = HyobBinary.ReadUtf8(r);
                var dimStyle = HyobBinary.ReadUtf8(r);
                double tx = r.ReadDouble(), ty = r.ReadDouble(), tz = r.ReadDouble();
                double meas = r.ReadDouble();
                double nx = r.ReadDouble(), ny = r.ReadDouble(), nz = r.ReadDouble();
                uint pc = r.ReadUInt32();
                var pts = new List<HyobPoint3d>((int)pc);
                for (uint i = 0; i < pc; i++)
                {
                    double x = r.ReadDouble(), y = r.ReadDouble(), z = r.ReadDouble();
                    pts.Add(new HyobPoint3d(x, y, z));
                }
                uint ec = r.ReadUInt32();
                var extras = new List<double>((int)ec);
                for (uint i = 0; i < ec; i++) extras.Add(r.ReadDouble());

                return new HyobDimension(sub, layer, hh, blockName, dimText, dimStyle,
                    tx, ty, tz, meas, nx, ny, nz, pts, extras);
            }
        }
    }
}
