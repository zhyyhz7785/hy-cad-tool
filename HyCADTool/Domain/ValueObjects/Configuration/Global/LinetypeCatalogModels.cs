using System.Collections.Generic;

namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    /// <summary>
    /// 单条线型的可序列化摘要（写入 hy-linetype-catalog.json 与导出 .lin 时共用）。
    /// </summary>
    public sealed class LinetypeCatalogItem
    {
        public string Name { get; set; }
        public string Comments { get; set; }
        public double PatternLength { get; set; }
        public int NumDashes { get; set; }
        public bool IsDependent { get; set; }
        public bool HasComplexSegments { get; set; }
        public List<double> DashLengths { get; set; }
        /// <summary>不含「*名称,说明」行的图案行，例如 <c>A,0.5,-0.25</c> 或含 [TEXT]/[SHAPE]。</summary>
        public string LinPatternLine { get; set; }
    }

    /// <summary>
    /// 落盘到 %APPDATA%\HyCADTool\hy-linetype-catalog.json 的完整清单。
    /// </summary>
    public sealed class LinetypeCatalogDocument
    {
        public string CapturedAtIsoUtc { get; set; }
        public string SourceDrawingFile { get; set; }
        public List<LinetypeCatalogItem> Items { get; set; }
    }

    /// <summary>
    /// 嵌入 hy-settings.json 的轻量快照（路径 + 名称列表，避免设置文件过大）。
    /// </summary>
    public sealed class LinetypeCatalogSnapshot
    {
        public string CapturedAtIsoUtc { get; set; }
        public string SourceDrawingFile { get; set; }
        public string CatalogJsonPath { get; set; }
        public string LastLinExportPath { get; set; }
        public List<string> Names { get; set; }
    }
}
