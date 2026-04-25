using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Domain.ValueObjects.Configuration.User;

namespace HyCADTool.Shared.AutoCAD.Configuration
{
    /// <summary>
    /// 将磁盘上的 <see cref="UserLayerSettings"/> 与程序内置默认表合并：保留用户修改、补齐新版本新增语义行。
    /// 当磁盘 <see cref="UserLayerSettings.Version"/> 小于 <see cref="LayerCatalogFactory.CurrentCatalogVersion"/> 时，
    /// 强制采用内置默认的图层名与线型（避免旧 hy-settings 永久锁死 05_hy_/01_hy_ 命名）；仍合并用户颜色/线宽/打印/锁定。
    /// </summary>
    public static class UserLayerSettingsMerger
    {
        public static UserLayerSettings MergeWithDefaults(UserLayerSettings fromDisk)
        {
            var defaults = LayerCatalogFactory.CreateDefaultItems();
            var bySemantic = new Dictionary<string, LayerDefinitionItem>(StringComparer.OrdinalIgnoreCase);

            if (fromDisk?.Items != null)
            {
                foreach (var it in fromDisk.Items)
                {
                    if (string.IsNullOrWhiteSpace(it?.SemanticId)) continue;
                    if (!bySemantic.ContainsKey(it.SemanticId))
                        bySemantic[it.SemanticId] = it.Clone();
                }
            }

            bool upgradeSchema = (fromDisk?.Version ?? 0) < LayerCatalogFactory.CurrentCatalogVersion;

            var merged = new List<LayerDefinitionItem>();
            foreach (var d in defaults)
            {
                if (bySemantic.TryGetValue(d.SemanticId, out var user))
                {
                    var row = d.Clone();
                    if (!upgradeSchema && !string.IsNullOrWhiteSpace(user.Name))
                        row.Name = SanitizeName(user.Name);
                    if (!upgradeSchema && !string.IsNullOrWhiteSpace(user.LinetypeName))
                        row.LinetypeName = user.LinetypeName.Trim();

                    row.AciColor = user.AciColor;
                    row.LineWeightRaw = user.LineWeightRaw;
                    row.IsPlottable = user.IsPlottable;
                    row.IsLocked = user.IsLocked;
                    merged.Add(row);
                }
                else
                    merged.Add(d.Clone());
            }

            return new UserLayerSettings
            {
                Version = LayerCatalogFactory.CurrentCatalogVersion,
                Items = merged
            };
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            var t = name.Trim();
            foreach (var c in new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=' })
                t = t.Replace(c.ToString(), "");
            return t;
        }
    }
}
