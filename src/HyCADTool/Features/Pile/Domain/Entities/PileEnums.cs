namespace HyCADTool.Features.Pile.Domain.Entities
{
    /// <summary>
    /// 桩截面类型（Pile Section Type）
    /// </summary>
    public enum PileSectionType
    {
        /// <summary>
        /// 圆形截面（Circular Section）
        /// </summary>
        Circle,

        /// <summary>
        /// 方形截面（Square Section）
        /// </summary>
        Square
    }

    /// <summary>
    /// 桩类型（Pile Type）
    /// </summary>
    public enum PileType
    {
        /// <summary>
        /// 角桩（Corner Pile）
        /// </summary>
        Corner,

        /// <summary>
        /// 边桩（Edge Pile）
        /// </summary>
        Edge,

        /// <summary>
        /// 中桩（Middle Pile）
        /// </summary>
        Middle
    }

    /// <summary>
    /// 桩布置方式（Pile Arrangement Type）
    /// </summary>
    public enum PileArrangementType
    {
        /// <summary>
        /// 矩形布置（Rectangular Layout）
        /// </summary>
        Rectangle,

        /// <summary>
        /// 梅花形布置（Circular/Staggered Layout）
        /// </summary>
        Circular
    }
}

