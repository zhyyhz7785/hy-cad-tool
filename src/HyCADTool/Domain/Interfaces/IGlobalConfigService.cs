using HyCADTool.Domain.ValueObjects.Configuration.Global;

namespace HyCADTool.Domain.Interfaces
{
    /// <summary>
    /// 全局配置服务接口
    /// 管理全局级别的配置（比例、样式、容差等）
    /// </summary>
    public interface IGlobalConfigService
    {
        /// <summary>
        /// 获取当前全局配置
        /// </summary>
        GlobalConfiguration GetConfiguration();

        /// <summary>
        /// 更新全局配置
        /// </summary>
        void UpdateConfiguration(GlobalConfiguration config);

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        void LoadConfiguration();

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        void SaveConfiguration();

        /// <summary>
        /// 重置为默认配置
        /// </summary>
        void ResetToDefault();

        // ========== 便捷访问属性 ==========

        /// <summary>
        /// 当前比例
        /// </summary>
        double Scale { get; set; }

        /// <summary>
        /// 标高长度
        /// </summary>
        double ElevationLength { get; set; }

        /// <summary>
        /// 容差配置
        /// </summary>
        ToleranceConfig Tolerance { get; }

        /// <summary>
        /// 样式配置
        /// </summary>
        StylesConfig Styles { get; }

        /// <summary>
        /// 路径配置
        /// </summary>
        PathConfig Paths { get; }
    }
}

