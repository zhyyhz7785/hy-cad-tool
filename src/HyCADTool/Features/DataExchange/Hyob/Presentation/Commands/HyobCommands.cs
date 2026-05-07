using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Opaque;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Tables;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;
using HyCADTool.Features.DataExchange.Hyob.Infrastructure.AutoCadMirror;
using HyCADTool.Features.DataExchange.Hyob.Infrastructure.Diff;
using HyCADTool.Features.DataExchange.Hyob.Infrastructure.Hygeom;
using HyCADTool.Features.DataExchange.Hyob.Infrastructure.Pack;
using HyCADTool.Features.DataExchange.Hyob.Infrastructure.RoundTrip;

namespace HyCADTool.Features.DataExchange.Hyob.Presentation.Commands
{
    // hyob 子系统命令实现。设计：docs/DataExchange/04-hyob实施总计划-2026-05-07-011100.md。
    //
    // M7 状态：
    //   - M2 白名单 4 种几何（Line / Polyline / Arc / Circle）✓
    //   - M3 白名单 3 种文字与块（DBText / MText / BlockReference 内联 Attr）✓
    //   - M4-A 标注 9 种 subtype 统一 schema ✓
    //   - M4-B 引线 MLeader（语义级）+ 填充 Hatch（模式参数级）+ Wipeout 类型预留 ✓
    //   - M5 XData / ExtensionDictionary 作为独立 hyob object，并入 by-handle 兄弟 entry ✓
    //   - M6 表/字典系统：LayerDef / TextStyleDef / DimStyleDef / BlockDef → tables/ 子树 ✓
    //   - M11-A Diff 算子：hyobD &lt;a&gt; &lt;b&gt; / 默认 HEAD~1 → HEAD（hash 级 commit→commit 变更追踪）✓
    //   - M9 V1：hyobD 支持 WIP 关键字（HEAD → 当前 Database 未提交状态预览） ✓
    //   - M11-B：hyobD 字段级 diff（Modified 条目展开 schema 字段 old → new） ✓
    //   - M12：hyobES 导出 hygeom JSON（hyob commit → AI 友好结构化文本） ✓
    //   - G/C/H 段 1：hyobL stat 摘要 + hyobD 智能折叠 + hyobES 过滤 ✓
    //   - M7：loose objects → Deflate 压缩 pack 文件（hyobG 落地）✓
    //   - hyobI / hyobS / hyobC / hyobL / hyobR / hyobD / hyobES / hyobG 全部真正落地
    //   - 其余命令（hyobCo / hyobB / hyobM / hyobP）仍为 stub

    // ---------- 共用辅助 ----------

    internal static class HyobContext
    {
        /// <summary>定位当前 DWG 的路径与 .hyob/ 布局；DWG 未保存时报错并返回 null。</summary>
        public static HyobLayoutPaths TryResolveLayout(Editor ed, Document doc)
        {
            if (doc == null)
            {
                ed?.WriteMessage("\n[hyob] 当前没有打开的文档。");
                return null;
            }
            var dwgPath = doc.Database.Filename;
            if (string.IsNullOrEmpty(dwgPath) || !File.Exists(dwgPath))
            {
                ed?.WriteMessage("\n[hyob] 当前 DWG 尚未保存到磁盘，无法定位 .hyob/ 路径。请先 SAVEAS 后再用 hyob* 命令。");
                return null;
            }
            return new HyobLayoutPaths(dwgPath);
        }

        public static (HyobObjectStore Objects, HyobRefStore Refs) OpenStores(HyobLayoutPaths paths)
        {
            return (new HyobObjectStore(paths.ObjectsDir), new HyobRefStore(paths.HyobRoot));
        }
    }

    // ---------- 报告辅助 ----------

    internal static class HyobReporting
    {
        /// <summary>把 type_id 翻译成可读名（白名单 4 种 + Opaque）。</summary>
        public static string KindName(HyobObjectKind kind)
        {
            switch (kind)
            {
                case HyobObjectKind.Line:           return "Line";
                case HyobObjectKind.Polyline:       return "Polyline";
                case HyobObjectKind.Arc:            return "Arc";
                case HyobObjectKind.Circle:         return "Circle";
                case HyobObjectKind.DBText:         return "DBText";
                case HyobObjectKind.MText:          return "MText";
                case HyobObjectKind.BlockReference: return "BlockRef";
                case HyobObjectKind.Dimension:      return "Dim";
                case HyobObjectKind.MLeader:        return "MLeader";
                case HyobObjectKind.HatchBoundary:  return "Hatch";
                case HyobObjectKind.Wipeout:        return "Wipeout";
                case HyobObjectKind.XDataAttachment:    return "XData";
                case HyobObjectKind.ExtensionDictionary: return "ExtDict";
                case HyobObjectKind.LayerDef:       return "Layer";
                case HyobObjectKind.TextStyleDef:   return "TextStyle";
                case HyobObjectKind.DimStyleDef:    return "DimStyle";
                case HyobObjectKind.BlockDef:       return "BlockDef";
                case HyobObjectKind.Opaque:         return "Opaque";
                default: return $"0x{(ushort)kind:X4}";
            }
        }

        public static void WriteMirrorBreakdown(Editor ed, MirrorStats stats)
        {
            if (ed == null || stats.ByTypeId == null || stats.ByTypeId.Count == 0) return;
            var ordered = stats.ByTypeId
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => (ushort)kv.Key);
            var parts = ordered.Select(kv => $"{KindName(kv.Key)}={kv.Value}");
            ed.WriteMessage($"\n  分类   : {string.Join(", ", parts)}");
        }

        public static void WriteAttachmentCounts(Editor ed, MirrorStats stats)
        {
            if (ed == null) return;
            if (stats.XDataCount == 0 && stats.ExtDictCount == 0) return;
            ed.WriteMessage($"\n  附加   : XData={stats.XDataCount}, ExtDict={stats.ExtDictCount}");
        }

        public static void WriteTablesCounts(Editor ed, MirrorStats stats)
        {
            if (ed == null) return;
            if (stats.LayerCount == 0 && stats.TextStyleCount == 0 &&
                stats.DimStyleCount == 0 && stats.BlockDefCount == 0) return;
            ed.WriteMessage(
                $"\n  表     : Layer={stats.LayerCount}, TextStyle={stats.TextStyleCount}, " +
                $"DimStyle={stats.DimStyleCount}, BlockDef={stats.BlockDefCount}");
        }
    }

    // ---------- 自检 (M1 12 项 + M2 4 项 Typed) ----------

    internal static class HyobSelfCheck
    {
        public static (int Pass, int Fail) Run(Editor ed)
        {
            int passed = 0, failed = 0;
            void Report(string name, bool ok, string detail = null)
            {
                if (ok) { passed++; ed?.WriteMessage($"\n  ✓ {name}"); }
                else   { failed++; ed?.WriteMessage($"\n  ✗ {name}{(detail != null ? "：" + detail : "")}"); }
            }

            try
            {
                uint crc = Crc32.Compute(Encoding.ASCII.GetBytes("123456789"));
                Report("CRC32(\"123456789\") == 0xCBF43926", crc == 0xCBF43926u, $"实际 0x{crc:X8}");
            }
            catch (Exception ex) { Report("CRC32 标准向量", false, ex.Message); }

            try
            {
                var h1 = Hash.OfPayload(Encoding.UTF8.GetBytes("hyob"));
                var h2 = Hash.OfPayload(Encoding.UTF8.GetBytes("hyob"));
                var h3 = Hash.OfPayload(Encoding.UTF8.GetBytes("hyo"));
                Report("Hash 自一致 + 异输入异哈希", h1 == h2 && h1 != h3);
            }
            catch (Exception ex) { Report("Hash 自一致", false, ex.Message); }

            try
            {
                var h = Hash.OfPayload(Encoding.UTF8.GetBytes("round-trip"));
                Report("Hash Hex round-trip", h == Hash.FromHex(h.ToHex()));
            }
            catch (Exception ex) { Report("Hash Hex round-trip", false, ex.Message); }

            try
            {
                byte[] payload = Encoding.UTF8.GetBytes("HelloHyobOpaquePayload");
                var hdr = new HyobObjectHeader(HyobObjectKind.Opaque, 1, payload.Length, 0x42);
                byte[] blob = hdr.Encode(payload);
                var (h2, p2) = HyobObjectHeader.Decode(blob);
                bool ok = h2.TypeId == HyobObjectKind.Opaque && h2.SchemaVersion == 1 && h2.Flags == 0x42
                       && h2.PayloadLength == payload.Length && BytesEqual(payload, p2);
                Report("HyobObjectHeader Encode→Decode", ok);
            }
            catch (Exception ex) { Report("HyobObjectHeader", false, ex.Message); }

            try
            {
                var hdr = new HyobObjectHeader(HyobObjectKind.Opaque, 1, 4);
                byte[] blob = hdr.Encode(new byte[] { 1, 2, 3, 4 });
                blob[blob.Length - 1] ^= 0xFF;
                bool threw = false;
                try { HyobObjectHeader.Decode(blob); }
                catch (InvalidDataException) { threw = true; }
                Report("CRC 损坏时 Decode 抛 InvalidDataException", threw);
            }
            catch (Exception ex) { Report("CRC 损坏检测", false, ex.Message); }

            try
            {
                var src = new HyobOpaqueObject("AcDbLine", "ACAD", "3F2A",
                    new byte[] { 0x4C, 0x49, 0x4E, 0x45 });
                var dec = HyobOpaqueObject.Decode(src.EncodeBlob());
                bool ok = dec.DwgClassName == "AcDbLine" && dec.HandleHex == "3F2A"
                       && BytesEqual(dec.RawDxf, src.RawDxf);
                Report("HyobOpaqueObject Encode→Decode", ok);
            }
            catch (Exception ex) { Report("HyobOpaqueObject", false, ex.Message); }

            try
            {
                var a = new HyobOpaqueObject("AcDbCircle", "ACAD", "ABCD", new byte[] { 1, 2, 3 });
                var b = new HyobOpaqueObject("AcDbCircle", "ACAD", "ABCD", new byte[] { 1, 2, 3 });
                Report("OpaqueObject content-addressable", Hash.OfPayload(a.EncodeBlob()) == Hash.OfPayload(b.EncodeBlob()));
            }
            catch (Exception ex) { Report("OpaqueObject 内容寻址", false, ex.Message); }

            string tmp1 = null;
            try
            {
                tmp1 = Path.Combine(Path.GetTempPath(), "hyob-selfcheck-" + Guid.NewGuid().ToString("N"));
                var store = new HyobObjectStore(tmp1);
                var obj = new HyobOpaqueObject("AcDbArc", "ACAD", "DEAD", new byte[] { 0xCA, 0xFE });
                byte[] blob = obj.EncodeBlob();
                Hash h1 = store.Write(blob);
                Hash h2 = store.Write(blob);
                bool ok = h1 == h2 && BytesEqual(blob, store.Read(h1)) && store.EnumerateAll().Any(x => x == h1);
                Report("HyobObjectStore 写/读/幂等/枚举", ok);
            }
            catch (Exception ex) { Report("HyobObjectStore", false, ex.Message); }
            finally { TryDelete(tmp1); }

            try
            {
                var hL = Hash.OfPayload(Encoding.UTF8.GetBytes("E001"));
                var hP = Hash.OfPayload(Encoding.UTF8.GetBytes("E002"));
                var t1 = new HyobTree(new[] {
                    new HyobTreeEntry("E002", HyobTreeEntryKind.Object, hP),
                    new HyobTreeEntry("E001", HyobTreeEntryKind.Object, hL) });
                var t2 = new HyobTree(new[] {
                    new HyobTreeEntry("E001", HyobTreeEntryKind.Object, hL),
                    new HyobTreeEntry("E002", HyobTreeEntryKind.Object, hP) });
                bool same = Hash.OfPayload(t1.EncodeBlob()) == Hash.OfPayload(t2.EncodeBlob());
                bool dec = HyobTree.Decode(t1.EncodeBlob()).Entries[0].Name == "E001";
                bool dup = false;
                try { new HyobTree(new[] {
                    new HyobTreeEntry("dup", HyobTreeEntryKind.Object, hL),
                    new HyobTreeEntry("dup", HyobTreeEntryKind.Object, hP) }); }
                catch (ArgumentException) { dup = true; }
                Report("HyobTree 顺序无关 + 同名拒绝", same && dec && dup);
            }
            catch (Exception ex) { Report("HyobTree", false, ex.Message); }

            try
            {
                var tree = Hash.OfPayload(Encoding.UTF8.GetBytes("tree"));
                var fixedTime = DateTimeOffset.FromUnixTimeSeconds(1714000000);
                var meta1 = new Dictionary<string, string> { { "z", "v2" }, { "a", "v1" } };
                var meta2 = new Dictionary<string, string> { { "a", "v1" }, { "z", "v2" } };
                var c1 = new HyobCommit(tree, "t", "m", time: fixedTime, meta: meta1);
                var c2 = new HyobCommit(tree, "t", "m", time: fixedTime, meta: meta2);
                Report("HyobCommit meta 顺序无关", Hash.OfPayload(c1.EncodeBlob()) == Hash.OfPayload(c2.EncodeBlob()));
            }
            catch (Exception ex) { Report("HyobCommit", false, ex.Message); }

            string tmp2 = null;
            try
            {
                tmp2 = Path.Combine(Path.GetTempPath(), "hyob-selfcheck-" + Guid.NewGuid().ToString("N"));
                var store = new HyobObjectStore(Path.Combine(tmp2, "objects"));
                var refs = new HyobRefStore(tmp2);
                refs.WriteHeadBranch("main");
                var obj = new HyobOpaqueObject("AcDbLine", "ACAD", "100", new byte[] { 1, 2, 3 });
                Hash oh = store.Write(obj.EncodeBlob());
                var t = new HyobTree(new[] { new HyobTreeEntry("E001", HyobTreeEntryKind.Object, oh) });
                Hash th = store.Write(t.EncodeBlob());
                var c = new HyobCommit(th, "t", "init", time: DateTimeOffset.FromUnixTimeSeconds(1714000000));
                Hash ch = store.Write(c.EncodeBlob());
                refs.WriteBranchTip("main", ch);
                bool head = refs.TryReadHead(out var b, out _);
                bool tip = refs.TryReadBranchTip("main", out var t2);
                Report("端到端 mini-DAG", head && b == "main" && tip && t2 == ch);
            }
            catch (Exception ex) { Report("端到端 mini-DAG", false, ex.Message); }
            finally { TryDelete(tmp2); }

            // ---- M2 Typed 编解码自检 ----
            try
            {
                var src = new HyobLine("L0", "100", 1.0, 2.0, 3.0, 4.0, 5.0, 6.0);
                var dec = HyobLine.Decode(src.EncodeBlob());
                bool ok = dec.Layer == "L0" && dec.HandleHex == "100"
                       && dec.StartX == 1.0 && dec.EndZ == 6.0;
                Report("HyobLine Encode→Decode", ok);
            }
            catch (Exception ex) { Report("HyobLine", false, ex.Message); }

            try
            {
                var src = new HyobCircle("L1", "200", 10, 20, 30, 5.5, 0, 0, 1);
                var dec = HyobCircle.Decode(src.EncodeBlob());
                bool ok = dec.Layer == "L1" && dec.Radius == 5.5 && dec.NormalZ == 1;
                Report("HyobCircle Encode→Decode", ok);
            }
            catch (Exception ex) { Report("HyobCircle", false, ex.Message); }

            try
            {
                var src = new HyobArc("L2", "300", 0, 0, 0, 10, 0.0, Math.PI, 0, 0, 1);
                var dec = HyobArc.Decode(src.EncodeBlob());
                bool ok = dec.Radius == 10 && Math.Abs(dec.EndAngleRad - Math.PI) < 1e-12;
                Report("HyobArc Encode→Decode", ok);
            }
            catch (Exception ex) { Report("HyobArc", false, ex.Message); }

            try
            {
                var verts = new List<HyobPolylineVertex>
                {
                    new HyobPolylineVertex(0, 0, 0),
                    new HyobPolylineVertex(10, 0, 0.5),
                    new HyobPolylineVertex(10, 10, 0),
                };
                var src = new HyobPolyline("L3", "400", closed: true,
                                           elevation: 1.0, nx: 0, ny: 0, nz: 1,
                                           constantWidth: 0.0, vertices: verts);
                var dec = HyobPolyline.Decode(src.EncodeBlob());
                bool ok = dec.Closed && dec.Vertices.Count == 3
                       && dec.Vertices[1].Bulge == 0.5 && dec.Elevation == 1.0;
                Report("HyobPolyline Encode→Decode (3 vertex + bulge + closed)", ok);
            }
            catch (Exception ex) { Report("HyobPolyline", false, ex.Message); }

            // ---- M3 文字与块自检 ----
            try
            {
                var src = new HyobDBText("L4", "500", "标高 ±0.000", "STANDARD",
                    1, 2, 3, 250.0, 0.0, 1.0, 0.0, 0.0,
                    0, 0, 1, horizontalMode: 0, verticalMode: 0,
                    ax: 0, ay: 0, az: 0, flags: 0);
                var dec = HyobDBText.Decode(src.EncodeBlob());
                bool ok = dec.TextString == "标高 ±0.000" && dec.TextStyleName == "STANDARD"
                       && dec.Height == 250.0 && dec.PosX == 1;
                Report("HyobDBText Encode→Decode (中文 + 特殊字符)", ok);
            }
            catch (Exception ex) { Report("HyobDBText", false, ex.Message); }

            try
            {
                var src = new HyobMText("L5", "600",
                    "{\\fSimSun|b0|i0|c134|p2;多行\\P测试}", "STANDARD",
                    0, 0, 0, 350.0, 1000.0, 0.0,
                    0, 0, 1, 1, 0, 0,
                    attachment: 1, drawDirection: 1, lineSpacingStyle: 1,
                    lineSpacingFactor: 1.0, backgroundFill: 0, backgroundColorArgb: 0xFFFFFFFF,
                    backgroundScaleFactor: 1.5);
                var dec = HyobMText.Decode(src.EncodeBlob());
                bool ok = dec.Contents.Contains("多行") && dec.TextHeight == 350.0
                       && dec.BackgroundColorArgb == 0xFFFFFFFF;
                Report("HyobMText Encode→Decode (RTF 控制码 + 中文)", ok);
            }
            catch (Exception ex) { Report("HyobMText", false, ex.Message); }

            try
            {
                var attrs = new List<HyobBlockAttribute>
                {
                    new HyobBlockAttribute("TAG1", "Value-1"),
                    new HyobBlockAttribute("LEVEL", "+5.000"),
                };
                var src = new HyobBlockReference("L6", "700", "BLK_TITLE",
                    100, 200, 0, 1, 1, 1, 0,
                    0, 0, 1, attrs);
                var dec = HyobBlockReference.Decode(src.EncodeBlob());
                bool ok = dec.BlockName == "BLK_TITLE" && dec.Attributes.Count == 2
                       && dec.Attributes[1].Tag == "LEVEL"
                       && dec.Attributes[1].TextString == "+5.000";
                Report("HyobBlockReference Encode→Decode (含 2 属性)", ok);
            }
            catch (Exception ex) { Report("HyobBlockReference", false, ex.Message); }

            // ---- M4-A 标注自检：覆盖 Aligned / Rotated / Radial 三种 subtype + 边界 ----
            try
            {
                var pts = new List<HyobPoint3d>
                {
                    new HyobPoint3d(0, 0, 0),
                    new HyobPoint3d(1000, 0, 0),
                    new HyobPoint3d(500, 200, 0),
                };
                var extras = new List<double> { 0.0 };  // Aligned: oblique
                var src = new HyobDimension(HyobDimension.Subtype.Aligned,
                    "L7", "800", "*D2", "<>", "STANDARD",
                    500, 200, 0, 1000.0, 0, 0, 1, pts, extras);
                var dec = HyobDimension.Decode(src.EncodeBlob());
                bool ok = dec.DimType == HyobDimension.Subtype.Aligned
                       && dec.DefiningPoints.Count == 3
                       && dec.DefiningPoints[1].X == 1000
                       && dec.Extras.Count == 1
                       && dec.Measurement == 1000.0
                       && dec.DimensionText == "<>";
                Report("HyobDimension(Aligned) Encode→Decode", ok);
            }
            catch (Exception ex) { Report("HyobDimension Aligned", false, ex.Message); }

            try
            {
                var pts = new List<HyobPoint3d>
                {
                    new HyobPoint3d(0, 0, 0),
                    new HyobPoint3d(0, 100, 0),
                    new HyobPoint3d(50, 50, 0),
                };
                var extras = new List<double> { Math.PI / 4, 0.0 };  // Rotation, Oblique
                var src = new HyobDimension(HyobDimension.Subtype.Rotated,
                    "L8", "801", "", "", "STANDARD",
                    50, 50, 0, 100.0, 0, 0, 1, pts, extras);
                var dec = HyobDimension.Decode(src.EncodeBlob());
                bool ok = dec.DimType == HyobDimension.Subtype.Rotated
                       && Math.Abs(dec.Extras[0] - Math.PI / 4) < 1e-12;
                Report("HyobDimension(Rotated) Encode→Decode", ok);
            }
            catch (Exception ex) { Report("HyobDimension Rotated", false, ex.Message); }

            try
            {
                var pts = new List<HyobPoint3d>
                {
                    new HyobPoint3d(50, 50, 0),
                    new HyobPoint3d(100, 50, 0),
                };
                var extras = new List<double> { 25.0 };  // LeaderLength
                var src = new HyobDimension(HyobDimension.Subtype.Radial,
                    "L9", "802", "", "R<>", "STANDARD",
                    150, 50, 0, 50.0, 0, 0, 1, pts, extras);
                var dec = HyobDimension.Decode(src.EncodeBlob());
                bool ok = dec.DimType == HyobDimension.Subtype.Radial
                       && dec.DefiningPoints.Count == 2
                       && dec.Extras[0] == 25.0;
                Report("HyobDimension(Radial) Encode→Decode", ok);
            }
            catch (Exception ex) { Report("HyobDimension Radial", false, ex.Message); }

            try
            {
                var src = new HyobDimension(HyobDimension.Subtype.Other,
                    "L10", "803", "", "", "",
                    0, 0, 0, 0, 0, 0, 1,
                    new List<HyobPoint3d>(), new List<double>());
                var dec = HyobDimension.Decode(src.EncodeBlob());
                bool ok = dec.DimType == HyobDimension.Subtype.Other
                       && dec.DefiningPoints.Count == 0 && dec.Extras.Count == 0;
                Report("HyobDimension(Other) 0-point/0-extra 边界", ok);
            }
            catch (Exception ex) { Report("HyobDimension Other", false, ex.Message); }

            // ---- M4-B 引线与填充自检 ----
            try
            {
                var src = new HyobMLeader("L11", "900",
                    contentType: HyobMLeader.ContentMText,
                    mtextContents: "标注 \\P 多行",
                    blockName: "",
                    mleaderStyleName: "Standard",
                    tx: 100, ty: 200, tz: 0,
                    textHeight: 250.0, arrowSize: 80.0,
                    doglegLength: 100.0, landingGap: 25.0,
                    scale: 1.0, blockRotation: 0.0,
                    leaderCount: 1, leaderLineCount: 2);
                var dec = HyobMLeader.Decode(src.EncodeBlob());
                bool ok = dec.ContentType == HyobMLeader.ContentMText
                       && dec.MTextContents == "标注 \\P 多行"
                       && dec.MLeaderStyleName == "Standard"
                       && dec.LeaderLineCount == 2;
                Report("HyobMLeader(MText) Encode→Decode", ok);
            }
            catch (Exception ex) { Report("HyobMLeader", false, ex.Message); }

            try
            {
                var src = new HyobMLeader("L12", "901",
                    contentType: HyobMLeader.ContentBlock,
                    mtextContents: "",
                    blockName: "BLK_NOTE",
                    mleaderStyleName: "Standard",
                    tx: 0, ty: 0, tz: 0,
                    textHeight: 0, arrowSize: 0,
                    doglegLength: 0, landingGap: 0,
                    scale: 1.0, blockRotation: 0.5,
                    leaderCount: 0, leaderLineCount: 0);
                var dec = HyobMLeader.Decode(src.EncodeBlob());
                bool ok = dec.ContentType == HyobMLeader.ContentBlock
                       && dec.BlockName == "BLK_NOTE"
                       && dec.BlockRotation == 0.5;
                Report("HyobMLeader(Block) 边界 (无 leader line)", ok);
            }
            catch (Exception ex) { Report("HyobMLeader Block", false, ex.Message); }

            try
            {
                var src = new HyobHatch("L13", "A00",
                    patternType: 0, patternName: "ANSI31",
                    patternScale: 100.0, patternAngleRad: 0.0, patternSpace: 50.0,
                    hatchStyle: 0, elevation: 0.0,
                    nx: 0, ny: 0, nz: 1,
                    numberOfLoops: 1, numberOfPatternDefinitions: 1,
                    area: 12500.0, associative: 1);
                var dec = HyobHatch.Decode(src.EncodeBlob());
                bool ok = dec.PatternName == "ANSI31"
                       && dec.PatternScale == 100.0
                       && dec.NumberOfLoops == 1
                       && dec.Associative == 1;
                Report("HyobHatch Encode→Decode", ok);
            }
            catch (Exception ex) { Report("HyobHatch", false, ex.Message); }

            try
            {
                var boundary = new List<HyobPoint2d>
                {
                    new HyobPoint2d(0, 0), new HyobPoint2d(100, 0),
                    new HyobPoint2d(100, 50), new HyobPoint2d(0, 50),
                };
                var src = new HyobWipeout("L14", "B00", 0, 0, 0, 100.0, 50.0, 0.0, 256, boundary);
                var dec = HyobWipeout.Decode(src.EncodeBlob());
                bool ok = dec.Boundary.Count == 4 && dec.Boundary[2].Y == 50;
                Report("HyobWipeout (4-顶点矩形边界)", ok);
            }
            catch (Exception ex) { Report("HyobWipeout", false, ex.Message); }

            // ---- M5 XData / ExtDict 自检 ----
            try
            {
                var g1 = new HyobXDataAppGroup("HYREIN", new List<HyobXDataEntry>
                {
                    new HyobXDataEntry(1000, "钢筋数据"),
                    new HyobXDataEntry(1040, "3.14"),
                    new HyobXDataEntry(1071, "42"),
                });
                var g2 = new HyobXDataAppGroup("HYAXIS", new List<HyobXDataEntry>
                {
                    new HyobXDataEntry(1000, "AXIS-1"),
                });
                var src = new HyobXDataAttachment(new List<HyobXDataAppGroup> { g1, g2 });
                var dec = HyobXDataAttachment.Decode(src.EncodeBlob());
                bool ok = dec.Groups.Count == 2
                       && dec.Groups[0].AppName == "HYREIN"
                       && dec.Groups[0].Entries.Count == 3
                       && dec.Groups[0].Entries[0].ValueStr == "钢筋数据"
                       && dec.Groups[1].AppName == "HYAXIS";
                Report("HyobXDataAttachment Encode→Decode (2 RegApp)", ok);
            }
            catch (Exception ex) { Report("HyobXDataAttachment", false, ex.Message); }

            try
            {
                var entries = new List<HyobExtDictEntry>
                {
                    new HyobExtDictEntry("HyBoltData", HyobExtDictEntry.KindXrecord, "(1000,bolts)(40,16.0)"),
                    new HyobExtDictEntry("HySubDict",  HyobExtDictEntry.KindDictionary, "<5>"),
                    new HyobExtDictEntry("HyOther",    HyobExtDictEntry.KindOther, "AcDbXrecord"),
                };
                var src = new HyobExtensionDictionary(entries);
                var dec = HyobExtensionDictionary.Decode(src.EncodeBlob());
                bool ok = dec.Entries.Count == 3
                       && dec.Entries[0].KeyName == "HyBoltData"
                       && dec.Entries[0].Kind == HyobExtDictEntry.KindXrecord
                       && dec.Entries[1].Kind == HyobExtDictEntry.KindDictionary;
                Report("HyobExtensionDictionary Encode→Decode (3 entry)", ok);
            }
            catch (Exception ex) { Report("HyobExtensionDictionary", false, ex.Message); }

            try
            {
                var emptyXData = new HyobXDataAttachment(new List<HyobXDataAppGroup>());
                var emptyExt = new HyobExtensionDictionary(new List<HyobExtDictEntry>());
                var d1 = HyobXDataAttachment.Decode(emptyXData.EncodeBlob());
                var d2 = HyobExtensionDictionary.Decode(emptyExt.EncodeBlob());
                bool ok = d1.Groups.Count == 0 && d2.Entries.Count == 0;
                Report("XData / ExtDict 空集合边界", ok);
            }
            catch (Exception ex) { Report("XData/ExtDict 空边界", false, ex.Message); }

            try
            {
                var src = new HyobLayerDef(
                    name: "钢筋", colorIndex: 1,
                    linetypeName: "Continuous", lineweightMm: 0.30,
                    isOff: 0, isFrozen: 0, isLocked: 0, isPlottable: 1, isUsed: 1,
                    description: "底板配筋图层");
                var dec = HyobLayerDef.Decode(src.EncodeBlob());
                bool ok = dec.Name == "钢筋"
                       && dec.ColorIndex == 1
                       && dec.LinetypeName == "Continuous"
                       && Math.Abs(dec.LineweightMm - 0.30) < 1e-9
                       && dec.IsPlottable == 1
                       && dec.Description == "底板配筋图层";
                Report("HyobLayerDef Encode→Decode (中文名 + 线宽)", ok);
            }
            catch (Exception ex) { Report("HyobLayerDef", false, ex.Message); }

            try
            {
                var src = new HyobTextStyleDef(
                    name: "HzTxt", fileName: "gbcbig.shx", bigFontFileName: "hztxt.shx",
                    textSize: 0.0, widthFactor: 0.7, obliqueRad: 0.0,
                    isVertical: 0, isShape: 0,
                    fontTypeface: "宋体", fontPitchFamily: 34, fontCharset: 134,
                    fontBold: 0, fontItalic: 0);
                var dec = HyobTextStyleDef.Decode(src.EncodeBlob());
                bool ok = dec.Name == "HzTxt"
                       && dec.FileName == "gbcbig.shx"
                       && dec.BigFontFileName == "hztxt.shx"
                       && Math.Abs(dec.WidthFactor - 0.7) < 1e-9
                       && dec.FontTypeface == "宋体"
                       && dec.FontCharset == 134;
                Report("HyobTextStyleDef Encode→Decode (中文 typeface + bigfont)", ok);
            }
            catch (Exception ex) { Report("HyobTextStyleDef", false, ex.Message); }

            try
            {
                var src = new HyobDimStyleDef(
                    name: "HyDim",
                    dimScale: 40.0, dimTextHeight: 3.5, dimArrowSize: 2.5,
                    dimExtOffset: 0.625, dimExtExtension: 1.25, dimBaselineDistance: 3.75,
                    textStyleName: "HzTxt", ldrArrowBlockName: "_ArchTick",
                    textColorIndex: 256, dimLineColorIndex: 256, extLineColorIndex: 256,
                    linearScaleFactor: 1.0, decimalPlaces: 0,
                    tofl: 1, tih: 0, toh: 0);
                var dec = HyobDimStyleDef.Decode(src.EncodeBlob());
                bool ok = dec.Name == "HyDim"
                       && Math.Abs(dec.DimScale - 40.0) < 1e-9
                       && dec.TextStyleName == "HzTxt"
                       && dec.LdrArrowBlockName == "_ArchTick"
                       && dec.Tofl == 1 && dec.Tih == 0 && dec.Toh == 0
                       && dec.DecimalPlaces == 0;
                Report("HyobDimStyleDef Encode→Decode (常用 14 项 + 引线块)", ok);
            }
            catch (Exception ex) { Report("HyobDimStyleDef", false, ex.Message); }

            try
            {
                var src = new HyobBlockDef(
                    name: "TitleBar",
                    description: "图框块",
                    isLayout: 0, isFromExternalReference: 0, isAnonymous: 0,
                    ox: 100.0, oy: 200.0, oz: 0.0,
                    units: 4, // Millimeters
                    entityCount: 17,
                    pathName: string.Empty);
                var dec = HyobBlockDef.Decode(src.EncodeBlob());
                bool ok = dec.Name == "TitleBar"
                       && dec.Description == "图框块"
                       && dec.IsLayout == 0
                       && dec.EntityCount == 17
                       && Math.Abs(dec.OriginX - 100.0) < 1e-9
                       && Math.Abs(dec.OriginY - 200.0) < 1e-9
                       && dec.Units == 4;
                Report("HyobBlockDef Encode→Decode (含 entity 计数)", ok);
            }
            catch (Exception ex) { Report("HyobBlockDef", false, ex.Message); }

            // ---- M7 Pack 文件自检 ----
            string tmp7 = null;
            try
            {
                tmp7 = Path.Combine(Path.GetTempPath(), "hyob-pack-selfcheck-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tmp7);
                string packPath = Path.Combine(tmp7, "test.pack");

                var blobs = new List<KeyValuePair<Hash, byte[]>>();
                var origs = new Dictionary<string, byte[]>();
                for (int i = 0; i < 5; i++)
                {
                    var obj = new HyobOpaqueObject("AcDbLine", "ACAD", "00" + i,
                        Encoding.UTF8.GetBytes("payload-" + i + "-" + new string('x', 200)));
                    byte[] blob = obj.EncodeBlob();
                    var h = Hash.OfPayload(blob);
                    blobs.Add(new KeyValuePair<Hash, byte[]>(h, blob));
                    origs[h.ToHex()] = blob;
                }
                int written = HyobPackFile.Write(packPath, blobs);

                var reader = HyobPackReader.Open(packPath);
                bool allRead = reader.Count == written;
                foreach (var kv in origs)
                {
                    if (!reader.TryRead(Hash.FromHex(kv.Key), out var read)) { allRead = false; break; }
                    if (!BytesEqual(read, kv.Value)) { allRead = false; break; }
                }
                Report("HyobPackFile Write/Read round-trip (5 obj × 200B Deflate)", allRead);
            }
            catch (Exception ex) { Report("HyobPackFile round-trip", false, ex.Message); }

            try
            {
                string tmpRoot = Path.Combine(Path.GetTempPath(), "hyob-pack-store-" + Guid.NewGuid().ToString("N"));
                var store = new HyobObjectStore(tmpRoot);
                var obj1 = new HyobOpaqueObject("AcDbLine", "ACAD", "AAA1", Encoding.UTF8.GetBytes("loose-1"));
                var obj2 = new HyobOpaqueObject("AcDbLine", "ACAD", "AAA2", Encoding.UTF8.GetBytes("loose-2"));
                Hash h1 = store.Write(obj1.EncodeBlob());
                Hash h2 = store.Write(obj2.EncodeBlob());

                var packer = new HyobPacker();
                var result = packer.Pack(store);

                bool ok = result.PackedCount == 2
                       && result.DeletedLoose == 2
                       && store.PackCount == 1
                       && store.EnumerateLoose().Count() == 0
                       && store.Exists(h1) && store.Exists(h2)
                       && BytesEqual(store.Read(h1), obj1.EncodeBlob())
                       && BytesEqual(store.Read(h2), obj2.EncodeBlob());

                var obj3 = new HyobOpaqueObject("AcDbLine", "ACAD", "AAA3", Encoding.UTF8.GetBytes("loose-after-pack"));
                Hash h3 = store.Write(obj3.EncodeBlob());
                ok = ok
                  && store.Exists(h3)
                  && BytesEqual(store.Read(h3), obj3.EncodeBlob())
                  && store.EnumerateLoose().Count() == 1;

                Report("HyobPacker pack→read→add-loose 共存", ok);
                try { Directory.Delete(tmpRoot, recursive: true); } catch { }
            }
            catch (Exception ex) { Report("HyobPacker pack→read→add-loose", false, ex.Message); }
            finally { TryDelete(tmp7); }

            return (passed, failed);
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static void TryDelete(string dir)
        {
            try { if (dir != null && Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
            catch { }
        }
    }

    // ---------- hyobI ----------

    /// <summary>
    /// hyobI - 在当前 DWG 旁初始化 .hyob/ 目录：先跑 12 项 Domain 自检，再创建仓库并写第一次 commit。
    /// </summary>
    public class HyobInitCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed  = doc?.Editor;
            ed?.WriteMessage("\n[hyob] hyobI: 启动 —— 先跑 Domain 自检");

            var (pass, fail) = HyobSelfCheck.Run(ed);
            ed?.WriteMessage($"\n[hyob] 自检：PASS {pass} / FAIL {fail}");
            if (fail > 0)
            {
                ed?.WriteMessage("\n[hyob] 自检失败，已中止 init。请修复 Domain 后重试。");
                return;
            }

            var paths = HyobContext.TryResolveLayout(ed, doc);
            if (paths == null) return;

            bool freshInit = !paths.Exists();
            paths.EnsureCreated();

            var (objects, refs) = HyobContext.OpenStores(paths);
            if (freshInit) refs.WriteHeadBranch("main");

            var mirror = new AutoCadDatabaseMirror(objects, refs);
            string msg = freshInit ? "init: 首次镜像" : "init: 重复 init，追加快照";
            var (commitHash, stats) = mirror.MirrorDatabaseIntoHyob(
                doc.Database, author: Environment.UserName ?? "hyob", message: msg, command: "hyobI");

            ed?.WriteMessage($"\n[hyob] hyobI: 完成");
            ed?.WriteMessage($"\n  路径   : {paths.HyobRoot}");
            ed?.WriteMessage($"\n  分支   : main");
            ed?.WriteMessage($"\n  对象   : {stats.TotalCount} 个 (Typed={stats.TypedCount}, Opaque={stats.OpaqueCount})");
            HyobReporting.WriteMirrorBreakdown(ed, stats);
            HyobReporting.WriteAttachmentCounts(ed, stats);
            HyobReporting.WriteTablesCounts(ed, stats);
            ed?.WriteMessage($"\n  commit : {commitHash.ToHex().Substring(0, 12)}…");
        }
    }

    // ---------- hyobS ----------

    /// <summary>hyobS - 显示当前 .hyob/ 仓库状态：路径、分支、HEAD、最新 commit 元信息。</summary>
    public class HyobStatusCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed  = doc?.Editor;
            var paths = HyobContext.TryResolveLayout(ed, doc);
            if (paths == null) return;

            if (!paths.Exists())
            {
                ed?.WriteMessage($"\n[hyob] 仓库未初始化（{paths.HyobRoot} 不存在）。请先执行 hyobI。");
                return;
            }

            var (objects, refs) = HyobContext.OpenStores(paths);
            ed?.WriteMessage($"\n[hyob] 仓库：{paths.HyobRoot}");

            if (!refs.TryReadHead(out var branch, out var detached))
            {
                ed?.WriteMessage("\n  HEAD : (未设置)");
                return;
            }
            if (branch != null)
            {
                ed?.WriteMessage($"\n  HEAD : ref → refs/heads/{branch}");
                if (refs.TryReadBranchTip(branch, out var tip))
                {
                    ed?.WriteMessage($"\n  tip  : {tip.ToHex().Substring(0, 12)}…");
                    if (objects.TryRead(tip, out var blob))
                    {
                        try
                        {
                            var c = HyobCommit.Decode(blob);
                            ed?.WriteMessage($"\n  msg  : {c.Message}");
                            ed?.WriteMessage($"\n  cmd  : {c.Command}");
                            ed?.WriteMessage($"\n  by   : {c.Author}  @ {c.Time.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
                            if (c.Meta.TryGetValue("entityCount", out var ec))
                                ed?.WriteMessage($"\n  ents : {ec}");
                            ed?.WriteMessage($"\n  parents: {c.Parents.Count}");
                        }
                        catch (Exception ex) { ed?.WriteMessage($"\n  (commit 解码失败：{ex.Message})"); }
                    }
                }
                else
                {
                    ed?.WriteMessage($"\n  tip  : (空 — branch '{branch}' 尚无 commit)");
                }
            }
            else
            {
                ed?.WriteMessage($"\n  HEAD : detached → {detached.ToHex().Substring(0, 12)}…");
            }
        }
    }

    // ---------- hyobC ----------

    /// <summary>hyobC - 把当前 Database 同步状态 commit 到 hyob，自动接到当前分支 tip 上。</summary>
    public class HyobCommitCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed  = doc?.Editor;
            var paths = HyobContext.TryResolveLayout(ed, doc);
            if (paths == null) return;

            if (!paths.Exists())
            {
                ed?.WriteMessage($"\n[hyob] 仓库未初始化。请先执行 hyobI。");
                return;
            }

            var (objects, refs) = HyobContext.OpenStores(paths);
            var mirror = new AutoCadDatabaseMirror(objects, refs);

            var (commitHash, stats) = mirror.MirrorDatabaseIntoHyob(
                doc.Database,
                author: Environment.UserName ?? "hyob",
                message: $"manual commit @ {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                command: "hyobC");

            ed?.WriteMessage($"\n[hyob] hyobC: 已提交");
            ed?.WriteMessage($"\n  commit : {commitHash.ToHex().Substring(0, 12)}…");
            ed?.WriteMessage($"\n  对象   : {stats.TotalCount} 个 (Typed={stats.TypedCount}, Opaque={stats.OpaqueCount})");
            HyobReporting.WriteMirrorBreakdown(ed, stats);
            HyobReporting.WriteAttachmentCounts(ed, stats);
            HyobReporting.WriteTablesCounts(ed, stats);
        }
    }

    // ---------- hyobL ----------

    /// <summary>hyobL - 沿 parent 链打印 commit 历史（git log 风格，最多 50 条）。</summary>
    public class HyobLogCommand
    {
        private const int MaxCommits = 50;

        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed  = doc?.Editor;
            var paths = HyobContext.TryResolveLayout(ed, doc);
            if (paths == null) return;

            if (!paths.Exists())
            {
                ed?.WriteMessage($"\n[hyob] 仓库未初始化。请先执行 hyobI。");
                return;
            }

            var (objects, refs) = HyobContext.OpenStores(paths);
            if (!refs.TryReadHead(out var branch, out var detached))
            {
                ed?.WriteMessage("\n[hyob] HEAD 未设置。");
                return;
            }

            Hash startHash;
            if (branch != null)
            {
                if (!refs.TryReadBranchTip(branch, out startHash))
                {
                    ed?.WriteMessage($"\n[hyob] 分支 '{branch}' 尚无 commit。");
                    return;
                }
            }
            else startHash = detached;

            var mirror = new AutoCadDatabaseMirror(objects, refs);
            var differ = new TreeDiffer(objects);
            ed?.WriteMessage($"\n[hyob] hyobL: HEAD → {(branch ?? "(detached)")}");
            int n = 0;
            foreach (var (hash, commit) in mirror.WalkHistory(startHash, MaxCommits))
            {
                n++;
                string ec = commit.Meta.TryGetValue("entityCount", out var v) ? v : "-";
                ed?.WriteMessage(
                    $"\n  {hash.ToHex().Substring(0, 10)}  " +
                    $"{commit.Time.ToLocalTime():yyyy-MM-dd HH:mm:ss}  " +
                    $"[{commit.Command,-7}]  ents={ec,-5}  {commit.Message}");

                if (commit.Parents.Count > 0)
                {
                    try
                    {
                        var report = differ.Diff(commit.Parents[0], hash);
                        WriteLogStat(ed, report);
                    }
                    catch { /* 单条 stat 失败不阻断历史浏览 */ }
                }
            }
            ed?.WriteMessage($"\n[hyob] 共 {n} 条 commit{(n == MaxCommits ? "（已截断）" : "")}");
        }

        /// <summary>git log --stat 风格：在 commit 行下方加一条 +N ~M -K + 主导 type 缩写。</summary>
        private static void WriteLogStat(Editor ed, HyobDiffReport report)
        {
            if (ed == null || report.IsEmpty) return;
            var byKind = report.AggregateByKind();
            var ordered = byKind
                .OrderByDescending(kv => kv.Value.Add + kv.Value.Mod + kv.Value.Del)
                .ThenBy(kv => (ushort)kv.Key)
                .Take(4); // 最多显示 4 类，超出折叠
            var parts = ordered.Select(kv =>
            {
                var t = kv.Value;
                var sb = new StringBuilder();
                sb.Append(HyobReporting.KindName(kv.Key)).Append('=');
                if (t.Add > 0) sb.Append('+').Append(t.Add);
                if (t.Mod > 0) sb.Append((sb.Length > 0 && sb[sb.Length - 1] != '=') ? "/" : "").Append('~').Append(t.Mod);
                if (t.Del > 0) sb.Append((sb.Length > 0 && sb[sb.Length - 1] != '=') ? "/" : "").Append('-').Append(t.Del);
                return sb.ToString();
            });
            ed.WriteMessage(
                $"\n              +{report.TotalAdded} ~{report.TotalModified} -{report.TotalDeleted}  " +
                $"{string.Join(", ", parts)}{(byKind.Count > 4 ? ", ..." : "")}");
        }
    }

    // ---------- 其余命令仍为 stub（按计划在后续里程碑落地） ----------

    /// <summary>
    /// hyobD - 比较 commit / 当前 Database 的 hash 级变更。设计：04 §11（M11-A）+ M9（WIP 模式）。
    ///
    /// 用法：
    /// <list type="bullet">
    ///   <item>无参 → 默认 <c>HEAD~1 → HEAD</c></item>
    ///   <item>提示输入 from / to 表达式：
    ///     <c>HEAD</c> / <c>HEAD~N</c> / 分支名 / commit hash 前缀（≥4 字符） /
    ///     <c>WIP</c>（Working：当前 Database 未提交状态）</item>
    /// </list>
    /// 常用模式：
    /// <list type="bullet">
    ///   <item><c>HEAD WIP</c>：HEAD → 当前 Database = 预览即将 hyobC 的内容（git status / git diff）</item>
    ///   <item><c>HEAD~1 HEAD</c>：上一次 commit → HEAD = 看刚刚 commit 了什么</item>
    /// </list>
    /// </summary>
    public class HyobDiffCommand
    {
        private const int MaxRowsToList = 200;
        private const int VerboseAutoThreshold = 30;
        private const string WipKeyword = "WIP";

        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            var paths = HyobContext.TryResolveLayout(ed, doc);
            if (paths == null) return;
            if (!paths.Exists())
            {
                ed?.WriteMessage("\n[hyob] hyobD: 当前 DWG 未初始化 .hyob/，请先 hyobI。");
                return;
            }

            var (objects, refs) = HyobContext.OpenStores(paths);
            var resolver = new RefResolver(objects, refs);
            var mirror = new AutoCadDatabaseMirror(objects, refs);

            var fromExpr = PromptRef(ed, "from", "HEAD~1");
            if (fromExpr == null) return;
            var toExpr = PromptRef(ed, "to", "HEAD");
            if (toExpr == null) return;

            // 解析 from / to 为 (treeHash, displayLabel)
            if (!TryResolveTree(fromExpr, doc, resolver, mirror, objects, ed, "from", out var fromTree, out var fromDisplay))
                return;
            if (!TryResolveTree(toExpr, doc, resolver, mirror, objects, ed, "to", out var toTree, out var toDisplay))
                return;

            var differ = new TreeDiffer(objects);
            HyobDiffReport report;
            try { report = differ.DiffTrees(fromTree, toTree); }
            catch (Exception ex) { ed?.WriteMessage($"\n[hyob] hyobD: diff 失败：{ex.Message}"); return; }

            WriteReportFlexible(ed, report, fromExpr, toExpr, fromDisplay, toDisplay);
        }

        /// <summary>把 ref 表达式解析为 tree hash + 显示用短标签。WIP 走 BuildSnapshot；其它走 RefResolver。</summary>
        private static bool TryResolveTree(
            string expr,
            Document doc,
            RefResolver resolver,
            AutoCadDatabaseMirror mirror,
            HyobObjectStore objects,
            Editor ed,
            string label,
            out Hash treeHash,
            out string display)
        {
            treeHash = default; display = expr;
            if (string.Equals(expr, WipKeyword, StringComparison.OrdinalIgnoreCase))
            {
                if (doc == null)
                {
                    ed?.WriteMessage("\n[hyob] hyobD: WIP 解析失败，无活动文档");
                    return false;
                }
                try
                {
                    using (doc.LockDocument())
                    {
                        var (root, _) = mirror.BuildSnapshot(doc.Database);
                        treeHash = root;
                    }
                    display = "WIP";
                    return true;
                }
                catch (Exception ex)
                {
                    ed?.WriteMessage($"\n[hyob] hyobD: 解析 {label}=WIP 失败：{ex.Message}");
                    return false;
                }
            }
            try
            {
                var commit = resolver.Resolve(expr);
                if (!objects.TryRead(commit, out var blob))
                {
                    ed?.WriteMessage($"\n[hyob] hyobD: 解析 {label} 失败：commit 不存在 {commit.ToHex().Substring(0, 12)}");
                    return false;
                }
                var c = HyobCommit.Decode(blob);
                treeHash = c.Tree;
                display = commit.ToHex().Substring(0, 10);
                return true;
            }
            catch (Exception ex)
            {
                ed?.WriteMessage($"\n[hyob] hyobD: 解析 {label} 失败：{ex.Message}");
                return false;
            }
        }

        private static string PromptRef(Editor ed, string label, string defaultExpr)
        {
            if (ed == null) return defaultExpr;
            var opts = new PromptStringOptions($"\n[hyob] hyobD {label} (回车=默认 '{defaultExpr}')")
            {
                AllowSpaces = false,
                DefaultValue = defaultExpr,
                UseDefaultValue = true,
            };
            var res = ed.GetString(opts);
            if (res.Status != PromptStatus.OK) return null;
            return string.IsNullOrWhiteSpace(res.StringResult) ? defaultExpr : res.StringResult.Trim();
        }

        private static void WriteReportFlexible(
            Editor ed, HyobDiffReport report,
            string fromExpr, string toExpr,
            string fromDisplay, string toDisplay)
        {
            if (ed == null) return;
            ed.WriteMessage($"\n[hyob] hyobD: {fromDisplay} → {toDisplay}  ({fromExpr} → {toExpr})");

            if (report.IsEmpty)
            {
                ed.WriteMessage("\n  ✓ 两个 commit 内容完全一致（hash 相等）");
                return;
            }

            int total = report.Entries.Count;
            if (total > VerboseAutoThreshold)
                WriteAggregated(ed, report, total);
            else
                WriteVerbose(ed, report, total);

            ed.WriteMessage($"\n  总计   : +{report.TotalAdded} ~{report.TotalModified} -{report.TotalDeleted}");

            var byKind = report.AggregateByKind();
            if (byKind.Count > 0)
            {
                var parts = byKind
                    .OrderByDescending(kv => kv.Value.Add + kv.Value.Mod + kv.Value.Del)
                    .ThenBy(kv => (ushort)kv.Key)
                    .Select(kv => $"{HyobReporting.KindName(kv.Key)}=+{kv.Value.Add}/~{kv.Value.Mod}/-{kv.Value.Del}");
                ed.WriteMessage($"\n  分类   : {string.Join(", ", parts)}");
            }
        }

        /// <summary>≤30 条：每条逐行展开（含字段级 diff）。git diff 风格。</summary>
        private static void WriteVerbose(Editor ed, HyobDiffReport report, int total)
        {
            int shown = Math.Min(total, MaxRowsToList);
            ed.WriteMessage($"\n  共 {total} 条变更" + (shown < total ? $"（仅显示前 {shown} 条）" : "") + "：");

            for (int i = 0; i < shown; i++)
            {
                var e = report.Entries[i];
                char sign = e.Kind == HyobDiffKind.Added ? '+'
                          : e.Kind == HyobDiffKind.Deleted ? '-' : '~';
                string typeText;
                if (e.Kind == HyobDiffKind.Modified && e.OldType != e.NewType)
                    typeText = $"{HyobReporting.KindName(e.OldType)} → {HyobReporting.KindName(e.NewType)}";
                else
                    typeText = HyobReporting.KindName(e.RepresentativeType);
                ed.WriteMessage($"\n  {sign} {PadRight(e.Path, 56)} {typeText}");

                if (e.Kind == HyobDiffKind.Modified && e.Changes != null && e.Changes.Count > 0)
                {
                    int fieldsShown = Math.Min(e.Changes.Count, 12);
                    for (int j = 0; j < fieldsShown; j++)
                    {
                        var c = e.Changes[j];
                        ed.WriteMessage($"\n      {PadRight(c.Field, 22)} {c.OldValue} → {c.NewValue}");
                    }
                    if (e.Changes.Count > fieldsShown)
                        ed.WriteMessage($"\n      …还有 {e.Changes.Count - fieldsShown} 个字段变更");
                }
            }
        }

        /// <summary>
        /// >30 条：按 (type, kind) 分组聚合 + 字段变更主导模式分析。
        /// 每组显示：count、前 5 个 path 示例、字段变更频率（"layer 改动 240/243 次（"钢筋"→"标注"）"）。
        /// </summary>
        private static void WriteAggregated(Editor ed, HyobDiffReport report, int total)
        {
            ed.WriteMessage($"\n  共 {total} 条变更（智能折叠：> {VerboseAutoThreshold} 条按 type 聚合，hyobDv 可全展开）：");

            var groups = report.Entries
                .GroupBy(e => (Type: e.RepresentativeType, Kind: e.Kind))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => (ushort)g.Key.Type);

            foreach (var g in groups)
            {
                char sign = g.Key.Kind == HyobDiffKind.Added ? '+'
                          : g.Key.Kind == HyobDiffKind.Deleted ? '-' : '~';
                string typeName = HyobReporting.KindName(g.Key.Type);
                int count = g.Count();
                ed.WriteMessage($"\n  {sign} {typeName} × {count}");

                int sampleCount = Math.Min(5, count);
                var samples = g.Take(sampleCount).Select(e => ShortPath(e.Path));
                ed.WriteMessage($"\n      示例: {string.Join(", ", samples)}{(count > sampleCount ? ", ..." : "")}");

                if (g.Key.Kind == HyobDiffKind.Modified)
                {
                    var fieldStats = AnalyzeFieldChanges(g);
                    foreach (var fs in fieldStats.Take(5))
                    {
                        if (fs.UniqueValuePairs == 1 && fs.Sample != null)
                        {
                            ed.WriteMessage(
                                $"\n      ◆ {fs.Field} 全部 {fs.Hits}/{count}：{fs.Sample.OldValue} → {fs.Sample.NewValue}");
                        }
                        else
                        {
                            ed.WriteMessage(
                                $"\n      ◆ {fs.Field} 改动 {fs.Hits}/{count} ({fs.UniqueValuePairs} 种不同值)");
                        }
                    }
                    if (fieldStats.Count > 5)
                        ed.WriteMessage($"\n      …还有 {fieldStats.Count - 5} 个字段被改动");
                }
            }
        }

        /// <summary>把 "entities/by-handle/" 等通用前缀截掉，只保留 leaf 名供示例展示。</summary>
        private static string ShortPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            int idx = path.LastIndexOf('/');
            return idx >= 0 ? path.Substring(idx + 1) : path;
        }

        private sealed class FieldStat
        {
            public string Field;
            public int Hits;
            public int UniqueValuePairs;
            public HyobFieldChange Sample;
        }

        /// <summary>统计同 type 的 Modified 组里：每个字段被改动了多少次，以及是否所有改动用同一组 (old → new)。</summary>
        private static List<FieldStat> AnalyzeFieldChanges(IEnumerable<HyobDiffEntry> group)
        {
            var perField = new Dictionary<string, Dictionary<(string, string), int>>(StringComparer.Ordinal);
            foreach (var e in group)
            {
                if (e.Changes == null) continue;
                foreach (var c in e.Changes)
                {
                    if (!perField.TryGetValue(c.Field, out var counter))
                    {
                        counter = new Dictionary<(string, string), int>();
                        perField[c.Field] = counter;
                    }
                    var k = (c.OldValue, c.NewValue);
                    counter.TryGetValue(k, out int n);
                    counter[k] = n + 1;
                }
            }

            var result = new List<FieldStat>(perField.Count);
            foreach (var kv in perField)
            {
                int hits = 0;
                foreach (var v in kv.Value.Values) hits += v;
                FieldStat fs = new FieldStat
                {
                    Field = kv.Key,
                    Hits = hits,
                    UniqueValuePairs = kv.Value.Count,
                    Sample = null,
                };
                if (kv.Value.Count == 1)
                {
                    foreach (var pair in kv.Value)
                    {
                        fs.Sample = new HyobFieldChange(kv.Key, pair.Key.Item1, pair.Key.Item2);
                    }
                }
                result.Add(fs);
            }
            result.Sort((a, b) => b.Hits.CompareTo(a.Hits));
            return result;
        }

        private static string PadRight(string s, int width)
        {
            if (s == null) s = string.Empty;
            int visualLen = 0;
            foreach (var ch in s) visualLen += (ch > 0x7F ? 2 : 1);
            if (visualLen >= width) return s;
            return s + new string(' ', width - visualLen);
        }
    }

    /// <summary>hyobB - 创建 / 列出 / 切换 layer（M8 落地）。</summary>
    public class HyobBranchCommand
    {
        public void Execute()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n[hyob] hyobB: stub —— 将在 M8 落地（USD 风格多 layer 合成）。");
        }
    }

    /// <summary>hyobCo - 切换 HEAD 到指定 commit / layer（M1-E 落地，含 Database 反向 patch）。</summary>
    public class HyobCheckoutCommand
    {
        public void Execute()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n[hyob] hyobCo: stub —— 将在 M1-E 落地（含 Database 反向 patch）。");
        }
    }

    /// <summary>hyobM - 三路合并（M8/M10 落地）。</summary>
    public class HyobMergeCommand
    {
        public void Execute()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n[hyob] hyobM: stub —— 将在 M8/M10 落地。");
        }
    }

    /// <summary>
    /// hyobES - 把指定 hyob commit 导出为 hygeom JSON。设计：04 §12（M12）。
    /// 输出到 <c>&lt;dwg&gt;.hyob/exports/&lt;commit-short&gt;.hygeom.json</c>。
    /// 默认导出 HEAD；提示输入其他 ref（HEAD~N / 分支名 / hash 前缀）。
    /// </summary>
    public class HyobExportHygeomCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            var paths = HyobContext.TryResolveLayout(ed, doc);
            if (paths == null) return;
            if (!paths.Exists())
            {
                ed?.WriteMessage("\n[hyob] hyobES: 当前 DWG 未初始化 .hyob/，请先 hyobI。");
                return;
            }

            var (objects, refs) = HyobContext.OpenStores(paths);
            var resolver = new RefResolver(objects, refs);

            var refExpr = PromptString(ed, "commit", "HEAD", allowSpaces: false);
            if (refExpr == null) return;

            Hash commitHash;
            try { commitHash = resolver.Resolve(refExpr); }
            catch (Exception ex) { ed?.WriteMessage($"\n[hyob] hyobES: 解析失败：{ex.Message}"); return; }

            // 可选过滤器（空表达式 = 全部）
            var filter = PromptFilter(ed);

            var shortHash = commitHash.ToHex().Substring(0, 10);
            string suffix = filter.IsEmpty ? string.Empty
                : "." + SanitizeFileName(filter.LayerName ?? filter.TypeName ?? filter.HandlePrefix);
            var outPath = Path.Combine(paths.ExportsDir, shortHash + suffix + ".hygeom.json");

            ed?.WriteMessage($"\n[hyob] hyobES: 导出 commit {shortHash} → {outPath}");
            if (!filter.IsEmpty) ed?.WriteMessage($"\n  过滤   : {filter.Describe()}");

            HygeomExporter.ExportStats stats;
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var exporter = new HygeomExporter(objects);
                stats = exporter.Export(commitHash, outPath, filter);
                sw.Stop();
                ed?.WriteMessage($"\n  耗时   : {sw.ElapsedMilliseconds} ms");
            }
            catch (Exception ex) { ed?.WriteMessage($"\n[hyob] hyobES: 导出失败：{ex.Message}"); return; }

            ed?.WriteMessage($"\n  实体   : {stats.EntityCount} 条" +
                (stats.EntitiesFiltered > 0 ? $" (过滤掉 {stats.EntitiesFiltered} 条)" : ""));
            if (stats.XDataCount > 0 || stats.ExtDictCount > 0)
                ed?.WriteMessage($"\n  附加   : XData={stats.XDataCount}, ExtDict={stats.ExtDictCount}");
            ed?.WriteMessage(
                $"\n  表     : Layer={stats.LayerCount}, TextStyle={stats.TextStyleCount}, " +
                $"DimStyle={stats.DimStyleCount}, BlockDef={stats.BlockDefCount}");
            ed?.WriteMessage($"\n  文件   : {FormatBytes(stats.FileBytes)}");
            ed?.WriteMessage($"\n  ✓ 完成");
        }

        /// <summary>
        /// 提示用户输入过滤表达式。支持：
        /// 空 / *  → 不过滤（全部）；
        /// "layer:钢筋" / "type:Dimension" / "handle:174E" → 单条件；
        /// 多条件用空格分隔："layer:钢筋 type:Dimension"。
        /// </summary>
        private static ExportFilter PromptFilter(Editor ed)
        {
            var f = new ExportFilter();
            if (ed == null) return f;
            var opts = new PromptStringOptions("\n[hyob] hyobES filter (回车=全部, 例: layer:钢筋 / type:Dimension / handle:174E)")
            {
                AllowSpaces = true,
                DefaultValue = string.Empty,
                UseDefaultValue = true,
            };
            var res = ed.GetString(opts);
            if (res.Status != PromptStatus.OK) return f;
            var s = (res.StringResult ?? string.Empty).Trim();
            if (s.Length == 0 || s == "*") return f;

            foreach (var token in s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int colon = token.IndexOf(':');
                if (colon <= 0) continue;
                var key = token.Substring(0, colon).Trim().ToLowerInvariant();
                var val = token.Substring(colon + 1).Trim();
                if (val.Length == 0) continue;
                switch (key)
                {
                    case "layer":  f.LayerName = val; break;
                    case "type":   f.TypeName = val; break;
                    case "handle": f.HandlePrefix = val; break;
                }
            }
            return f;
        }

        private static string SanitizeFileName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "filtered";
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
                sb.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
            return sb.ToString();
        }

        private static string PromptString(Editor ed, string label, string defaultExpr, bool allowSpaces)
        {
            if (ed == null) return defaultExpr;
            var opts = new PromptStringOptions($"\n[hyob] hyobES {label} (回车=默认 '{defaultExpr}')")
            {
                AllowSpaces = allowSpaces,
                DefaultValue = defaultExpr,
                UseDefaultValue = true,
            };
            var res = ed.GetString(opts);
            if (res.Status != PromptStatus.OK) return null;
            return string.IsNullOrWhiteSpace(res.StringResult) ? defaultExpr : res.StringResult.Trim();
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            return (bytes / 1024.0 / 1024.0).ToString("F2") + " MB";
        }
    }

    /// <summary>
    /// hyobR - 强制跑一次 round-trip 自检：从 HEAD 沿 parent 链遍历整个 commit DAG，
    /// 校验所有 commit / tree / object 的链路完整性、解码正确性。
    /// </summary>
    public class HyobRoundTripCommand
    {
        private const int MaxIssuesShown = 10;

        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed  = doc?.Editor;
            var paths = HyobContext.TryResolveLayout(ed, doc);
            if (paths == null) return;

            if (!paths.Exists())
            {
                ed?.WriteMessage($"\n[hyob] 仓库未初始化。请先执行 hyobI。");
                return;
            }

            var (objects, refs) = HyobContext.OpenStores(paths);
            var validator = new RoundTripValidator(objects, refs);
            var report = validator.Validate();

            ed?.WriteMessage($"\n[hyob] hyobR: round-trip 自检完成");
            ed?.WriteMessage($"\n  commits : {report.CommitCount}");
            ed?.WriteMessage($"\n  trees   : {report.TreeCount}");
            ed?.WriteMessage($"\n  objects : {report.ObjectCount}  (Opaque={report.OpaqueCount}, Typed={report.TypedCount})");

            if (report.TypedByKind.Count > 0)
            {
                var parts = report.TypedByKind
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => (ushort)kv.Key)
                    .Select(kv => $"{HyobReporting.KindName(kv.Key)}={kv.Value}");
                ed?.WriteMessage($"\n  typed   : {string.Join(", ", parts)}");
            }

            if (report.Warnings.Count > 0)
            {
                ed?.WriteMessage($"\n  warnings: {report.Warnings.Count}");
                int i = 0;
                foreach (var w in report.Warnings)
                {
                    if (i++ >= MaxIssuesShown) { ed?.WriteMessage("\n    ... (已截断)"); break; }
                    ed?.WriteMessage($"\n    ! {w}");
                }
            }

            if (report.Errors.Count > 0)
            {
                ed?.WriteMessage($"\n  errors  : {report.Errors.Count}");
                int i = 0;
                foreach (var e in report.Errors)
                {
                    if (i++ >= MaxIssuesShown) { ed?.WriteMessage("\n    ... (已截断)"); break; }
                    ed?.WriteMessage($"\n    ✗ {e}");
                }
            }
            else
            {
                ed?.WriteMessage($"\n  status  : ✓ 无错误，仓库链路完整");
            }
        }
    }

    /// <summary>
    /// hyobG - 把所有 loose objects 打包成压缩 .pack（M7）。打包后 loose 文件被删除，
    /// pack 与 loose 共存读时优先 loose（向后兼容）。
    /// </summary>
    public class HyobGcCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc?.Editor;
            var paths = HyobContext.TryResolveLayout(ed, doc);
            if (paths == null) return;
            if (!paths.Exists())
            {
                ed?.WriteMessage("\n[hyob] hyobG: 当前 DWG 未初始化 .hyob/，请先 hyobI。");
                return;
            }

            var store = new HyobObjectStore(paths.ObjectsDir);

            int looseBefore = store.EnumerateLoose().Count();
            int packsBefore = store.PackCount;
            ed?.WriteMessage(
                $"\n[hyob] hyobG: 开始打包 —— 当前 loose={looseBefore}, pack={packsBefore}");

            if (looseBefore == 0)
            {
                ed?.WriteMessage("\n[hyob] hyobG: 没有 loose 对象需要打包。");
                return;
            }

            var packer = new HyobPacker();
            HyobPacker.PackResult result;
            try
            {
                result = packer.Pack(store);
            }
            catch (Exception ex)
            {
                ed?.WriteMessage($"\n[hyob] hyobG: 打包失败：{ex.Message}");
                return;
            }

            double ratio = result.LooseBytesBefore > 0
                ? (double)result.PackBytes / result.LooseBytesBefore
                : 0.0;
            string ratioStr = $"{ratio * 100.0:F1}%";

            ed?.WriteMessage($"\n[hyob] hyobG: 打包完成");
            ed?.WriteMessage($"\n  pack    : {Path.GetFileName(result.PackPath ?? "(none)")}");
            ed?.WriteMessage($"\n  对象    : {result.PackedCount} 个");
            ed?.WriteMessage(
                $"\n  尺寸    : {FormatBytes(result.LooseBytesBefore)} → {FormatBytes(result.PackBytes)}  (压缩 {ratioStr})");
            ed?.WriteMessage($"\n  已删 loose : {result.DeletedLoose}");
            ed?.WriteMessage($"\n  pack 总数 : {store.PackCount}");

            try
            {
                var (validateStore, refs) = HyobContext.OpenStores(paths);
                var validator = new RoundTripValidator(validateStore, refs);
                var report = validator.Validate();
                if (report.Errors.Count == 0)
                {
                    ed?.WriteMessage(
                        $"\n  自检    : ✓ commits={report.CommitCount} trees={report.TreeCount} objs={report.ObjectCount}");
                }
                else
                {
                    ed?.WriteMessage($"\n  自检    : ✗ {report.Errors.Count} 个错误");
                    foreach (var e in report.Errors.Take(5)) ed?.WriteMessage("\n    " + e);
                }
            }
            catch (Exception ex)
            {
                ed?.WriteMessage($"\n  自检    : 跳过（{ex.Message}）");
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("F1") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("F1") + " GB";
        }
    }

    /// <summary>hyobP - 弹出历史面板（M10 落地）。</summary>
    public class HyobShowHistoryPanelCommand
    {
        public void Execute()
        {
            var ed = Application.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n[hyob] hyobP: stub —— 将在 M10 落地。");
        }
    }
}
