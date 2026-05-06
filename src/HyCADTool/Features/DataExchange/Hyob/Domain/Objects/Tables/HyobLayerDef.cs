using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Tables
{
    /// <summary>
    /// hyob 表定义 - 图层 LayerDef（type_id = 0x0100）。设计：02 §5.3 / 04 §5。
    /// payload schema v1：
    /// <code>
    ///   [ utf8   name ]
    ///   [ int32  color_index ]
    ///   [ utf8   linetype_name ]
    ///   [ double lineweight_mm ]
    ///   [ byte   is_off ]
    ///   [ byte   is_frozen ]
    ///   [ byte   is_locked ]
    ///   [ byte   is_plottable ]
    ///   [ byte   is_used ]
    ///   [ utf8   description ]
    /// </code>
    /// </summary>
    public sealed class HyobLayerDef : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public string Name { get; }
        public int ColorIndex { get; }
        public string LinetypeName { get; }
        public double LineweightMm { get; }
        public byte IsOff { get; }
        public byte IsFrozen { get; }
        public byte IsLocked { get; }
        public byte IsPlottable { get; }
        public byte IsUsed { get; }
        public string Description { get; }

        public HyobObjectKind TypeId => HyobObjectKind.LayerDef;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobLayerDef(
            string name, int colorIndex,
            string linetypeName, double lineweightMm,
            byte isOff, byte isFrozen, byte isLocked,
            byte isPlottable, byte isUsed,
            string description)
        {
            Name = name ?? string.Empty;
            ColorIndex = colorIndex;
            LinetypeName = linetypeName ?? string.Empty;
            LineweightMm = lineweightMm;
            IsOff = isOff;
            IsFrozen = isFrozen;
            IsLocked = isLocked;
            IsPlottable = isPlottable;
            IsUsed = isUsed;
            Description = description ?? string.Empty;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Name);
                w.Write(ColorIndex);
                HyobBinary.WriteUtf8(w, LinetypeName);
                w.Write(LineweightMm);
                w.Write(IsOff);
                w.Write(IsFrozen);
                w.Write(IsLocked);
                w.Write(IsPlottable);
                w.Write(IsUsed);
                HyobBinary.WriteUtf8(w, Description);
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobLayerDef Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.LayerDef)
                throw new InvalidDataException($"期望 LayerDef（0x0100），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"LayerDef schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var name = HyobBinary.ReadUtf8(r);
                int color = r.ReadInt32();
                var lt = HyobBinary.ReadUtf8(r);
                double lw = r.ReadDouble();
                byte off = r.ReadByte();
                byte fz = r.ReadByte();
                byte lk = r.ReadByte();
                byte pl = r.ReadByte();
                byte us = r.ReadByte();
                var desc = HyobBinary.ReadUtf8(r);
                return new HyobLayerDef(name, color, lt, lw, off, fz, lk, pl, us, desc);
            }
        }
    }
}
