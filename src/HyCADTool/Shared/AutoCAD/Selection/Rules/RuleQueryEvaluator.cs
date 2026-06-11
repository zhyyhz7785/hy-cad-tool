using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Shared.AutoCAD.Metadata;

namespace HyCADTool.Shared.AutoCAD.Selection.Rules
{
    public static class RuleQueryEvaluator
    {
        private const double NumericTolerance = 1e-6;

        /// <summary>
        /// 结构化单条件求值（不经字符串解析）。
        /// </summary>
        public static bool MatchesCondition(Entity entity, string propertyName, string op, string valueText)
        {
            if (entity == null || string.IsNullOrWhiteSpace(propertyName) || string.IsNullOrWhiteSpace(op))
                return false;

            if (!RulePropertyCatalog.TryGetValue(entity, propertyName, out var value, out var type, includeAdvanced: true))
                return false;

            return Compare(value, type, NormalizeOperator(op), valueText ?? string.Empty, null);
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
                    case "==": return Math.Abs(n - target) < NumericTolerance;
                    case "!=": return Math.Abs(n - target) >= NumericTolerance;
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

        private static string NormalizeOperator(string op)
        {
            if (string.Equals(op, "=", StringComparison.OrdinalIgnoreCase))
                return "==";
            return op.ToLowerInvariant();
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
    }
}
