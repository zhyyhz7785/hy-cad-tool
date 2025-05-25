using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.HelpClass;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HyCADTool.Config
{
    public static class ConfigManager
    {
        private static readonly string FolderPath = @"E:\BaiduSyncdisk\Code\testResult";
        private static readonly string ExCsvFile = Path.Combine(FolderPath, "0ExHyConfig.csv");
        private static readonly string ConfigCsvFile = Path.Combine(FolderPath, "0HyConfig.csv");

        public static void ExportConfigToCsv()
        {
            Directory.CreateDirectory(FolderPath);
            var sb = new StringBuilder();
            sb.AppendLine("Type,Key,SubKey,V1,V2,V3,V4,V5,V6,V7,V8,V9,V10");
            // BaseConfig
            sb.AppendLine($"BaseConfig,Scale,,{BaseConfig.Scale}");
            sb.AppendLine($"BaseConfig,ElevationLength,,{BaseConfig.ElevationLength}");

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

            // Pile config
            var pile = PileConfig.Instance;
            sb.AppendLine($"Pile,Section,,{pile.Section}");
            sb.AppendLine($"Pile,DiameterOrEdge,,{pile.DiameterOrEdge}");
            sb.AppendLine($"Pile,ArrangementType,,{pile.ArrangementType}");
            sb.AppendLine($"Pile,PileArrangeRate,,{pile.PileArrangeRate}");
            sb.AppendLine($"Pile,Margin,,{pile.Margin.up},{pile.Margin.down},{pile.Margin.left},{pile.Margin.right}");
            sb.AppendLine($"Pile,MinPileCenterDistance,,{pile.MinPileCenterDistance}");
            sb.AppendLine($"Pile,InputDisplacementRate,,{pile.InputDisplacementRate}");
            sb.AppendLine($"Pile,InputDistanceFromContour,,{pile.InputDistanceFromContour}");

            File.WriteAllText(ExCsvFile, sb.ToString(), Encoding.UTF8);
        }

        public static void ImportConfigFromCsv(string typeFilter = null, string subKeyFilter = null)
        {
            Tools.ZTools.RegisterStandardLinetypes();
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
                    var v = parts.Skip(3).ToArray();

                    if (!string.IsNullOrEmpty(typeFilter) && !string.Equals(type, typeFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!string.IsNullOrEmpty(subKeyFilter) && !string.Equals(subKey, subKeyFilter, StringComparison.OrdinalIgnoreCase))
                        continue;

                    switch (type)
                    {
                        case "Layer":
                            if (short.TryParse(v[0], out var color) && Enum.TryParse(v[2], out LineWeight lw))
                            {
                                Tools.ZTools.CreateLayer(key, color, v[1], lw);
                            }
                            break;
                        case "TextStyle":
                            if (double.TryParse(v[2], out var size) && double.TryParse(v[3], out var xscale))
                            {
                                Tools.ZTools.CreateTextStyle(key, v[0], v[1], size, xscale);
                            }
                            break;
                        case "Pile":
                            var pile = PileConfig.Instance;
                            switch (key)
                            {
                                case "Section": pile.Section = Enum.TryParse(v[0], out PileSectionType s) ? s : PileSectionType.Circle; break;
                                case "DiameterOrEdge": if (double.TryParse(v[0], out var d)) pile.DiameterOrEdge = d; break;
                                case "ArrangementType": pile.ArrangementType = Enum.TryParse(v[0], out PileArrangementType a) ? a : PileArrangementType.Rectangle; break;
                                case "PileArrangeRate": if (double.TryParse(v[0], out var r)) pile.PileArrangeRate = r; break;
                                case "Margin":
                                    if (v.Length >= 4 && double.TryParse(v[0], out var up) && double.TryParse(v[1], out var down) &&
                                        double.TryParse(v[2], out var left) && double.TryParse(v[3], out var right))
                                        pile.Margin = (up, down, left, right);
                                    break;
                                case "MinPileCenterDistance": if (double.TryParse(v[0], out var m)) pile.MinPileCenterDistance = m; break;
                                case "InputDisplacementRate": if (double.TryParse(v[0], out var dr)) pile.InputDisplacementRate = dr; break;
                                case "InputDistanceFromContour": if (double.TryParse(v[0], out var c)) pile.InputDistanceFromContour = c; break;
                            }
                            break;
                        case "BaseConfig":
                            switch (key)
                            {
                                case "Scale": if (double.TryParse(v[0], out var s)) BaseConfig.Scale = s; break;
                                case "ElevationLength": if (double.TryParse(v[0], out var el)) BaseConfig.ElevationLength = el; break;
                            }
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
