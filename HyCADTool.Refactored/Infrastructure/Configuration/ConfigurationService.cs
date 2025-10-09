using System;
using System.IO;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration;

namespace HyCADTool.Refactored.Infrastructure.Configuration
{
    /// <summary>
    /// 配置服务实现
    /// 策略：JSON 优先 → CSV 兜底 → 默认值
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly IStyleService _styleService;
        private readonly ILayerService _layerService;
        private JsonConfigurationLoader _jsonLoader;
        private CsvConfigurationLoader _csvLoader;

        private BaseConfiguration _baseConfig;
        private PileConfiguration _pileConfig;
        private bool _isLoaded = false;

        public ConfigurationService(IStyleService styleService, ILayerService layerService)
        {
            _styleService = styleService ?? throw new ArgumentNullException(nameof(styleService));
            _layerService = layerService ?? throw new ArgumentNullException(nameof(layerService));
        }

        /// <summary>
        /// 加载所有配置
        /// </summary>
        public void LoadAllConfigurations()
        {
            if (_isLoaded)
                return;

            // 初始化加载器
            InitializeLoaders();

            // 加载配置
            LoadBaseAndPileConfigurations();

            // 应用样式和图层到 AutoCAD
            ApplyStylesToAutoCAD();
            ApplyLayersToAutoCAD();

            _isLoaded = true;
        }

        /// <summary>
        /// 获取基础配置
        /// </summary>
        public BaseConfiguration GetBaseConfiguration()
        {
            if (!_isLoaded)
                LoadAllConfigurations();

            return _baseConfig ?? BaseConfiguration.CreateDefault();
        }

        /// <summary>
        /// 更新基础配置
        /// </summary>
        public void UpdateBaseConfiguration(BaseConfiguration config)
        {
            _baseConfig = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 获取桩基配置
        /// </summary>
        public PileConfiguration GetPileConfiguration()
        {
            if (!_isLoaded)
                LoadAllConfigurations();

            return _pileConfig ?? PileConfiguration.CreateDefault();
        }

        /// <summary>
        /// 更新桩基配置
        /// </summary>
        public void UpdatePileConfiguration(PileConfiguration config)
        {
            _pileConfig = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// 保存所有配置到文件
        /// </summary>
        public void SaveAllConfigurations()
        {
            // TODO: 实现配置保存逻辑
            throw new NotImplementedException("配置保存功能将在后续版本实现");
        }

        /// <summary>
        /// 验证配置对象
        /// </summary>
        public bool ValidateConfiguration<T>(T config) where T : class
        {
            if (config == null)
                return false;

            try
            {
                // 基本验证：尝试访问配置对象
                if (config is BaseConfiguration baseConfig)
                {
                    return baseConfig.Scale > 0 && baseConfig.ElevationLength > 0;
                }
                else if (config is PileConfiguration pileConfig)
                {
                    return pileConfig.DiameterOrEdge >= 200 && pileConfig.DiameterOrEdge <= 1200;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        #region 私有方法

        /// <summary>
        /// 初始化配置加载器
        /// </summary>
        private void InitializeLoaders()
        {
            try
            {
                // JSON 加载器（优先）
                var jsonPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "config.json");

                if (File.Exists(jsonPath))
                {
                    _jsonLoader = new JsonConfigurationLoader(jsonPath);
                }
            }
            catch
            {
                _jsonLoader = null;
            }

            try
            {
                // CSV 加载器（兜底）
                var csvPath = @"E:\BaiduSyncdisk\Code\testResult\0HyConfig.csv";
                if (File.Exists(csvPath))
                {
                    _csvLoader = new CsvConfigurationLoader(csvPath);
                }
            }
            catch
            {
                _csvLoader = null;
            }
        }

        /// <summary>
        /// 加载基础配置和桩基配置
        /// </summary>
        private void LoadBaseAndPileConfigurations()
        {
            // 策略 1：尝试 JSON 加载
            if (_jsonLoader != null)
            {
                try
                {
                    _baseConfig = _jsonLoader.LoadBaseConfiguration();
                    _pileConfig = _jsonLoader.LoadPileConfiguration();
                    return; // JSON 加载成功，直接返回
                }
                catch
                {
                    // JSON 加载失败，继续尝试 CSV
                }
            }

            // 策略 2：尝试 CSV 加载
            if (_csvLoader != null)
            {
                try
                {
                    _baseConfig = _csvLoader.LoadBaseConfiguration();
                    _pileConfig = _csvLoader.LoadPileConfiguration();
                    return; // CSV 加载成功，直接返回
                }
                catch
                {
                    // CSV 加载失败，使用默认值
                }
            }

            // 策略 3：使用默认值
            _baseConfig = BaseConfiguration.CreateDefault();
            _pileConfig = PileConfiguration.CreateDefault();
        }

        /// <summary>
        /// 应用样式配置到 AutoCAD
        /// </summary>
        private void ApplyStylesToAutoCAD()
        {
            try
            {
                // 优先从 JSON 加载样式配置
                TextStyleConfig textStyle = null;
                DimensionStyleConfig dimStyle = null;
                MLeaderStyleConfig mleaderStyle = null;

                if (_jsonLoader != null)
                {
                    try
                    {
                        textStyle = _jsonLoader.LoadTextStyleConfiguration();
                        dimStyle = _jsonLoader.LoadDimensionStyleConfiguration();
                        mleaderStyle = _jsonLoader.LoadMLeaderStyleConfiguration();
                    }
                    catch
                    {
                        // JSON 加载失败，尝试 CSV
                    }
                }

                // 如果 JSON 失败，从 CSV 加载
                if (textStyle == null && _csvLoader != null)
                {
                    try
                    {
                        var textStyles = _csvLoader.LoadTextStyleConfigurations();
                        if (textStyles.Count > 0)
                            textStyle = textStyles[0];

                        var dimStyles = _csvLoader.LoadDimensionStyleConfigurations();
                        if (dimStyles.Count > 0)
                            dimStyle = dimStyles[0];
                    }
                    catch
                    {
                        // CSV 也失败，使用默认值
                    }
                }

                // 应用文字样式
                if (textStyle != null)
                {
                    _styleService.CreateOrUpdateTextStyle(textStyle);
                }
                else
                {
                    _styleService.CreateOrUpdateTextStyle(
                        TextStyleConfig.CreateDefault("HyCAD_Standard", 1.0));
                }

                // 应用标注样式
                if (dimStyle != null)
                {
                    _styleService.CreateOrUpdateDimensionStyle(dimStyle);
                }
                else
                {
                    _styleService.CreateOrUpdateDimensionStyle(
                        DimensionStyleConfig.CreateDefault("HyCAD_Dim", "HyCAD_Standard", 1.0));
                }

                // 应用多重引线样式
                if (mleaderStyle != null)
                {
                    _styleService.CreateOrUpdateMLeaderStyle(mleaderStyle);
                }
                else
                {
                    _styleService.CreateOrUpdateMLeaderStyle(
                        MLeaderStyleConfig.CreateDefault("HyCAD_Mleader", "HyCAD_Standard"));
                }
            }
            catch
            {
                // 样式创建失败，不影响插件加载
            }
        }

        /// <summary>
        /// 应用图层配置到 AutoCAD
        /// </summary>
        private void ApplyLayersToAutoCAD()
        {
            try
            {
                // 从 CSV 加载图层配置（JSON 中不包含图层配置）
                if (_csvLoader != null)
                {
                    try
                    {
                        var layers = _csvLoader.LoadLayerConfigurations();
                        foreach (var layer in layers)
                        {
                            if (!_layerService.LayerExists(layer.Name))
                            {
                                _layerService.CreateLayer(layer.Name, layer.ColorIndex);
                            }
                        }
                    }
                    catch
                    {
                        // CSV 图层加载失败，不影响插件加载
                    }
                }

                // 确保默认图层存在
                EnsureDefaultLayers();
            }
            catch
            {
                // 图层创建失败，不影响插件加载
            }
        }

        /// <summary>
        /// 确保默认图层存在
        /// </summary>
        private void EnsureDefaultLayers()
        {
            if (!_layerService.LayerExists("00_Hy_配筋"))
                _layerService.CreateLayer("00_Hy_配筋", 1); // 红色

            if (!_layerService.LayerExists("00_Hy_标注"))
                _layerService.CreateLayer("00_Hy_标注", 3); // 绿色

            if (!_layerService.LayerExists("00_Hy_轴线"))
                _layerService.CreateLayer("00_Hy_轴线", 5); // 蓝色
        }

        #endregion
    }
}

