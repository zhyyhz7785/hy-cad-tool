using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using HyCADTool.Features.Road.Events;
using Xunit;

namespace HyCADTool.Tests.Domain.Events.Road
{
    /// <summary>
    /// P0 事件总线行为测试（对应用户决策 3：真实发布订阅）。
    ///
    /// 必须验证的 6 条硬性质：
    /// 1. Subscribe/Publish 能精确按事件类型分发；
    /// 2. 订阅者为弱引用（被 GC 回收后不再调用）；
    /// 3. 多线程并发 Publish 线程安全；
    /// 4. 单个订阅者抛异常不影响其他订阅者 + 触发 SubscriberErrored；
    /// 5. Dispose 订阅句柄立即生效；
    /// 6. 不会把 A 类型的事件递给 B 类型的订阅者（类型隔离）。
    /// </summary>
    public class RoadEventBusTests
    {
        private static AlignmentChangedEvent MakeEvent()
        {
            return new AlignmentChangedEvent(Guid.NewGuid(), Guid.NewGuid(), RoadChangeKind.Created);
        }

        [Fact]
        public void Publish_WithSingleSubscriber_InvokesHandler()
        {
            var bus = new RoadEventBus();
            AlignmentChangedEvent received = null;
            bus.Subscribe<AlignmentChangedEvent>(e => received = e);

            var evt = MakeEvent();
            bus.Publish(evt);

            received.Should().NotBeNull();
            received.Should().BeSameAs(evt);
        }

        [Fact]
        public void Publish_WithMultipleSubscribers_InvokesAll()
        {
            var bus = new RoadEventBus();
            int count1 = 0, count2 = 0, count3 = 0;
            bus.Subscribe<AlignmentChangedEvent>(_ => count1++);
            bus.Subscribe<AlignmentChangedEvent>(_ => count2++);
            bus.Subscribe<AlignmentChangedEvent>(_ => count3++);

            bus.Publish(MakeEvent());

            count1.Should().Be(1);
            count2.Should().Be(1);
            count3.Should().Be(1);
        }

        [Fact]
        public void Publish_TypeIsolation_OnlyMatchingTypeReceives()
        {
            var bus = new RoadEventBus();
            int alignmentCount = 0, profileCount = 0;

            bus.Subscribe<AlignmentChangedEvent>(_ => alignmentCount++);
            bus.Subscribe<ProfileChangedEvent>(_ => profileCount++);

            bus.Publish(MakeEvent());

            alignmentCount.Should().Be(1);
            profileCount.Should().Be(0);
        }

        [Fact]
        public void Dispose_Subscription_StopsDelivery()
        {
            var bus = new RoadEventBus();
            int count = 0;
            var subscription = bus.Subscribe<AlignmentChangedEvent>(_ => count++);

            bus.Publish(MakeEvent());
            count.Should().Be(1);

            subscription.Dispose();
            bus.Publish(MakeEvent());
            count.Should().Be(1, "Dispose 后不再接收事件");
        }

        [Fact]
        public void HandlerException_DoesNotBreakOtherSubscribers_AndRaisesSubscriberErrored()
        {
            var bus = new RoadEventBus();
            int goodCount = 0;
            Exception captured = null;

            bus.SubscriberErrored += (s, e) => captured = e.Exception;
            bus.Subscribe<AlignmentChangedEvent>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<AlignmentChangedEvent>(_ => goodCount++);

            Action act = () => bus.Publish(MakeEvent());
            act.Should().NotThrow("订阅者异常必须隔离");

            goodCount.Should().Be(1, "另一个订阅者必须正常收到事件");
            captured.Should().NotBeNull();
            captured.Should().BeOfType<InvalidOperationException>();
        }

        [Fact]
        public void Publish_Concurrently_ThreadSafe_AndCountsMatch()
        {
            var bus = new RoadEventBus();
            int count = 0;
            bus.Subscribe<AlignmentChangedEvent>(_ => Interlocked.Increment(ref count));

            const int threads = 8;
            const int perThread = 500;
            Parallel.For(0, threads, _ =>
            {
                for (int i = 0; i < perThread; i++)
                    bus.Publish(MakeEvent());
            });

            count.Should().Be(threads * perThread);
        }

        [Fact]
        public void SubscriberCount_ReflectsLiveSubscriptions()
        {
            var bus = new RoadEventBus();
            bus.SubscriberCount.Should().Be(0);

            var s1 = bus.Subscribe<AlignmentChangedEvent>(_ => { });
            var s2 = bus.Subscribe<ProfileChangedEvent>(_ => { });
            bus.SubscriberCount.Should().BeGreaterOrEqualTo(2);

            s1.Dispose();
            s2.Dispose();
        }

        [Fact]
        public void Subscribe_WithNullHandler_Throws()
        {
            var bus = new RoadEventBus();
            Action act = () => bus.Subscribe<AlignmentChangedEvent>(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void Publish_WithNullEvent_Throws()
        {
            var bus = new RoadEventBus();
            Action act = () => bus.Publish<AlignmentChangedEvent>(null);
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void WeakReference_AllowsHandlerToBeCollected()
        {
            var bus = new RoadEventBus();
            int count = 0;
            SubscribeAndReturn(bus, () => count++);

            for (int i = 0; i < 5; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            bus.Publish(MakeEvent());
            count.Should().Be(0, "回收后的处理器不应再被调用（弱引用语义）");
        }

        /// <summary>
        /// 把订阅动作包在独立方法里，让 target 对象可以被 GC 回收。
        /// </summary>
        private static void SubscribeAndReturn(IRoadEventBus bus, Action onReceived)
        {
            object holder = new object();
            _ = bus.Subscribe<AlignmentChangedEvent>(e =>
            {
                _ = holder;
                onReceived();
            });
        }

        [Fact]
        public void Publish_InsideHandler_DoesNotDeadlock_AndDeliversNested()
        {
            var bus = new RoadEventBus();
            var log = new List<string>();

            bus.Subscribe<AlignmentChangedEvent>(e =>
            {
                log.Add("outer");
                if (log.Count == 1)
                    bus.Publish(new ProfileChangedEvent(e.RoadDesignId, Guid.NewGuid(), Guid.NewGuid(), RoadChangeKind.Created));
            });
            bus.Subscribe<ProfileChangedEvent>(_ => log.Add("inner"));

            bus.Publish(MakeEvent());

            log.Should().ContainInOrder("outer", "inner");
        }
    }
}
