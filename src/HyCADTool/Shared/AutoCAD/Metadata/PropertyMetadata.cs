namespace HyCADTool.Shared.AutoCAD.Metadata
{
    /// <summary>
    /// 实体属性元数据
    /// 用于描述 AutoCAD 实体的可筛选属性信息
    /// </summary>
    public class PropertyMetadata
    {
        /// <summary>
        /// 实体类型名称（如 "Line", "Circle", "Polyline"）
        /// </summary>
        public string EntityType { get; set; }

        /// <summary>
        /// 属性名称（如 "Length", "Radius", "Area"）
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// 属性类型（如 "double", "Int32", "string", "Point3d"）
        /// </summary>
        public string PropertyType { get; set; }

        /// <summary>
        /// 显示名称（中文，如 "长度", "半径", "面积"）
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// 分类（如 "几何属性", "样式类属性", "其他属性"）
        /// </summary>
        public string Category { get; set; }
    }
}
