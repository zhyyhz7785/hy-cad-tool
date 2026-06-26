namespace HyCADTool.UniverEditor
{
    /// <summary>WebView exportSnapshot 回传消息（net8 侧 DTO）。</summary>
    public sealed class UniverWebSnapshotMessage
    {
        public string SnapshotJson { get; init; }

        public string PublishMode { get; init; }

        public int? ClipStartRow { get; init; }

        public int? ClipStartCol { get; init; }

        public int? ClipEndRow { get; init; }

        public int? ClipEndCol { get; init; }
    }
}
