using System.Collections.Generic;
using Newtonsoft.Json;

namespace HyCADTool.Features.DesignSpec.Services
{
    /// <summary>与 HyMd cadCompareMetrics JSON 对齐（camelCase）。</summary>
    public sealed class CadCompareBundleDto
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonProperty("generatedAt")] public string GeneratedAt { get; set; }
        [JsonProperty("sourcePath")] public string SourcePath { get; set; }
        [JsonProperty("markdown")] public string Markdown { get; set; }
        [JsonProperty("reports")] public List<CadCompareReportDto> Reports { get; set; }
    }

    public sealed class CadCompareReportDto
    {
        [JsonProperty("schemaVersion")] public int SchemaVersion { get; set; }
        [JsonProperty("surface")] public string Surface { get; set; }
        [JsonProperty("recipe")] public string Recipe { get; set; }
        [JsonProperty("caseId")] public string CaseId { get; set; }
        [JsonProperty("fontResolved")] public string FontResolved { get; set; }
        [JsonProperty("textHeightMm")] public double TextHeightMm { get; set; }
        [JsonProperty("columnWidthMm")] public double ColumnWidthMm { get; set; }
        [JsonProperty("lineHeightCss")] public string LineHeightCss { get; set; }
        [JsonProperty("glyphs")] public List<CadCompareGlyphDto> Glyphs { get; set; }
        [JsonProperty("wrap")] public CadCompareWrapDto Wrap { get; set; }
        [JsonProperty("vertical")] public CadCompareVerticalDto Vertical { get; set; }
        [JsonProperty("blocks")] public List<CadCompareBlockDto> Blocks { get; set; }
    }

    public sealed class CadCompareGlyphDto
    {
        [JsonProperty("ch")] public string Ch { get; set; }
        [JsonProperty("advanceMm")] public double AdvanceMm { get; set; }
    }

    public sealed class CadCompareWrapDto
    {
        [JsonProperty("asciiN")] public int AsciiN { get; set; }
        [JsonProperty("cjkN")] public int CjkN { get; set; }
        [JsonProperty("mixedN")] public int MixedN { get; set; }
        [JsonProperty("widthMm")] public double WidthMm { get; set; }
    }

    public sealed class CadCompareVerticalDto
    {
        [JsonProperty("oneLineMm")] public double OneLineMm { get; set; }
        [JsonProperty("twoLineMm")] public double TwoLineMm { get; set; }
        [JsonProperty("paraGapMm")] public double ParaGapMm { get; set; }
        [JsonProperty("h1HeightMm")] public double H1HeightMm { get; set; }
    }

    public sealed class CadCompareBlockDto
    {
        [JsonProperty("caseId")] public string CaseId { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("markdown")] public string Markdown { get; set; }
        [JsonProperty("xMm")] public double XMm { get; set; }
        [JsonProperty("yMm")] public double YMm { get; set; }
        [JsonProperty("widthMm")] public double WidthMm { get; set; }
        [JsonProperty("heightMm")] public double HeightMm { get; set; }
        [JsonProperty("lineCount")] public int LineCount { get; set; }
    }
}
