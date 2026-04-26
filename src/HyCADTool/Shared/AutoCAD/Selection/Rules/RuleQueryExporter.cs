using System;
using System.Text.RegularExpressions;

namespace HyCADTool.Shared.AutoCAD.Selection.Rules
{
    public static class RuleQueryExporter
    {
        public static string Export(string query, string exportKind)
        {
            var text = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            switch (exportKind)
            {
                case "C#":
                    return ToCSharp(text);
                case "WHERE":
                    return ToWhere(text);
                case "Python":
                    return ToPython(text);
                default:
                    return text;
            }
        }

        private static string ToCSharp(string query)
        {
            var text = ToExpression(query);
            text = Regex.Replace(text, @"(?<field>\w+)\s+contains\s+(?<value>""[^""]*""|'[^']*')",
                m => $"GetString(entity, \"{m.Groups["field"].Value}\").Contains({ToDoubleQuoted(m.Groups["value"].Value)})",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<field>\w+)\s+like\s+(?<value>""[^""]*""|'[^']*')",
                m => $"WildcardMatch(GetString(entity, \"{m.Groups["field"].Value}\"), {ToDoubleQuoted(m.Groups["value"].Value)})",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<field>\w+)\s+between\s+(?<a>.+?)\s+and\s+(?<b>[^\s\)]+)",
                m => $"Between(GetDouble(entity, \"{m.Groups["field"].Value}\"), {m.Groups["a"].Value}, {m.Groups["b"].Value})",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bAND\b", "&&", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bOR\b", "||", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<![!<>=])=(?!=)", "==");

            return $"ids = ids.Where(id => MatchEntityRule(id, entity => {text}));";
        }

        private static string ToWhere(string query)
        {
            var text = query.Trim();
            text = Regex.Replace(text, @"\b&&\b", "AND");
            text = text.Replace("&&", " AND ").Replace("||", " OR ");
            text = Regex.Replace(text, @"\bcontains\b", "LIKE", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"==", "=");
            text = Regex.Replace(text, @"""([^""]*)""", "'$1'");
            return $"WHERE {text}";
        }

        private static string ToPython(string query)
        {
            var text = ToExpression(query);
            text = Regex.Replace(text, @"(?<field>\w+)\s+contains\s+(?<value>""[^""]*""|'[^']*')",
                m => $"{ToSingleQuoted(m.Groups["value"].Value)} in str(props.get('{m.Groups["field"].Value}', ''))",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<field>\w+)\s+like\s+(?<value>""[^""]*""|'[^']*')",
                m => $"fnmatch(str(props.get('{m.Groups["field"].Value}', '')), {ToSingleQuoted(m.Groups["value"].Value)})",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<field>\w+)\s+between\s+(?<a>.+?)\s+and\s+(?<b>[^\s\)]+)",
                m => $"{m.Groups["a"].Value} <= float(props.get('{m.Groups["field"].Value}', 0)) <= {m.Groups["b"].Value}",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bAND\b|&&", "and", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\bOR\b|\|\|", "or", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?<field>\b[A-Za-z_]\w*\b)\s*(?<op>==|!=|>=|<=|>|<|=)\s*(?<value>""[^""]*""|'[^']*'|[^\s\)]+)",
                m => $"props.get('{m.Groups["field"].Value}') {NormalizePythonOperator(m.Groups["op"].Value)} {ToSingleQuoted(m.Groups["value"].Value)}");

            return $"# pseudo arcpy-style rule\nselected = [e for e in entities if {text}]";
        }

        private static string ToExpression(string query)
        {
            return query.Trim();
        }

        private static string NormalizePythonOperator(string op)
        {
            return op == "=" ? "==" : op;
        }

        private static string ToDoubleQuoted(string value)
        {
            return "\"" + TrimQuotes(value).Replace("\"", "\\\"") + "\"";
        }

        private static string ToSingleQuoted(string value)
        {
            return "'" + TrimQuotes(value).Replace("'", "\\'") + "'";
        }

        private static string TrimQuotes(string value)
        {
            return (value ?? string.Empty).Trim().Trim('"', '\'');
        }
    }
}
