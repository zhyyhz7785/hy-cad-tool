using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HyCADTool.Refactored.Domain.Models.Settlement;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// 解析结果：包含土层列表和从文本中提取的元数据
    /// </summary>
    public class ParseResult
    {
        public List<SoilLayer> Layers { get; set; } = new List<SoilLayer>();

        /// <summary>识别到的表格类型列表</summary>
        public List<string> DetectedTableTypes { get; set; } = new List<string>();

        /// <summary>从文本提取的 γm (kN/m³)，null 表示未检测到</summary>
        public double? GammaM { get; set; }

        /// <summary>从文本提取的地下水埋深 (m)，null 表示未检测到</summary>
        public double? GroundwaterDepth { get; set; }

        /// <summary>解析摘要信息</summary>
        public string Summary { get; set; } = "";
    }

    /// <summary>
    /// 智能 Markdown 表格解析器，支持多表类型识别与按地层编号合并。
    /// 可识别：地层结构表、承载力/压缩模量表、桩基参数表、水文地质表。
    /// </summary>
    public static class MarkdownTableParser
    {
        private enum TableType
        {
            Unknown,
            LayerStructure,
            BearingAndModulus,
            PileDesign,
            Hydrogeology
        }

        /// <summary>
        /// 从 Markdown 文本中解析所有表格，按地层编号合并为统一 SoilLayer 列表
        /// </summary>
        public static ParseResult ParseAll(string markdown)
        {
            var result = new ParseResult();
            if (string.IsNullOrWhiteSpace(markdown)) return result;

            var tables = SplitIntoTables(markdown);
            var layerDict = new Dictionary<string, SoilLayer>(StringComparer.Ordinal);

            foreach (var table in tables)
            {
                var tableType = IdentifyTableType(table.Headers);
                if (tableType == TableType.Unknown) continue;

                string typeName = TableTypeName(tableType);
                if (!result.DetectedTableTypes.Contains(typeName))
                    result.DetectedTableTypes.Add(typeName);

                foreach (var row in table.Rows)
                {
                    MergeRow(layerDict, table.ColumnMap, row, tableType);
                }
            }

            if (layerDict.Count > 0)
                result.Layers = layerDict.Values.ToList();

            ExtractMetadata(markdown, result);

            var parts = new List<string>();
            parts.Add($"{result.Layers.Count} 层");
            if (result.DetectedTableTypes.Count > 0)
                parts.Add("表格: " + string.Join("+", result.DetectedTableTypes));
            if (result.GammaM.HasValue)
                parts.Add($"γm={result.GammaM.Value:F1}");
            if (result.GroundwaterDepth.HasValue)
                parts.Add($"水位={result.GroundwaterDepth.Value:F1}m");
            result.Summary = string.Join("，", parts);

            return result;
        }

        /// <summary>
        /// 向后兼容：仅返回 SoilLayer 列表
        /// </summary>
        public static List<SoilLayer> Parse(string markdown)
        {
            return ParseAll(markdown).Layers;
        }

        /// <summary>
        /// 地层编号归一化键，用于多表合并与增量粘贴（③₁ 与 ③-1 视为同一层）。
        /// </summary>
        public static string CanonicalLayerKey(string idOrName)
        {
            return CanonicalLayerId(idOrName ?? "");
        }

        #region 表格分割

        private class RawTable
        {
            public string[] Headers;
            public Dictionary<string, int> ColumnMap;
            public List<string[]> Rows = new List<string[]>();
        }

        /// <summary>
        /// 将文本拆分为多个独立表格（以表头行为界）
        /// </summary>
        private static List<RawTable> SplitIntoTables(string markdown)
        {
            var tables = new List<RawTable>();
            var lines = markdown.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            RawTable current = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || !line.Contains("|"))
                {
                    if (current != null && current.Rows.Count > 0)
                        current = null;
                    continue;
                }

                if (IsSeparatorRow(line))
                    continue;

                var cells = SplitTableRow(line);
                if (cells.Length < 2) continue;

                if (current == null || IsLikelyHeader(cells))
                {
                    current = new RawTable
                    {
                        Headers = cells,
                        ColumnMap = BuildColumnMap(cells)
                    };
                    tables.Add(current);
                    continue;
                }

                current.Rows.Add(cells);
            }

            return tables;
        }

        /// <summary>
        /// 判断一行是否更像表头而非数据行（包含关键字文字而非纯数字）
        /// </summary>
        private static bool IsLikelyHeader(string[] cells)
        {
            var headerKeywords = new[] { "编号", "层号", "名称", "土层", "厚度", "模量",
                "es", "fak", "qsik", "qpk", "承载力", "侧阻", "端阻",
                "击", "重度", "γ", "项目", "内容", "来源", "备注", "数据" };
            int keywordCount = 0;
            foreach (var cell in cells)
            {
                var lower = cell.ToLowerInvariant().Replace("**", "");
                foreach (var kw in headerKeywords)
                {
                    if (lower.Contains(kw)) { keywordCount++; break; }
                }
            }
            return keywordCount >= 2;
        }

        #endregion

        #region 表头分析与表类型识别

        private static Dictionary<string, int> BuildColumnMap(string[] headers)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < headers.Length; i++)
            {
                var h = headers[i].ToLowerInvariant().Replace("**", "").Trim();

                if (h.Contains("编号") || h.Contains("层号"))
                    map["id"] = i;
                else if ((h.Contains("名称") || h.Contains("土层")) && !h.Contains("项目"))
                    map["name"] = i;
                else if (h.Contains("平均厚") || (h.Contains("厚度") && !h.Contains("范围")))
                {
                    if (!map.ContainsKey("thickness")) map["thickness"] = i;
                }
                else if (h.Contains("厚度"))
                {
                    if (!map.ContainsKey("thickness_range")) map["thickness_range"] = i;
                    if (!map.ContainsKey("thickness")) map["thickness"] = i;
                }
                else if (h.Contains("es") || h.Contains("压缩模量"))
                    map["es"] = i;
                else if (h.Contains("fak") || h.Contains("承载力"))
                    map["fak"] = i;
                else if (h.Contains("γ") || h.Contains("重度"))
                    map["gamma"] = i;
                else if (Regex.IsMatch(h, @"n\s*[\(（]|标贯|击"))
                    map["nspt"] = i;
                else if (h.Contains("qsik") || h.Contains("侧阻"))
                    map["qsik"] = i;
                else if (h.Contains("qpk") || h.Contains("端阻"))
                    map["qpk"] = i;
                else if (h.Contains("状态") || h.Contains("描述") || h.Contains("说明"))
                    map["desc"] = i;
                else if (h.Contains("项目"))
                    map["item"] = i;
                else if (h.Contains("内容"))
                    map["content"] = i;
                else if (h.Contains("备注"))
                    map["remark"] = i;
            }

            if (!map.ContainsKey("id")) map["id"] = 0;
            if (!map.ContainsKey("name") && !map.ContainsKey("item")) map["name"] = 1;

            return map;
        }

        private static TableType IdentifyTableType(string[] headers)
        {
            var joined = string.Join(" ", headers).ToLowerInvariant().Replace("**", "");

            // 工程概况 / 水文说明：项目+内容(或说明/描述)，不作为土层表
            if (joined.Contains("项目") && (joined.Contains("内容") || joined.Contains("说明") || joined.Contains("描述")))
                return TableType.Hydrogeology;

            if (joined.Contains("qsik") || joined.Contains("qpk") || joined.Contains("侧阻") || joined.Contains("端阻"))
                return TableType.PileDesign;

            if (joined.Contains("fak") || joined.Contains("承载力"))
            {
                if (joined.Contains("es") || joined.Contains("模量") || joined.Contains("重度") || joined.Contains("γ"))
                    return TableType.BearingAndModulus;
                return TableType.BearingAndModulus;
            }

            if (joined.Contains("es") || joined.Contains("模量"))
                return TableType.BearingAndModulus;

            // 地层结构：必须像「剖面/分层」表，不能仅凭「名称」误判工程概况(序号|名称|说明)
            bool hasThickness = joined.Contains("厚度");
            bool hasLayerHeader = joined.Contains("编号") || joined.Contains("层号") || joined.Contains("地层编号")
                || joined.Contains("地层") || joined.Contains("土层") || joined.Contains("土类");
            if (hasThickness && (hasLayerHeader || joined.Contains("名称")))
                return TableType.LayerStructure;

            return TableType.Unknown;
        }

        private static string TableTypeName(TableType t)
        {
            switch (t)
            {
                case TableType.LayerStructure: return "地层";
                case TableType.BearingAndModulus: return "承载力/模量";
                case TableType.PileDesign: return "桩基参数";
                case TableType.Hydrogeology: return "水文地质";
                default: return "未知";
            }
        }

        #endregion

        #region 行解析与合并

        /// <summary>
        /// 工程概况类表第一列常为「建设单位」「项目位置」等，不是地层编号。
        /// </summary>
        private static bool IsMetadataKeyLabel(string cell)
        {
            if (string.IsNullOrWhiteSpace(cell)) return false;
            cell = cell.Trim().Replace("**", "");
            return Regex.IsMatch(cell,
                @"^(建设单位|勘察单位|勘察等级|项目位置|工程名称|项目名称|地貌单元|设计范围|建设地点|报告编号|勘察日期|工程地址|委托单位|设计单位|建筑面积|项目概况|场地位置|建设规模|设计依据|勘察依据|地理坐标|水准点|气候特征|场地类别|工程规模|建设内容)");
        }

        /// <summary>
        /// 首列须像地层编号：带圈数字、或 1～2 位数字及可选子层(如 3-1)，否则整行丢弃。
        /// </summary>
        private static bool RowLooksLikeSoilLayer(string rawId, string rawName, TableType tableType)
        {
            rawId = rawId?.Trim().Replace("**", "") ?? "";
            rawName = rawName?.Trim().Replace("**", "") ?? "";
            if (IsMetadataKeyLabel(rawId))
                return false;

            if (Regex.IsMatch(rawId, @"[\u2460-\u2473]"))
                return true;
            if (Regex.IsMatch(rawId, @"^[\d]{1,2}\s*([\-－]\s*[\d]+)?$"))
                return true;

            SplitCombinedIdAndName(rawId, rawName, out string idPart, out _);
            if (IsMetadataKeyLabel(idPart))
                return false;
            if (Regex.IsMatch(idPart, @"[\u2460-\u2473]"))
                return true;
            if (Regex.IsMatch(idPart.Trim(), @"^[\d]{1,2}\s*([\-－]\s*[\d]+)?$"))
                return true;

            // 承载力/桩参表：首列多为带圈编号
            if (tableType == TableType.BearingAndModulus || tableType == TableType.PileDesign)
                return Regex.IsMatch(rawId, @"[\u2460-\u2473]") || Regex.IsMatch(rawId, @"^[\d]{1,2}");

            return false;
        }

        private static void MergeRow(Dictionary<string, SoilLayer> dict,
            Dictionary<string, int> colMap, string[] cells, TableType tableType)
        {
            string GetCell(string key)
            {
                if (!colMap.TryGetValue(key, out int idx) || idx >= cells.Length) return "";
                return cells[idx].Trim().Replace("**", "");
            }

            if (tableType == TableType.Hydrogeology)
                return; // 水文地质表由 ExtractMetadata 处理

            string rawId = GetCell("id");
            string rawName = GetCell("name");
            if (!RowLooksLikeSoilLayer(rawId, rawName, tableType))
                return;

            SplitCombinedIdAndName(rawId, rawName, out string id, out string name);
            id = CanonicalLayerId(id);
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name)) return;

            string key = !string.IsNullOrEmpty(id) ? id : name;

            string dictKey = CanonicalLayerId(key);
            if (!dict.TryGetValue(dictKey, out SoilLayer layer))
            {
                layer = new SoilLayer { Id = id, Name = name };
                dict[dictKey] = layer;
            }

            if (string.IsNullOrEmpty(layer.Name) && !string.IsNullOrEmpty(name))
                layer.Name = name;
            if (string.IsNullOrEmpty(layer.Id) && !string.IsNullOrEmpty(id))
                layer.Id = id;

            switch (tableType)
            {
                case TableType.LayerStructure:
                    MergeIfPositive(ref layer, "Thickness", ParseNumber(GetCell("thickness")));
                    MergeIfPositive(ref layer, "Es", ParseNumber(GetCell("es")));
                    string desc = GetCell("desc");
                    if (!string.IsNullOrEmpty(desc) && string.IsNullOrEmpty(layer.Description))
                        layer.Description = desc;
                    break;

                case TableType.BearingAndModulus:
                    MergeIfPositive(ref layer, "Es", ParseNumber(GetCell("es")));
                    MergeIfPositive(ref layer, "Fak", ParseNumber(GetCell("fak")));
                    MergeIfPositive(ref layer, "Gamma", ParseNumber(GetCell("gamma")));
                    MergeIfPositive(ref layer, "Nspt", ParseNumber(GetCell("nspt")));
                    break;

                case TableType.PileDesign:
                    MergeIfPositive(ref layer, "Qsik", ParseNumber(GetCell("qsik")));
                    MergeIfPositive(ref layer, "Qpk", ParseNumber(GetCell("qpk")));
                    string remark = GetCell("remark");
                    if (!string.IsNullOrEmpty(remark))
                    {
                        if (!string.IsNullOrEmpty(layer.Description))
                            layer.Description += "；" + remark;
                        else
                            layer.Description = remark;
                    }
                    break;
            }
        }

        private static void MergeIfPositive(ref SoilLayer layer, string prop, double val)
        {
            if (val <= 0) return;
            switch (prop)
            {
                case "Thickness": if (layer.Thickness <= 0) layer.Thickness = val; break;
                case "Es": if (layer.Es <= 0) layer.Es = val; break;
                case "Fak": if (layer.Fak <= 0) layer.Fak = val; break;
                case "Gamma": if (layer.Gamma <= 0) layer.Gamma = val; break;
                case "Nspt": if (layer.Nspt <= 0) layer.Nspt = val; break;
                case "Qsik": if (layer.Qsik <= 0) layer.Qsik = val; break;
                case "Qpk": if (layer.Qpk <= 0) layer.Qpk = val; break;
            }
        }

        /// <summary>
        /// 将地层编号中的 Markdown 粗体等去掉
        /// </summary>
        private static string NormalizeLayerId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "";
            return id.Replace("**", "").Trim();
        }

        /// <summary>
        /// 地勘表常见：第一列将「编号+名称」写在一起，如 "② 粉质黏土"、"③-1 粉砂"、"③₁ 粉砂"。
        /// 若名称列有独立内容则优先采用名称列。
        /// </summary>
        private static void SplitCombinedIdAndName(string rawId, string rawName, out string id, out string name)
        {
            id = "";
            name = rawName?.Replace("**", "").Trim() ?? "";
            rawId = rawId?.Replace("**", "").Trim() ?? "";
            if (string.IsNullOrEmpty(rawId))
                return;

            // ①～⑳ + 可选 "-1"/下标 + 空白 + 余下为土层名称
            var m = Regex.Match(rawId,
                @"^([\u2460-\u2473](?:\s*[-－﹣]\s*[\d]+|[\u2080-\u2089]+)?)\s+(.+)$");
            if (m.Success)
            {
                id = m.Groups[1].Value.Trim();
                string nameFromCombined = m.Groups[2].Value.Trim();
                if (string.IsNullOrEmpty(name))
                    name = nameFromCombined;
                return;
            }

            id = rawId;
        }

        /// <summary>
        /// 统一编号形式，便于多表合并：全角减号→半角、③₁→③-1；
        /// 若整格仍为「② 粉质黏土」形式，只取编号段作键。
        /// </summary>
        private static string CanonicalLayerId(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "";
            id = NormalizeLayerId(id);
            SplitCombinedIdAndName(id, "", out string idOnly, out _);
            if (!string.IsNullOrEmpty(idOnly))
                id = idOnly;
            id = id.Replace("﹣", "-").Replace("－", "-").Replace("–", "-");
            // ③₁ → ③-1
            id = Regex.Replace(id, @"([\u2460-\u2473])([\u2080-\u2089]+)",
                m => m.Groups[1].Value + "-" + SubscriptDigitsToAscii(m.Groups[2].Value));
            return id.Trim();
        }

        private static string SubscriptDigitsToAscii(string sub)
        {
            if (string.IsNullOrEmpty(sub)) return "";
            var map = new System.Collections.Generic.Dictionary<char, char>
            {
                ['₀'] = '0', ['₁'] = '1', ['₂'] = '2', ['₃'] = '3', ['₄'] = '4',
                ['₅'] = '5', ['₆'] = '6', ['₇'] = '7', ['₈'] = '8', ['₉'] = '9'
            };
            var sb = new System.Text.StringBuilder();
            foreach (char c in sub)
            {
                if (map.TryGetValue(c, out char d)) sb.Append(d);
                else if (char.IsDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        #endregion

        #region 元数据提取

        private static void ExtractMetadata(string markdown, ParseResult result)
        {
            // γm：覆土平均重度
            result.GammaM = TryExtractDouble(markdown,
                @"(?:γm|γ_m|gamma_?m|覆土.*?重度|平均重度)\s*[=:：]\s*([\d]+\.?[\d]*)");

            // 地下水埋深
            var gwMatch = Regex.Match(markdown,
                @"(?:稳定水位|地下水位|水位).*?(?:埋深|深度)\s*[=:：]?\s*([\d]+\.?[\d]*)\s*[～~\-]\s*([\d]+\.?[\d]*)",
                RegexOptions.IgnoreCase);
            if (gwMatch.Success)
            {
                if (double.TryParse(gwMatch.Groups[1].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double d1) &&
                    double.TryParse(gwMatch.Groups[2].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double d2))
                {
                    result.GroundwaterDepth = (d1 + d2) / 2.0;
                }
            }
            else
            {
                result.GroundwaterDepth = TryExtractDouble(markdown,
                    @"(?:稳定水位|地下水位|水位).*?(?:埋深|深度)\s*[=:：]?\s*([\d]+\.?[\d]*)");
            }

            // 如果表格中有各层 γ，尝试估算 γm（取平均）
            if (!result.GammaM.HasValue && result.Layers.Count > 0)
            {
                var withGamma = result.Layers.Where(l => l.Gamma > 0).ToList();
                if (withGamma.Count > 0)
                {
                    double totalWeight = 0, totalThickness = 0;
                    foreach (var l in withGamma)
                    {
                        double t = l.Thickness > 0 ? l.Thickness : 1.0;
                        totalWeight += l.Gamma * t;
                        totalThickness += t;
                    }
                    if (totalThickness > 0)
                        result.GammaM = Math.Round(totalWeight / totalThickness, 1);
                }
            }
        }

        private static double? TryExtractDouble(string text, string pattern)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (match.Success && double.TryParse(match.Groups[1].Value,
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double val) && val > 0)
                return val;
            return null;
        }

        #endregion

        #region 工具方法

        private static string[] SplitTableRow(string line)
        {
            line = line.Trim();
            if (line.StartsWith("|")) line = line.Substring(1);
            if (line.EndsWith("|")) line = line.Substring(0, line.Length - 1);
            var parts = line.Split('|');
            for (int i = 0; i < parts.Length; i++)
                parts[i] = parts[i].Trim();
            return parts;
        }

        private static bool IsSeparatorRow(string line)
        {
            var stripped = line.Replace("|", "").Replace("-", "").Replace(":", "").Replace(" ", "");
            return stripped.Length == 0;
        }

        /// <summary>
        /// 从可能含单位、粗体、特殊字符的字符串中提取数值
        /// </summary>
        private static double ParseNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            text = text.Replace("**", "").Replace("*", "").Replace("—", "").Replace("-", "").Trim();
            if (string.IsNullOrEmpty(text)) return 0;

            var match = Regex.Match(text, @"[\d]+\.?[\d]*");
            if (match.Success && double.TryParse(match.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double val))
                return val;

            return 0;
        }

        #endregion
    }
}
