using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>
    /// hyob TypedObject - 多重引线 MLeader（type_id = 0x000A）。设计：02 §5.3 / 04 §5。
    ///
    /// M4-B 简化策略：只存"语义级"元数据（contents/style/关键标量 + leader 数量统计）。
    /// 详细 vertex 拓扑（每条 leader line 的所有顶点）暂不存——这是 M5 / hyob v2 升级目标。
    /// 当前能保证：contents 变 / style 变 / 关键标量变 → hash 必变；纯顶点拖动 → hash 不变。
    /// 这与 hyob commit 级 diff 哲学一致（追踪语义变化优先于像素级几何）。
    ///
    /// payload schema v1：
    /// <code>
    ///   [ utf8   layer ]
    ///   [ utf8   handle_hex ]
    ///   [ byte   content_type ]            0=None, 1=Block, 2=MText, 3=Tolerance, 0xFF=Other
    ///   [ utf8   mtext_contents ]          仅当 content_type==2 有意义；否则空串
    ///   [ utf8   block_name ]              仅当 content_type==1 有意义；否则空串
    ///   [ utf8   mleader_style_name ]
    ///   [ double text_x, text_y, text_z ]
    ///   [ double text_height ]
    ///   [ double arrow_size ]
    ///   [ double dogleg_length ]
    ///   [ double landing_gap ]
    ///   [ double scale ]
    ///   [ double block_rotation ]
    ///   [ uint32 leader_count ]            cluster 数
    ///   [ uint32 leader_line_count ]       total leader line 数
    /// </code>
    /// </summary>
    public sealed class HyobMLeader : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public const byte ContentNone      = 0;
        public const byte ContentBlock     = 1;
        public const byte ContentMText     = 2;
        public const byte ContentTolerance = 3;
        public const byte ContentOther     = 0xFF;

        public string Layer { get; }
        public string HandleHex { get; }
        public byte ContentType { get; }
        public string MTextContents { get; }
        public string BlockName { get; }
        public string MLeaderStyleName { get; }
        public double TextX { get; }
        public double TextY { get; }
        public double TextZ { get; }
        public double TextHeight { get; }
        public double ArrowSize { get; }
        public double DoglegLength { get; }
        public double LandingGap { get; }
        public double Scale { get; }
        public double BlockRotation { get; }
        public uint LeaderCount { get; }
        public uint LeaderLineCount { get; }

        public HyobObjectKind TypeId => HyobObjectKind.MLeader;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobMLeader(
            string layer, string handleHex,
            byte contentType, string mtextContents, string blockName,
            string mleaderStyleName,
            double tx, double ty, double tz,
            double textHeight, double arrowSize,
            double doglegLength, double landingGap,
            double scale, double blockRotation,
            uint leaderCount, uint leaderLineCount)
        {
            Layer = layer ?? string.Empty;
            HandleHex = handleHex ?? string.Empty;
            ContentType = contentType;
            MTextContents = mtextContents ?? string.Empty;
            BlockName = blockName ?? string.Empty;
            MLeaderStyleName = mleaderStyleName ?? string.Empty;
            TextX = tx; TextY = ty; TextZ = tz;
            TextHeight = textHeight;
            ArrowSize = arrowSize;
            DoglegLength = doglegLength;
            LandingGap = landingGap;
            Scale = scale;
            BlockRotation = blockRotation;
            LeaderCount = leaderCount;
            LeaderLineCount = leaderLineCount;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Layer);
                HyobBinary.WriteUtf8(w, HandleHex);
                w.Write(ContentType);
                HyobBinary.WriteUtf8(w, MTextContents);
                HyobBinary.WriteUtf8(w, BlockName);
                HyobBinary.WriteUtf8(w, MLeaderStyleName);
                w.Write(TextX); w.Write(TextY); w.Write(TextZ);
                w.Write(TextHeight);
                w.Write(ArrowSize);
                w.Write(DoglegLength);
                w.Write(LandingGap);
                w.Write(Scale);
                w.Write(BlockRotation);
                w.Write(LeaderCount);
                w.Write(LeaderLineCount);
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobMLeader Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.MLeader)
                throw new InvalidDataException($"期望 MLeader（0x000A），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"MLeader schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var layer = HyobBinary.ReadUtf8(r);
                var hh = HyobBinary.ReadUtf8(r);
                byte ct = r.ReadByte();
                var mtext = HyobBinary.ReadUtf8(r);
                var bname = HyobBinary.ReadUtf8(r);
                var styleName = HyobBinary.ReadUtf8(r);
                double tx = r.ReadDouble(), ty = r.ReadDouble(), tz = r.ReadDouble();
                double th = r.ReadDouble();
                double arrow = r.ReadDouble();
                double dogleg = r.ReadDouble();
                double gap = r.ReadDouble();
                double scale = r.ReadDouble();
                double rot = r.ReadDouble();
                uint lc = r.ReadUInt32();
                uint llc = r.ReadUInt32();
                return new HyobMLeader(layer, hh, ct, mtext, bname, styleName,
                    tx, ty, tz, th, arrow, dogleg, gap, scale, rot, lc, llc);
            }
        }
    }
}
