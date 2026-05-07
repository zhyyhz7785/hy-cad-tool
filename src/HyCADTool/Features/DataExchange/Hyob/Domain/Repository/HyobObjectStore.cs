using System;
using System.Collections.Generic;
using System.IO;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Repository
{
    /// <summary>
    /// hyob Object 文件系统存储。content-addressable：blob 的 SHA-256 = 文件名。
    /// 路径布局：<c>&lt;root&gt;/&lt;hh&gt;/&lt;hex[2..]&gt;</c>，与 git 一致（前 2 位作为目录分桶，避免单目录文件过多）。
    /// 设计：02 §4 / 04 §4。
    ///
    /// 首期不做：
    ///   - zstd 压缩（M7）
    ///   - pack 文件 / delta（M7）
    ///   - 并发锁（首期单文档单线程足够）
    /// </summary>
    public sealed class HyobObjectStore
    {
        private readonly string _rootPath;
        private readonly List<HyobPackReader> _packs;

        public string RootPath => _rootPath;

        /// <summary>pack 文件目录：&lt;objects&gt;/pack/*.pack</summary>
        public string PackDir => Path.Combine(_rootPath, "pack");

        /// <summary>当前已加载的 pack 数量（M7）。</summary>
        public int PackCount => _packs.Count;

        public HyobObjectStore(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
                throw new ArgumentException("rootPath 不能为空", nameof(rootPath));
            _rootPath = rootPath;
            Directory.CreateDirectory(_rootPath);
            _packs = new List<HyobPackReader>();
            ReloadPacks();
        }

        /// <summary>
        /// 重新扫描 pack 目录加载所有 .pack 文件。打包后或外部增删 pack 时手工调用。
        /// </summary>
        public void ReloadPacks()
        {
            _packs.Clear();
            string dir = PackDir;
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.EnumerateFiles(dir, "*.pack"))
            {
                try { _packs.Add(HyobPackReader.Open(f)); }
                catch { /* 损坏 pack 跳过；hyobR 会报告 */ }
            }
        }

        public string PathOf(Hash hash)
        {
            string hex = hash.ToHex();
            return Path.Combine(_rootPath, hex.Substring(0, 2), hex.Substring(2));
        }

        /// <summary>对象是否存在（loose 或任意 pack 均可）。</summary>
        public bool Exists(Hash hash)
        {
            if (File.Exists(PathOf(hash))) return true;
            foreach (var p in _packs) if (p.Contains(hash)) return true;
            return false;
        }

        /// <summary>
        /// 写入 blob，返回其 SHA-256 哈希。重复写入幂等：内容相同 → 路径相同 → 不重写。
        /// 原子写入：先写 .tmp 再 rename，避免半写文件被读到。
        /// </summary>
        public Hash Write(byte[] blob)
        {
            if (blob == null) throw new ArgumentNullException(nameof(blob));
            var hash = Hash.OfPayload(blob);
            var path = PathOf(hash);

            // content-addressable：已存在即等价，无需重写
            if (File.Exists(path)) return hash;

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var tmp = path + ".tmp";
            File.WriteAllBytes(tmp, blob);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return hash;
        }

        public byte[] Read(Hash hash)
        {
            if (TryRead(hash, out var blob)) return blob;
            throw new FileNotFoundException($"hyob Object 不存在：{hash.ToHex()}", PathOf(hash));
        }

        /// <summary>读取顺序：loose → packs（最新写入优先；pack 是历史归档）。</summary>
        public bool TryRead(Hash hash, out byte[] blob)
        {
            var path = PathOf(hash);
            if (File.Exists(path)) { blob = File.ReadAllBytes(path); return true; }
            foreach (var p in _packs)
            {
                if (p.TryRead(hash, out blob)) return true;
            }
            blob = null;
            return false;
        }

        /// <summary>仅删除 loose 副本；pack 内的对象不可单独删（需要重打包）。</summary>
        public void Delete(Hash hash)
        {
            var path = PathOf(hash);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>仅枚举 loose 对象（不含 pack）。打包流程用此找出待迁移对象。</summary>
        public IEnumerable<Hash> EnumerateLoose() => EnumerateAllLooseInternal();

        /// <summary>枚举 pack 中所有对象（不含 loose）。</summary>
        public IEnumerable<Hash> EnumeratePacked()
        {
            foreach (var p in _packs)
                foreach (var h in p.EnumerateHashes())
                    yield return h;
        }

        private IEnumerable<Hash> EnumerateAllLooseInternal() => EnumerateAllInternal();

        /// <summary>
        /// 遍历 store 中所有已存对象的 hash（loose + packs，pack 同 hash 去重）。
        /// 用于 GC / round-trip 验证。开发期低性能 OK。
        /// </summary>
        public IEnumerable<Hash> EnumerateAll()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var h in EnumerateAllInternal())
            {
                if (seen.Add(h.ToHex())) yield return h;
            }
            foreach (var p in _packs)
            {
                foreach (var h in p.EnumerateHashes())
                {
                    if (seen.Add(h.ToHex())) yield return h;
                }
            }
        }

        private IEnumerable<Hash> EnumerateAllInternal()
        {
            if (!Directory.Exists(_rootPath)) yield break;
            foreach (var dir in Directory.EnumerateDirectories(_rootPath))
            {
                var prefix = Path.GetFileName(dir);
                if (prefix == null || prefix.Length != 2) continue;
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    var name = Path.GetFileName(f);
                    if (string.IsNullOrEmpty(name) || name.EndsWith(".tmp")) continue;
                    if (name.Length != Hash.HexLength - 2) continue;
                    Hash h;
                    try { h = Hash.FromHex(prefix + name); }
                    catch { continue; }
                    yield return h;
                }
            }
        }
    }
}
