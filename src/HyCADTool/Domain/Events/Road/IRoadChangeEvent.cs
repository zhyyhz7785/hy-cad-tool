using System;

namespace HyCADTool.Domain.Events.Road
{
    /// <summary>
    /// 道路模型变更事件标记接口。
    ///
    /// 所有通过 <c>IRoadEventBus</c> 发布的道路相关事件都必须实现该接口。
    /// v1.1 起事件总线暂无默认订阅者（持久化改为命令收尾同步写盘）；
    /// v2 阶段起 Blender 插件、代码检测器、实时渲染层、面板 UI 等消费者将挂接进来。
    /// </summary>
    public interface IRoadChangeEvent
    {
        /// <summary>
        /// 事件发生的 UTC 时间戳。
        /// </summary>
        DateTime TimestampUtc { get; }

        /// <summary>
        /// 关联的道路设计项目 ID（对应 <c>RoadDesign.Id</c>）。
        /// 便于多项目 / 多文档场景下的分发过滤。
        /// </summary>
        Guid RoadDesignId { get; }
    }
}
