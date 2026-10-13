using System;
using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Session
{
    /// <summary>
    /// 一张图上的会话袋：按类型懒建槽位。文档关闭时由 <see cref="DocumentSessionStore.Forget"/> 释放。
    /// </summary>
    public sealed class DocumentSession : IDisposable
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Type, object> _slots = new Dictionary<Type, object>();
        private bool _disposed;

        public T Get<T>() where T : class, new()
        {
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(DocumentSession));

                object existing;
                if (_slots.TryGetValue(typeof(T), out existing))
                    return (T)existing;

                var created = new T();
                _slots[typeof(T)] = created;
                return created;
            }
        }

        public void Dispose()
        {
            List<object> snapshot;
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                snapshot = new List<object>(_slots.Values);
                _slots.Clear();
            }

            for (int i = 0; i < snapshot.Count; i++)
            {
                var disposable = snapshot[i] as IDisposable;
                if (disposable == null)
                    continue;
                try { disposable.Dispose(); }
                catch { }
            }
        }
    }
}
