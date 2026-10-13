using System;
using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Interaction
{
    /// <summary>
    /// 收集尚未写入数据库的临时对象；Dispose 时统一释放。已 Append 的对象先 <see cref="Detach"/>。
    /// </summary>
    public sealed class DisposableBag : IDisposable
    {
        private readonly List<IDisposable> _items = new List<IDisposable>();
        private bool _disposed;

        public T Add<T>(T item) where T : IDisposable
        {
            if (item != null)
                _items.Add(item);
            return item;
        }

        public void Detach(IDisposable item)
        {
            if (item == null)
                return;
            _items.Remove(item);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                try { _items[i].Dispose(); }
                catch { }
            }
            _items.Clear();
        }
    }
}
