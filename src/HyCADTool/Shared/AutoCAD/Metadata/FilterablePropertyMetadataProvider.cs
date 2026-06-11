using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Metadata
{
    /// <summary>
    /// 可筛选属性元数据提供者
    /// 从旧项目 HyCADtool/Config/FilterablePropertyMetadataProvider.cs 迁移
    /// 提供各种 AutoCAD 实体类型的可筛选属性列表
    /// </summary>
    public static class FilterablePropertyMetadataProvider
    {
        /// <summary>
        /// 获取所有实体类型的可筛选属性元数据列表
        /// </summary>
        public static List<PropertyMetadata> GetMetadataList()
        {
            return RulePropertyCatalog.GetMetadataList();
        }
    }
}
