using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed
{
    /// <summary>
    /// hyob TypedObject - 填充 Hatch（type_id = 0x000B）。设计：02 §5.3 / 04 §5。
    ///
    /// M4-B 简化策略：只存填充模式与样式参数（pattern/style/scale/angle...），
    /// 不存边界几何（多 loop × 多 segment × 异质几何 — Polyline / Arc / Ellipse / Spline）。
    /// 边界几何留 M5 / M6（HatchBoundary 子对象专用 type_id）升级。
    ///
    /// 当前能保证：pattern 变 / scale 变 / angle 变 → hash 必变；
    ///            纯边界拖动（loop count 不变）→ hash 不变。
    ///
    /// payload schema v1：
    /// <code>
    ///   [ utf8   layer ]
    ///   [ utf8   handle_hex ]
    ///   [ byte   pattern_type ]            HatchPatternType enum
    ///   [ utf8   pattern_name ]
    ///   [ double pattern_scale ]
    ///   [ double pattern_angle_rad ]
    ///   [ double pattern_space ]
    ///   [ byte   hatch_style ]             HatchStyle enum (Normal=0/Outer=1/Ignore=2)
    ///   [ double elevation ]
    ///   [ double normal_x, normal_y, normal_z ]
    ///   [ uint32 number_of_loops ]
    ///   [ uint32 number_of_pattern_definitions ]
    ///   [ double area ]                    AutoCAD 计算的填充面积；读不到 = 0
    ///   [ byte   associative ]             0=否, 1=是
    /// </code>
    /// </summary>
    public sealed class HyobHatch : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public string Layer { get; }
        public string HandleHex { get; }
        public byte PatternType { get; }
        public string PatternName { get; }
        public double PatternScale { get; }
        public double PatternAngleRad { get; }
        public double PatternSpace { get; }
        public byte HatchStyle { get; }
        public double Elevation { get; }
        public double NormalX { get; }
        public double NormalY { get; }
        public double NormalZ { get; }
        public uint NumberOfLoops { get; }
        public uint NumberOfPatternDefinitions { get; }
        public double Area { get; }
        public byte Associative { get; }

        public HyobObjectKind TypeId => HyobObjectKind.HatchBoundary;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobHatch(
            string layer, string handleHex,
            byte patternType, string patternName,
            double patternScale, double patternAngleRad, double patternSpace,
            byte hatchStyle, double elevation,
            double nx, double ny, double nz,
            uint numberOfLoops, uint numberOfPatternDefinitions,
            double area, byte associative)
        {
            Layer = layer ?? string.Empty;
            HandleHex = handleHex ?? string.Empty;
            PatternType = patternType;
            PatternName = patternName ?? string.Empty;
            PatternScale = patternScale;
            PatternAngleRad = patternAngleRad;
            PatternSpace = patternSpace;
            HatchStyle = hatchStyle;
            Elevation = elevation;
            NormalX = nx; NormalY = ny; NormalZ = nz;
            NumberOfLoops = numberOfLoops;
            NumberOfPatternDefinitions = numberOfPatternDefinitions;
            Area = area;
            Associative = associative;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Layer);
                HyobBinary.WriteUtf8(w, HandleHex);
                w.Write(PatternType);
                HyobBinary.WriteUtf8(w, PatternName);
                w.Write(PatternScale);
                w.Write(PatternAngleRad);
                w.Write(PatternSpace);
                w.Write(HatchStyle);
                w.Write(Elevation);
                w.Write(NormalX); w.Write(NormalY); w.Write(NormalZ);
                w.Write(NumberOfLoops);
                w.Write(NumberOfPatternDefinitions);
                w.Write(Area);
                w.Write(Associative);
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobHatch Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.HatchBoundary)
                throw new InvalidDataException($"期望 Hatch（0x000B），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"Hatch schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var layer = HyobBinary.ReadUtf8(r);
                var hh = HyobBinary.ReadUtf8(r);
                byte pt = r.ReadByte();
                var pname = HyobBinary.ReadUtf8(r);
                double pscale = r.ReadDouble();
                double pangle = r.ReadDouble();
                double pspace = r.ReadDouble();
                byte hstyle = r.ReadByte();
                double elev = r.ReadDouble();
                double nx = r.ReadDouble(), ny = r.ReadDouble(), nz = r.ReadDouble();
                uint loops = r.ReadUInt32();
                uint defs = r.ReadUInt32();
                double area = r.ReadDouble();
                byte assoc = r.ReadByte();
                return new HyobHatch(layer, hh, pt, pname, pscale, pangle, pspace,
                    hstyle, elev, nx, ny, nz, loops, defs, area, assoc);
            }
        }
    }
}
