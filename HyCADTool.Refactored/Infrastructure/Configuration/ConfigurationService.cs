using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.Global;
using HyCADTool.Refactored.Presentation.ViewModels;

namespace HyCADTool.Refactored.Infrastructure.Configuration
{
    /// <summary>
    /// 配置服务总协调实现
    /// 提供统一的配置访问入口（全局配置 + 模块配置）
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly IGlobalConfigService _globalConfigService;
        private readonly IModuleConfigService _moduleConfigService;

        public ConfigurationService(
            IGlobalConfigService globalConfigService,
            IModuleConfigService moduleConfigService)
        {
            _globalConfigService = globalConfigService;
            _moduleConfigService = moduleConfigService;
        }

        /// <summary>
        /// 获取全局配置
        /// </summary>
        public GlobalConfiguration Global => _globalConfigService.GetConfiguration();

        /// <summary>
        /// 获取指定模块的配置
        /// </summary>
        public T GetModuleConfig<T>(string moduleName) where T : class
        {
            return _moduleConfigService.GetModuleConfig<T>(moduleName);
        }

        /// <summary>
        /// 更新指定模块的配置
        /// </summary>
        public void UpdateModuleConfig<T>(string moduleName, T config) where T : class
        {
            _moduleConfigService.UpdateModuleConfig(moduleName, config);
        }

        /// <summary>
        /// 加载所有配置（全局 + 模块）
        /// </summary>
        public void LoadAll()
        {
            _globalConfigService.LoadConfiguration();
            _moduleConfigService.LoadAllConfigurations();
        }

        /// <summary>
        /// 保存所有配置（全局 + 模块）
        /// </summary>
        public void SaveAll()
        {
            _globalConfigService.SaveConfiguration();
            _moduleConfigService.SaveAllConfigurations();
        }

        /// <summary>
        /// 重置所有配置为默认值
        /// </summary>
        public void ResetAll()
        {
            _globalConfigService.ResetToDefault();
            // 重置所有模块配置
            _moduleConfigService.ResetModuleConfig("Pile");
            _moduleConfigService.ResetModuleConfig("Foundation");
            _moduleConfigService.ResetModuleConfig("Reinforcement");
            _moduleConfigService.ResetModuleConfig("Elevation");
        }

        // ========== 便捷访问方法 ==========

        /// <summary>
        /// 获取当前比例 — 唯一真相源：SettingsPanelViewModel
        /// </summary>
        public double GetScale()
        {
            return SettingsPanelViewModel.Current?.Scale ?? _globalConfigService.Scale;
        }

        /// <summary>
        /// 设置当前比例 — 同时写入 SettingsPanelViewModel 和全局配置
        /// </summary>
        public void SetScale(double scale)
        {
            var vm = SettingsPanelViewModel.Current;
            if (vm != null) vm.Scale = scale;
            _globalConfigService.Scale = scale;
        }
    }
}
