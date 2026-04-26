using System;
using HyCADTool.Domain.ValueObjects;

namespace HyCADTool.Domain.Services
{
    /// <summary>
    /// 标高文本解析器
    /// 提供平台无关的标高文本解析功能
    /// </summary>
    public static class ElevationParser
    {
        /// <summary>
        /// 解析标高文本
        /// 
        /// 支持格式：
        /// - "±0.000" 或 "%%P0.000" → 0.0m
        /// - "+2.000" → 2.0m
        /// - "-5.000" → -5.0m
        /// - "0.700" → 0.7m
        /// </summary>
        /// <param name="text">标高文本</param>
        /// <param name="elevation">解析后的标高对象</param>
        /// <returns>true 表示解析成功</returns>
        public static bool TryParse(string text, out Elevation elevation)
        {
            elevation = Elevation.FromMeters(0);
            
            if (string.IsNullOrWhiteSpace(text))
                return false;
            
            text = PreprocessText(text);
            
            // 特殊处理：±0.000 视为 0
            if (IsZeroElevation(text))
            {
                elevation = Elevation.FromMeters(0.0);
                return true;
            }
            
            // 使用正则表达式提取数字（支持正负号和小数）
            var pattern = @"[-+]?[0-9]*\.?[0-9]+";
            var match = System.Text.RegularExpressions.Regex.Match(text, pattern);
            
            if (match.Success && double.TryParse(match.Value, out double value))
            {
                elevation = Elevation.FromMeters(value);
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// 文本预处理
        /// </summary>
        private static string PreprocessText(string text)
        {
            return text.Trim()
                       .Replace("%%P", "")  // AutoCAD 的 ± 符号编码
                       .Replace("±", "")
                       .Replace("标高", "")
                       .Replace(":", "")
                       .Trim();
        }
        
        /// <summary>
        /// 判断是否为零标高
        /// </summary>
        private static bool IsZeroElevation(string text)
        {
            return text.Contains("%%P0.000") || 
                   text == "0.000" || 
                   text.Contains("±0.000") || 
                   text == "±" ||
                   text == "0" ||
                   text == "+0" ||
                   text == "-0";
        }
    }
}

