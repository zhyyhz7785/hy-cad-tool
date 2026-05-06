using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Repository
{
    /// <summary>
    /// 所有进 hyob 的对象（TypedObject / OpaqueObject / Tree / Commit / 共享 token）共同接口。
    /// 实现者负责输出带 header + payload + crc32 的完整 blob，由 <see cref="HyobObjectStore"/> 落盘。
    /// </summary>
    public interface IHyobObject
    {
        /// <summary>type_id（与 hyob Object header 中第 3-4 字节一致）。</summary>
        HyobObjectKind TypeId { get; }

        /// <summary>schema 版本号（type_id 一定时按版本分支解码）。</summary>
        byte SchemaVersion { get; }

        /// <summary>
        /// 序列化为完整 blob：HyobObjectHeader + payload + CRC32。
        /// blob 的 SHA-256 即此对象在 hyob 中的 Hash 身份。
        /// </summary>
        byte[] EncodeBlob();
    }
}
