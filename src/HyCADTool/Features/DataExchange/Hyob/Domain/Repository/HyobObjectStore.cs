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

        public string RootPath => _rootPath;

        public HyobObjectStore(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
                throw new ArgumentException("rootPath 不能为空", nameof(rootPath));
            _rootPath = rootPath;
            Directory.CreateDirectory(_rootPath);
        }

        public string PathOf(Hash hash)
        {
            string hex = hash.ToHex();
            return Path.Combine(_rootPath, hex.Substring(0, 2), hex.Substring(2));
        }

        public bool Exists(Hash hash) => File.Exists(PathOf(hash));

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
            var path = PathOf(hash);
            if (!File.Exists(path))
                throw new FileNotFoundException($"hyob Object 不存在：{hash.ToHex()}", path);
            return File.ReadAllBytes(path);
        }

        public bool TryRead(Hash hash, out byte[] blob)
        {
            var path = PathOf(hash);
            if (!File.Exists(path)) { blob = null; return false; }
            blob = File.ReadAllBytes(path);
            return true;
        }

        public void Delete(Hash hash)
        {
            var path = PathOf(hash);
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>
        /// 遍历 store 中所有已存对象的 hash（用于 GC / pack）。
        /// 实现：扫两层目录，跳过 .tmp 与命名不规范的文件。开发期低性能 OK。
        /// </summary>
        public IEnumerable<Hash> EnumerateAll()
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
