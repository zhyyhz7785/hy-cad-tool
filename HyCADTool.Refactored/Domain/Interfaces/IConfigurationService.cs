using HyCADTool.Refactored.Domain.ValueObjects.Configuration;

namespace HyCADTool.Refactored.Domain.Interfaces
{
    /// <summary>
    /// 配置服务接口
    /// 管理所有应用配置的加载、保存和访问
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// 获取基础配置
        /// </summary>
        BaseConfiguration GetBaseConfiguration();

        /// <summary>
        /// 更新基础配置
        /// </summary>
        void UpdateBaseConfiguration(BaseConfiguration config);

        /// <summary>
        /// 获取桩基配置
        /// </summary>
        PileConfiguration GetPileConfiguration();

        /// <summary>
        /// 更新桩基配置
        /// </summary>
        void UpdatePileConfiguration(PileConfiguration config);

        /// <summary>
        /// 加载所有配置
        /// 策略：JSON 优先 → CSV 兜底 → 默认值
        /// </summary>
        void LoadAllConfigurations();

        /// <summary>
        /// 保存所有配置到文件
        /// </summary>
        void SaveAllConfigurations();

        /// <summary>
        /// 验证配置对象的有效性
        /// </summary>
        bool ValidateConfiguration<T>(T config) where T : class;
    }
}

