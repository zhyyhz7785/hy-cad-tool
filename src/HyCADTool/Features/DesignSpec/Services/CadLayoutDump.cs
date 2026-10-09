using System.Collections.Generic;
using Newtonsoft.Json;

namespace HyCADTool.Features.DesignSpec.Services
{
    /// <summary>
    /// HyMd 屏外分页导出的贴图几何（camelCase JSON，与 TS CadLayoutDump 对齐）。
    /// schema v3：块带已折行，表格带列宽行高；CAD 只画不折。
    /// </summary>
    public sealed class CadLayoutDump
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("markdown")]
        public string Markdown { get; set; }

        [JsonProperty("sourcePath")]
        public string SourcePath { get; set; }

        [JsonProperty("textSizeMm")]
        public double TextSizeMm { get; set; }

        [JsonProperty("font")]
        public CadDumpFont Font { get; set; }

        [JsonProperty("lineSpacingFactor")]
        public double? LineSpacingFactor { get; set; }

        [JsonProperty("headingScales")]
        public double[] HeadingScales { get; set; }

        [JsonProperty("skippedBlocks")]
        public List<CadSkippedBlock> SkippedBlocks { get; set; }

        [JsonProperty("paper")]
        public CadDumpPaper Paper { get; set; }

        [JsonProperty("pages")]
        public List<CadDumpPage> Pages { get; set; }
    }

    public sealed class CadDumpFont
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("fontFileName")]
        public string FontFileName { get; set; }

        [JsonProperty("bigFontFileName")]
        public string BigFontFileName { get; set; }

        [JsonProperty("xScale")]
        public double XScale { get; set; }

        [JsonProperty("emScale")]
        public double EmScale { get; set; }

        [JsonProperty("styleName")]
        public string StyleName { get; set; }
    }

    public sealed class CadSkippedBlock
    {
        [JsonProperty("index")]
        public int Index { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }
    }

    public sealed class CadDumpPaper
    {
        [JsonProperty("widthMm")]
        public double WidthMm { get; set; }

        [JsonProperty("heightMm")]
        public double HeightMm { get; set; }

        [JsonProperty("marginMm")]
        public double[] MarginMm { get; set; }

        [JsonProperty("columns")]
        public int Columns { get; set; }

        [JsonProperty("gutterMm")]
        public double GutterMm { get; set; }
    }

    public sealed class CadDumpPage
    {
        [JsonProperty("pageIndex")]
        public int PageIndex { get; set; }

        [JsonProperty("blocks")]
        public List<CadDumpBlock> Blocks { get; set; }

        [JsonProperty("tables")]
        public List<CadDumpTable> Tables { get; set; }
    }

    public sealed class CadDumpRun
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("bold")]
        public bool Bold { get; set; }

        [JsonProperty("italic")]
        public bool Italic { get; set; }

        [JsonProperty("code")]
        public bool Code { get; set; }

        [JsonProperty("sizeMm")]
        public double? SizeMm { get; set; }
    }

    public sealed class CadDumpLineMarker
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("xMm")]
        public double XMm { get; set; }
    }

    public sealed class CadDumpLine
    {
        [JsonProperty("xMm")]
        public double XMm { get; set; }

        [JsonProperty("yMm")]
        public double YMm { get; set; }

        [JsonProperty("baselineMm")]
        public double BaselineMm { get; set; }

        [JsonProperty("widthMm")]
        public double WidthMm { get; set; }

        [JsonProperty("runs")]
        public List<CadDumpRun> Runs { get; set; }

        [JsonProperty("marker")]
        public CadDumpLineMarker Marker { get; set; }
    }

    public sealed class CadDumpBlock
    {
        [JsonProperty("kind")]
        public string Kind { get; set; }

        [JsonProperty("headingLevel")]
        public int? HeadingLevel { get; set; }

        [JsonProperty("markdown")]
        public string Markdown { get; set; }

        [JsonProperty("xMm")]
        public double XMm { get; set; }

        [JsonProperty("yMm")]
        public double YMm { get; set; }

        [JsonProperty("widthMm")]
        public double WidthMm { get; set; }

        [JsonProperty("heightMm")]
        public double HeightMm { get; set; }

        [JsonProperty("fontSizeMm")]
        public double FontSizeMm { get; set; }

        [JsonProperty("linePitchMm")]
        public double LinePitchMm { get; set; }

        [JsonProperty("lines")]
        public List<CadDumpLine> Lines { get; set; }
    }

    public sealed class CadDumpTableCell
    {
        [JsonProperty("align")]
        public string Align { get; set; }

        [JsonProperty("lines")]
        public List<CadDumpLine> Lines { get; set; }
    }

    public sealed class CadDumpTable
    {
        [JsonProperty("xMm")]
        public double XMm { get; set; }

        [JsonProperty("yMm")]
        public double YMm { get; set; }

        [JsonProperty("widthMm")]
        public double WidthMm { get; set; }

        [JsonProperty("heightMm")]
        public double HeightMm { get; set; }

        [JsonProperty("rows")]
        public List<List<string>> Rows { get; set; }

        [JsonProperty("columnWidthsMm")]
        public double[] ColumnWidthsMm { get; set; }

        [JsonProperty("rowHeightsMm")]
        public double[] RowHeightsMm { get; set; }

        [JsonProperty("cellPadXMm")]
        public double CellPadXMm { get; set; }

        [JsonProperty("cellPadYMm")]
        public double CellPadYMm { get; set; }

        [JsonProperty("fontSizeMm")]
        public double FontSizeMm { get; set; }

        [JsonProperty("linePitchMm")]
        public double LinePitchMm { get; set; }

        [JsonProperty("cells")]
        public List<List<CadDumpTableCell>> Cells { get; set; }
    }
}
