using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace HyCADTool.Domain.Events.Road
{
    /// <summary>
    /// <see cref="IRoadEventBus"/> 的默认实现。
    ///
    /// 实现要点（对应 P0 决策 3 - 真实发布订阅）：
    /// 1. 订阅者按事件类型分桶（<see cref="_subscribers"/> 按 Type 分组）。
    /// 2. 所有实例方法订阅者以 <c>WeakReference</c> 持有 Target，避免阻止 GC；
    ///    静态方法 / Lambda 捕获的闭包因 Target 非空会被弱引用，一旦闭包无其他引用即被回收。
    /// 3. 每次 <see cref="Publish{T}"/> 时先快照活跃订阅者，再串行调用；
    ///    订阅 / 退订使用每桶的 lock 串行化（ConcurrentBag 会破坏顺序，改用 List + lock）。
    /// 4. 订阅者回调抛异常时捕获、通过 <see cref="SubscriberErrored"/> 转发，不影响其他订阅者。
    /// 5. 单例（Autofac <c>SingleInstance</c>）：全插件共享一个总线；
    ///    分发基于 <see cref="IRoadChangeEvent.RoadDesignId"/> 由订阅者自行判断（v1 单项目，无需过滤）。
    /// </summary>
    public sealed class RoadEventBus : IRoadEventBus
    {
        private readonly ConcurrentDictionary<Type, SubscriberList> _subscribers
            = new ConcurrentDictionary<Type, SubscriberList>();

        public event EventHandler<RoadEventBusErrorEventArgs> SubscriberErrored;

        public int SubscriberCount
        {
            get
            {
                int total = 0;
                foreach (var kvp in _subscribers)
                    total += kvp.Value.CountAlive();
                return total;
            }
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IRoadChangeEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var list = _subscribers.GetOrAdd(typeof(TEvent), _ => new SubscriberList());
            var token = list.Add(handler);
            return new Subscription(() => list.Remove(token));
        }

        public void Publish<TEvent>(TEvent evt) where TEvent : IRoadChangeEvent
        {
            if (evt == null) throw new ArgumentNullException(nameof(evt));

            Type key = typeof(TEvent);
            if (!_subscribers.TryGetValue(key, out var list)) return;

            var snapshot = list.Snapshot();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var weakTarget = snapshot[i].WeakTarget;
                var method = snapshot[i].Method;

                object target = null;
                if (weakTarget != null)
                {
                    if (!weakTarget.TryGetTarget(out target))
                    {
                        list.Remove(snapshot[i]);
                        continue;
                    }
                }

                try
                {
                    method.Invoke(target, new object[] { evt });
                }
                catch (TargetInvocationException tiex)
                {
                    RaiseSubscriberErrored(key, tiex.InnerException ?? tiex);
                }
                catch (Exception ex)
                {
                    RaiseSubscriberErrored(key, ex);
                }
            }
        }

        private void RaiseSubscriberErrored(Type eventType, Exception ex)
        {
            var handler = SubscriberErrored;
            if (handler == null) return;
            try
            {
                handler(this, new RoadEventBusErrorEventArgs(eventType, ex));
            }
            catch
            {
                // 诊断通道自身异常不二次抛出，避免递归。
            }
        }

        /// <summary>
        /// 每种事件类型的订阅者清单（弱引用存储 + 每桶锁）。
        /// </summary>
        private sealed class SubscriberList
        {
            private readonly object _lock = new object();
            private readonly List<SubscriberToken> _items = new List<SubscriberToken>();

            public SubscriberToken Add(Delegate handler)
            {
                var token = new SubscriberToken(handler);
                lock (_lock)
                {
                    _items.Add(token);
                }
                return token;
            }

            public void Remove(SubscriberToken token)
            {
                lock (_lock)
                {
                    _items.Remove(token);
                }
            }

            public List<SubscriberToken> Snapshot()
            {
                lock (_lock)
                {
                    return new List<SubscriberToken>(_items);
                }
            }

            public int CountAlive()
            {
                int alive = 0;
                lock (_lock)
                {
                    for (int i = 0; i < _items.Count; i++)
                    {
                        if (_items[i].IsAlive) alive++;
                    }
                }
                return alive;
            }
        }

        /// <summary>
        /// 订阅者凭证：弱引用 + 方法元数据。
        /// </summary>
        private sealed class SubscriberToken
        {
            public WeakReference<object> WeakTarget { get; }
            public MethodInfo Method { get; }

            public SubscriberToken(Delegate handler)
            {
                Method = handler.Method;
                WeakTarget = handler.Target != null
                    ? new WeakReference<object>(handler.Target)
                    : null;
            }

            public bool IsAlive
            {
                get
                {
                    if (WeakTarget == null) return true;
                    return WeakTarget.TryGetTarget(out _);
                }
            }
        }

        private sealed class Subscription : IDisposable
        {
            private Action _dispose;

            public Subscription(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                var d = System.Threading.Interlocked.Exchange(ref _dispose, null);
                d?.Invoke();
            }
        }
    }
}
