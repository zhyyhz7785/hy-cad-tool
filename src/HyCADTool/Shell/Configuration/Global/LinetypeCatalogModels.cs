using System;
using System.Collections.Generic;

namespace HyCADTool.Shell.Configuration.Global
{
    /// <summary>单条线型记录（目录 JSON + .lin 导出）。</summary>
    public class LinetypeCatalogItem
    {
        public string Name { get; set; } = string.Empty;
        public string Comments { get; set; } = string.Empty;
        public double PatternLength { get; set; }
        public int NumDashes { get; set; }
        public bool IsDependent { get; set; }
        public bool HasComplexSegments { get; set; }
        public List<double> DashLengths { get; set; } = new List<double>();
        public string LinPatternLine { get; set; } = "A,";
    }

    /// <summary>完整线型目录文件（%AppData%\HyCADTool\hy-linetype-catalog.json）。</summary>
    public class LinetypeCatalogDocument
    {
        public string CapturedAtIsoUtc { get; set; } = string.Empty;
        public string SourceDrawingFile { get; set; } = string.Empty;
        public List<LinetypeCatalogItem> Items { get; set; } = new List<LinetypeCatalogItem>();
    }

    /// <summary>从当前 DWG 提取线型表后的元数据（写入 hy-settings.json）。</summary>
    public class LinetypeCatalogSnapshot
    {
        public string CapturedAtIsoUtc { get; set; } = string.Empty;
        public string SourceDrawingFile { get; set; } = string.Empty;
        public string CatalogJsonPath { get; set; } = string.Empty;
        public string LastLinExportPath { get; set; } = string.Empty;
        public List<string> Names { get; set; } = new List<string>();
    }
}
