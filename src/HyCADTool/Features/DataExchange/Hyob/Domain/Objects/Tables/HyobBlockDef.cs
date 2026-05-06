using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Tables
{
    /// <summary>
    /// hyob 表定义 - 块定义 BlockDef（type_id = 0x0104）。
    ///
    /// AutoCAD <see cref="Autodesk.AutoCAD.DatabaseServices.BlockTableRecord"/> 是 ModelSpace /
    /// PaperSpace / 用户 block / 匿名 block 的统一数据结构。本期 v1 只存元信息（name/origin/units/
    /// 标志位 + 内嵌 entity 数量），不展开嵌套实体（嵌套的 ModelSpace 实体已经在 entities/by-handle
    /// 单独镜像；用户 block 的内嵌 entity 留 M9 升级时按 hash 引用）。
    ///
    /// payload schema v1：
    /// <code>
    ///   [ utf8   name ]
    ///   [ utf8   description ]
    ///   [ byte   is_layout ]
    ///   [ byte   is_from_external_reference ]
    ///   [ byte   is_anonymous ]
    ///   [ double origin_x, origin_y, origin_z ]
    ///   [ byte   units ]                      UnitsValue 枚举
    ///   [ uint32 entity_count ]
    ///   [ utf8   path_name ]                  Xref path（仅 Xref 时非空）
    /// </code>
    /// </summary>
    public sealed class HyobBlockDef : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public string Name { get; }
        public string Description { get; }
        public byte IsLayout { get; }
        public byte IsFromExternalReference { get; }
        public byte IsAnonymous { get; }
        public double OriginX { get; }
        public double OriginY { get; }
        public double OriginZ { get; }
        public byte Units { get; }
        public uint EntityCount { get; }
        public string PathName { get; }

        public HyobObjectKind TypeId => HyobObjectKind.BlockDef;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobBlockDef(
            string name, string description,
            byte isLayout, byte isFromExternalReference, byte isAnonymous,
            double ox, double oy, double oz,
            byte units, uint entityCount, string pathName)
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            IsLayout = isLayout;
            IsFromExternalReference = isFromExternalReference;
            IsAnonymous = isAnonymous;
            OriginX = ox; OriginY = oy; OriginZ = oz;
            Units = units;
            EntityCount = entityCount;
            PathName = pathName ?? string.Empty;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Name);
                HyobBinary.WriteUtf8(w, Description);
                w.Write(IsLayout);
                w.Write(IsFromExternalReference);
                w.Write(IsAnonymous);
                w.Write(OriginX); w.Write(OriginY); w.Write(OriginZ);
                w.Write(Units);
                w.Write(EntityCount);
                HyobBinary.WriteUtf8(w, PathName);
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobBlockDef Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.BlockDef)
                throw new InvalidDataException($"期望 BlockDef（0x0104），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"BlockDef schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var name = HyobBinary.ReadUtf8(r);
                var desc = HyobBinary.ReadUtf8(r);
                byte layout = r.ReadByte();
                byte xref = r.ReadByte();
                byte anon = r.ReadByte();
                double ox = r.ReadDouble(), oy = r.ReadDouble(), oz = r.ReadDouble();
                byte units = r.ReadByte();
                uint cnt = r.ReadUInt32();
                var path = HyobBinary.ReadUtf8(r);
                return new HyobBlockDef(name, desc, layout, xref, anon, ox, oy, oz, units, cnt, path);
            }
        }
    }
}
