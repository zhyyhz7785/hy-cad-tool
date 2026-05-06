using System;
using System.Collections.Generic;
using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>ExtDict 内一个 entry：key + 类型分类 + 内容摘要。</summary>
    public readonly struct HyobExtDictEntry
    {
        /// <summary>Xrecord 子项。Summary = ResultBuffer.ToString。</summary>
        public const byte KindXrecord = 1;
        /// <summary>嵌套 Dictionary。Summary = "<n>" 表示嵌套字典 entry 数量。</summary>
        public const byte KindDictionary = 2;
        /// <summary>其他类型。Summary = RxClass 名。</summary>
        public const byte KindOther = 3;

        public string KeyName { get; }
        public byte Kind { get; }
        public string Summary { get; }

        public HyobExtDictEntry(string keyName, byte kind, string summary)
        {
            KeyName = keyName ?? string.Empty;
            Kind = kind;
            Summary = summary ?? string.Empty;
        }
    }

    /// <summary>
    /// hyob 附加对象 - ExtensionDictionary（type_id = 0x0121）。设计：02 §5.3 / 04 §5。
    ///
    /// AutoCAD entity 持有一个 <c>ExtensionDictionary</c>（DBDictionary ObjectId），
    /// 内容是任意命名 DBObject。HyCADTool 自身把 BoltData / AxisData / 钢筋表等业务
    /// 数据存在这里。
    ///
    /// M5 简化策略：只存"扁平 entry 摘要"——每个 entry 的 key + 类型 + summary 字符串。
    /// 嵌套 Dictionary 不递归展开，Summary 只标注 entry 数。这意味着：
    ///   - hash 能反映"用户在 ExtDict 顶层增删改 key"
    ///   - 嵌套 Dictionary 内部变化暂不感知（M5+ 升级 schema_ver = 2 时递归 hash 引用）
    ///
    /// payload schema v1：
    /// <code>
    ///   [ uint32 entry_count ]
    ///   loop entry_count:
    ///     [ utf8 key_name ]
    ///     [ byte kind ]                      1=Xrecord, 2=Dictionary, 3=Other
    ///     [ utf8 summary ]
    /// </code>
    /// </summary>
    public sealed class HyobExtensionDictionary : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public IReadOnlyList<HyobExtDictEntry> Entries { get; }

        public HyobObjectKind TypeId => HyobObjectKind.ExtensionDictionary;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobExtensionDictionary(IList<HyobExtDictEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            Entries = new List<HyobExtDictEntry>(entries);
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((uint)Entries.Count);
                foreach (var e in Entries)
                {
                    HyobBinary.WriteUtf8(w, e.KeyName);
                    w.Write(e.Kind);
                    HyobBinary.WriteUtf8(w, e.Summary);
                }
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobExtensionDictionary Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.ExtensionDictionary)
                throw new InvalidDataException($"期望 ExtDict（0x0121），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"ExtDict schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                uint count = r.ReadUInt32();
                var entries = new List<HyobExtDictEntry>((int)count);
                for (uint i = 0; i < count; i++)
                {
                    var key = HyobBinary.ReadUtf8(r);
                    byte kind = r.ReadByte();
                    var summary = HyobBinary.ReadUtf8(r);
                    entries.Add(new HyobExtDictEntry(key, kind, summary));
                }
                return new HyobExtensionDictionary(entries);
            }
        }
    }
}
