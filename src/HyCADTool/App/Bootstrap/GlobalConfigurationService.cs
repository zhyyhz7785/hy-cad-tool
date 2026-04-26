using HyCADTool.Shell.Contracts;
using HyCADTool.Shell.Configuration.Global;
using System;
using System.IO;

namespace HyCADTool.App.Bootstrap
{
    /// <summary>
    /// 全局配置服务实现
    /// 管理全局级别的配置（比例、样式、容差等）
    /// </summary>
    public class GlobalConfigurationService : IGlobalConfigService
    {
        private GlobalConfiguration _currentConfig;
        private readonly string _configFilePath;
        private readonly JsonConfigurationLoader _jsonLoader;

        public GlobalConfigurationService() : this(null)
        {
        }

        public GlobalConfigurationService(string configFilePath)
        {
            try
            {
                // 如果未提供路径，尝试从多个位置查找
                if (string.IsNullOrEmpty(configFilePath))
                {
                    _configFilePath = FindConfigFile();
                }
                else
                {
                    _configFilePath = configFilePath;
                }
                
                _jsonLoader = new JsonConfigurationLoader(_configFilePath);
                
                // 初始化时加载配置
                LoadConfiguration();
            }
            catch (Exception ex)
            {
                // 构造函数中的任何异常都会导致 DI 失败
                // 使用默认配置并记录错误
                System.Diagnostics.Debug.WriteLine($"GlobalConfigurationService 初始化失败: {ex.Message}");
                _configFilePath = "config.json";
                _jsonLoader = new JsonConfigurationLoader(_configFilePath);
                _currentConfig = GlobalConfiguration.CreateDefault();
            }
        }

        /// <summary>
        /// 查找配置文件
        /// 依次尝试：Assembly.Location 目录、当前目录、应用程序目录
        /// </summary>
        private string FindConfigFile()
        {
            var locations = new[]
            {
                // 1. 尝试从 Assembly.Location（正常情况）
                Path.GetDirectoryName(typeof(GlobalConfigurationService).Assembly.Location),
                // 2. 当前工作目录
                Environment.CurrentDirectory,
                // 3. AppDomain 基目录
                AppDomain.CurrentDomain.BaseDirectory,
                // 4. 已知的开发路径（用于热重启场景）
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "HyCADTool", "bin", "Debug")
            };

            foreach (var location in locations)
            {
                if (string.IsNullOrEmpty(location)) continue;
                
                var configPath = Path.Combine(location, "config.json");
                if (File.Exists(configPath))
                {
                    return configPath;
                }
            }

            // 如果都找不到，返回默认路径（后续会使用默认配置）
            return Path.Combine(Environment.CurrentDirectory, "config.json");
        }

        /// <summary>
        /// 获取当前全局配置
        /// </summary>
        public GlobalConfiguration GetConfiguration()
        {
            return _currentConfig ?? GlobalConfiguration.CreateDefault();
        }

        /// <summary>
        /// 更新全局配置
        /// </summary>
        public void UpdateConfiguration(GlobalConfiguration config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (!config.IsValid(out string error))
                throw new ArgumentException($"配置无效: {error}");

            _currentConfig = config;
        }

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public void LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    // 从 JSON 加载配置
                    var loadedConfig = _jsonLoader.LoadBaseConfiguration();
                    if (loadedConfig != null && loadedConfig.IsValid(out _))
                    {
                        _currentConfig = loadedConfig;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                // 加载失败时使用默认配置
                System.Diagnostics.Debug.WriteLine($"加载全局配置失败: {ex.Message}");
            }

            // 使用默认配置
            _currentConfig = GlobalConfiguration.CreateDefault();
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public void SaveConfiguration()
        {
            try
            {
                if (_currentConfig != null)
                {
                    _jsonLoader.SaveToFile(_currentConfig, _configFilePath);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"保存全局配置失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        public void ResetToDefault()
        {
            _currentConfig = GlobalConfiguration.CreateDefault();
        }

        // ========== 便捷访问属性 ==========

        /// <summary>
        /// 当前比例
        /// </summary>
        public double Scale
        {
            get => GetConfiguration().Scale.Default;
            set
            {
                var config = GetConfiguration();
                config.Scale.Default = value;
                UpdateConfiguration(config);
            }
        }

        /// <summary>
        /// 标高长度
        /// </summary>
        public double ElevationLength
        {
            get => GetConfiguration().ElevationLength;
            set
            {
                var config = GetConfiguration();
                config.ElevationLength = value;
                UpdateConfiguration(config);
            }
        }

        /// <summary>
        /// 容差配置
        /// </summary>
        public ToleranceConfig Tolerance => GetConfiguration().Tolerance;

        /// <summary>
        /// 样式配置
        /// </summary>
        public StylesConfig Styles => GetConfiguration().Styles;

        /// <summary>
        /// 路径配置
        /// </summary>
        public PathConfig Paths => GetConfiguration().Paths;
    }
}

