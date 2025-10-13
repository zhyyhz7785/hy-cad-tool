namespace HyCADTool.Refactored.Domain.Interfaces
{
    /// <summary>
    /// 模块配置服务接口
    /// 管理各功能模块的独立配置
    /// </summary>
    public interface IModuleConfigService
    {
        /// <summary>
        /// 获取指定模块的配置
        /// </summary>
        T GetModuleConfig<T>(string moduleName) where T : class;

        /// <summary>
        /// 更新指定模块的配置
        /// </summary>
        void UpdateModuleConfig<T>(string moduleName, T config) where T : class;

        /// <summary>
        /// 重置模块配置为默认值
        /// </summary>
        void ResetModuleConfig(string moduleName);

        /// <summary>
        /// 从文件加载所有模块配置
        /// </summary>
        void LoadAllConfigurations();

        /// <summary>
        /// 保存所有模块配置到文件
        /// </summary>
        void SaveAllConfigurations();

        /// <summary>
        /// 检查模块配置是否存在
        /// </summary>
        bool HasModuleConfig(string moduleName);
    }
}

