namespace HyCADTool.Domain.ValueObjects.Configuration.Global
{
    /// <summary>
    /// HyCAD 项目内嵌 .lin（Resources/HyCAD-Linetypes.lin）中的线型名。
    /// 与图层表、StyleService.LoadLinetype 调用处保持一致。
    /// </summary>
    public static class HyLinetypeNames
    {
        /// <summary>长划-点-短划-点，用于轴线/中心线（图案 A,12,-3,5,-3）。</summary>
        public const string Center = "点划线";

        /// <summary>短划-间隔虚线，用于 -虚 孪生层（图案 A,3,-2）。</summary>
        public const string Dashed = "虚线";
    }
}
