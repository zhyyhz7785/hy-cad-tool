namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata
{
    /// <summary>
    /// 道路模块使用的 AutoCAD 标准图层名 + 颜色索引常量。
    ///
    /// 命名规范：<c>05_hy_道路_子模块</c>（紧邻既有 <c>04_hy_*</c> 前缀后面）。
    /// - <see cref="AlignmentLayer"/>：P1 平面线位中心线（从 JSON 反向绘制时使用）。
    /// - 其他预留：P2 纵断面 / P5 走廊 / P3 标线。命令层 v1 暂不强制使用，面向 v2 扩展。
    ///
    /// 配合 <c>PluginInitializer.GetRequiredLayers()</c> 在插件启动时创建。
    /// </summary>
    public static class HyRoadLayers
    {
        public const string AlignmentLayer = "05_hy_道路_平面线位";
        public const short AlignmentColor = 6; // 品红：视觉上与既有道路人行横道图层区分

        public const string ProfileLayer = "05_hy_道路_纵断面";
        public const short ProfileColor = 2; // 黄：P2 预留

        public const string CorridorLayer = "05_hy_道路_走廊";
        public const short CorridorColor = 8; // 浅灰：P5 预留

        public const string MarkingLayer = "05_hy_道路_标线";
        public const short MarkingColor = 3; // 绿：P3 预留

        public const string StationLayer = "05_hy_道路_桩号";
        public const short StationColor = 7; // 白/黑：与图底线色反白，主桩文字清晰可读

        /// <summary>几何点标注（BP / EP / PI / BC / EC / TS / SC / CS / ST）。hyRoadAlnGeomPt 专用。</summary>
        public const string GeometryPointLayer = "05_hy_道路_几何点";
        public const short GeometryPointColor = 4; // 青：与桩号主文字（白）/ 中心线（品红）视觉分离

        /// <summary>偏移辅助线（hyRoadAlnOffset 左右路幅 / 路缘石示意线）。</summary>
        public const string OffsetLayer = "05_hy_道路_偏移线";
        public const short OffsetColor = 30; // 橙：与中心线（品红）/ 桩号（白）区分

        /// <summary>
        /// 平面交叉口转角圆弧（hyRoadIntersection）。
        /// <para>存放 <c>Intersection.CornerArcs</c> 转换得到的 AutoCAD <see cref="Autodesk.AutoCAD.DatabaseServices.Arc"/>；
        /// 每条弧挂 HY_ROAD Xdata（KIND="Intersection"，ID=Intersection.Id），支持幂等重建。</para>
        /// </summary>
        public const string IntersectionLayer = "05_hy_道路_交叉口";
        public const short IntersectionColor = 1; // 红：交叉口在平面图上应显眼，与中心线（品红）区分

        /// <summary>
        /// 缘石坡道（hyRoadCurbRamp）。GB 50763 §3.2 无障碍坡道，挂 HY_ROAD Xdata（KIND="CurbRamp"，ID=Intersection.Id）。
        /// </summary>
        public const string CurbRampLayer = "05_hy_道路_缘石坡道";
        public const short CurbRampColor = 11; // 淡红：与交叉口（红）同色系但明度较低，区别于主体转角圆弧

        /// <summary>
        /// 盲道（hyRoadTactilePaving）。GB 50763 §3.3 行进盲道 / 提示盲道，挂 HY_ROAD Xdata（KIND="TactilePaving"，ID=Intersection.Id）。
        /// </summary>
        public const string TactilePavingLayer = "05_hy_道路_盲道";
        public const short TactilePavingColor = 42; // 土黄：实际盲道材质为黄色地砖

        // =========================================================================
        //  M3 标准横断面图：9 个图层
        // =========================================================================
        // 命名：05_hy_道路_横断面_<语义>，允许用户按图层批量改色 / 冻结 / 出图样板匹配

        /// <summary>横断面-轮廓（主轮廓线、顶面 polyline）。</summary>
        public const string CrossSectionOutlineLayer = "05_hy_道路_横断面_轮廓";
        public const short CrossSectionOutlineColor = 7; // 白

        /// <summary>横断面-中心线（虚线，细）。</summary>
        public const string CrossSectionCenterlineLayer = "05_hy_道路_横断面_中心线";
        public const short CrossSectionCenterlineColor = 1; // 红

        /// <summary>横断面-机动车道填色 / 阴影。</summary>
        public const string CrossSectionPavementLayer = "05_hy_道路_横断面_车行道";
        public const short CrossSectionPavementColor = 5; // 蓝

        /// <summary>横断面-人行道填色 / 阴影。</summary>
        public const string CrossSectionSidewalkLayer = "05_hy_道路_横断面_人行道";
        public const short CrossSectionSidewalkColor = 52; // 橙黄

        /// <summary>横断面-路牙（立缘石 / 平石的"L 型"凸起几何）。v2 新增，与人行道分离便于改色 / 冻结。</summary>
        public const string CrossSectionKerbLayer = "05_hy_道路_横断面_路牙";
        public const short CrossSectionKerbColor = 8; // 深灰：立缘石的素色混凝土质感

        /// <summary>横断面-绿化带填色 / 阴影（含中分带 / 分车绿带）。</summary>
        public const string CrossSectionGreenLayer = "05_hy_道路_横断面_绿化带";
        public const short CrossSectionGreenColor = 92; // 绿

        /// <summary>横断面-尺寸链（底部 / 顶部尺寸线）。</summary>
        public const string CrossSectionDimensionLayer = "05_hy_道路_横断面_尺寸链";
        public const short CrossSectionDimensionColor = 4; // 青

        /// <summary>横断面-横坡 / 高差 / 条带名文字（MTEXT）。</summary>
        public const string CrossSectionAnnotationLayer = "05_hy_道路_横断面_文字";
        public const short CrossSectionAnnotationColor = 7; // 白

        /// <summary>横断面-图题（底部居中大字）。</summary>
        public const string CrossSectionTitleLayer = "05_hy_道路_横断面_图题";
        public const short CrossSectionTitleColor = 3; // 绿

        /// <summary>横断面-方位 / 箭头。</summary>
        public const string CrossSectionOrientationLayer = "05_hy_道路_横断面_方位";
        public const short CrossSectionOrientationColor = 6; // 品红

        /// <summary>
        /// 返回本模块需要注册的所有图层（(name, color) 对）。
        /// </summary>
        public static (string layerName, short colorIndex)[] GetAll()
        {
            return new[]
            {
                (AlignmentLayer, AlignmentColor),
                (ProfileLayer, ProfileColor),
                (CorridorLayer, CorridorColor),
                (MarkingLayer, MarkingColor),
                (StationLayer, StationColor),
                (GeometryPointLayer, GeometryPointColor),
                (OffsetLayer, OffsetColor),
                (IntersectionLayer, IntersectionColor),
                (CurbRampLayer, CurbRampColor),
                (TactilePavingLayer, TactilePavingColor),
                (CrossSectionOutlineLayer, CrossSectionOutlineColor),
                (CrossSectionCenterlineLayer, CrossSectionCenterlineColor),
                (CrossSectionPavementLayer, CrossSectionPavementColor),
                (CrossSectionSidewalkLayer, CrossSectionSidewalkColor),
                (CrossSectionKerbLayer, CrossSectionKerbColor),
                (CrossSectionGreenLayer, CrossSectionGreenColor),
                (CrossSectionDimensionLayer, CrossSectionDimensionColor),
                (CrossSectionAnnotationLayer, CrossSectionAnnotationColor),
                (CrossSectionTitleLayer, CrossSectionTitleColor),
                (CrossSectionOrientationLayer, CrossSectionOrientationColor),
            };
        }
    }
}
