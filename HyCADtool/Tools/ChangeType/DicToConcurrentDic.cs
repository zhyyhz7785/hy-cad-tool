using System.Collections.Concurrent;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class EtGpt
    {
        // 将 Dictionary 转换为 ConcurrentDictionary
        public static ConcurrentDictionary<TKey, TValue> ToConcurrentDictionary<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        {
            return new ConcurrentDictionary<TKey, TValue>(dictionary);
        }
        // 将 ConcurrentDictionary 转换为 Dictionary
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> concurrentDictionary)
        {
            return new Dictionary<TKey, TValue>(concurrentDictionary);
        }
    }
}
