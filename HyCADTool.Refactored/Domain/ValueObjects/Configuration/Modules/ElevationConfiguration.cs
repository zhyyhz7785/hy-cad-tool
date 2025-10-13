using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Configuration.Modules
{
    /// <summary>
    /// 标高配置值对象
    /// 用于标高符号绘制和标注的相关参数
    /// </summary>
    public class ElevationConfiguration
    {
        /// <summary>
        /// 标高符号长度
        /// </summary>
        public double SymbolLength { get; set; }

        /// <summary>
        /// 标高文字格式（使用 string.Format 格式，如 "±{0:F3}"）
        /// </summary>
        public string TextFormat { get; set; }

        /// <summary>
        /// 是否显示单位
        /// </summary>
        public bool ShowUnit { get; set; }

        /// <summary>
        /// 单位文本（如 "m"）
        /// </summary>
        public string UnitText { get; set; }

        /// <summary>
        /// 标高符号类型（三角形、矩形等）
        /// </summary>
        public string SymbolType { get; set; }

        /// <summary>
        /// 标高符号大小
        /// </summary>
        public double SymbolSize { get; set; }

        /// <summary>
        /// 文字偏移距离
        /// </summary>
        public double TextOffset { get; set; }

        /// <summary>
        /// 小数位数
        /// </summary>
        public int DecimalPlaces { get; set; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public ElevationConfiguration()
        {
            SymbolLength = 2.0;
            TextFormat = "±{0:F3}";
            ShowUnit = false;
            UnitText = "m";
            SymbolType = "Triangle";
            SymbolSize = 1.0;
            TextOffset = 0.5;
            DecimalPlaces = 3;
        }

        /// <summary>
        /// 验证配置是否有效
        /// </summary>
        public bool IsValid(out string error)
        {
            if (SymbolLength <= 0)
            {
                error = "标高符号长度必须大于 0";
                return false;
            }

            if (string.IsNullOrWhiteSpace(TextFormat))
            {
                error = "标高文字格式不能为空";
                return false;
            }

            if (SymbolSize <= 0)
            {
                error = "标高符号大小必须大于 0";
                return false;
            }

            if (DecimalPlaces < 0)
            {
                error = "小数位数不能为负";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static ElevationConfiguration CreateDefault()
        {
            return new ElevationConfiguration
            {
                SymbolLength = 2.0,
                TextFormat = "±{0:F3}",
                ShowUnit = false,
                UnitText = "m",
                SymbolType = "Triangle",
                SymbolSize = 1.0,
                TextOffset = 0.5,
                DecimalPlaces = 3
            };
        }

        /// <summary>
        /// 格式化标高值
        /// </summary>
        public string FormatElevation(double elevation)
        {
            string formatted = string.Format(TextFormat, elevation);
            if (ShowUnit && !string.IsNullOrWhiteSpace(UnitText))
            {
                formatted += UnitText;
            }
            return formatted;
        }

        /// <summary>
        /// 克隆配置
        /// </summary>
        public ElevationConfiguration Clone()
        {
            return new ElevationConfiguration
            {
                SymbolLength = this.SymbolLength,
                TextFormat = this.TextFormat,
                ShowUnit = this.ShowUnit,
                UnitText = this.UnitText,
                SymbolType = this.SymbolType,
                SymbolSize = this.SymbolSize,
                TextOffset = this.TextOffset,
                DecimalPlaces = this.DecimalPlaces
            };
        }

        public override string ToString()
        {
            return $"ElevationConfiguration[Length={SymbolLength}, Format={TextFormat}]";
        }
    }
}

