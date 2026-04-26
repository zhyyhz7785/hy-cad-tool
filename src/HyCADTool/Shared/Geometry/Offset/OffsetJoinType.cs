namespace HyCADTool.Shared.Geometry.Offset
{
    /// <summary>
    /// 多边形偏移时的转角连接类型
    /// 源代码参考：Clipper2.JoinType
    /// License: Boost Software License 1.0
    /// Original: https://github.com/AngusJohnson/Clipper2
    /// </summary>
    public enum OffsetJoinType
    {
        /// <summary>
        /// 圆角连接
        /// 对应 Clipper2.JoinType.Round
        /// 在转角处插入圆弧，最平滑
        /// </summary>
        Round,
        
        /// <summary>
        /// 斜接连接
        /// 对应 Clipper2.JoinType.Miter
        /// 延伸两条边直到相交，可能产生尖角
        /// </summary>
        Miter,
        
        /// <summary>
        /// 直角连接
        /// 对应 Clipper2.JoinType.Square
        /// 在转角处添加垂直延伸
        /// </summary>
        Square
    }
}












