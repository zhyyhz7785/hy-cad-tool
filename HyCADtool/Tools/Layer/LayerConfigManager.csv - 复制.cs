using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HyCADTool.Config
{
    public static class LayerConfigManagerCsv
    {
        private static readonly string FolderPath = @"E:\BaiduSyncdisk\Code\testResult";
        private static readonly string ExCsvFile = Path.Combine(FolderPath, "0ExHyConfig.csv");
        private static readonly string ConfigCsvFile = Path.Combine(FolderPath, "0HyConfig.csv");

        public static void ExportConfigToCsv()
        {
            Directory.CreateDirectory(FolderPath);
            var sb = new StringBuilder();
            sb.AppendLine("Type,Key,SubKey,V1,V2,V3,V4,V5,V6,V7,V8,V9,V10");

            foreach (var layer in GetCurrentLayers())
            {
                sb.AppendLine($"Layer,{layer.Name},Common,{layer.ColorIndex},{layer.LineType},{layer.LineWeight}");
            }

            foreach (var style in GetCurrentTextStyles())
            {
                sb.AppendLine($"TextStyle,{style.Name},Common,{style.FontFileName},{style.BigFontFileName},{style.TextSize},{style.XScale}");
            }

            foreach (var dim in GetCurrentDimStyles())
            {
                sb.AppendLine($"DimStyle,{dim.Name},Common,{dim.TextStyleName},{dim.Dimtxt},{dim.Dimexo},{dim.Dimexe},{dim.Dimdec},{dim.Dimgap},{dim.Dimasz},{dim.Dimdle},{dim.Dimtdec}");
            }

            File.WriteAllText(ExCsvFile, sb.ToString(), Encoding.UTF8);
        }

        public static void ImportConfigFromCsv(string typeFilter = null, string subKeyFilter = null)
        {
            HyTool.RegisterStandardLinetypes();
            if (!File.Exists(ConfigCsvFile)) return;

            using (var fs = new FileStream(ConfigCsvFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                bool skipHeader = true;
                while ((line = reader.ReadLine()) != null)
                {
                    if (skipHeader) { skipHeader = false; continue; }
                    var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                    if (parts.Length < 4) continue;

                    var type = parts[0];
                    var key = parts[1];
                    var subKey = parts.Length > 2 ? parts[2] : "";
                    var v1 = parts.Length > 3 ? parts[3] : "";
                    var v2 = parts.Length > 4 ? parts[4] : "";
                    var v3 = parts.Length > 5 ? parts[5] : "";
                    var v4 = parts.Length > 6 ? parts[6] : "";

                    if (!string.IsNullOrEmpty(typeFilter) && !string.Equals(type, typeFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrEmpty(subKeyFilter) && !string.Equals(subKey, subKeyFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    switch (type)
                    {
                        case "Layer":
                            if (short.TryParse(v1, out var color) && Enum.TryParse(v3, out LineWeight lw))
                            {
                                HyTool.CreateLayer(key, color, v2, lw);
                            }
                            break;
                        case "TextStyle":
                            if (double.TryParse(v3, out var size) && double.TryParse(v4, out var xscale))
                            {
                                HyTool.CreateTextStyle(key, v1, v2, size, xscale);
                            }
                            break;
                        case "Global":
                            break;
                    }
                }
            }
        }

        public static void ImportAllFromCsv() => ImportConfigFromCsv();

        private static List<LayerDefinition> GetCurrentLayers()
        {
            var result = new List<LayerDefinition>();
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                foreach (ObjectId id in lt)
                {
                    var ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    result.Add(new LayerDefinition
                    {
                        Name = ltr.Name,
                        ColorIndex = ltr.Color.ColorIndex,
                        LineType = GetLinetypeName(ltr),
                        LineWeight = ltr.LineWeight
                    });
                }
                tr.Commit();
            }
            return result;
        }

        private static List<TextStyleDefinition> GetCurrentTextStyles()
        {
            var result = new List<TextStyleDefinition>();
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var table = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                foreach (ObjectId id in table)
                {
                    var rec = (TextStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    result.Add(new TextStyleDefinition
                    {
                        Name = rec.Name,
                        FontFileName = rec.FileName,
                        BigFontFileName = rec.BigFontFileName,
                        TextSize = rec.TextSize,
                        XScale = rec.XScale
                    });
                }
                tr.Commit();
            }
            return result;
        }

        private static List<DimStyleDefinition> GetCurrentDimStyles()
        {
            var result = new List<DimStyleDefinition>();
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var table = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                foreach (ObjectId id in table)
                {
                    var rec = (DimStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
                    result.Add(new DimStyleDefinition
                    {
                        Name = rec.Name,
                        TextStyleName = rec.Dimtxsty.GetObject(OpenMode.ForRead) is TextStyleTableRecord ts ? ts.Name : "",
                        Dimtxt = rec.Dimtxt,
                        Dimexo = rec.Dimexo,
                        Dimexe = rec.Dimexe,
                        Dimdec = rec.Dimdec,
                        Dimgap = rec.Dimgap,
                        Dimasz = rec.Dimasz,
                        Dimdle = rec.Dimdle,
                        Dimtdec = rec.Dimtdec
                    });
                }
                tr.Commit();
            }
            return result;
        }

        private static string GetLinetypeName(LayerTableRecord layer)
        {
            if (!layer.LinetypeObjectId.IsValid) return "Continuous";
            using (var tr = layer.Database.TransactionManager.StartTransaction())
            {
                var ltr = (LinetypeTableRecord)tr.GetObject(layer.LinetypeObjectId, OpenMode.ForRead);
                tr.Commit();
                return ltr.Name;
            }
        }

        private class LayerDefinition
        {
            public string Name { get; set; }
            public short ColorIndex { get; set; }
            public string LineType { get; set; }
            public LineWeight LineWeight { get; set; }
        }

        private class TextStyleDefinition
        {
            public string Name { get; set; }
            public string FontFileName { get; set; }
            public string BigFontFileName { get; set; }
            public double TextSize { get; set; }
            public double XScale { get; set; }
        }

        private class DimStyleDefinition
        {
            public string Name { get; set; }
            public string TextStyleName { get; set; }
            public double Dimtxt { get; set; }
            public double Dimexo { get; set; }
            public double Dimexe { get; set; }
            public int Dimdec { get; set; }
            public double Dimgap { get; set; }
            public double Dimasz { get; set; }
            public double Dimdle { get; set; }
            public int Dimtdec { get; set; }
        }
    }
}
