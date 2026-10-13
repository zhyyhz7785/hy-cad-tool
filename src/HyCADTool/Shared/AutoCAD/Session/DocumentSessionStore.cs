using System;
using System.Runtime.CompilerServices;

namespace HyCADTool.Shared.AutoCAD.Session
{
    /// <summary>
    /// 按文档隔离的会话仓库。键应是 AutoCAD 的 Database（同一张图另存为后对象不变）；
    /// <see cref="ConditionalWeakTable{TKey,TValue}"/> 挂在键上，键被回收时袋子一起走。
    /// 无 AutoCAD 引用，Linux 测试用普通对象当键。
    /// </summary>
    public static class DocumentSessionStore
    {
        private static readonly ConditionalWeakTable<object, DocumentSession> Map =
            new ConditionalWeakTable<object, DocumentSession>();
        private static readonly object Gate = new object();

        public static T Get<T>(object documentKey) where T : class, new()
        {
            if (documentKey == null)
                throw new ArgumentNullException(nameof(documentKey));
            lock (Gate)
                return Map.GetOrCreateValue(documentKey).Get<T>();
        }

        /// <summary>
        /// 文档即将销毁时主动丢掉袋子，避免句柄复用前读到旧槽。
        /// 先 Remove 再 Dispose，与 Get 同一把锁，避免拿到已释放的袋子。
        /// </summary>
        public static void Forget(object documentKey)
        {
            if (documentKey == null)
                return;

            DocumentSession session;
            lock (Gate)
            {
                if (!Map.TryGetValue(documentKey, out session))
                    return;
                Map.Remove(documentKey);
                session.Dispose();
            }
        }
    }
}
