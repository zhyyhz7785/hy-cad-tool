using System;
using System.IO;
using HyCADTool.Domain.ValueObjects.Configuration.Global;
using HyCADTool.Domain.ValueObjects.Configuration.Modules;
using HyCADTool.Domain.Entities.Pile;
using HyCADTool.Domain.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HyCADTool.Shared.Bootstrap
{
    /// <summary>
    /// JSON 配置文件加载器
    /// 从 config.json 文件读取配置
    /// </summary>
    public class JsonConfigurationLoader
    {
        private readonly string _configPath;
        private JObject _configData;

        public JsonConfigurationLoader(string configPath = "config.json")
        {
            _configPath = configPath;
        }

        /// <summary>
        /// 加载全局配置
        /// </summary>
        public GlobalConfiguration LoadBaseConfiguration()
        {
            EnsureConfigLoaded();

            try
            {
                var globalData = _configData["GlobalConfiguration"];
                if (globalData == null)
                {
                    // 兼容旧格式：从 BaseConfigData 加载
                    return LoadLegacyBaseConfiguration();
                }

                // 加载新格式的全局配置
                var config = new GlobalConfiguration
                {
                    Scale = LoadScaleConfig(globalData["Scale"]),
                    Tolerance = LoadToleranceConfig(globalData["Tolerance"]),
                    Paths = LoadPathConfig(globalData["Paths"]),
                    Styles = LoadStylesConfigFromGlobal(globalData["Styles"]),
                    ElevationLength = ParseDouble(globalData["ElevationLength"]?.ToString(), 2.0)
                };

                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载全局配置失败: {ex.Message}");
                return GlobalConfiguration.CreateDefault();
            }
        }

        /// <summary>
        /// 加载旧格式的基础配置（兼容性）
        /// </summary>
        private GlobalConfiguration LoadLegacyBaseConfiguration()
        {
            var baseData = _configData["BaseConfigData"];
            if (baseData == null)
                return GlobalConfiguration.CreateDefault();

            // 从旧格式加载样式配置
            var textStyle = LoadTextStyleConfiguration();
            var dimStyle = LoadDimensionStyleConfiguration();
            var mleaderStyle = LoadMLeaderStyleConfiguration();

            return new GlobalConfiguration
            {
                Scale = ScaleConfig.CreateDefault(),
                Tolerance = ToleranceConfig.CreateDefault(),
                Paths = PathConfig.CreateDefault(),
                Styles = new StylesConfig
                {
                    TextStyle = textStyle,
                    DimensionStyle = dimStyle,
                    MLeaderStyle = mleaderStyle
                },
                ElevationLength = 2.0
            };
        }

        /// <summary>
        /// 从全局配置节点加载比例配置
        /// </summary>
        private ScaleConfig LoadScaleConfig(JToken scaleData)
        {
            if (scaleData == null)
                return ScaleConfig.CreateDefault();

            return new ScaleConfig
            {
                Default = ParseDouble(scaleData["Default"]?.ToString(), 40.0),
                MinValue = ParseDouble(scaleData["Range"]?[0]?.ToString(), 1.0),
                MaxValue = ParseDouble(scaleData["Range"]?[1]?.ToString(), 200.0)
            };
        }

        /// <summary>
        /// 从全局配置节点加载容差配置
        /// </summary>
        private ToleranceConfig LoadToleranceConfig(JToken toleranceData)
        {
            if (toleranceData == null)
                return ToleranceConfig.CreateDefault();

            return new ToleranceConfig
            {
                Double = ParseDouble(toleranceData["Double"]?.ToString(), 1e-2),
                Vector = ParseDouble(toleranceData["Vector"]?.ToString(), 1e-2),
                Point = ParseDouble(toleranceData["Point"]?.ToString(), 1e-2)
            };
        }

        /// <summary>
        /// 从全局配置节点加载路径配置
        /// </summary>
        private PathConfig LoadPathConfig(JToken pathData)
        {
            if (pathData == null)
                return PathConfig.CreateDefault();

            return new PathConfig
            {
                DefaultExportPath = pathData["DefaultExport"]?.ToString() ?? string.Empty,
                DefaultImportPath = pathData["DefaultImport"]?.ToString() ?? string.Empty,
                TempFilesPath = pathData["TempFiles"]?.ToString() ?? Path.Combine(Path.GetTempPath(), "HyCADTool"),
                ConfigDirectory = pathData["ConfigDirectory"]?.ToString() ?? string.Empty
            };
        }

        /// <summary>
        /// 从全局配置节点加载样式配置
        /// </summary>
        private StylesConfig LoadStylesConfigFromGlobal(JToken stylesData)
        {
            if (stylesData == null)
                return StylesConfig.CreateDefault();

            var textStyleData = stylesData["TextStyle"];
            var dimStyleData = stylesData["DimensionStyle"];
            var mleaderData = stylesData["MLeaderStyle"];

            return new StylesConfig
            {
                TextStyle = LoadTextStyleFromToken(textStyleData),
                DimensionStyle = LoadDimensionStyleFromToken(dimStyleData),
                MLeaderStyle = LoadMLeaderStyleFromToken(mleaderData)
            };
        }

        private TextStyleConfig LoadTextStyleFromToken(JToken data)
        {
            if (data == null)
                return TextStyleConfig.CreateDefault("0_Hy_40", 1.0);

            return new TextStyleConfig
            {
                Name = data["Name"]?.ToString() ?? "0_Hy_40",
                FontFileName = data["FontFileName"]?.ToString() ?? "tssdeng.shx",
                BigFontFileName = data["BigFontFileName"]?.ToString() ?? "hztxt.shx",
                TextSize = ParseDouble(data["TextSize"]?.ToString(), 2.5),
                XScale = ParseDouble(data["XScale"]?.ToString(), 0.7)
            };
        }

        private DimensionStyleConfig LoadDimensionStyleFromToken(JToken data)
        {
            if (data == null)
                return DimensionStyleConfig.CreateDefault("0_Hy_40_Dim", "0_Hy_40", 1.0);

            return new DimensionStyleConfig
            {
                Name = data["Name"]?.ToString() ?? "0_Hy_40_Dim",
                TextStyleName = data["TextStyleName"]?.ToString() ?? "0_Hy_40",
                TextHeight = ParseDouble(data["TextHeight"]?.ToString(), 2.5),
                ExtensionLineOffset = ParseDouble(data["ExtensionLineOffset"]?.ToString(), 1.0),
                ExtensionLineExtend = ParseDouble(data["ExtensionLineExtend"]?.ToString(), 1.0),
                DecimalPlaces = ParseInt(data["DecimalPlaces"]?.ToString(), 0),
                TextGap = ParseDouble(data["TextGap"]?.ToString(), 1.0),
                ArrowSize = ParseDouble(data["ArrowSize"]?.ToString(), 1.0),
                TextDecimalPlaces = ParseInt(data["TextDecimalPlaces"]?.ToString(), 0)
            };
        }

        private MLeaderStyleConfig LoadMLeaderStyleFromToken(JToken data)
        {
            if (data == null)
                return MLeaderStyleConfig.CreateDefault("0_Hy_40_Mleader", "0_Hy_40");

            var name = data["Name"]?.ToString() ?? "0_Hy_40_Mleader";
            var textStyleName = data["TextStyleName"]?.ToString() ?? "0_Hy_40";

            return new MLeaderStyleConfig(name, textStyleName);
        }

        /// <summary>
        /// 加载桩基配置
        /// </summary>
        public PileConfiguration LoadPileConfiguration()
        {
            EnsureConfigLoaded();

            try
            {
                var pileData = _configData["PileConfigData"];
                if (pileData == null)
                    return PileConfiguration.CreateDefault();

                var section = ParseEnum<PileSectionType>(
                    pileData["Section"]?["default"]?.ToString(), PileSectionType.Circle);

                var diameter = ParseDouble(
                    pileData["DiameterOrEdge"]?["default"]?.ToString(), 400.0);

                var arrangementType = ParseEnum<PileArrangementType>(
                    pileData["ArrangementType"]?["default"]?.ToString(), PileArrangementType.Rectangle);

                var arrangeRate = ParseDouble(
                    pileData["PileArrangeRate"]?["default"]?.ToString(), 0.5);

                var marginData = pileData["Margin"]?["default"];
                var margin = (
                    ParseDouble(marginData?["Up"]?.ToString(), 400.0),
                    ParseDouble(marginData?["Down"]?.ToString(), 400.0),
                    ParseDouble(marginData?["Left"]?.ToString(), 400.0),
                    ParseDouble(marginData?["Right"]?.ToString(), 400.0)
                );

                var minDistance = ParseDouble(
                    pileData["MinPileCenterDistance"]?["default"]?.ToString(), 1200.0);

                var displacementRate = ParseDouble(
                    pileData["InputDisplacementRate"]?["default"]?.ToString(), 0.02);

                var distanceFromContour = ParseDouble(
                    pileData["InputDistanceFromContour"]?["default"]?.ToString(), 400.0);

                return new PileConfiguration(
                    section, diameter, arrangementType, arrangeRate,
                    margin, minDistance, displacementRate, distanceFromContour);
            }
            catch
            {
                return PileConfiguration.CreateDefault();
            }
        }

        /// <summary>
        /// 加载文字样式配置
        /// </summary>
        public TextStyleConfig LoadTextStyleConfiguration()
        {
            EnsureConfigLoaded();

            try
            {
                var textStyleData = _configData["BaseConfigData"]?["TextStyle"];
                if (textStyleData == null)
                    return TextStyleConfig.CreateDefault("0_Hy_40", 1.0);

                var name = textStyleData["name"]?.ToString() ?? "0_Hy_40";
                var fontFile = textStyleData["fontFileName"]?.ToString() ?? "tssdeng.shx";
                var bigFont = textStyleData["bigFontFileName"]?.ToString() ?? "hztxt.shx";
                var textSize = ParseDouble(textStyleData["textSize"]?.ToString(), 2.5);
                var xScale = ParseDouble(textStyleData["textXScale"]?.ToString(), 0.7);

                return new TextStyleConfig
                {
                    Name = name,
                    FontFileName = fontFile,
                    BigFontFileName = bigFont,
                    TextSize = textSize,
                    XScale = xScale
                };
            }
            catch
            {
                return TextStyleConfig.CreateDefault("0_Hy_40", 1.0);
            }
        }

        /// <summary>
        /// 加载标注样式配置
        /// </summary>
        public DimensionStyleConfig LoadDimensionStyleConfiguration()
        {
            EnsureConfigLoaded();

            try
            {
                var dimStyleData = _configData["BaseConfigData"]?["DimStyle"];
                if (dimStyleData == null)
                    return DimensionStyleConfig.CreateDefault("0_Hy_40_Dim", "0_Hy_40", 1.0);

                var name = dimStyleData["name"]?.ToString() ?? "0_Hy_40_Dim";
                var textStyleName = dimStyleData["textStyleName"]?.ToString() ?? "0_Hy_40";
                var dimtxt = ParseDouble(dimStyleData["dimtxt"]?.ToString(), 2.5);
                var dimexo = ParseDouble(dimStyleData["dimexo"]?.ToString(), 1.0);
                var dimexe = ParseDouble(dimStyleData["dimexe"]?.ToString(), 1.0);
                var dimdle = ParseDouble(dimStyleData["dimdle"]?.ToString(), 0.5);
                var dimgap = ParseDouble(dimStyleData["dimgap"]?.ToString(), 1.0);
                var dimasz = ParseDouble(dimStyleData["dimasz"]?.ToString(), 1.0);
                var dimdec = ParseInt(dimStyleData["dimdec"]?.ToString(), 0);
                var dimtdec = ParseInt(dimStyleData["dimtdec"]?.ToString(), 0);

                return new DimensionStyleConfig
                {
                    Name = name,
                    TextStyleName = textStyleName,
                    TextHeight = dimtxt,
                    ExtensionLineOffset = dimexo,
                    ExtensionLineExtend = dimexe,
                    DecimalPlaces = dimdec,
                    TextGap = dimgap,
                    ArrowSize = dimasz,
                    TextDecimalPlaces = dimtdec
                };
            }
            catch
            {
                return DimensionStyleConfig.CreateDefault("0_Hy_40_Dim", "0_Hy_40", 1.0);
            }
        }

        /// <summary>
        /// 加载多重引线样式配置
        /// </summary>
        public MLeaderStyleConfig LoadMLeaderStyleConfiguration()
        {
            EnsureConfigLoaded();

            try
            {
                var mleaderData = _configData["BaseConfigData"]?["MLeaderStyle"];
                if (mleaderData == null)
                    return MLeaderStyleConfig.CreateDefault("0_Hy_40_Mleader", "0_Hy_40");

                var name = mleaderData["name"]?.ToString() ?? "0_Hy_40_Mleader";
                var textStyleName = mleaderData["textStyleName"]?.ToString() ?? "0_Hy_40";

                return new MLeaderStyleConfig(name, textStyleName);
            }
            catch
            {
                return MLeaderStyleConfig.CreateDefault("0_Hy_40_Mleader", "0_Hy_40");
            }
        }

        /// <summary>
        /// 泛型方法：从JSON文件加载配置对象
        /// </summary>
        public T LoadFromFile<T>(string filePath) where T : class
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"配置文件不存在: {filePath}");

            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<T>(json);
        }

        /// <summary>
        /// 泛型方法：保存配置对象到JSON文件
        /// </summary>
        public void SaveToFile<T>(T config, string filePath) where T : class
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        /// <summary>
        /// 确保配置文件已加载
        /// </summary>
        private void EnsureConfigLoaded()
        {
            if (_configData == null)
            {
                if (!File.Exists(_configPath))
                    throw new FileNotFoundException($"配置文件不存在: {_configPath}");

                var json = File.ReadAllText(_configPath);
                _configData = JObject.Parse(json);
            }
        }

        /// <summary>
        /// 解析枚举值
        /// </summary>
        private T ParseEnum<T>(string value, T defaultValue) where T : struct
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (Enum.TryParse<T>(value, true, out var result))
                return result;

            return defaultValue;
        }

        /// <summary>
        /// 解析双精度浮点数
        /// </summary>
        private double ParseDouble(string value, double defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (double.TryParse(value, out var result))
                return result;

            return defaultValue;
        }

        /// <summary>
        /// 解析整数
        /// </summary>
        private int ParseInt(string value, int defaultValue)
        {
            if (string.IsNullOrWhiteSpace(value))
                return defaultValue;

            if (int.TryParse(value, out var result))
                return result;

            return defaultValue;
        }
    }
}

