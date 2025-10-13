using System;

namespace HyCADTool.Refactored.Domain.ValueObjects.Configuration.Global
{
    /// <summary>
    /// 多重引线样式配置值对象
    /// </summary>
    public class MLeaderStyleConfig
    {
        /// <summary>
        /// 样式名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 文字样式名称
        /// </summary>
        public string TextStyleName { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        public MLeaderStyleConfig(string name, string textStyleName)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("样式名称不能为空", nameof(name));

            if (string.IsNullOrWhiteSpace(textStyleName))
                throw new ArgumentException("文字样式名称不能为空", nameof(textStyleName));

            Name = name;
            TextStyleName = textStyleName;
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static MLeaderStyleConfig CreateDefault(string name, string textStyleName)
        {
            return new MLeaderStyleConfig(name, textStyleName);
        }

        /// <summary>
        /// 创建标准配置（基于比例）
        /// </summary>
        public static MLeaderStyleConfig CreateStandard(string baseName, string textStyleName, double scale = 1.0)
        {
            var name = scale > 1.0 ? $"{baseName}_{scale:F0}" : baseName;
            return new MLeaderStyleConfig(name, textStyleName);
        }
    }
}

