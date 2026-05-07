using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.Pack
{
    /// <summary>
    /// hyob pack 编排器（M7）。把 ObjectStore 里的所有 loose object Deflate 压缩到单个 .pack 文件，
    /// 然后删除已打包的 loose 副本，从而大幅减少文件数与磁盘占用。
    ///
    /// 安全策略：
    ///   1. 先把所有 loose 读入并压缩，写到临时 pack 文件
    ///   2. rename 到正式名 packs/&lt;sha256-of-pack&gt;.pack（content-addressing）
    ///   3. ReloadPacks 让 store 看到新 pack
    ///   4. 校验：所有原 loose 都能从新 pack 读出且 byte-equal
    ///   5. 通过校验后才逐个删除 loose（任一阶段失败都不丢数据）
    ///
    /// 设计：04 §7（M7 Pack）。
    /// </summary>
    public sealed class HyobPacker
    {
        public sealed class PackResult
        {
            public string PackPath { get; set; }
            public int PackedCount { get; set; }
            public long LooseBytesBefore { get; set; }
            public long PackBytes { get; set; }
            public bool Verified { get; set; }
            public int DeletedLoose { get; set; }
        }

        public PackResult Pack(HyobObjectStore store)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));

            var looseHashes = store.EnumerateLoose().ToList();
            if (looseHashes.Count == 0)
            {
                return new PackResult
                {
                    PackPath = null,
                    PackedCount = 0,
                    LooseBytesBefore = 0,
                    PackBytes = 0,
                    Verified = true,
                    DeletedLoose = 0
                };
            }

            long looseBytes = 0;
            var blobs = new Dictionary<Hash, byte[]>();
            foreach (var h in looseHashes)
            {
                if (!store.TryRead(h, out var blob))
                    throw new InvalidOperationException($"打包：loose 对象读取失败 {h.ToHex()}");
                blobs[h] = blob;
                looseBytes += blob.Length;
            }

            string packDir = store.PackDir;
            Directory.CreateDirectory(packDir);
            string tmpName = "pack-" + Guid.NewGuid().ToString("N") + ".pack.tmp";
            string tmpPath = Path.Combine(packDir, tmpName);

            HyobPackFile.Write(tmpPath, blobs.Select(kv =>
                new KeyValuePair<Hash, byte[]>(kv.Key, kv.Value)));

            byte[] packBytes = File.ReadAllBytes(tmpPath);
            string packHash = Hash.OfPayload(packBytes).ToHex().Substring(0, 16);
            string finalName = "pack-" + packHash + ".pack";
            string finalPath = Path.Combine(packDir, finalName);
            if (File.Exists(finalPath)) File.Delete(finalPath);
            File.Move(tmpPath, finalPath);
            long packFileSize = new FileInfo(finalPath).Length;

            var verifyReader = HyobPackReader.Open(finalPath);
            int verified = 0;
            foreach (var kv in blobs)
            {
                if (!verifyReader.TryRead(kv.Key, out var read))
                    throw new InvalidDataException($"pack 校验失败：{kv.Key.ToHex()} 在新 pack 中找不到");
                if (read.Length != kv.Value.Length)
                    throw new InvalidDataException($"pack 校验失败：{kv.Key.ToHex()} 长度不一致");
                for (int i = 0; i < read.Length; i++)
                {
                    if (read[i] != kv.Value[i])
                        throw new InvalidDataException($"pack 校验失败：{kv.Key.ToHex()} byte[{i}] 不一致");
                }
                verified++;
            }

            store.ReloadPacks();

            int deleted = 0;
            foreach (var h in looseHashes)
            {
                store.Delete(h);
                deleted++;
            }

            CleanupEmptyDirs(store.RootPath);

            return new PackResult
            {
                PackPath = finalPath,
                PackedCount = verified,
                LooseBytesBefore = looseBytes,
                PackBytes = packFileSize,
                Verified = true,
                DeletedLoose = deleted
            };
        }

        private static void CleanupEmptyDirs(string root)
        {
            if (!Directory.Exists(root)) return;
            foreach (var dir in Directory.EnumerateDirectories(root).ToList())
            {
                var name = Path.GetFileName(dir);
                if (name == null || name.Length != 2) continue;
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                        Directory.Delete(dir);
                }
                catch { /* 忽略，下次再清 */ }
            }
        }
    }
}
