using System;
using System.Collections.Generic;
using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>BlockReference 内联属性键值（M3 简化：tag → text_string）。</summary>
    public readonly struct HyobBlockAttribute
    {
        public string Tag { get; }
        public string TextString { get; }
        public HyobBlockAttribute(string tag, string textString)
        {
            Tag = tag ?? string.Empty;
            TextString = textString ?? string.Empty;
        }
    }

    /// <summary>
    /// hyob TypedObject - 块引用 BlockReference（type_id = 0x0007）。设计：02 §5.3 / 04 §5。
    ///
    /// 简化策略（M3）：内联存属性（tag/text）键值对，AttributeReference 暂不独立成 object。
    /// M9 引入指针式拓扑后，attribute 升级为单独 hash + 在此处只引用 hash。
    ///
    /// payload schema v1：
    /// <code>
    ///   [ utf8   layer ]
    ///   [ utf8   handle_hex ]
    ///   [ utf8   block_name ]
    ///   [ double pos_x, pos_y, pos_z ]
    ///   [ double scale_x, scale_y, scale_z ]
    ///   [ double rotation_rad ]
    ///   [ double normal_x, normal_y, normal_z ]
    ///   [ uint32 attr_count ]
    ///   loop attr_count:
    ///     [ utf8 tag ]
    ///     [ utf8 text_string ]
    /// </code>
    /// </summary>
    public sealed class HyobBlockReference : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public string Layer { get; }
        public string HandleHex { get; }
        public string BlockName { get; }
        public double PosX { get; }
        public double PosY { get; }
        public double PosZ { get; }
        public double ScaleX { get; }
        public double ScaleY { get; }
        public double ScaleZ { get; }
        public double RotationRad { get; }
        public double NormalX { get; }
        public double NormalY { get; }
        public double NormalZ { get; }
        public IReadOnlyList<HyobBlockAttribute> Attributes { get; }

        public HyobObjectKind TypeId => HyobObjectKind.BlockReference;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobBlockReference(
            string layer, string handleHex, string blockName,
            double px, double py, double pz,
            double sx, double sy, double sz,
            double rotationRad,
            double nx, double ny, double nz,
            IList<HyobBlockAttribute> attributes)
        {
            if (attributes == null) throw new ArgumentNullException(nameof(attributes));
            Layer = layer ?? string.Empty;
            HandleHex = handleHex ?? string.Empty;
            BlockName = blockName ?? string.Empty;
            PosX = px; PosY = py; PosZ = pz;
            ScaleX = sx; ScaleY = sy; ScaleZ = sz;
            RotationRad = rotationRad;
            NormalX = nx; NormalY = ny; NormalZ = nz;
            Attributes = new List<HyobBlockAttribute>(attributes);
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Layer);
                HyobBinary.WriteUtf8(w, HandleHex);
                HyobBinary.WriteUtf8(w, BlockName);
                w.Write(PosX); w.Write(PosY); w.Write(PosZ);
                w.Write(ScaleX); w.Write(ScaleY); w.Write(ScaleZ);
                w.Write(RotationRad);
                w.Write(NormalX); w.Write(NormalY); w.Write(NormalZ);
                w.Write((uint)Attributes.Count);
                foreach (var a in Attributes)
                {
                    HyobBinary.WriteUtf8(w, a.Tag);
                    HyobBinary.WriteUtf8(w, a.TextString);
                }
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobBlockReference Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.BlockReference)
                throw new InvalidDataException($"期望 BlockReference（0x0007），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"BlockReference schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var layer = HyobBinary.ReadUtf8(r);
                var hh = HyobBinary.ReadUtf8(r);
                var name = HyobBinary.ReadUtf8(r);
                double px = r.ReadDouble(), py = r.ReadDouble(), pz = r.ReadDouble();
                double sx = r.ReadDouble(), sy = r.ReadDouble(), sz = r.ReadDouble();
                double rot = r.ReadDouble();
                double nx = r.ReadDouble(), ny = r.ReadDouble(), nz = r.ReadDouble();
                uint cnt = r.ReadUInt32();
                var attrs = new List<HyobBlockAttribute>((int)cnt);
                for (uint i = 0; i < cnt; i++)
                {
                    var tag = HyobBinary.ReadUtf8(r);
                    var txt = HyobBinary.ReadUtf8(r);
                    attrs.Add(new HyobBlockAttribute(tag, txt));
                }
                return new HyobBlockReference(layer, hh, name, px, py, pz, sx, sy, sz, rot, nx, ny, nz, attrs);
            }
        }
    }
}
