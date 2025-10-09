using System;
using System.IO;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration;
using HyCADTool.Refactored.Domain.Enums;
using Newtonsoft.Json.Linq;

namespace HyCADTool.Refactored.Infrastructure.Configuration
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
        /// 加载基础配置
        /// </summary>
        public BaseConfiguration LoadBaseConfiguration()
        {
            EnsureConfigLoaded();

            try
            {
                var baseData = _configData["BaseConfigData"];
                if (baseData == null)
                    return BaseConfiguration.CreateDefault();

                // JSON 文件中的 BaseConfigData 主要包含样式配置
                // Scale 和 ElevationLength 使用默认值
                return BaseConfiguration.CreateDefault();
            }
            catch
            {
                return BaseConfiguration.CreateDefault();
            }
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

