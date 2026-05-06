using System;
using System.Collections.Generic;
using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>XData 内单个 (dxf_code, value_str) entry。</summary>
    public readonly struct HyobXDataEntry
    {
        public int DxfCode { get; }
        public string ValueStr { get; }
        public HyobXDataEntry(int dxfCode, string valueStr)
        {
            DxfCode = dxfCode;
            ValueStr = valueStr ?? string.Empty;
        }
    }

    /// <summary>XData 中按 RegApp 分组的 entry 集合。</summary>
    public sealed class HyobXDataAppGroup
    {
        public string AppName { get; }
        public IReadOnlyList<HyobXDataEntry> Entries { get; }
        public HyobXDataAppGroup(string appName, IList<HyobXDataEntry> entries)
        {
            AppName = appName ?? string.Empty;
            Entries = new List<HyobXDataEntry>(entries ?? new List<HyobXDataEntry>());
        }
    }

    /// <summary>
    /// hyob 附加对象 - XData（type_id = 0x0120）。设计：02 §5.3 / 04 §5。
    ///
    /// AutoCAD entity 的 XData 是一个 <c>ResultBuffer</c>（TypedValue 列表），
    /// 按 DxfCode=1001 分组到不同 RegApp 命名空间。
    ///
    /// M5 简化策略：每个 TypedValue 的 value 通过 <see cref="object.ToString"/> 转字符串存。
    /// 这样：
    ///   - hash 能精确反映"用户改了 XData 内容"
    ///   - 但 double/Point3d 精度依赖 ToString 格式（不字节级 round-trip）
    ///   - M11 升级 schema_ver = 2 时按 DxfCode 分支 typed-aware encode
    ///
    /// payload schema v1：
    /// <code>
    ///   [ uint32 app_count ]
    ///   loop app_count:
    ///     [ utf8   app_name ]
    ///     [ uint32 entry_count ]
    ///     loop entry_count:
    ///       [ int32 dxf_code ]
    ///       [ utf8  value_str ]
    /// </code>
    /// </summary>
    public sealed class HyobXDataAttachment : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public IReadOnlyList<HyobXDataAppGroup> Groups { get; }

        public HyobObjectKind TypeId => HyobObjectKind.XDataAttachment;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobXDataAttachment(IList<HyobXDataAppGroup> groups)
        {
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            Groups = new List<HyobXDataAppGroup>(groups);
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((uint)Groups.Count);
                foreach (var g in Groups)
                {
                    HyobBinary.WriteUtf8(w, g.AppName);
                    w.Write((uint)g.Entries.Count);
                    foreach (var e in g.Entries)
                    {
                        w.Write(e.DxfCode);
                        HyobBinary.WriteUtf8(w, e.ValueStr);
                    }
                }
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobXDataAttachment Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.XDataAttachment)
                throw new InvalidDataException($"期望 XData（0x0120），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"XData schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                uint appCount = r.ReadUInt32();
                var groups = new List<HyobXDataAppGroup>((int)appCount);
                for (uint i = 0; i < appCount; i++)
                {
                    var appName = HyobBinary.ReadUtf8(r);
                    uint entryCount = r.ReadUInt32();
                    var entries = new List<HyobXDataEntry>((int)entryCount);
                    for (uint j = 0; j < entryCount; j++)
                    {
                        int code = r.ReadInt32();
                        var val = HyobBinary.ReadUtf8(r);
                        entries.Add(new HyobXDataEntry(code, val));
                    }
                    groups.Add(new HyobXDataAppGroup(appName, entries));
                }
                return new HyobXDataAttachment(groups);
            }
        }
    }
}
