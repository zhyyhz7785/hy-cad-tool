using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HyCADTool.Shell.Commands
{
    /// <summary>
    /// 单条命令映射（Refactored 自有版本，与 ReCall.CommandEntry 字段一致）。
    /// </summary>
    public sealed class CommandEntry
    {
        public string Type { get; set; }
        public string Method { get; set; }
        public object[] Ctor { get; set; }
        public string[] CtorEnumTypes { get; set; }

        public string Category { get; set; }
        public string DisplayName { get; set; }
        public string Icon { get; set; }
        public string Tooltip { get; set; }
        public int Order { get; set; } = 100;
        public string RoadPanelGroup { get; set; }
    }

    /// <summary>分类分组结果（按 Order 升序）。</summary>
    public sealed class CategoryGroup
    {
        public string Category { get; set; }
        public List<CommandListItem> Items { get; set; } = new List<CommandListItem>();
    }

    public sealed class CommandListItem
    {
        public string Key { get; set; }
        public CommandEntry Entry { get; set; }
        public string DisplayName => string.IsNullOrEmpty(Entry?.DisplayName) ? Key : Entry.DisplayName;
        public string Category => string.IsNullOrEmpty(Entry?.Category) ? "杂项" : Entry.Category;
        public string Tooltip => string.IsNullOrEmpty(Entry?.Tooltip) ? DisplayName : Entry.Tooltip;
        public string Icon => Entry?.Icon;
        public int Order => Entry?.Order ?? 100;
        public string RoadPanelGroup => Entry?.RoadPanelGroup;
    }

    /// <summary>
    /// 命令目录（Refactored 内部版本）：从 commands.json 加载分组数据，给 Blender 面板 / Ribbon / CUIX 用。
    /// 不依赖 ReCall.dll —— 生产模式（NETLOAD HyCADTool.dll，无 ReCall）也工作。
    ///
    /// 文件定位策略：
    ///   1) 自身 Location（生产模式 NETLOAD 走这条 → bin\Production\commands.json）
    ///   2) 兜底：在 AppDomain 找已加载的 ReCall.dll 同目录（dev/C2 模式 → bin\Debug\commands.json）
    ///   3) 都失败 → 返回相对路径 "commands.json"
    ///
    /// 与 ReCall.CommandTable 是**两份独立实现**，读同一份 commands.json，互不依赖。
    /// </summary>
    public static class CommandCatalog
    {
        public const string FILE_NAME = "commands.json";

        private static Dictionary<string, CommandEntry> _entries
            = new Dictionary<string, CommandEntry>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _lastFileMtimeUtc = DateTime.MinValue;
        private static DateTime _lastLoadUtc = DateTime.MinValue;
        private static string _lastError;
        private static readonly object _sync = new object();

        public static DateTime LastLoadUtc => _lastLoadUtc;
        public static string LastError => _lastError;

        public static string GetFilePath()
        {
            try
            {
                var selfLoc = typeof(CommandCatalog).Assembly.Location;
                if (!string.IsNullOrEmpty(selfLoc))
                {
                    var dir = Path.GetDirectoryName(selfLoc);
                    var path = Path.Combine(string.IsNullOrEmpty(dir) ? "." : dir, FILE_NAME);
                    if (File.Exists(path)) return path;
                }

                var recall = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "ReCall", StringComparison.OrdinalIgnoreCase));
                var recallLoc = recall?.Location;
                if (!string.IsNullOrEmpty(recallLoc))
                {
                    var dir = Path.GetDirectoryName(recallLoc);
                    return Path.Combine(string.IsNullOrEmpty(dir) ? "." : dir, FILE_NAME);
                }
            }
            catch
            {
            }
            return FILE_NAME;
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

        public static void EnsureLoaded()
        {
            lock (_sync)
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
        }

        public static void ForceReload()
        {
            lock (_sync)
            {
                _lastLoadUtc = DateTime.MinValue;
                _lastFileMtimeUtc = DateTime.MinValue;
            }
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
                    dict[p.Name] = new CommandEntry
                    {
                        Type = (string)obj["type"],
                        Method = (string)obj["method"] ?? "Execute",
                        Ctor = obj["ctor"]?.ToObject<object[]>(),
                        CtorEnumTypes = obj["ctorEnumTypes"]?.ToObject<string[]>(),
                        Category = (string)obj["category"],
                        DisplayName = (string)obj["displayName"],
                        Icon = (string)obj["icon"],
                        Tooltip = (string)obj["tooltip"],
                        Order = obj["order"] != null ? (int)obj["order"] : 100,
                        RoadPanelGroup = (string)obj["roadPanelGroup"],
                    };
                }
            }

            _entries = dict;
        }

        public static CommandEntry Get(string key)
        {
            EnsureLoaded();
            _entries.TryGetValue(key, out var entry);
            return entry;
        }

        public static bool Contains(string key)
        {
            EnsureLoaded();
            return _entries.ContainsKey(key);
        }

        public static IReadOnlyDictionary<string, CommandEntry> Snapshot()
        {
            EnsureLoaded();
            return _entries;
        }

        public static List<CategoryGroup> GroupByCategory()
        {
            EnsureLoaded();

            var buckets = new Dictionary<string, List<CommandListItem>>(StringComparer.Ordinal);
            foreach (var pair in _entries)
            {
                if (pair.Value == null) continue;
                if (!string.IsNullOrEmpty(pair.Key) && pair.Key.StartsWith("_")) continue;
                if (string.Equals(pair.Key, "Hy", StringComparison.OrdinalIgnoreCase)) continue;

                var item = new CommandListItem { Key = pair.Key, Entry = pair.Value };
                var cat = item.Category;
                if (!buckets.TryGetValue(cat, out var list))
                {
                    list = new List<CommandListItem>();
                    buckets[cat] = list;
                }
                list.Add(item);
            }

            var result = new List<CategoryGroup>();
            foreach (var cat in PresetCategoryOrder)
            {
                if (buckets.TryGetValue(cat, out var list))
                {
                    list.Sort(CompareListItem);
                    result.Add(new CategoryGroup { Category = cat, Items = list });
                    buckets.Remove(cat);
                }
            }
            var remaining = new List<string>(buckets.Keys);
            remaining.Sort(StringComparer.Ordinal);
            foreach (var cat in remaining)
            {
                var list = buckets[cat];
                list.Sort(CompareListItem);
                result.Add(new CategoryGroup { Category = cat, Items = list });
            }
            return result;
        }

        private static int CompareListItem(CommandListItem a, CommandListItem b)
        {
            int c = a.Order.CompareTo(b.Order);
            if (c != 0) return c;
            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
        }

        public static readonly string[] PresetCategoryOrder = new[]
        {
            "常用",
            "钢筋",
            "底板配筋",
            "桩基",
            "沉降",
            "标高",
            "尺寸标注",
            "地脚螺栓",
            "设备基础",
            "图框视口",
            "道路",
            "块引线",
            "导出说明",
            "多段线垫层",
            "杂项",
            "测试",
        };
    }
}
