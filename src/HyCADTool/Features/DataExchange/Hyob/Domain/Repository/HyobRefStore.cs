using System;
using System.Collections.Generic;
using System.IO;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Repository
{
    /// <summary>
    /// hyob HEAD / 分支引用文件系统读写。设计：02 §4 / 04 §4。
    ///
    /// 文件系统布局（与 git 同形）：
    /// <code>
    ///   <root>/HEAD                  = "ref: refs/heads/<name>"   或   "<commit-hex>"   (detached)
    ///   <root>/refs/heads/<name>     = "<commit-hex>"
    /// </code>
    ///
    /// 文件以 UTF-8 文本写入并以 "\n" 结尾（git 风格），读时 Trim。
    /// </summary>
    public sealed class HyobRefStore
    {
        private const string HeadFileName = "HEAD";
        private const string HeadsRel = "refs/heads";
        private const string RefPrefix = "ref: ";

        private readonly string _rootPath;

        private string HeadPath => Path.Combine(_rootPath, HeadFileName);
        private string HeadsDirPath => Path.Combine(_rootPath, "refs", "heads");

        public string RootPath => _rootPath;

        public HyobRefStore(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
                throw new ArgumentException("rootPath 不能为空", nameof(rootPath));
            _rootPath = rootPath;
            Directory.CreateDirectory(_rootPath);
            Directory.CreateDirectory(HeadsDirPath);
        }

        // ---------- HEAD ----------

        /// <summary>
        /// 读 HEAD。
        ///   返回 false：HEAD 文件不存在（仓库未初始化）。
        ///   返回 true 且 <paramref name="branchName"/> 非空：HEAD 指向 branch。
        ///   返回 true 且 <paramref name="branchName"/> 为空：detached HEAD，用 <paramref name="detachedHash"/>。
        /// </summary>
        public bool TryReadHead(out string branchName, out Hash detachedHash)
        {
            branchName = null;
            detachedHash = default;

            if (!File.Exists(HeadPath)) return false;

            var text = File.ReadAllText(HeadPath).Trim();
            if (text.StartsWith(RefPrefix, StringComparison.Ordinal))
            {
                var refPath = text.Substring(RefPrefix.Length).Trim();
                var prefix = HeadsRel + "/";
                if (!refPath.StartsWith(prefix, StringComparison.Ordinal))
                    throw new InvalidDataException($"HEAD 引用不识别：{refPath}");
                branchName = refPath.Substring(prefix.Length);
                if (string.IsNullOrEmpty(branchName))
                    throw new InvalidDataException("HEAD 引用 branch 名为空");
                return true;
            }

            // detached
            if (text.Length != Hash.HexLength)
                throw new InvalidDataException($"HEAD 内容不识别（既非 ref 也非 hash）：{text}");
            detachedHash = Hash.FromHex(text);
            return true;
        }

        public void WriteHeadBranch(string branchName)
        {
            if (string.IsNullOrEmpty(branchName))
                throw new ArgumentException("branchName 不能为空", nameof(branchName));
            File.WriteAllText(HeadPath, $"{RefPrefix}{HeadsRel}/{branchName}\n");
        }

        public void WriteHeadDetached(Hash hash)
        {
            File.WriteAllText(HeadPath, hash.ToHex() + "\n");
        }

        // ---------- branches ----------

        public bool TryReadBranchTip(string branchName, out Hash hash)
        {
            hash = default;
            if (string.IsNullOrEmpty(branchName)) return false;
            var path = Path.Combine(HeadsDirPath, branchName);
            if (!File.Exists(path)) return false;
            var text = File.ReadAllText(path).Trim();
            if (text.Length != Hash.HexLength) return false;
            try { hash = Hash.FromHex(text); return true; }
            catch { return false; }
        }

        public void WriteBranchTip(string branchName, Hash hash)
        {
            if (string.IsNullOrEmpty(branchName))
                throw new ArgumentException("branchName 不能为空", nameof(branchName));
            var path = Path.Combine(HeadsDirPath, branchName);
            File.WriteAllText(path, hash.ToHex() + "\n");
        }

        public bool BranchExists(string branchName)
        {
            if (string.IsNullOrEmpty(branchName)) return false;
            return File.Exists(Path.Combine(HeadsDirPath, branchName));
        }

        public IEnumerable<string> EnumerateBranches()
        {
            if (!Directory.Exists(HeadsDirPath)) yield break;
            foreach (var f in Directory.EnumerateFiles(HeadsDirPath))
            {
                var name = Path.GetFileName(f);
                if (!string.IsNullOrEmpty(name)) yield return name;
            }
        }
    }
}
