using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Opaque
{
    /// <summary>
    /// hyob 黑盒对象（type_id = 0xFFFF）。所有 HyCAD 不展开的 DWG 类型都走这条。
    /// 设计：03 §3.1 / 04 §5。
    ///
    /// payload schema v1（Decode 时按此固定顺序解析）：
    /// <code>
    ///   [ utf8        dwg_class_name   ]      "AcDbLine" / "AcDbSpline" / "AeccDbAlignment" ...
    ///   [ utf8        dwg_app_name     ]      "ACAD" / "AeccCatalog" ...
    ///   [ utf8        handle_hex       ]      原 DWG Handle 的十六进制字符串
    ///   [ uint32+raw  raw_dxf          ]      AutoCAD 提供的 DXF group code 二进制（M1-D 由 DxfBinarySerializer 填充）
    ///   [ uint32+raw  raw_xdata        ]      M5 才填，目前留空（长度 0）
    ///   [ uint32+raw  raw_extdict      ]      M5 才填，目前留空（长度 0）
    /// </code>
    /// 含义：白名单类型升级前，原始数据按字节保留 + handle 锁定身份，往返不丢；
    /// 升级到 TypedObject 时 hyob 是 append-only，旧的 OpaqueObject blob 不动。
    /// </summary>
    public sealed class HyobOpaqueObject : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public string DwgClassName { get; }
        public string DwgAppName { get; }
        public string HandleHex { get; }
        public byte[] RawDxf { get; }
        public byte[] RawXData { get; }
        public byte[] RawExtensionDictionary { get; }

        public HyobObjectKind TypeId => HyobObjectKind.Opaque;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobOpaqueObject(
            string dwgClassName,
            string dwgAppName,
            string handleHex,
            byte[] rawDxf,
            byte[] rawXData = null,
            byte[] rawExtensionDictionary = null)
        {
            DwgClassName = dwgClassName ?? string.Empty;
            DwgAppName = dwgAppName ?? string.Empty;
            HandleHex = handleHex ?? string.Empty;
            RawDxf = rawDxf ?? new byte[0];
            RawXData = rawXData ?? new byte[0];
            RawExtensionDictionary = rawExtensionDictionary ?? new byte[0];
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, DwgClassName);
                HyobBinary.WriteUtf8(w, DwgAppName);
                HyobBinary.WriteUtf8(w, HandleHex);
                HyobBinary.WriteLengthPrefixedBytes(w, RawDxf);
                HyobBinary.WriteLengthPrefixedBytes(w, RawXData);
                HyobBinary.WriteLengthPrefixedBytes(w, RawExtensionDictionary);
                w.Flush();
                payload = ms.ToArray();
            }

            var header = new HyobObjectHeader(TypeId, SchemaVersion, payload.Length);
            return header.Encode(payload);
        }

        public static HyobOpaqueObject Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.Opaque)
                throw new InvalidDataException($"期望 Opaque（0xFFFF），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"OpaqueObject schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var className = HyobBinary.ReadUtf8(r);
                var appName = HyobBinary.ReadUtf8(r);
                var handleHex = HyobBinary.ReadUtf8(r);
                var rawDxf = HyobBinary.ReadLengthPrefixedBytes(r);
                var rawXData = HyobBinary.ReadLengthPrefixedBytes(r);
                var rawExtDict = HyobBinary.ReadLengthPrefixedBytes(r);
                return new HyobOpaqueObject(className, appName, handleHex, rawDxf, rawXData, rawExtDict);
            }
        }
    }
}
