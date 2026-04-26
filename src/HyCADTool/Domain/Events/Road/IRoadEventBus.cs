using System;

namespace HyCADTool.Domain.Events.Road
{
    /// <summary>
    /// 道路模型事件总线（Domain 层接口）。
    ///
    /// 设计要点（P0 决策 3 - 接口 + 真实发布订阅）：
    /// - 按事件类型精确分发（订阅 <see cref="AlignmentChangedEvent"/> 时不会收到 <see cref="ProfileChangedEvent"/>）。
    /// - 订阅者以弱引用持有，防止面板 ViewModel / 服务释放后内存泄漏。
    /// - 线程安全：允许任意线程 <see cref="Publish{T}"/>；订阅 / 退订同步。
    /// - 异常隔离：单个订阅者抛出异常不影响其他订阅者，错误走 <see cref="SubscriberErrored"/> 或内部日志。
    ///
    /// v1.1 起事件总线暂无默认订阅者（持久化改为命令收尾同步写盘）；
    /// v2 起扩展到 Blender 桥 / 代码检测 / 实时渲染 / 面板 UI 实时计数刷新等消费者。
    /// </summary>
    public interface IRoadEventBus
    {
        /// <summary>
        /// 订阅事件。
        /// </summary>
        /// <typeparam name="TEvent">事件类型。</typeparam>
        /// <param name="handler">事件处理委托。内部以弱引用持有其 Target（静态方法除外）。</param>
        /// <returns>可释放的订阅凭证；Dispose 即退订。</returns>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IRoadChangeEvent;

        /// <summary>
        /// 发布事件。所有匹配类型的活跃订阅者都会被调用；已释放的弱引用自动清理。
        /// </summary>
        void Publish<TEvent>(TEvent evt) where TEvent : IRoadChangeEvent;

        /// <summary>
        /// 订阅者回调抛出异常时触发；可用于日志、诊断、面板提示。
        /// 允许为 <c>null</c>，忽略异常。
        /// </summary>
        event EventHandler<RoadEventBusErrorEventArgs> SubscriberErrored;

        /// <summary>
        /// 当前所有订阅者数量（诊断用）。
        /// </summary>
        int SubscriberCount { get; }
    }

    /// <summary>
    /// 订阅者异常事件参数。
    /// </summary>
    public sealed class RoadEventBusErrorEventArgs : EventArgs
    {
        public Type EventType { get; }
        public Exception Exception { get; }

        public RoadEventBusErrorEventArgs(Type eventType, Exception exception)
        {
            EventType = eventType;
            Exception = exception;
        }
    }
}
