using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Models
{
    /// <summary>Tree 条目类型（1=Object 叶节点 / 2=Tree 子树）。</summary>
    public enum HyobTreeEntryKind : byte
    {
        Object = 1,
        Tree   = 2,
    }

    /// <summary>Tree 中的一条记录：(name, kind, hash)。不可变。</summary>
    public sealed class HyobTreeEntry
    {
        public string Name { get; }
        public HyobTreeEntryKind Kind { get; }
        public Hash Hash { get; }

        public HyobTreeEntry(string name, HyobTreeEntryKind kind, Hash hash)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Name 不能为空", nameof(name));
            Name = name;
            Kind = kind;
            Hash = hash;
        }

        public override string ToString() => $"{Kind,-6} {Hash.ToHex().Substring(0, 12)}… {Name}";
    }

    /// <summary>
    /// hyob 命名空间树（type_id = 0x0F01）。设计：02 §3.3 / 04 §5。
    /// 二进制 schema v1：
    /// <code>
    ///   [ uint32 entry_count ]
    ///   loop entry_count:
    ///     [ byte   kind  (1=Object / 2=Tree) ]
    ///     [ utf8   name ]
    ///     [ Hash   hash ]
    /// </code>
    /// 关键不变量：entries **按 Name ordinal 升序写入**，保证 content-addressable 一致性
    /// （同样的 name→hash 集合永远产出同样的 SHA-256）。
    /// </summary>
    public sealed class HyobTree : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;
        private readonly List<HyobTreeEntry> _entries;

        public IReadOnlyList<HyobTreeEntry> Entries => _entries;
        public int Count => _entries.Count;

        public HyobObjectKind TypeId => HyobObjectKind.Tree;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobTree(IEnumerable<HyobTreeEntry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            _entries = entries.OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
            for (int i = 1; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Name, _entries[i - 1].Name, StringComparison.Ordinal))
                    throw new ArgumentException($"Tree 同名条目重复：{_entries[i].Name}");
            }
        }

        public bool TryFind(string name, out HyobTreeEntry entry)
        {
            entry = _entries.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.Ordinal));
            return entry != null;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write((uint)_entries.Count);
                foreach (var e in _entries)
                {
                    w.Write((byte)e.Kind);
                    HyobBinary.WriteUtf8(w, e.Name);
                    HyobBinary.WriteHash(w, e.Hash);
                }
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobTree Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.Tree)
                throw new InvalidDataException($"期望 Tree（0x0F01），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"Tree schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                uint count = r.ReadUInt32();
                var list = new List<HyobTreeEntry>((int)count);
                for (uint i = 0; i < count; i++)
                {
                    var kind = (HyobTreeEntryKind)r.ReadByte();
                    var name = HyobBinary.ReadUtf8(r);
                    var hash = HyobBinary.ReadHash(r);
                    list.Add(new HyobTreeEntry(name, kind, hash));
                }
                return new HyobTree(list);
            }
        }
    }
}
