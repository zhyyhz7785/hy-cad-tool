using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Models
{
    /// <summary>
    /// hyob 提交对象（type_id = 0x0F02）。设计：02 §3.4 / 04 §5。
    /// commit 自身也是 IHyobObject，按 SHA-256 寻址进 ObjectStore，由 RefStore 把 branch tip 指向它。
    ///
    /// 二进制 schema v1：
    /// <code>
    ///   [ utf8   author ]
    ///   [ utf8   email ]
    ///   [ int64  time_unix_seconds ]
    ///   [ utf8   command ]                 触发该 commit 的 HyCAD 命令名（hyobC / external-edit / ...）
    ///   [ utf8   message ]
    ///   [ uint32 parent_count ]            0 = root commit；1 = 普通；2+ = merge
    ///   loop parent_count: [ Hash parent ]
    ///   [ Hash   tree ]
    ///   [ uint32 meta_count ]
    ///   loop meta_count:
    ///     [ utf8 key, utf8 value ]         meta 按 key ordinal 排序写入，保证 content-addressable 一致性
    /// </code>
    /// </summary>
    public sealed class HyobCommit : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public IReadOnlyList<Hash> Parents { get; }
        public Hash Tree { get; }
        public string Author { get; }
        public string Email { get; }
        public DateTimeOffset Time { get; }
        public string Command { get; }
        public string Message { get; }
        public IReadOnlyDictionary<string, string> Meta { get; }

        public HyobObjectKind TypeId => HyobObjectKind.Commit;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobCommit(
            Hash tree,
            string author,
            string message,
            IEnumerable<Hash> parents = null,
            string email = null,
            DateTimeOffset? time = null,
            string command = null,
            IReadOnlyDictionary<string, string> meta = null)
        {
            Tree = tree;
            Author = author ?? "hyob";
            Message = message ?? string.Empty;
            Email = email ?? string.Empty;
            Time = time ?? DateTimeOffset.UtcNow;
            Command = command ?? string.Empty;
            Meta = meta ?? new Dictionary<string, string>();

            var ps = parents != null ? new List<Hash>(parents) : new List<Hash>();
            if (ps.Count > 16) throw new ArgumentException("parents 数量上限 16", nameof(parents));
            Parents = ps;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Author);
                HyobBinary.WriteUtf8(w, Email);
                w.Write(Time.ToUnixTimeSeconds());
                HyobBinary.WriteUtf8(w, Command);
                HyobBinary.WriteUtf8(w, Message);

                w.Write((uint)Parents.Count);
                foreach (var p in Parents) HyobBinary.WriteHash(w, p);

                HyobBinary.WriteHash(w, Tree);

                var sortedMeta = Meta.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
                w.Write((uint)sortedMeta.Count);
                foreach (var kv in sortedMeta)
                {
                    HyobBinary.WriteUtf8(w, kv.Key);
                    HyobBinary.WriteUtf8(w, kv.Value);
                }

                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobCommit Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.Commit)
                throw new InvalidDataException($"期望 Commit（0x0F02），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"Commit schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var author = HyobBinary.ReadUtf8(r);
                var email = HyobBinary.ReadUtf8(r);
                var time = DateTimeOffset.FromUnixTimeSeconds(r.ReadInt64());
                var command = HyobBinary.ReadUtf8(r);
                var message = HyobBinary.ReadUtf8(r);

                uint parentCount = r.ReadUInt32();
                var parents = new List<Hash>((int)parentCount);
                for (uint i = 0; i < parentCount; i++) parents.Add(HyobBinary.ReadHash(r));

                var tree = HyobBinary.ReadHash(r);

                uint metaCount = r.ReadUInt32();
                var meta = new Dictionary<string, string>((int)metaCount, StringComparer.Ordinal);
                for (uint i = 0; i < metaCount; i++)
                {
                    var k = HyobBinary.ReadUtf8(r);
                    var v = HyobBinary.ReadUtf8(r);
                    meta[k] = v;
                }

                return new HyobCommit(tree, author, message, parents, email, time, command, meta);
            }
        }
    }
}
