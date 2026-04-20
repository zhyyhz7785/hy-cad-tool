using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HyCADTool.Refactored.Domain.Models.Road;
using Newtonsoft.Json;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// 历史还原服务（M6）—— 纯 Domain 层，仅做 JSON / 文件 I/O，不依赖 AutoCAD。
    ///
    /// <para><b>目录约定</b></para>
    /// 对 DWG 路径 <c>C:\prj\abc.dwg</c>，历史目录为 <c>C:\prj\abc.roaddesign.history\</c>：
    /// <list type="bullet">
    ///   <item><c>history-index.json</c>：所有条目的索引（<see cref="HistoryIndex"/>）。</item>
    ///   <item><c>snap-0001-20260420-143015.json</c>：第 1 号快照（文件名含序号 + 时间便于人工识别）。</item>
    /// </list>
    ///
    /// <para><b>调用者职责</b></para>
    /// <list type="bullet">
    ///   <item><c>hyRoadSnapshot</c>（M6.3）：弹输入框拿 description → 调 <see cref="CreateSnapshot"/></item>
    ///   <item><c>hyRoadHistory</c>（M6.3）：打开 <c>RoadHistoryWindow</c>，窗口内调 <see cref="LoadIndex"/> / <see cref="Restore"/> / <see cref="DeleteEntry"/></item>
    ///   <item><c>hyRoadRestoreTo</c>（M6.3）：命令行指定序号一步还原</item>
    /// </list>
    /// </summary>
    public sealed class HistoryService
    {
        /// <summary>历史目录后缀（含点前缀，拼接在 dwg basename 后）。</summary>
        public const string HistorySuffix = ".roaddesign.history";

        /// <summary>索引文件名。</summary>
        public const string IndexFileName = "history-index.json";

        private readonly JsonSerializerSettings _settings;

        public HistoryService()
        {
            _settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                Culture = System.Globalization.CultureInfo.InvariantCulture,
            };
        }

        // =============================================================================
        // 路径工具
        // =============================================================================

        /// <summary>
        /// 按 DWG 路径推断历史目录（与 DWG 同级）。
        /// <para>若 <paramref name="dwgPath"/> 为空 / 无效返回 null。</para>
        /// </summary>
        public static string GetHistoryDirectory(string dwgPath)
        {
            if (string.IsNullOrWhiteSpace(dwgPath)) return null;
            var dir = Path.GetDirectoryName(dwgPath);
            if (string.IsNullOrEmpty(dir)) dir = ".";
            var name = Path.GetFileNameWithoutExtension(dwgPath);
            if (string.IsNullOrEmpty(name)) return null;
            return Path.Combine(dir, name + HistorySuffix);
        }

        /// <summary>拼接索引文件的完整路径。</summary>
        public static string GetIndexFilePath(string historyDir)
            => string.IsNullOrWhiteSpace(historyDir) ? null : Path.Combine(historyDir, IndexFileName);

        /// <summary>计算快照文件名：<c>snap-NNNN-yyyyMMdd-HHmmss.json</c>。</summary>
        public static string MakeSnapshotFileName(int seqNo, DateTime localTime)
            => $"snap-{seqNo:D4}-{localTime:yyyyMMdd-HHmmss}.json";

        // =============================================================================
        // 读 / 写 索引
        // =============================================================================

        /// <summary>
        /// 从 <paramref name="historyDir"/> 读取索引。
        /// <para>目录或索引文件不存在时返回空的 <see cref="HistoryIndex"/>（<see cref="HistoryIndex.NextSeqNo"/>=1）。</para>
        /// </summary>
        public HistoryIndex LoadIndex(string historyDir)
        {
            var result = new HistoryIndex();
            if (string.IsNullOrWhiteSpace(historyDir)) return result;
            if (!Directory.Exists(historyDir)) return result;

            var indexPath = GetIndexFilePath(historyDir);
            if (!File.Exists(indexPath)) return result;

            try
            {
                string text = File.ReadAllText(indexPath, Encoding.UTF8);
                var loaded = JsonConvert.DeserializeObject<HistoryIndex>(text, _settings);
                if (loaded == null) return result;
                if (loaded.Entries == null) loaded.Entries = new List<HistoryEntry>();
                if (loaded.NextSeqNo <= 0)
                {
                    loaded.NextSeqNo = loaded.Entries.Count == 0 ? 1 : loaded.Entries.Max(e => e.SeqNo) + 1;
                }
                return loaded;
            }
            catch (JsonException)
            {
                // 索引文件损坏 → 返回空索引，上层可以继续创建新快照（旧快照文件仍在，可由用户手工抢救）
                return new HistoryIndex();
            }
        }

        /// <summary>写索引（原子替换）。</summary>
        public void SaveIndex(string historyDir, HistoryIndex index)
        {
            if (string.IsNullOrWhiteSpace(historyDir)) throw new ArgumentException("historyDir 不能为空", nameof(historyDir));
            if (index == null) throw new ArgumentNullException(nameof(index));

            Directory.CreateDirectory(historyDir);
            var indexPath = GetIndexFilePath(historyDir);

            string content = JsonConvert.SerializeObject(index, _settings);
            string tmp = indexPath + ".tmp";
            File.WriteAllText(tmp, content, new UTF8Encoding(false));

            if (File.Exists(indexPath))
            {
                File.Replace(tmp, indexPath, indexPath + ".bak", ignoreMetadataErrors: true);
                try { File.Delete(indexPath + ".bak"); } catch { }
            }
            else
            {
                File.Move(tmp, indexPath);
            }
        }

        // =============================================================================
        // 创建 / 读取 / 删除 快照
        // =============================================================================

        /// <summary>
        /// 创建一个还原点。全量序列化 <paramref name="design"/> 为 JSON 写入 <paramref name="historyDir"/>，并更新索引。
        ///
        /// <para>返回新创建的 <see cref="HistoryEntry"/>；空/不合法输入返回 null。</para>
        ///
        /// 描述规范（M8 自动提取命令约定）：
        /// <list type="bullet">
        ///   <item>手动保存（hyRoadSnapshot）：用户输入的中文描述</item>
        ///   <item>自动保存（hyRoadExtract*）：<c>"从图形提取_绿化带_路线1"</c></item>
        ///   <item>自动保存（hyRoadAlnByPi）：<c>"导线法新建路线_路线2"</c></item>
        /// </list>
        /// </summary>
        public HistoryEntry CreateSnapshot(string historyDir, RoadDesign design, string description, string userName = null)
        {
            if (string.IsNullOrWhiteSpace(historyDir)) return null;
            if (design == null) return null;

            Directory.CreateDirectory(historyDir);

            var now = DateTime.Now;
            var index = LoadIndex(historyDir);
            int seqNo = index.NextSeqNo;
            string fileName = MakeSnapshotFileName(seqNo, now);
            string snapPath = Path.Combine(historyDir, fileName);

            string json = JsonConvert.SerializeObject(design, _settings);
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);

            // 原子写
            string tmp = snapPath + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(snapPath)) File.Delete(snapPath);
            File.Move(tmp, snapPath);

            var entry = new HistoryEntry
            {
                SeqNo = seqNo,
                TimestampUtc = now.ToUniversalTime(),
                TimestampLocal = now,
                UserName = userName ?? Environment.UserName ?? string.Empty,
                Description = description ?? string.Empty,
                SnapshotFileName = fileName,
                Checksum = ComputeSha256Hex(bytes),
                SnapshotBytes = bytes.LongLength,
                SchemaVersion = SchemaVersion.Current,
            };

            index.Entries.Add(entry);
            index.NextSeqNo = seqNo + 1;
            SaveIndex(historyDir, index);
            return entry;
        }

        /// <summary>
        /// 读取指定条目对应的快照并反序列化成 <see cref="RoadDesign"/>。
        /// <para>读取前会校验 <see cref="HistoryEntry.Checksum"/>；损坏时抛出 <see cref="InvalidDataException"/>。</para>
        /// <para>条目或快照文件不存在时返回 null。</para>
        /// </summary>
        public RoadDesign Restore(string historyDir, HistoryEntry entry)
        {
            if (string.IsNullOrWhiteSpace(historyDir)) return null;
            if (entry == null || string.IsNullOrWhiteSpace(entry.SnapshotFileName)) return null;

            string snapPath = Path.Combine(historyDir, entry.SnapshotFileName);
            if (!File.Exists(snapPath)) return null;

            byte[] bytes = File.ReadAllBytes(snapPath);
            if (!string.IsNullOrEmpty(entry.Checksum))
            {
                string actual = ComputeSha256Hex(bytes);
                if (!string.Equals(actual, entry.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"快照 {entry.SnapshotFileName} Checksum 校验失败：记录={entry.Checksum}, 实际={actual}。");
                }
            }

            string json = new UTF8Encoding(false).GetString(bytes);
            return JsonConvert.DeserializeObject<RoadDesign>(json, _settings);
        }

        /// <summary>按 SeqNo 便捷还原；找不到返回 null。</summary>
        public RoadDesign RestoreBySeqNo(string historyDir, int seqNo)
        {
            var index = LoadIndex(historyDir);
            var entry = index.Entries.FirstOrDefault(e => e.SeqNo == seqNo);
            return entry == null ? null : Restore(historyDir, entry);
        }

        /// <summary>
        /// 删除索引中的指定条目及其快照文件。
        /// <para>返回 true = 成功删除（或者本就不存在），false = 参数不合法。</para>
        /// </summary>
        public bool DeleteEntry(string historyDir, int seqNo)
        {
            if (string.IsNullOrWhiteSpace(historyDir)) return false;
            var index = LoadIndex(historyDir);
            int idx = index.Entries.FindIndex(e => e.SeqNo == seqNo);
            if (idx < 0) return true; // idempotent

            var entry = index.Entries[idx];
            index.Entries.RemoveAt(idx);
            SaveIndex(historyDir, index);

            try
            {
                string snapPath = Path.Combine(historyDir, entry.SnapshotFileName);
                if (File.Exists(snapPath)) File.Delete(snapPath);
            }
            catch { /* 删快照文件失败不影响索引一致性 */ }

            return true;
        }

        // =============================================================================
        // 辅助
        // =============================================================================

        /// <summary>计算 SHA-256 并以小写十六进制返回。</summary>
        public static string ComputeSha256Hex(byte[] bytes)
        {
            if (bytes == null) return string.Empty;
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
