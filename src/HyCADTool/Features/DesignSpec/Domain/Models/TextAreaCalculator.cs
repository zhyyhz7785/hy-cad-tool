using System;

namespace HyCADTool.Features.DesignSpec.Domain.Models
{
    /// <summary>
    /// 根据配置计算文字排版区域（纯算法，平台无关）
    /// 每栏宽度优先由图纸页面尺寸驱动，缺失页面参数时回退为字符数驱动。
    /// </summary>
    public static class TextAreaCalculator
    {
        public class TextAreaResult
        {
            /// <summary>总宽度（所有栏 + 间距）</summary>
            public double TotalWidth { get; set; }
            /// <summary>总高度（用户指定）</summary>
            public double TotalHeight { get; set; }
            /// <summary>每栏的独立宽度数组</summary>
            public double[] ColumnWidths { get; set; }
            /// <summary>栏间距</summary>
            public double ColumnGutter { get; set; }
            /// <summary>栏数</summary>
            public int ColumnCount { get; set; }
        }

        public static TextAreaResult Calculate(DesignSpecConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.Normalize();
            config.Validate();

            int cols = Math.Max(1, config.ColumnCount);
            double gutter = config.ActualColumnGutter;

            var widths = new double[cols];
            double totalW = 0;
            for (int i = 0; i < cols; i++)
            {
                widths[i] = config.GetColumnWidth(i);
                totalW += widths[i];
            }
            totalW += gutter * (cols - 1);

            return new TextAreaResult
            {
                TotalWidth = totalW,
                TotalHeight = config.ActualTotalHeight,
                ColumnWidths = widths,
                ColumnGutter = gutter,
                ColumnCount = cols
            };
        }
    }
}
