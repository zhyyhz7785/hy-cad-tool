using System;

namespace HyCADTool.Features.DataExchange.Hyob.Presentation.ViewModels
{
    /// <summary>
    /// hyob 历史面板里单个 commit 行的只读视图模型（M10）。
    /// 不持有 HyobCommit 对象本身，仅平铺展示字段。
    /// </summary>
    public sealed class HyobHistoryItem
    {
        public string ShortHash { get; }
        public string FullHash { get; }
        public string TimeText { get; }
        public string Command { get; }
        public string Message { get; }
        public string EntityCount { get; }
        public int ParentCount { get; }
        public DateTimeOffset Time { get; }

        public HyobHistoryItem(
            string shortHash,
            string fullHash,
            DateTimeOffset time,
            string command,
            string message,
            string entityCount,
            int parentCount)
        {
            ShortHash = shortHash ?? string.Empty;
            FullHash = fullHash ?? string.Empty;
            Time = time;
            TimeText = time.ToLocalTime().ToString("MM-dd HH:mm:ss");
            Command = command ?? string.Empty;
            Message = message ?? string.Empty;
            EntityCount = entityCount ?? "-";
            ParentCount = parentCount;
        }
    }
}
