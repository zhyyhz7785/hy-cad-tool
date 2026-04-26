using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 文本 → <see cref="PiElement"/> 列表的纯字符串解析器。100% 无 AutoCAD 依赖，便于单元测试。
    ///
    /// 支持场景：
    /// - CSV 文件（"导出自 Excel / Civil 3D Alignment Report"）；
    /// - 用户从 Excel 复制到剪贴板的 Tab 分隔文本；
    /// - 手写混合格式（中文逗号 / 空格 / 分号 / 多空格，都归一化为单一分隔符）。
    ///
    /// 列顺序固定：<c>x, y, [R, Ls_in, Ls_out, tag]</c>。后四列可缺省；仅首点、末点的 R / Ls 不参与几何（首尾只用坐标）。
    ///
    /// 宽松规则（尽量接受设计院真实现场的粗糙输入）：
    /// - UTF-8 BOM 自动剥除；
    /// - 行首/行尾空白忽略；
    /// - 空行忽略；
    /// - 以 <c>#</c>、<c>//</c> 或 `--` 开头的注释行忽略；
    /// - 首行若整行全是非数字（例如"X,Y,R,Ls_in,Ls_out,Tag"），自动识别为表头跳过；
    /// - 分隔符：Tab / 英文逗号 / 中文逗号 / 分号 / 连续空白都视作一个分隔符。
    /// </summary>
    public static class PiElementTextParser
    {
        // 全角空格 \u3000、Tab、英/中文逗号、分号、连续空白都视作分隔符
        private static readonly char[] Separators = new[] { '\t', ',', '，', ';', '；', ' ', '\u3000' };

        /// <summary>
        /// 解析文本为 <see cref="PiElement"/> 列表。解析成功返回 true，失败 / 行级错误全收集到 <paramref name="errors"/>。
        /// 即使部分行解析失败，成功行仍会保留到 <paramref name="elements"/>，命令层自己决定"全有全无"或"跳过坏行"。
        /// </summary>
        public static bool TryParse(string text, out List<PiElement> elements, out List<string> errors)
        {
            elements = new List<PiElement>();
            errors = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
            {
                errors.Add("输入文本为空。");
                return false;
            }

            // 剥 UTF-8 BOM
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);

            using (var reader = new StringReader(text))
            {
                int lineNo = 0;
                bool headerHandled = false;
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNo++;
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0) continue;
                    if (IsCommentLine(trimmed)) continue;

                    // 第一行若全非数字则视为表头，跳过一次
                    if (!headerHandled)
                    {
                        headerHandled = true;
                        if (LooksLikeHeader(trimmed)) continue;
                    }

                    if (TryParseRow(trimmed, lineNo, out var element, out var rowError))
                    {
                        elements.Add(element);
                    }
                    else
                    {
                        errors.Add(rowError);
                    }
                }
            }

            if (elements.Count < 2)
            {
                errors.Add($"有效 PI 点不足 2 个（当前 {elements.Count}），无法构造平面线位。");
                return false;
            }

            return errors.Count == 0;
        }

        private static bool IsCommentLine(string line)
        {
            if (line.StartsWith("#", StringComparison.Ordinal)) return true;
            if (line.StartsWith("//", StringComparison.Ordinal)) return true;
            if (line.StartsWith("--", StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool LooksLikeHeader(string line)
        {
            // 拆成 token 后，若所有 token 都不是数字即视为表头
            var tokens = line.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return false;
            foreach (var t in tokens)
            {
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return false;
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.CurrentCulture, out _)) return false;
            }
            return true;
        }

        private static bool TryParseRow(string row, int lineNo, out PiElement element, out string error)
        {
            element = default;
            var tokens = row.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 2)
            {
                error = $"第 {lineNo} 行：列数不足（至少需要 X, Y）。原文：{row}";
                return false;
            }

            if (!TryParseDouble(tokens[0], out double x))
            {
                error = $"第 {lineNo} 行：X 坐标不是有效数字（{tokens[0]}）。";
                return false;
            }
            if (!TryParseDouble(tokens[1], out double y))
            {
                error = $"第 {lineNo} 行：Y 坐标不是有效数字（{tokens[1]}）。";
                return false;
            }

            double radius = 0;
            double lsIn = 0;
            double lsOut = 0;
            string tag = null;

            if (tokens.Length >= 3 && !TryParseOptional(tokens[2], out radius))
            {
                error = $"第 {lineNo} 行：R 不是有效数字（{tokens[2]}）。";
                return false;
            }
            if (tokens.Length >= 4 && !TryParseOptional(tokens[3], out lsIn))
            {
                error = $"第 {lineNo} 行：Ls_in 不是有效数字（{tokens[3]}）。";
                return false;
            }
            if (tokens.Length >= 5 && !TryParseOptional(tokens[4], out lsOut))
            {
                error = $"第 {lineNo} 行：Ls_out 不是有效数字（{tokens[4]}）。";
                return false;
            }
            if (tokens.Length >= 6)
            {
                tag = tokens[5];
            }

            element = new PiElement(new Point2D(x, y), radius, lsIn, lsOut, tag);
            error = null;
            return true;
        }

        private static bool TryParseDouble(string s, out double value)
        {
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return true;
            return false;
        }

        /// <summary>空串 / "-" / "na" 视为 0，方便 CSV 留空。</summary>
        private static bool TryParseOptional(string s, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return true;
            if (string.Equals(s, "-", StringComparison.Ordinal)) return true;
            if (string.Equals(s, "na", StringComparison.OrdinalIgnoreCase)) return true;
            return TryParseDouble(s, out value);
        }
    }
}
