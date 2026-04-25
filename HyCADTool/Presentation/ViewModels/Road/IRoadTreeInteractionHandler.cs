namespace HyCADTool.Presentation.ViewModels.Road
{
    /// <summary>
    /// 项目树交互动作分发（045 / M4）。
    ///
    /// 让 <see cref="RoadProjectTreeViewModel"/> 在纯 VM 层表达「双击打开 / ZoomTo / 右键菜单命令」意图，
    /// 真正的 AutoCAD / WPF 操作由具体实现（DI 注入）承担。测试时注入
    /// <see cref="NullRoadTreeInteractionHandler"/> 可以验证 VM 在各种节点 Kind 下的决策，不触 AutoCAD。
    ///
    /// 约定：
    /// <list type="bullet">
    ///   <item>所有方法对 null 节点必须静默返回（不抛）；</item>
    ///   <item>实现方自己处理 AutoCAD 事务与异常，避免污染 VM；</item>
    ///   <item>返回值保留给未来扩展（如返回 ObjectId 或 AsyncResult）；本阶段全部 void。</item>
    /// </list>
    /// </summary>
    public interface IRoadTreeInteractionHandler
    {
        /// <summary>双击节点 / 回车：打开对应的编辑器。</summary>
        void Open(RoadTreeNode node);

        /// <summary>右键「ZoomTo」：在 AutoCAD 中定位到实体（带 Xdata ID）。</summary>
        void ZoomTo(RoadTreeNode node);

        /// <summary>右键「重命名」：弹输入框让用户改 Header，然后写回领域对象。</summary>
        void Rename(RoadTreeNode node);

        /// <summary>右键「删除」：从领域聚合中移除对象（含二次确认）。</summary>
        void Delete(RoadTreeNode node);

        /// <summary>右键「导出 LandXML」：只对 Alignment / Profile / Surface 可见。</summary>
        void ExportLandXml(RoadTreeNode node);

        /// <summary>选中节点：在 AutoCAD 内高亮对应实体（不改视图）。</summary>
        void Highlight(RoadTreeNode node);

        /// <summary>工作流：新建路线并命名（<c>hyRoadAName</c>）。</summary>
        void NewAlignment();

        /// <summary>工作流：为路线挂接横断面区段（<c>hyRoadAlnAssign</c>）。</summary>
        void AssignCrossSection(RoadTreeNode contextNode);

        /// <summary>工作流：按区段/模板出平面（<c>hyRoadAlnPlan</c>）。</summary>
        void GeneratePlanFromTree(RoadTreeNode contextNode);

        /// <summary>工作流：自动检测并生成交叉口（<c>hyRoadAutoIntersection</c>）。</summary>
        void DetectIntersections();
    }

    /// <summary>
    /// 默认空实现（045 / M4）：用于单元测试 / 设计时 / 未注入 handler 的场景。
    /// 不抛不闪。可通过 <see cref="Calls"/> 属性读取调用次数做断言。
    /// </summary>
    public sealed class NullRoadTreeInteractionHandler : IRoadTreeInteractionHandler
    {
        /// <summary>调用计数（测试辅助）。</summary>
        public RoadTreeHandlerCallCount Calls { get; } = new RoadTreeHandlerCallCount();

        public void Open(RoadTreeNode node) { if (node != null) Calls.Open++; }
        public void ZoomTo(RoadTreeNode node) { if (node != null) Calls.ZoomTo++; }
        public void Rename(RoadTreeNode node) { if (node != null) Calls.Rename++; }
        public void Delete(RoadTreeNode node) { if (node != null) Calls.Delete++; }
        public void ExportLandXml(RoadTreeNode node) { if (node != null) Calls.ExportLandXml++; }
        public void Highlight(RoadTreeNode node) { if (node != null) Calls.Highlight++; }
        public void NewAlignment() { Calls.NewAlignment++; }
        public void AssignCrossSection(RoadTreeNode contextNode) { Calls.AssignCrossSection++; }
        public void GeneratePlanFromTree(RoadTreeNode contextNode) { Calls.GeneratePlan++; }
        public void DetectIntersections() { Calls.DetectIntersections++; }
    }

    /// <summary>测试辅助：各方法命中次数。</summary>
    public sealed class RoadTreeHandlerCallCount
    {
        public int Open { get; set; }
        public int ZoomTo { get; set; }
        public int Rename { get; set; }
        public int Delete { get; set; }
        public int ExportLandXml { get; set; }
        public int Highlight { get; set; }
        public int NewAlignment { get; set; }
        public int AssignCrossSection { get; set; }
        public int GeneratePlan { get; set; }
        public int DetectIntersections { get; set; }
    }
}
