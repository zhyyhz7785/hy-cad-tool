using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Shared.AutoCAD.Metadata;

namespace HyCADTool.Shared.AutoCAD.Selection.Rules
{
    public static class RuleCompletionProvider
    {
        private static readonly string[] NumericOperators = { "==", "!=", ">", "<", ">=", "<=", "between" };
        private static readonly string[] TextOperators = { "==", "!=", "contains", "like", "in" };
        private static readonly string[] BooleanOperators = { "==", "!=" };

        public static IReadOnlyList<RuleCompletionItem> GetCompletions(Entity sampleEntity, string query, bool includeAdvanced = false)
        {
            if (sampleEntity == null)
            {
                return new[]
                {
                    new RuleCompletionItem
                    {
                        Kind = "提示",
                        DisplayText = "请先选择一个样例对象",
                        InsertText = string.Empty
                    }
                };
            }

            var descriptors = RulePropertyCatalog.GetDescriptors(sampleEntity, includeAdvanced).ToList();
            var text = (query ?? string.Empty).TrimEnd();
            var current = GetCurrentCondition(text);
            var tokens = current.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (tokens.Length == 0 || IsLogicalTail(text))
                return BuildFieldItems(descriptors);

            var field = FindDescriptor(descriptors, tokens[0]);
            if (tokens.Length == 1)
            {
                if (field == null)
                    return BuildFieldItems(descriptors.Where(p => p.PropertyName.StartsWith(tokens[0], StringComparison.OrdinalIgnoreCase)));

                return BuildOperatorItems(field);
            }

            if (tokens.Length == 2)
                return BuildValueItems(sampleEntity, field, tokens[1]);

            return BuildLogicalItems();
        }

        public static string ApplyCompletion(string query, RuleCompletionItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.InsertText))
                return query ?? string.Empty;

            var text = query ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return item.InsertText;

            var trimmed = text.TrimEnd();
            if (item.Kind == "字段")
            {
                var prefix = GetPrefixBeforeCurrentCondition(trimmed);
                return prefix + item.InsertText;
            }

            if (item.Kind == "操作符")
                return trimmed + " " + item.InsertText + " ";

            if (item.Kind == "值")
                return trimmed + " " + item.InsertText;

            if (item.Kind == "逻辑")
                return trimmed + " " + item.InsertText + " ";

            return trimmed + " " + item.InsertText;
        }

        private static IReadOnlyList<RuleCompletionItem> BuildFieldItems(IEnumerable<RulePropertyDescriptor> descriptors)
        {
            return descriptors
                .OrderBy(p => p.Priority)
                .ThenBy(p => p.PropertyName)
                .Take(18)
                .Select(p => new RuleCompletionItem
                {
                    Kind = "字段",
                    DisplayText = $"字段 · {p.PropertyName} · {p.DisplayName} · {p.PropertyType}",
                    InsertText = p.PropertyName
                })
                .ToList();
        }

        private static IReadOnlyList<RuleCompletionItem> BuildOperatorItems(RulePropertyDescriptor descriptor)
        {
            return GetOperators(descriptor.PropertyType)
                .Select(op => new RuleCompletionItem
                {
                    Kind = "操作符",
                    DisplayText = $"操作符 · {op}",
                    InsertText = op
                })
                .ToList();
        }

        private static IReadOnlyList<RuleCompletionItem> BuildValueItems(Entity entity, RulePropertyDescriptor descriptor, string op)
        {
            if (descriptor == null)
                return new List<RuleCompletionItem>();

            var actual = descriptor.GetValue(entity);
            var actualText = FormatValue(actual, descriptor.PropertyType);
            var items = new List<RuleCompletionItem>
            {
                new RuleCompletionItem
                {
                    Kind = "值",
                    DisplayText = $"当前值 · {actualText}",
                    InsertText = actualText
                }
            };

            if (string.Equals(op, "between", StringComparison.OrdinalIgnoreCase) && IsNumeric(descriptor.PropertyType))
            {
                items.Add(new RuleCompletionItem
                {
                    Kind = "值",
                    DisplayText = "区间示例 · 100 and 300",
                    InsertText = "100 and 300"
                });
            }
            else if (string.Equals(op, "like", StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new RuleCompletionItem
                {
                    Kind = "值",
                    DisplayText = "通配示例 · \"*road*\"",
                    InsertText = "\"*road*\""
                });
            }

            return items;
        }

        private static IReadOnlyList<RuleCompletionItem> BuildLogicalItems()
        {
            return new[]
            {
                new RuleCompletionItem { Kind = "逻辑", DisplayText = "逻辑 · AND", InsertText = "AND" },
                new RuleCompletionItem { Kind = "逻辑", DisplayText = "逻辑 · OR", InsertText = "OR" }
            };
        }

        private static IEnumerable<string> GetOperators(string propertyType)
        {
            if (IsNumeric(propertyType))
                return NumericOperators;

            if (string.Equals(propertyType, "Boolean", StringComparison.OrdinalIgnoreCase))
                return BooleanOperators;

            return TextOperators;
        }

        private static string FormatValue(object value, string propertyType)
        {
            if (value == null)
                return "\"\"";

            if (IsNumeric(propertyType) || string.Equals(propertyType, "Boolean", StringComparison.OrdinalIgnoreCase))
                return value.ToString();

            return "\"" + value.ToString().Replace("\"", "\\\"") + "\"";
        }

        private static RulePropertyDescriptor FindDescriptor(IEnumerable<RulePropertyDescriptor> descriptors, string field)
        {
            return descriptors.FirstOrDefault(p => string.Equals(p.PropertyName, field, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsNumeric(string propertyType)
        {
            return string.Equals(propertyType, "Double", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyType, "Int32", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyType, "Int64", StringComparison.OrdinalIgnoreCase)
                || string.Equals(propertyType, "Single", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLogicalTail(string text)
        {
            return text.EndsWith(" AND", StringComparison.OrdinalIgnoreCase)
                || text.EndsWith(" OR", StringComparison.OrdinalIgnoreCase)
                || text.EndsWith("&&", StringComparison.OrdinalIgnoreCase)
                || text.EndsWith("||", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetCurrentCondition(string text)
        {
            var index = LastLogicalEndIndex(text);
            return index < 0 ? text.Trim() : text.Substring(index).Trim();
        }

        private static string GetPrefixBeforeCurrentCondition(string text)
        {
            var index = LastLogicalEndIndex(text);
            return index < 0 ? string.Empty : text.Substring(0, index);
        }

        private static int LastLogicalEndIndex(string text)
        {
            var markers = new[] { " AND ", " OR ", " && ", " || " };
            var lastIndex = -1;
            var lastLength = 0;

            foreach (var marker in markers)
            {
                var index = text.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index > lastIndex)
                {
                    lastIndex = index;
                    lastLength = marker.Length;
                }
            }

            return lastIndex < 0 ? -1 : lastIndex + lastLength;
        }
    }
}
