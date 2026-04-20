using System;

namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 历史还原点条目（M6）—— 对应用户图 1「还原点」表格一行。
    ///
    /// <para><b>字段语义</b></para>
    /// <list type="bullet">
    ///   <item><see cref="SeqNo"/>：序号（从 1 递增，单调不减）。</item>
    ///   <item><see cref="TimestampUtc"/>：创建时间 UTC。</item>
    ///   <item><see cref="TimestampLocal"/>：创建时的本地时间，UI 显示用。</item>
    ///   <item><see cref="UserName"/>：创建者（默认 <c>Environment.UserName</c>）。</item>
    ///   <item><see cref="Description"/>：中文描述，对应图 1「描述」列。</item>
    ///   <item><see cref="SnapshotFileName"/>：相对 <c>&lt;Dwg&gt;.roaddesign.history/</c> 的文件名。</item>
    ///   <item><see cref="Checksum"/>：快照 JSON 的 SHA-256 十六进制小写串，用于还原前完整性校验。</item>
    /// </list>
    ///
    /// <para>所有字段均 public 可读写，以便 Newtonsoft.Json 直接反序列化。</para>
    /// </summary>
    public sealed class HistoryEntry
    {
        public int SeqNo { get; set; }

        public DateTime TimestampUtc { get; set; }

        public DateTime TimestampLocal { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string SnapshotFileName { get; set; } = string.Empty;

        public string Checksum { get; set; } = string.Empty;

        /// <summary>快照 JSON 字节数（统计信息）。</summary>
        public long SnapshotBytes { get; set; }

        /// <summary>Schema 版本（写入时刻的 <c>SchemaVersion.Current</c>），用于未来跨版本显示。</summary>
        public string SchemaVersion { get; set; }

        public override string ToString()
            => $"HistoryEntry[#{SeqNo} {TimestampLocal:yyyy-MM-dd HH:mm} {Description} → {SnapshotFileName}]";
    }

    /// <summary>
    /// 历史索引文件（<c>history-index.json</c>）的顶级结构。
    /// 按 <see cref="Entries"/> 降序展示（新的在前），写盘时保持按 <see cref="HistoryEntry.SeqNo"/> 升序以便追加。
    /// </summary>
    public sealed class HistoryIndex
    {
        /// <summary>下一个可用序号（= 当前 Entries 中最大 SeqNo + 1；为空时为 1）。</summary>
        public int NextSeqNo { get; set; } = 1;

        /// <summary>
        /// 条目列表，按 <see cref="HistoryEntry.SeqNo"/> 升序排列。
        /// UI 展示由 ViewModel 决定是否反转。
        /// </summary>
        public System.Collections.Generic.List<HistoryEntry> Entries { get; set; } = new System.Collections.Generic.List<HistoryEntry>();
    }
}
