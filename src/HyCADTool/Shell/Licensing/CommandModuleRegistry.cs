using System;
using System.Collections.Generic;

namespace HyCADTool.Shell.Licensing
{
    /// <summary>
    /// 模块命令清单登记口。各模块把自己的档位表 / 路由表登记进来，
    /// <see cref="CommandTiers"/> 与 <see cref="CommandRoutes"/> 读汇总结果。
    /// </summary>
    public static class CommandModuleRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> TierTables =
            new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, IReadOnlyDictionary<string, CommandRoute>> RouteTables =
            new Dictionary<string, IReadOnlyDictionary<string, CommandRoute>>(StringComparer.OrdinalIgnoreCase);

        public static IReadOnlyCollection<string> RegisteredModules
        {
            get
            {
                lock (Gate)
                    return new List<string>(TierTables.Keys);
            }
        }

        public static void Register(
            string moduleId,
            IReadOnlyDictionary<string, string> tiers,
            IReadOnlyDictionary<string, CommandRoute> routes)
        {
            if (string.IsNullOrWhiteSpace(moduleId))
                throw new ArgumentException("模块名不能为空", nameof(moduleId));
            if (tiers == null)
                throw new ArgumentNullException(nameof(tiers));
            if (routes == null)
                throw new ArgumentNullException(nameof(routes));

            lock (Gate)
            {
                TierTables[moduleId] = tiers;
                RouteTables[moduleId] = routes;
            }
        }

        public static IReadOnlyDictionary<string, string> MergeTiers()
        {
            lock (Gate)
            {
                var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var table in TierTables.Values)
                {
                    foreach (var pair in table)
                        merged[pair.Key] = pair.Value;
                }
                return merged;
            }
        }

        public static IReadOnlyDictionary<string, CommandRoute> MergeRoutes()
        {
            lock (Gate)
            {
                var merged = new Dictionary<string, CommandRoute>(StringComparer.OrdinalIgnoreCase);
                foreach (var table in RouteTables.Values)
                {
                    foreach (var pair in table)
                        merged[pair.Key] = pair.Value;
                }
                return merged;
            }
        }
    }
}
