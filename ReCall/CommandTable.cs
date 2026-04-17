using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace HyCADTool.ReCall
{
    /// <summary>
    /// 单条命令映射：<c>[CommandMethod(key)]</c> → 反射目标 (Type.Method) + 可选构造参数。
    /// ctor 中的枚举用字符串书写（如 "Standard"），并在 ctorEnumTypes 对应位置写枚举类型全名。
    /// </summary>
    public sealed class CommandEntry
    {
        public string Type { get; set; }
        public string Method { get; set; }
        public object[] Ctor { get; set; }
        public string[] CtorEnumTypes { get; set; }
    }

    /// <summary>
    /// 命令表门面：从 ReCall.dll 同目录的 <c>commands.json</c> 加载 key→CommandEntry 映射。
    /// 按文件 LastWriteTime 自动失效缓存 —— 改 JSON 后下次 Invoke 立即生效，无需 C2、无需重启 AutoCAD。
    /// </summary>
    public static class CommandTable
    {
        public const string FILE_NAME = "commands.json";

        // 与 AutoCAD 命令行语义对齐：命令不区分大小写。
        // 这样 commands.json 里写 "Hy"、CommandFacade 里 Invoke("hy")、用户输入 "HY" 都能匹配到同一条。
        private static Dictionary<string, CommandEntry> _entries
            = new Dictionary<string, CommandEntry>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _lastFileMtimeUtc = DateTime.MinValue;
        private static DateTime _lastLoadUtc = DateTime.MinValue;
        private static string _lastError;

        public static DateTime LastLoadUtc => _lastLoadUtc;
        public static string LastError => _lastError;

        public static string GetFilePath()
        {
            try
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(string.IsNullOrEmpty(dir) ? "." : dir, FILE_NAME);
            }
            catch
            {
                return FILE_NAME;
            }
        }

        public static DateTime? GetFileMtimeUtc()
        {
            try
            {
                var p = GetFilePath();
                return File.Exists(p) ? File.GetLastWriteTimeUtc(p) : (DateTime?)null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 按需加载：文件不存在或 mtime 未变则走内存缓存，否则重新读取 JSON。
        /// 任何异常不抛，错误信息记录到 <see cref="LastError"/> 供 hyRecallSelfCheck 查看。
        /// </summary>
        public static void EnsureLoaded()
        {
            try
            {
                var p = GetFilePath();
                if (!File.Exists(p))
                {
                    _entries = new Dictionary<string, CommandEntry>(StringComparer.OrdinalIgnoreCase);
                    _lastError = "commands.json 不存在：" + p;
                    return;
                }

                var mtime = File.GetLastWriteTimeUtc(p);
                if (_lastLoadUtc != DateTime.MinValue && mtime == _lastFileMtimeUtc)
                {
                    return;
                }

                LoadFrom(p);
                _lastFileMtimeUtc = mtime;
                _lastLoadUtc = DateTime.UtcNow;
                _lastError = null;
            }
            catch (System.Exception ex)
            {
                _lastError = ex.GetType().Name + ": " + ex.Message;
            }
        }

        /// <summary>强制重新加载（忽略缓存）。</summary>
        public static void ForceReload()
        {
            _lastLoadUtc = DateTime.MinValue;
            _lastFileMtimeUtc = DateTime.MinValue;
            EnsureLoaded();
        }

        private static void LoadFrom(string path)
        {
            var text = File.ReadAllText(path);
            var root = JObject.Parse(text);
            var cmds = root["commands"] as JObject;
            var dict = new Dictionary<string, CommandEntry>(StringComparer.OrdinalIgnoreCase);

            if (cmds != null)
            {
                foreach (var p in cmds.Properties())
                {
                    if (p.Value == null || p.Value.Type == JTokenType.Null)
                    {
                        dict[p.Name] = null;
                        continue;
                    }
                    if (p.Value.Type != JTokenType.Object)
                    {
                        continue;
                    }

                    var obj = (JObject)p.Value;
                    var entry = new CommandEntry
                    {
                        Type = (string)obj["type"],
                        Method = (string)obj["method"] ?? "Execute",
                        Ctor = obj["ctor"]?.ToObject<object[]>(),
                        CtorEnumTypes = obj["ctorEnumTypes"]?.ToObject<string[]>(),
                    };
                    dict[p.Name] = entry;
                }
            }

            _entries = dict;
        }

        /// <summary>
        /// 按 key 获取命令条目。null 表示键不存在或是占位符（N1~N50 未分配时）。
        /// </summary>
        public static CommandEntry Get(string key)
        {
            EnsureLoaded();
            _entries.TryGetValue(key, out var entry);
            return entry;
        }

        /// <summary>
        /// 是否存在该 key（即使它是占位符 null 值）。用于区分"键不存在"与"键是占位符"。
        /// </summary>
        public static bool Contains(string key)
        {
            EnsureLoaded();
            return _entries.ContainsKey(key);
        }

        /// <summary>当前全部命令条目快照，按内部字典顺序。</summary>
        public static IReadOnlyDictionary<string, CommandEntry> Snapshot()
        {
            EnsureLoaded();
            return _entries;
        }

        public sealed class ValidationResult
        {
            public string Key;
            public CommandEntry Entry;
            public bool TypeFound;
            public bool MethodFound;
            public string Note;
        }

        /// <summary>
        /// 对整张表做只读的"类型/方法可解析性"校验，不触发业务副作用。
        /// 给 hyRecallSelfCheck / C2 完成后的自检使用。
        /// </summary>
        public static List<ValidationResult> ValidateAll(Assembly refactored)
        {
            EnsureLoaded();
            var results = new List<ValidationResult>();

            foreach (var pair in _entries)
            {
                var r = new ValidationResult { Key = pair.Key, Entry = pair.Value };

                if (pair.Value == null)
                {
                    r.Note = "<占位符 未分配>";
                    results.Add(r);
                    continue;
                }

                if (string.Equals(pair.Key, "_HyExec", StringComparison.Ordinal))
                {
                    r.TypeFound = true;
                    r.MethodFound = true;
                    r.Note = "<特殊：面板待执行命令>";
                    results.Add(r);
                    continue;
                }

                if (refactored == null)
                {
                    r.Note = "Refactored 未加载（请先 C2）";
                    results.Add(r);
                    continue;
                }

                var type = refactored.GetType(pair.Value.Type);
                r.TypeFound = type != null;
                if (type == null)
                {
                    r.Note = "TypeNotFound: " + pair.Value.Type;
                }
                else
                {
                    var m = type.GetMethod(pair.Value.Method,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                    r.MethodFound = m != null;
                    if (m == null)
                    {
                        r.Note = "MethodNotFound: " + pair.Value.Type + "." + pair.Value.Method;
                    }
                }
                results.Add(r);
            }

            return results;
        }
    }
}
