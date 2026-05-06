using System.IO;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Tables
{
    /// <summary>
    /// hyob 表定义 - 标注样式 DimStyleDef（type_id = 0x0103）。
    ///
    /// AutoCAD DimStyle 含 80+ DIMVAR 字段，本期 v1 只存最常用的 ~14 项 +
    /// 文字/箭头/颜色等关联引用名。其他字段在 schema_ver = 2 升级时补全。
    ///
    /// payload schema v1：
    /// <code>
    ///   [ utf8   name ]
    ///   [ double dim_scale ]                  DIMSCALE
    ///   [ double dim_text_height ]            DIMTXT
    ///   [ double dim_arrow_size ]             DIMASZ
    ///   [ double dim_ext_offset ]             DIMEXO
    ///   [ double dim_ext_extension ]          DIMEXE
    ///   [ double dim_baseline_distance ]      DIMDLI
    ///   [ utf8   text_style_name ]            DIMTXSTY 解析名
    ///   [ utf8   ldr_arrow_block_name ]       DIMLDRBLK 解析名
    ///   [ int32  text_color_index ]           DIMCLRT
    ///   [ int32  dim_line_color_index ]       DIMCLRD
    ///   [ int32  ext_line_color_index ]       DIMCLRE
    ///   [ double linear_scale_factor ]        DIMLFAC
    ///   [ int32  decimal_places ]             DIMDEC
    ///   [ byte   tofl ]                       DIMTOFL 0/1
    ///   [ byte   tih ]                        DIMTIH 0/1
    ///   [ byte   toh ]                        DIMTOH 0/1
    /// </code>
    /// </summary>
    public sealed class HyobDimStyleDef : IHyobObject
    {
        public const byte SchemaVersionV1 = 1;

        public string Name { get; }
        public double DimScale { get; }
        public double DimTextHeight { get; }
        public double DimArrowSize { get; }
        public double DimExtOffset { get; }
        public double DimExtExtension { get; }
        public double DimBaselineDistance { get; }
        public string TextStyleName { get; }
        public string LdrArrowBlockName { get; }
        public int TextColorIndex { get; }
        public int DimLineColorIndex { get; }
        public int ExtLineColorIndex { get; }
        public double LinearScaleFactor { get; }
        public int DecimalPlaces { get; }
        public byte Tofl { get; }
        public byte Tih { get; }
        public byte Toh { get; }

        public HyobObjectKind TypeId => HyobObjectKind.DimStyleDef;
        public byte SchemaVersion => SchemaVersionV1;

        public HyobDimStyleDef(
            string name,
            double dimScale, double dimTextHeight, double dimArrowSize,
            double dimExtOffset, double dimExtExtension, double dimBaselineDistance,
            string textStyleName, string ldrArrowBlockName,
            int textColorIndex, int dimLineColorIndex, int extLineColorIndex,
            double linearScaleFactor, int decimalPlaces,
            byte tofl, byte tih, byte toh)
        {
            Name = name ?? string.Empty;
            DimScale = dimScale;
            DimTextHeight = dimTextHeight;
            DimArrowSize = dimArrowSize;
            DimExtOffset = dimExtOffset;
            DimExtExtension = dimExtExtension;
            DimBaselineDistance = dimBaselineDistance;
            TextStyleName = textStyleName ?? string.Empty;
            LdrArrowBlockName = ldrArrowBlockName ?? string.Empty;
            TextColorIndex = textColorIndex;
            DimLineColorIndex = dimLineColorIndex;
            ExtLineColorIndex = extLineColorIndex;
            LinearScaleFactor = linearScaleFactor;
            DecimalPlaces = decimalPlaces;
            Tofl = tofl;
            Tih = tih;
            Toh = toh;
        }

        public byte[] EncodeBlob()
        {
            byte[] payload;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                HyobBinary.WriteUtf8(w, Name);
                w.Write(DimScale);
                w.Write(DimTextHeight);
                w.Write(DimArrowSize);
                w.Write(DimExtOffset);
                w.Write(DimExtExtension);
                w.Write(DimBaselineDistance);
                HyobBinary.WriteUtf8(w, TextStyleName);
                HyobBinary.WriteUtf8(w, LdrArrowBlockName);
                w.Write(TextColorIndex);
                w.Write(DimLineColorIndex);
                w.Write(ExtLineColorIndex);
                w.Write(LinearScaleFactor);
                w.Write(DecimalPlaces);
                w.Write(Tofl);
                w.Write(Tih);
                w.Write(Toh);
                w.Flush();
                payload = ms.ToArray();
            }
            return new HyobObjectHeader(TypeId, SchemaVersion, payload.Length).Encode(payload);
        }

        public static HyobDimStyleDef Decode(byte[] blob)
        {
            var (h, payload) = HyobObjectHeader.Decode(blob);
            if (h.TypeId != HyobObjectKind.DimStyleDef)
                throw new InvalidDataException($"期望 DimStyleDef（0x0103），实际 type_id=0x{(ushort)h.TypeId:X4}");
            if (h.SchemaVersion != SchemaVersionV1)
                throw new InvalidDataException($"DimStyleDef schema_ver 不支持：{h.SchemaVersion}");

            using (var ms = new MemoryStream(payload))
            using (var r = new BinaryReader(ms))
            {
                var name = HyobBinary.ReadUtf8(r);
                double dimScale = r.ReadDouble();
                double dimTxt = r.ReadDouble();
                double dimAsz = r.ReadDouble();
                double dimExo = r.ReadDouble();
                double dimExe = r.ReadDouble();
                double dimDli = r.ReadDouble();
                var ts = HyobBinary.ReadUtf8(r);
                var ldr = HyobBinary.ReadUtf8(r);
                int clrt = r.ReadInt32();
                int clrd = r.ReadInt32();
                int clre = r.ReadInt32();
                double lfac = r.ReadDouble();
                int dec = r.ReadInt32();
                byte tofl = r.ReadByte();
                byte tih = r.ReadByte();
                byte toh = r.ReadByte();
                return new HyobDimStyleDef(name, dimScale, dimTxt, dimAsz, dimExo, dimExe,
                    dimDli, ts, ldr, clrt, clrd, clre, lfac, dec, tofl, tih, toh);
            }
        }
    }
}
