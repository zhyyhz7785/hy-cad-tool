using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.Global;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.Modules;
using HyCADTool.Refactored.Domain.Entities.Pile;
using HyCADTool.Refactored.Domain.Enums;

// LayerConfig 位于 Global 命名空间
using LayerConfig = HyCADTool.Refactored.Domain.ValueObjects.Configuration.Global.LayerConfig;

namespace HyCADTool.Refactored.Infrastructure.Configuration
{
    /// <summary>
    /// CSV 配置文件加载器
    /// 从旧格式的 CSV 文件读取配置（兜底方案）
    /// </summary>
    public class CsvConfigurationLoader
    {
        private readonly string _csvPath;
        private List<CsvLine> _cachedLines;

        public CsvConfigurationLoader(string csvPath)
        {
            _csvPath = csvPath;
        }

        /// <summary>
        /// 加载全局配置（从CSV）
        /// </summary>
        public GlobalConfiguration LoadGlobalConfiguration()
        {
            var lines = EnsureCsvLoaded();

            double scale = 40.0;
            double elevationLength = 2.0;

            foreach (var line in lines.Where(l => l.Type == "BaseConfig"))
            {
                if (line.Key == "Scale" && double.TryParse(line.V1, out var s) && s > 0)
                    scale = s;
                else if (line.Key == "ElevationLength" && double.TryParse(line.V1, out var el) && el > 0)
                    elevationLength = el;
            }

            return new GlobalConfiguration
            {
                Scale = new ScaleConfig { Default = scale, MinValue = 1.0, MaxValue = 200.0 },
                Tolerance = ToleranceConfig.CreateDefault(),
                Paths = PathConfig.CreateDefault(),
                Styles = new StylesConfig
                {
                    TextStyle = LoadTextStyleConfiguration(),
                    DimensionStyle = LoadDimensionStyleConfiguration(),
                    MLeaderStyle = LoadMLeaderStyleConfiguration()
                },
                ElevationLength = elevationLength
            };
        }

        /// <summary>
        /// 加载桩基配置
        /// </summary>
        public PileConfiguration LoadPileConfiguration()
        {
            var lines = EnsureCsvLoaded();

            var section = PileSectionType.Circle;
            double diameter = 400.0;
            var arrangementType = PileArrangementType.Rectangle;
            double arrangeRate = 0.5;
            var margin = (400.0, 400.0, 400.0, 400.0);
            double minDistance = 1200.0;
            double displacementRate = 0.02;
            double distanceFromContour = 400.0;

            foreach (var line in lines.Where(l => l.Type == "Pile"))
            {
                switch (line.Key)
                {
                    case "Section":
                        section = Enum.TryParse<PileSectionType>(line.V1, out var s) ? s : PileSectionType.Circle;
                        break;
                    case "DiameterOrEdge":
                        if (double.TryParse(line.V1, out var d))
                            diameter = d;
                        break;
                    case "ArrangementType":
                        arrangementType = Enum.TryParse<PileArrangementType>(line.V1, out var a) ? a : PileArrangementType.Rectangle;
                        break;
                    case "PileArrangeRate":
                        if (double.TryParse(line.V1, out var r))
                            arrangeRate = r;
                        break;
                    case "Margin":
                        if (line.Values.Length >= 4 &&
                            double.TryParse(line.V1, out var up) &&
                            double.TryParse(line.V2, out var down) &&
                            double.TryParse(line.V3, out var left) &&
                            double.TryParse(line.V4, out var right))
                        {
                            margin = (up, down, left, right);
                        }
                        break;
                    case "MinPileCenterDistance":
                        if (double.TryParse(line.V1, out var m))
                            minDistance = m;
                        break;
                    case "InputDisplacementRate":
                        if (double.TryParse(line.V1, out var dr))
                            displacementRate = dr;
                        break;
                    case "InputDistanceFromContour":
                        if (double.TryParse(line.V1, out var c))
                            distanceFromContour = c;
                        break;
                }
            }

            return new PileConfiguration(
                section, diameter, arrangementType, arrangeRate,
                margin, minDistance, displacementRate, distanceFromContour);
        }

        /// <summary>
        /// 加载图层配置列表
        /// </summary>
        public List<LayerConfig> LoadLayerConfigurations()
        {
            var lines = EnsureCsvLoaded();
            var layers = new List<LayerConfig>();

            foreach (var line in lines.Where(l => l.Type == "Layer"))
            {
                if (short.TryParse(line.V1, out var colorIndex))
                {
                    layers.Add(new LayerConfig
                    {
                        Name = line.Key,
                        ColorIndex = colorIndex
                    });
                }
            }

            return layers;
        }

        /// <summary>
        /// 加载单个文字样式配置（用于全局配置）
        /// </summary>
        private TextStyleConfig LoadTextStyleConfiguration()
        {
            var styles = LoadTextStyleConfigurations();
            // 返回第一个样式或默认样式
            return styles.FirstOrDefault() ?? TextStyleConfig.CreateDefault("0_Hy_40", 1.0);
        }

        /// <summary>
        /// 加载单个标注样式配置（用于全局配置）
        /// </summary>
        private DimensionStyleConfig LoadDimensionStyleConfiguration()
        {
            var styles = LoadDimensionStyleConfigurations();
            // 返回第一个样式或默认样式
            return styles.FirstOrDefault() ?? DimensionStyleConfig.CreateDefault("0_Hy_40_Dim", "0_Hy_40", 1.0);
        }

        /// <summary>
        /// 加载文字样式配置列表
        /// </summary>
        public List<TextStyleConfig> LoadTextStyleConfigurations()
        {
            var lines = EnsureCsvLoaded();
            var styles = new List<TextStyleConfig>();

            foreach (var line in lines.Where(l => l.Type == "TextStyle"))
            {
                if (double.TryParse(line.V3, out var size) &&
                    double.TryParse(line.V4, out var xscale))
                {
                    styles.Add(new TextStyleConfig
                    {
                        Name = line.Key,
                        FontFileName = line.V1,
                        BigFontFileName = line.V2,
                        TextSize = size,
                        XScale = xscale
                    });
                }
            }

            return styles;
        }

        /// <summary>
        /// 加载标注样式配置列表
        /// </summary>
        public List<DimensionStyleConfig> LoadDimensionStyleConfigurations()
        {
            var lines = EnsureCsvLoaded();
            var styles = new List<DimensionStyleConfig>();

            foreach (var line in lines.Where(l => l.Type == "DimStyle"))
            {
                if (line.Values.Length >= 9 &&
                    double.TryParse(line.V2, out var dimtxt) &&
                    double.TryParse(line.V3, out var dimexo) &&
                    double.TryParse(line.V4, out var dimexe) &&
                    int.TryParse(line.V5, out var dimdec) &&
                    double.TryParse(line.V6, out var dimgap) &&
                    double.TryParse(line.V7, out var dimasz) &&
                    double.TryParse(line.V8, out var dimdle) &&
                    int.TryParse(line.V9, out var dimtdec))
                {
                    styles.Add(new DimensionStyleConfig
                    {
                        Name = line.Key,
                        TextStyleName = line.V1,
                        TextHeight = dimtxt,
                        ExtensionLineOffset = dimexo,
                        ExtensionLineExtend = dimexe,
                        DecimalPlaces = dimdec,
                        TextGap = dimgap,
                        ArrowSize = dimasz,
                        TextDecimalPlaces = dimtdec
                    });
                }
            }

            return styles;
        }

        /// <summary>
        /// 加载多重引线样式配置
        /// </summary>
        private MLeaderStyleConfig LoadMLeaderStyleConfiguration()
        {
            var lines = EnsureCsvLoaded();
            
            // CSV 中查找 MLeader 样式配置
            var mleaderLine = lines.FirstOrDefault(l => l.Type == "MLeaderStyle" || l.Type == "MLeader");
            
            if (mleaderLine != null && !string.IsNullOrWhiteSpace(mleaderLine.Key))
            {
                string styleName = mleaderLine.Key;
                string textStyleName = !string.IsNullOrWhiteSpace(mleaderLine.V1) ? mleaderLine.V1 : "0_Hy_40";
                return new MLeaderStyleConfig(styleName, textStyleName);
            }
            
            // 返回默认配置
            return MLeaderStyleConfig.CreateDefault("0_Hy_40_Mleader", "0_Hy_40");
        }

        /// <summary>
        /// 确保 CSV 文件已加载
        /// </summary>
        private List<CsvLine> EnsureCsvLoaded()
        {
            if (_cachedLines == null)
            {
                if (!File.Exists(_csvPath))
                    throw new FileNotFoundException($"CSV 配置文件不存在: {_csvPath}");

                _cachedLines = ParseCsv();
            }

            return _cachedLines;
        }

        /// <summary>
        /// 解析 CSV 文件
        /// </summary>
        private List<CsvLine> ParseCsv()
        {
            var lines = new List<CsvLine>();

            using (var fs = new FileStream(_csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(fs, Encoding.UTF8))
            {
                string line;
                bool skipHeader = true;

                while ((line = reader.ReadLine()) != null)
                {
                    if (skipHeader)
                    {
                        skipHeader = false;
                        continue;
                    }

                    var parts = line.Split(',').Select(p => p.Trim()).ToArray();
                    if (parts.Length < 4)
                        continue;

                    lines.Add(new CsvLine
                    {
                        Type = parts[0],
                        Key = parts[1],
                        SubKey = parts.Length > 2 ? parts[2] : "",
                        Values = parts.Skip(3).ToArray()
                    });
                }
            }

            return lines;
        }

        /// <summary>
        /// CSV 行数据结构
        /// </summary>
        private class CsvLine
        {
            public string Type { get; set; }
            public string Key { get; set; }
            public string SubKey { get; set; }
            public string[] Values { get; set; }

            public string V1 => Values.Length > 0 ? Values[0] : "";
            public string V2 => Values.Length > 1 ? Values[1] : "";
            public string V3 => Values.Length > 2 ? Values[2] : "";
            public string V4 => Values.Length > 3 ? Values[3] : "";
            public string V5 => Values.Length > 4 ? Values[4] : "";
            public string V6 => Values.Length > 5 ? Values[5] : "";
            public string V7 => Values.Length > 6 ? Values[6] : "";
            public string V8 => Values.Length > 7 ? Values[7] : "";
            public string V9 => Values.Length > 8 ? Values[8] : "";
            public string V10 => Values.Length > 9 ? Values[9] : "";
        }
    }
}

