using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
            // 分支名允许 '/'（如 'feat/x'）→ 文件系统映射成子目录，必须先建好。
            var parentDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parentDir)) Directory.CreateDirectory(parentDir);
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
            // 分支名允许 '/'（git 风格），文件系统中是嵌套子目录。递归扫，并把分隔符规一化为 '/'。
            int prefixLen = HeadsDirPath.Length + 1;
            foreach (var f in Directory.EnumerateFiles(HeadsDirPath, "*", SearchOption.AllDirectories))
            {
                if (f.Length <= prefixLen) continue;
                var rel = f.Substring(prefixLen).Replace(Path.DirectorySeparatorChar, '/');
                if (!string.IsNullOrEmpty(rel)) yield return rel;
            }
        }

        /// <summary>
        /// 删除分支文件。返回 false：分支不存在。
        /// 业务约束（不在此处强制）：调用方须保证不删 HEAD 当前指向的分支。
        /// </summary>
        public bool DeleteBranch(string branchName)
        {
            if (string.IsNullOrEmpty(branchName)) return false;
            var path = Path.Combine(HeadsDirPath, branchName);
            if (!File.Exists(path)) return false;
            File.Delete(path);
            // 顺手清空 'feat/' 这种留下来的空中间目录，停在 refs/heads 处。
            try
            {
                var dir = Path.GetDirectoryName(path);
                while (!string.IsNullOrEmpty(dir)
                       && !string.Equals(Path.GetFullPath(dir), Path.GetFullPath(HeadsDirPath), StringComparison.OrdinalIgnoreCase)
                       && Directory.Exists(dir)
                       && !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                    dir = Path.GetDirectoryName(dir);
                }
            }
            catch { /* 清空目录失败不影响删除语义 */ }
            return true;
        }

        /// <summary>
        /// 分支名合法性。规则（M8）：
        ///   · 长度 1..64
        ///   · 仅 ASCII 字母 / 数字 / '-' / '_' / '/'
        ///   · 不以 '-' 或 '/' 开头，不以 '/' 结尾
        ///   · 不含连续 '/'
        /// 不接受任何空白字符或路径分隔符 '\\'，避免跨平台 / 文件系统注入。
        /// </summary>
        public static bool IsValidBranchName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.Length > 64) return false;
            if (name[0] == '-' || name[0] == '/') return false;
            if (name[name.Length - 1] == '/') return false;

            char prev = '\0';
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool ok = (c >= 'a' && c <= 'z')
                       || (c >= 'A' && c <= 'Z')
                       || (c >= '0' && c <= '9')
                       || c == '-' || c == '_' || c == '/';
                if (!ok) return false;
                if (c == '/' && prev == '/') return false;
                prev = c;
            }
            return true;
        }
    }
}
