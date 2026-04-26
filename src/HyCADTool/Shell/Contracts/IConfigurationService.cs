using HyCADTool.Domain.ValueObjects.Configuration.Global;

namespace HyCADTool.Domain.Interfaces
{
    /// <summary>
    /// 配置服务总接口
    /// 提供统一的配置访问入口（全局配置 + 模块配置）
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// 获取全局配置
        /// </summary>
        GlobalConfiguration Global { get; }

        /// <summary>
        /// 获取指定模块的配置
        /// </summary>
        T GetModuleConfig<T>(string moduleName) where T : class;

        /// <summary>
        /// 更新指定模块的配置
        /// </summary>
        void UpdateModuleConfig<T>(string moduleName, T config) where T : class;

        /// <summary>
        /// 加载所有配置（全局 + 模块）
        /// </summary>
        void LoadAll();

        /// <summary>
        /// 保存所有配置（全局 + 模块）
        /// </summary>
        void SaveAll();

        /// <summary>
        /// 重置所有配置为默认值
        /// </summary>
        void ResetAll();

        // ========== 便捷访问方法 ==========

        /// <summary>
        /// 获取当前比例
        /// </summary>
        double GetScale();

        /// <summary>
        /// 设置当前比例
        /// </summary>
        void SetScale(double scale);
    }
}
