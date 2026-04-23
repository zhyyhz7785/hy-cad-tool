using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.User;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Configuration
{
    /// <summary>
    /// 将磁盘上的 <see cref="UserLayerSettings"/> 与程序内置默认表合并：保留用户修改、补齐新版本新增语义行。
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

            var merged = new List<LayerDefinitionItem>();
            foreach (var d in defaults)
            {
                if (bySemantic.TryGetValue(d.SemanticId, out var user))
                {
                    var row = d.Clone();
                    if (!string.IsNullOrWhiteSpace(user.Name)) row.Name = SanitizeName(user.Name);
                    row.AciColor = user.AciColor;
                    if (!string.IsNullOrWhiteSpace(user.LinetypeName)) row.LinetypeName = user.LinetypeName;
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
                Version = Math.Max(1, fromDisk?.Version ?? 1),
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
