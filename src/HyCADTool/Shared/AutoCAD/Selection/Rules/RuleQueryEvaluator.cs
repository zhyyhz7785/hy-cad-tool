using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Shared.AutoCAD.Extensions;

namespace HyCADTool.Shared.AutoCAD.Selection.Rules
{
    public static class RuleQueryEvaluator
    {
        public static ObjectId[] Filter(Database database, ObjectId[] ids, string query)
        {
            if (database == null || ids == null || ids.Length == 0 || string.IsNullOrWhiteSpace(query))
                return new ObjectId[0];

            var normalized = Normalize(query);
            var result = new List<ObjectId>();

            using (var tr = database.TransactionManager.StartTransaction())
            {
                foreach (var id in ids)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead, false) is Entity entity))
                        continue;

                    try
                    {
                        if (Evaluate(entity, normalized))
                            result.Add(id);
                    }
                    catch
                    {
                        // Invalid rule fragments simply do not match this entity.
                    }
                }

                tr.Commit();
            }

            return result.ToArray();
        }

        public static bool CanParse(string query)
        {
            return !string.IsNullOrWhiteSpace(query) && TryParseCondition(Normalize(query), out _);
        }

        public static string Preview(string query)
        {
            var normalized = Normalize(query);
            if (ContainsTopLevel(normalized, "||") || ContainsTopLevelWord(normalized, "OR"))
                return "CompositePredicate(Or)";
            if (ContainsTopLevel(normalized, "&&") || ContainsTopLevelWord(normalized, "AND"))
                return "CompositePredicate(And)";
            if (TryParseCondition(normalized, out var condition))
                return $"PropertyPredicate({condition.PropertyName} {condition.OperatorText} {condition.ValueText})";

            return "FunctionPredicate / 未识别片段";
        }

        public static IReadOnlyList<string> GetFieldNames(string query)
        {
            var fields = new List<string>();
            CollectFieldNames(Normalize(query), fields);
            return fields;
        }

        private static bool Evaluate(Entity entity, string query)
        {
            var orParts = SplitLogical(query, "||", "OR");
            if (orParts.Count > 1)
                return orParts.Any(part => Evaluate(entity, part));

            var andParts = SplitLogical(query, "&&", "AND");
            if (andParts.Count > 1)
                return andParts.All(part => Evaluate(entity, part));

            return EvaluateCondition(entity, query);
        }

        private static void CollectFieldNames(string query, ICollection<string> fields)
        {
            var orParts = SplitLogical(query, "||", "OR");
            if (orParts.Count > 1)
            {
                foreach (var part in orParts)
                    CollectFieldNames(part, fields);
                return;
            }

            var andParts = SplitLogical(query, "&&", "AND");
            if (andParts.Count > 1)
            {
                foreach (var part in andParts)
                    CollectFieldNames(part, fields);
                return;
            }

            if (TryParseCondition(query, out var condition))
                fields.Add(condition.PropertyName);
        }

        private static bool EvaluateCondition(Entity entity, string query)
        {
            if (!TryParseCondition(query, out var condition))
                return false;

            var props = entity.GetFilterableProperties();
            if (!props.TryGetValue(condition.PropertyName, out var prop))
                return false;

            return Compare(prop.Value, prop.Type, condition.OperatorText, condition.ValueText, condition.SecondValueText);
        }

        private static bool TryParseCondition(string query, out RuleCondition condition)
        {
            condition = null;
            var text = TrimOuterParentheses(query?.Trim() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var between = Regex.Match(text, @"^(?<field>\w+)\s+between\s+(?<a>.+?)\s+and\s+(?<b>.+)$", RegexOptions.IgnoreCase);
            if (between.Success)
            {
                condition = new RuleCondition
                {
                    PropertyName = between.Groups["field"].Value,
                    OperatorText = "between",
                    ValueText = between.Groups["a"].Value,
                    SecondValueText = between.Groups["b"].Value
                };
                return true;
            }

            var match = Regex.Match(text, @"^(?<field>\w+)\s*(?<op>==|!=|>=|<=|>|<|=|contains|like|LIKE|in|IN)\s*(?<value>.+)$", RegexOptions.IgnoreCase);
            if (!match.Success)
                return false;

            condition = new RuleCondition
            {
                PropertyName = match.Groups["field"].Value,
                OperatorText = NormalizeOperator(match.Groups["op"].Value),
                ValueText = match.Groups["value"].Value
            };
            return true;
        }

        private static bool Compare(object actual, string actualType, string op, string rawValue, string rawSecondValue)
        {
            var actualString = actual?.ToString() ?? string.Empty;
            var value = TrimQuotes(rawValue);

            if (op == "contains")
                return actualString.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

            if (op == "like")
                return WildcardMatch(actualString, value);

            if (op == "in")
                return value.Trim('(', ')')
                    .Split(',')
                    .Select(TrimQuotes)
                    .Any(v => string.Equals(actualString, v, StringComparison.OrdinalIgnoreCase));

            if (op == "between")
            {
                if (!TryDouble(actual, out var n) || !TryDouble(value, out var a) || !TryDouble(TrimQuotes(rawSecondValue), out var b))
                    return false;
                return n >= Math.Min(a, b) && n <= Math.Max(a, b);
            }

            if (IsNumericType(actualType))
            {
                if (!TryDouble(actual, out var n) || !TryDouble(value, out var target))
                    return false;

                switch (op)
                {
                    case "==": return Math.Abs(n - target) < 1e-9;
                    case "!=": return Math.Abs(n - target) >= 1e-9;
                    case ">": return n > target;
                    case "<": return n < target;
                    case ">=": return n >= target;
                    case "<=": return n <= target;
                }
            }

            var compare = string.Compare(actualString, value, StringComparison.OrdinalIgnoreCase);
            switch (op)
            {
                case "==": return compare == 0;
                case "!=": return compare != 0;
                default: return false;
            }
        }

        private static string Normalize(string query)
        {
            return (query ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Trim();
        }

        private static string NormalizeOperator(string op)
        {
            if (string.Equals(op, "=", StringComparison.OrdinalIgnoreCase))
                return "==";
            return op.ToLowerInvariant();
        }

        private static List<string> SplitLogical(string query, string symbol, string word)
        {
            var parts = query.Split(new[] { symbol }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();
            if (parts.Count > 1)
                return parts;

            return Regex.Split(query, $@"\s+{word}\s+", RegexOptions.IgnoreCase)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();
        }

        private static bool ContainsTopLevel(string query, string symbol)
        {
            return query?.IndexOf(symbol, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsTopLevelWord(string query, string word)
        {
            return Regex.IsMatch(query ?? string.Empty, $@"\s+{word}\s+", RegexOptions.IgnoreCase);
        }

        private static string TrimOuterParentheses(string text)
        {
            while (text.StartsWith("(") && text.EndsWith(")") && text.Length > 1)
                text = text.Substring(1, text.Length - 2).Trim();
            return text;
        }

        private static string TrimQuotes(string value)
        {
            return (value ?? string.Empty).Trim().Trim('"', '\'');
        }

        private static bool TryDouble(object value, out double result)
        {
            return double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out result)
                || double.TryParse(Convert.ToString(value), out result);
        }

        private static bool IsNumericType(string type)
        {
            return string.Equals(type, "Double", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "Int32", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "Int64", StringComparison.OrdinalIgnoreCase)
                || string.Equals(type, "Single", StringComparison.OrdinalIgnoreCase);
        }

        private static bool WildcardMatch(string actual, string pattern)
        {
            var regex = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";
            return Regex.IsMatch(actual ?? string.Empty, regex, RegexOptions.IgnoreCase);
        }

        private sealed class RuleCondition
        {
            public string PropertyName { get; set; }
            public string OperatorText { get; set; }
            public string ValueText { get; set; }
            public string SecondValueText { get; set; }
        }
    }
}
