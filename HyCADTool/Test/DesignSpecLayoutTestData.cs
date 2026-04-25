using System;
using System.Text;
using HyCADTool.Domain.Models.Text;

namespace HyCADTool.Test
{
    /// <summary>
    /// 设计说明栏宽回归测试数据（纯计算，无 AutoCAD 依赖）。
    /// 用于 TrueType/SHX 对照测试时快速生成样本与期望值。
    /// </summary>
    public static class DesignSpecLayoutTestData
    {
        public static void ApplyTrueTypePreset(DesignSpecConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.FontFileName = "Microsoft YaHei";
            config.BigFontFileName = string.Empty;
            config.BoldFontName = "Microsoft YaHei";
        }

        public static void ApplyShxPreset(DesignSpecConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.FontFileName = "tssdeng.shx";
            config.BigFontFileName = "hztxt.shx";
            config.BoldFontName = "SimHei";
        }

        public static double GetExpectedColumnWidthMm(
            double pageWidthMm,
            double marginLeftMm,
            double marginRightMm,
            double columnGutterMm,
            int columnCount,
            double scale)
        {
            int cols = Math.Max(1, columnCount);
            double available = (pageWidthMm - marginLeftMm - marginRightMm) * scale;
            double gutterTotal = Math.Max(0, columnGutterMm) * scale * Math.Max(0, cols - 1);
            return (available - gutterTotal) / cols;
        }

        public static int GetExpectedCharsPerColumn(DesignSpecConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            double colWidth = config.GetColumnWidth(0);
            double charWidth = Math.Max(0.01, config.TextSize * config.TextXScale * config.Scale);
            return Math.Max(1, (int)Math.Floor(colWidth / charWidth));
        }

        public static string BuildAsciiLine(int length)
        {
            if (length <= 0) return string.Empty;
            return new string('A', length);
        }

        public static string BuildCjkLine(int length)
        {
            if (length <= 0) return string.Empty;
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
                sb.Append('\u4E2D');
            return sb.ToString();
        }

        public static string BuildMixedLine(int length)
        {
            if (length <= 0) return string.Empty;
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
                sb.Append(i % 2 == 0 ? '\u4E2D' : 'A');
            return sb.ToString();
        }

        /// <summary>
        /// 根据实测每行字符数与理论值计算校正系数。
        /// 系数 > 1 代表实测更宽松（每行容纳更多字符）。
        /// </summary>
        public static double GetCalibrationFactor(int measuredCharsPerLine, int expectedCharsPerLine)
        {
            int safeExpected = Math.Max(1, expectedCharsPerLine);
            int safeMeasured = Math.Max(1, measuredCharsPerLine);
            return (double)safeMeasured / safeExpected;
        }

        /// <summary>
        /// 生成 N 与 N+1 边界测试段落（用于观察是否在第 N+1 个字符换行）。
        /// </summary>
        public static string BuildWrapBoundarySample(int expectedCharsPerLine, bool cjk)
        {
            int n = Math.Max(1, expectedCharsPerLine);
            string lineN = cjk ? BuildCjkLine(n) : BuildAsciiLine(n);
            string lineNPlusOne = cjk ? BuildCjkLine(n + 1) : BuildAsciiLine(n + 1);
            return "# WrapBoundary\n\n"
                + "N:\n\n" + lineN + "\n\n"
                + "N+1:\n\n" + lineNPlusOne + "\n";
        }

        public static string BuildComparisonSummary(
            int trueTypeMeasuredChars,
            int shxMeasuredChars,
            int expectedChars)
        {
            double ttfFactor = GetCalibrationFactor(trueTypeMeasuredChars, expectedChars);
            double shxFactor = GetCalibrationFactor(shxMeasuredChars, expectedChars);
            return $"Expected={expectedChars}, TrueType={trueTypeMeasuredChars} (x{ttfFactor:0.###}), "
                + $"SHX={shxMeasuredChars} (x{shxFactor:0.###})";
        }
    }
}
