using System;

namespace HyCADTool.Shared.Core
{
    /// <summary>
    /// 标记进程级可变静态状态。架构测试允许带此标记的字段，调用方须写明理由。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Event | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class ProcessWideAttribute : Attribute
    {
        public string Reason { get; }

        public ProcessWideAttribute(string reason)
        {
            Reason = reason;
        }
    }
}
