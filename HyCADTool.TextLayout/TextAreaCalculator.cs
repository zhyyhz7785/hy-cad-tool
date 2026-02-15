using System;

namespace HyCADTool.TextLayout
{
    public static class TextAreaCalculator
    {
        public class TextAreaResult
        {
            public double TotalWidth { get; set; }
            public double TotalHeight { get; set; }
            public double[] ColumnWidths { get; set; }
            public double ColumnGutter { get; set; }
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
