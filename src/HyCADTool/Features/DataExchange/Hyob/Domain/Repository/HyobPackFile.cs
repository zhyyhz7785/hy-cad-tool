using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Repository
{
    /// <summary>
    /// hyob pack 文件格式（M7）。简化版 git pack：把多个 loose object 用 Deflate 压缩拼成单文件 + 前向 index。
    /// 与 git pack 不兼容（是 hyob 自家格式），但概念一致。
    ///
    /// 文件结构（小端 / 全二进制）：
    /// <code>
    ///   [magic 4B "HYPK"][version u32 = 1][count u32]
    ///   [index entries: count × (hash 32B + data_offset u64 + compressed_size u32 + original_size u32)]
    ///   [data section: count × deflate(blob) 拼接]
    /// </code>
    /// data_offset 相对于 data section 起始处。
    /// 设计：04 §7（M7 Pack 仓库压缩）。
    /// </summary>
    public static class HyobPackFile
    {
        public const uint Magic = 0x4B505948; // "HYPK"（小端）
        public const uint Version = 1;

        public const int IndexEntrySize = Hash.ByteLength + 8 + 4 + 4; // 32 + 8 + 4 + 4 = 48 字节

        public static int HeaderSize => 4 + 4 + 4; // magic + version + count

        /// <summary>
        /// 把若干 (hash, blob) 写入 pack 文件。blob 为 hyob ObjectStore 写入的整个 header+payload+CRC（即 EncodeBlob 的产物）。
        /// 已存在重复 hash 的会被去重（保留第一份）。
        /// </summary>
        public static int Write(string path, IEnumerable<KeyValuePair<Hash, byte[]>> objects)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (objects == null) throw new ArgumentNullException(nameof(objects));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var compressed = new List<(Hash Hash, byte[] Compressed, int OrigSize)>();
            foreach (var kv in objects)
            {
                string hex = kv.Key.ToHex();
                if (!seen.Add(hex)) continue;
                byte[] blob = kv.Value ?? throw new ArgumentException("blob 不能为空", nameof(objects));
                byte[] cz = Deflate(blob);
                compressed.Add((kv.Key, cz, blob.Length));
            }
            int count = compressed.Count;

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(Magic);
                bw.Write(Version);
                bw.Write((uint)count);

                long offset = 0;
                foreach (var item in compressed)
                {
                    bw.Write(item.Hash.ToArray());
                    bw.Write((ulong)offset);
                    bw.Write((uint)item.Compressed.Length);
                    bw.Write((uint)item.OrigSize);
                    offset += item.Compressed.Length;
                }
                foreach (var item in compressed)
                {
                    bw.Write(item.Compressed);
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return count;
        }

        internal static byte[] Deflate(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
                {
                    ds.Write(data, 0, data.Length);
                }
                return ms.ToArray();
            }
        }

        internal static byte[] Inflate(byte[] data, int origSize)
        {
            using (var ms = new MemoryStream(data, writable: false))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            {
                var buf = new byte[origSize];
                int read = 0;
                while (read < origSize)
                {
                    int n = ds.Read(buf, read, origSize - read);
                    if (n <= 0) break;
                    read += n;
                }
                if (read != origSize)
                    throw new InvalidDataException(
                        $"hyob pack：解压尺寸不符，期望 {origSize}，实际 {read}");
                return buf;
            }
        }
    }

    /// <summary>
    /// pack 文件读取器。打开后把整个 index 读入内存，需要某 hash 时 mmap-style 按 offset 读 data 段并解压。
    /// 不持有 FileStream（每次 TryRead 重新打开），避免长期文件句柄占用 / 多文档共享。
    /// </summary>
    public sealed class HyobPackReader
    {
        private readonly string _path;
        private readonly long _dataStart;
        private readonly Dictionary<string, IndexEntry> _index;

        public string Path => _path;
        public int Count => _index.Count;
        public long DataStart => _dataStart;

        private struct IndexEntry
        {
            public long Offset;
            public int CompressedSize;
            public int OriginalSize;
        }

        private HyobPackReader(string path, long dataStart, Dictionary<string, IndexEntry> index)
        {
            _path = path;
            _dataStart = dataStart;
            _index = index;
        }

        public static HyobPackReader Open(string path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var br = new BinaryReader(fs))
            {
                uint magic = br.ReadUInt32();
                if (magic != HyobPackFile.Magic)
                    throw new InvalidDataException($"非 hyob pack 文件：{path}");
                uint ver = br.ReadUInt32();
                if (ver != HyobPackFile.Version)
                    throw new InvalidDataException($"hyob pack 版本不支持：{ver}");
                uint count = br.ReadUInt32();

                var dict = new Dictionary<string, IndexEntry>(StringComparer.Ordinal);
                for (uint i = 0; i < count; i++)
                {
                    byte[] hashBytes = br.ReadBytes(Hash.ByteLength);
                    if (hashBytes.Length != Hash.ByteLength)
                        throw new InvalidDataException("pack index 截断");
                    ulong offset = br.ReadUInt64();
                    uint csize = br.ReadUInt32();
                    uint osize = br.ReadUInt32();
                    string hex = new Hash(hashBytes).ToHex();
                    dict[hex] = new IndexEntry
                    {
                        Offset = (long)offset,
                        CompressedSize = (int)csize,
                        OriginalSize = (int)osize
                    };
                }
                long dataStart = HyobPackFile.HeaderSize + (long)count * HyobPackFile.IndexEntrySize;
                return new HyobPackReader(path, dataStart, dict);
            }
        }

        public bool Contains(Hash hash) => _index.ContainsKey(hash.ToHex());

        public bool TryRead(Hash hash, out byte[] blob)
        {
            if (!_index.TryGetValue(hash.ToHex(), out var entry)) { blob = null; return false; }
            using (var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fs.Seek(_dataStart + entry.Offset, SeekOrigin.Begin);
                var compressed = new byte[entry.CompressedSize];
                int read = 0;
                while (read < entry.CompressedSize)
                {
                    int n = fs.Read(compressed, read, entry.CompressedSize - read);
                    if (n <= 0) break;
                    read += n;
                }
                if (read != entry.CompressedSize)
                    throw new InvalidDataException("pack data 段截断");
                blob = HyobPackFile.Inflate(compressed, entry.OriginalSize);
                return true;
            }
        }

        public IEnumerable<Hash> EnumerateHashes()
        {
            foreach (var hex in _index.Keys) yield return Hash.FromHex(hex);
        }
    }
}
