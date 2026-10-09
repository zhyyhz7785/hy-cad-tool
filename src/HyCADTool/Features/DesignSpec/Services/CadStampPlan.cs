using System.Collections.Generic;
using Newtonsoft.Json;

namespace HyCADTool.Features.DesignSpec.Services
{
    /// <summary>
    /// HyMd 编好的落笔清单（camelCase JSON，与 TS CadStampPlan 对齐）。CAD 只取点 + 按坐标写实体。
    /// </summary>
    public sealed class CadStampPlan
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("scale")]
        public double Scale { get; set; }

        [JsonProperty("styleName")]
        public string StyleName { get; set; }

        [JsonProperty("fontFileName")]
        public string FontFileName { get; set; }

        [JsonProperty("bigFontFileName")]
        public string BigFontFileName { get; set; }

        [JsonProperty("textXScale")]
        public double TextXScale { get; set; }

        [JsonProperty("pageWidthMm")]
        public double PageWidthMm { get; set; }

        [JsonProperty("pageHeightMm")]
        public double PageHeightMm { get; set; }

        [JsonProperty("marginMm")]
        public double[] MarginMm { get; set; }

        [JsonProperty("columns")]
        public int Columns { get; set; }

        [JsonProperty("gutterMm")]
        public double GutterMm { get; set; }

        [JsonProperty("lineSpacingFactor")]
        public double LineSpacingFactor { get; set; }

        [JsonProperty("headingScales")]
        public double[] HeadingScales { get; set; }

        [JsonProperty("markdown")]
        public string Markdown { get; set; }

        [JsonProperty("sourcePath")]
        public string SourcePath { get; set; }

        [JsonProperty("skippedCount")]
        public int SkippedCount { get; set; }

        [JsonProperty("pages")]
        public List<CadStampPagePlan> Pages { get; set; }
    }

    public sealed class CadStampPagePlan
    {
        [JsonProperty("pageIndex")]
        public int PageIndex { get; set; }

        [JsonProperty("pageTopMm")]
        public double PageTopMm { get; set; }

        [JsonProperty("frames")]
        public List<CadStampFrame> Frames { get; set; }

        [JsonProperty("mtexts")]
        public List<CadStampMText> Mtexts { get; set; }

        [JsonProperty("tables")]
        public List<CadStampTablePlan> Tables { get; set; }
    }

    public sealed class CadStampFrame
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("pointsMm")]
        public List<double[]> PointsMm { get; set; }
    }

    public sealed class CadStampMText
    {
        [JsonProperty("xMm")]
        public double XMm { get; set; }

        [JsonProperty("yMm")]
        public double YMm { get; set; }

        [JsonProperty("heightMm")]
        public double HeightMm { get; set; }

        [JsonProperty("width")]
        public double Width { get; set; }

        [JsonProperty("attachment")]
        public string Attachment { get; set; }

        [JsonProperty("lineSpacingExactly")]
        public double LineSpacingExactly { get; set; }

        [JsonProperty("contents")]
        public string Contents { get; set; }
    }

    public sealed class CadStampTablePlan
    {
        [JsonProperty("xMm")]
        public double XMm { get; set; }

        [JsonProperty("yMm")]
        public double YMm { get; set; }

        [JsonProperty("fontSizeMm")]
        public double FontSizeMm { get; set; }

        [JsonProperty("cellPadXMm")]
        public double CellPadXMm { get; set; }

        [JsonProperty("cellPadYMm")]
        public double CellPadYMm { get; set; }

        [JsonProperty("columnWidthsMm")]
        public double[] ColumnWidthsMm { get; set; }

        [JsonProperty("rowHeightsMm")]
        public double[] RowHeightsMm { get; set; }

        [JsonProperty("aligns")]
        public List<List<string>> Aligns { get; set; }

        [JsonProperty("cells")]
        public List<List<string>> Cells { get; set; }
    }

    /// <summary>
    /// 管道 stamp 载荷：有 plan 则按清单落笔，否则走 dump → InsertFromHyMdDump。
    /// </summary>
    public sealed class CadStampRequest
    {
        public CadLayoutDump Dump { get; set; }
        public CadStampPlan Plan { get; set; }
    }
}
