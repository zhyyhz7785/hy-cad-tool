namespace HyCADTool.Refactored.Domain.Interfaces
{
    /// <summary>
    /// 样式服务接口（平台无关）
    /// 定义各种AutoCAD样式管理的抽象操作
    /// </summary>
    public interface IStyleService
    {
        // === 文字样式 ===

        /// <summary>
        /// 创建文字样式
        /// </summary>
        /// <param name="styleName">样式名称</param>
        /// <param name="fontName">字体名称</param>
        /// <param name="bigFontName">大字体名称</param>
        /// <param name="textHeight">文字高度</param>
        /// <param name="widthFactor">宽度因子</param>
        /// <returns>样式ObjectId</returns>
        string CreateTextStyle(string styleName, string fontName = "tssdeng.shx", string bigFontName = "hztxt.shx", double textHeight = 2.5, double widthFactor = 0.7);

        /// <summary>
        /// 设置当前文字样式
        /// </summary>
        /// <param name="styleName">样式名称</param>
        void SetCurrentTextStyle(string styleName);

        // === 标注样式 ===

        /// <summary>
        /// 创建标注样式
        /// </summary>
        string CreateDimensionStyle(string styleName, string textStyleName = null, double scale = 1.0,
            double dimtxt = 2.5, double dimexo = 1.0, double dimexe = 1.0,
            double dimdle = 0.5, double dimgap = 1.0, double dimasz = 1.0);

        /// <summary>
        /// 设置当前标注样式
        /// </summary>
        void SetCurrentDimensionStyle(string styleName);

        // === 多重引线样式 ===

        /// <summary>
        /// 创建多重引线样式
        /// </summary>
        string CreateMLeaderStyle(string styleName, string textStyleName = null, double scale = 1.0,
            double arrowSize = 2.0, double landingGap = 0.5, double textHeight = 2.5,
            int textColorIndex = 7);

        /// <summary>
        /// 设置当前多重引线样式
        /// </summary>
        /// <param name="styleName">样式名称</param>
        void SetCurrentMLeaderStyle(string styleName);

        // === 线型样式 ===

        /// <summary>
        /// 加载线型文件
        /// </summary>
        /// <param name="linetypeFilePath">线型文件路径</param>
        /// <param name="linetypeName">要加载的线型名称，null表示加载所有</param>
        void LoadLinetype(string linetypeFilePath, string linetypeName = null);

        /// <summary>
        /// 导出线型到文件
        /// </summary>
        /// <param name="outputPath">输出文件路径</param>
        void ExportLinetypesToFile(string outputPath);

        // === 表格样式 ===

        /// <summary>
        /// 创建表格样式
        /// </summary>
        /// <param name="styleName">样式名称</param>
        /// <param name="textStyleName">关联的文字样式名称</param>
        /// <returns>样式ObjectId</returns>
        string CreateTableStyle(string styleName, string textStyleName = null);

        /// <summary>
        /// 设置当前表格样式
        /// </summary>
        /// <param name="styleName">样式名称</param>
        void SetCurrentTableStyle(string styleName);

        // === 通用样式管理 ===

        /// <summary>
        /// 检查样式是否存在
        /// </summary>
        /// <param name="styleName">样式名称</param>
        /// <param name="styleType">样式类型</param>
        /// <returns>样式是否存在</returns>
        bool StyleExists(string styleName, StyleType styleType);

        /// <summary>
        /// 删除样式
        /// </summary>
        /// <param name="styleName">样式名称</param>
        /// <param name="styleType">样式类型</param>
        /// <returns>删除成功返回true</returns>
        bool DeleteStyle(string styleName, StyleType styleType);
    }

    /// <summary>
    /// 样式类型枚举
    /// </summary>
    public enum StyleType
    {
        /// <summary>文字样式</summary>
        TextStyle,
        /// <summary>标注样式</summary>
        DimensionStyle,
        /// <summary>多重引线样式</summary>
        MLeaderStyle,
        /// <summary>表格样式</summary>
        TableStyle,
        /// <summary>线型</summary>
        Linetype
    }
}