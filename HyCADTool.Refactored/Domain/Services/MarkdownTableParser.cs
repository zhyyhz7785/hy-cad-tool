using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HyCADTool.Refactored.Domain.Models.Settlement;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// 解析粘贴的 Markdown 表格为 SoilLayer 列表
    /// 期望列序：编号 | 名称 | 厚度(或厚度范围) | 平均厚度 | Es(MPa) | 状态描述 | ...
    /// 也支持简化列：编号 | 名称 | 厚度 | Es | 描述
    /// </summary>
    public static class MarkdownTableParser
    {
        /// <summary>
        /// 从 Markdown 表格文本解析土层列表
        /// </summary>
        public static List<SoilLayer> Parse(string markdown)
        {
            var result = new List<SoilLayer>();
            if (string.IsNullOrWhiteSpace(markdown)) return result;

            var lines = markdown.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            int headerIndex = -1;
            int[] columnMap = null; // 映射：0=Id, 1=Name, 2=Thickness, 3=Es, 4=Description

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line) || !line.Contains("|")) continue;

                var cells = SplitTableRow(line);
                if (cells.Length < 3) continue;

                if (IsSeparatorRow(line))
                    continue;

                if (headerIndex < 0)
                {
                    columnMap = DetectColumns(cells);
                    headerIndex = i;
                    continue;
                }

                var layer = ParseRow(cells, columnMap);
                if (layer != null)
                    result.Add(layer);
            }

            return result;
        }

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
        /// 根据表头文字推断列映射
        /// </summary>
        private static int[] DetectColumns(string[] headerCells)
        {
            int idCol = -1, nameCol = -1, thicknessCol = -1, esCol = -1, descCol = -1;

            for (int i = 0; i < headerCells.Length; i++)
            {
                var h = headerCells[i].ToLowerInvariant();

                if (h.Contains("编号") || h.Contains("层号"))
                    idCol = i;
                else if (h.Contains("名称") || h.Contains("土层"))
                    nameCol = i;
                else if (h.Contains("es") || h.Contains("压缩模量") || h.Contains("模量"))
                    esCol = i;
                else if (h.Contains("平均厚") || (h.Contains("厚度") && !h.Contains("范围")))
                {
                    if (thicknessCol < 0) thicknessCol = i;
                }
                else if (h.Contains("厚度"))
                {
                    if (thicknessCol < 0) thicknessCol = i;
                }
                else if (h.Contains("状态") || h.Contains("描述") || h.Contains("说明"))
                    descCol = i;
            }

            // 如果没检测到平均厚度列但有厚度范围列，取下一列
            if (thicknessCol < 0) thicknessCol = 2;
            if (idCol < 0) idCol = 0;
            if (nameCol < 0) nameCol = 1;

            return new[] { idCol, nameCol, thicknessCol, esCol, descCol };
        }

        private static SoilLayer ParseRow(string[] cells, int[] map)
        {
            string GetCell(int colIndex)
            {
                if (colIndex < 0 || colIndex >= cells.Length) return "";
                return cells[colIndex].Trim();
            }

            var id = GetCell(map[0]);
            var name = GetCell(map[1]);
            var thicknessStr = GetCell(map[2]);
            var esStr = map[3] >= 0 ? GetCell(map[3]) : "";
            var desc = map[4] >= 0 ? GetCell(map[4]) : "";

            // 跳过表头重复行或空行
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(name)) return null;

            double thickness = ParseNumber(thicknessStr);
            double es = ParseNumber(esStr);

            // 厚度为 0 且无 Es → 可能是无效行（如 "揭露最大 6.9" 的末层）
            if (thickness <= 0 && es <= 0) return null;

            return new SoilLayer
            {
                Id = id,
                Name = name,
                Thickness = thickness,
                Es = es,
                Description = desc
            };
        }

        /// <summary>
        /// 从可能含单位、粗体、特殊字符的字符串中提取数值
        /// </summary>
        private static double ParseNumber(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            // 去除 Markdown 粗体标记
            text = text.Replace("**", "");

            // 尝试提取第一个数值（支持小数点）
            var match = Regex.Match(text, @"[\d]+\.?[\d]*");
            if (match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                return val;

            return 0;
        }
    }
}
