using HyCADTool.Domain.Interfaces;
using HyCADTool.Domain.ValueObjects.Configuration.Modules;
using System;
using System.Collections.Generic;
using System.IO;

namespace HyCADTool.Shared.Bootstrap
{
    /// <summary>
    /// 模块配置服务实现
    /// 管理各功能模块的独立配置
    /// </summary>
    public class ModuleConfigurationService : IModuleConfigService
    {
        private readonly Dictionary<string, object> _moduleConfigs;
        private readonly string _configFilePath;
        private readonly JsonConfigurationLoader _jsonLoader;

        public ModuleConfigurationService() : this(null)
        {
        }

        public ModuleConfigurationService(string configFilePath)
        {
            _moduleConfigs = new Dictionary<string, object>();
            
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
                LoadAllConfigurations();
            }
            catch (Exception ex)
            {
                // 构造函数中的任何异常都会导致 DI 失败
                // 使用默认配置并记录错误
                System.Diagnostics.Debug.WriteLine($"ModuleConfigurationService 初始化失败: {ex.Message}");
                _configFilePath = "config.json";
                _jsonLoader = new JsonConfigurationLoader(_configFilePath);
                LoadDefaultConfigurations();
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
                Path.GetDirectoryName(typeof(ModuleConfigurationService).Assembly.Location),
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
        /// 获取指定模块的配置
        /// </summary>
        public T GetModuleConfig<T>(string moduleName) where T : class
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名称不能为空", nameof(moduleName));

            // 如果缓存中存在，直接返回
            if (_moduleConfigs.TryGetValue(moduleName, out var config))
            {
                return config as T;
            }

            // 否则创建默认配置并缓存
            var defaultConfig = CreateDefaultConfig<T>(moduleName);
            if (defaultConfig != null)
            {
                _moduleConfigs[moduleName] = defaultConfig;
                return defaultConfig;
            }

            return null;
        }

        /// <summary>
        /// 更新指定模块的配置
        /// </summary>
        public void UpdateModuleConfig<T>(string moduleName, T config) where T : class
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名称不能为空", nameof(moduleName));

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            _moduleConfigs[moduleName] = config;
        }

        /// <summary>
        /// 重置模块配置为默认值
        /// </summary>
        public void ResetModuleConfig(string moduleName)
        {
            if (_moduleConfigs.ContainsKey(moduleName))
            {
                _moduleConfigs.Remove(moduleName);
            }
        }

        /// <summary>
        /// 从文件加载所有模块配置
        /// </summary>
        public void LoadAllConfigurations()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    // 加载各模块配置
                    LoadModuleConfig("Pile", _jsonLoader.LoadPileConfiguration());
                    
                    // 其他模块配置使用默认值（待从 JSON 加载）
                    LoadModuleConfig("Foundation", FoundationConfiguration.CreateDefault());
                    LoadModuleConfig("Reinforcement", ReinforcementConfiguration.CreateDefault());
                    LoadModuleConfig("Elevation", ElevationConfiguration.CreateDefault());
                }
                else
                {
                    LoadDefaultConfigurations();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载模块配置失败: {ex.Message}");
                LoadDefaultConfigurations();
            }
        }

        /// <summary>
        /// 保存所有模块配置到文件
        /// </summary>
        public void SaveAllConfigurations()
        {
            try
            {
                // TODO: 实现模块配置的保存逻辑
                // 需要将 _moduleConfigs 序列化到 JSON 文件
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"保存模块配置失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 检查模块配置是否存在
        /// </summary>
        public bool HasModuleConfig(string moduleName)
        {
            return _moduleConfigs.ContainsKey(moduleName);
        }

        // ========== 私有辅助方法 ==========

        private void LoadModuleConfig<T>(string moduleName, T config) where T : class
        {
            if (config != null)
            {
                _moduleConfigs[moduleName] = config;
            }
        }

        private void LoadDefaultConfigurations()
        {
            _moduleConfigs["Pile"] = PileConfiguration.CreateDefault();
            _moduleConfigs["Foundation"] = FoundationConfiguration.CreateDefault();
            _moduleConfigs["Reinforcement"] = ReinforcementConfiguration.CreateDefault();
            _moduleConfigs["Elevation"] = ElevationConfiguration.CreateDefault();
        }

        private T CreateDefaultConfig<T>(string moduleName) where T : class
        {
            // 根据模块名称创建默认配置
            switch (moduleName)
            {
                case "Pile":
                    return PileConfiguration.CreateDefault() as T;
                case "Foundation":
                    return FoundationConfiguration.CreateDefault() as T;
                case "Reinforcement":
                    return ReinforcementConfiguration.CreateDefault() as T;
                case "Elevation":
                    return ElevationConfiguration.CreateDefault() as T;
                default:
                    return null;
            }
        }
    }
}

