using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Tables;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.AutoCadMirror
{
    /// <summary>
    /// AutoCAD 4 张核心 SymbolTable（Layer / TextStyle / DimStyle / Block）→ hyob 表/字典。
    /// 设计：02 §5.3 / 04 §6（M6 表/字典系统）。
    ///
    /// 输出 4 个子树：
    /// <code>
    ///   tables/
    ///     layers/      &lt;layer_name&gt;       → HyobLayerDef
    ///     text_styles/ &lt;style_name&gt;       → HyobTextStyleDef
    ///     dim_styles/  &lt;style_name&gt;       → HyobDimStyleDef
    ///     blocks/      &lt;block_name&gt;       → HyobBlockDef（含 Layout / 用户 block / 匿名 block）
    ///   </code>
    /// </summary>
    public sealed class TablesMirror
    {
        public readonly struct Result
        {
            public Hash TablesTreeHash { get; }
            public int LayerCount { get; }
            public int TextStyleCount { get; }
            public int DimStyleCount { get; }
            public int BlockDefCount { get; }

            public Result(Hash tablesTreeHash, int layerCount, int textStyleCount,
                          int dimStyleCount, int blockDefCount)
            {
                TablesTreeHash = tablesTreeHash;
                LayerCount = layerCount;
                TextStyleCount = textStyleCount;
                DimStyleCount = dimStyleCount;
                BlockDefCount = blockDefCount;
            }
        }

        private readonly HyobObjectStore _objects;

        public TablesMirror(HyobObjectStore objects)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        public Result Mirror(Database db, Transaction tx)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (tx == null) throw new ArgumentNullException(nameof(tx));

            var layersHash = MirrorLayers(db, tx, out int layerCount);
            var textStylesHash = MirrorTextStyles(db, tx, out int textStyleCount);
            var dimStylesHash = MirrorDimStyles(db, tx, out int dimStyleCount);
            var blocksHash = MirrorBlocks(db, tx, out int blockDefCount);

            var tablesTree = new HyobTree(new[]
            {
                new HyobTreeEntry("layers",      HyobTreeEntryKind.Tree, layersHash),
                new HyobTreeEntry("text_styles", HyobTreeEntryKind.Tree, textStylesHash),
                new HyobTreeEntry("dim_styles",  HyobTreeEntryKind.Tree, dimStylesHash),
                new HyobTreeEntry("blocks",      HyobTreeEntryKind.Tree, blocksHash),
            });
            Hash tablesTreeHash = _objects.Write(tablesTree.EncodeBlob());

            return new Result(tablesTreeHash, layerCount, textStyleCount, dimStyleCount, blockDefCount);
        }

        private Hash MirrorLayers(Database db, Transaction tx, out int count)
        {
            count = 0;
            var entries = new List<HyobTreeEntry>();
            var lt = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForRead);

            foreach (ObjectId id in lt)
            {
                if (id.IsErased) continue;
                LayerTableRecord rec;
                try { rec = (LayerTableRecord)tx.GetObject(id, OpenMode.ForRead); }
                catch { continue; }

                string ltName = SafeReadLinetypeName(tx, rec.LinetypeObjectId);
                double lwMm = LineWeightToMm(rec.LineWeight);

                var def = new HyobLayerDef(
                    name: rec.Name,
                    colorIndex: rec.Color.ColorIndex,
                    linetypeName: ltName,
                    lineweightMm: lwMm,
                    isOff:       (byte)(rec.IsOff       ? 1 : 0),
                    isFrozen:    (byte)(rec.IsFrozen    ? 1 : 0),
                    isLocked:    (byte)(rec.IsLocked    ? 1 : 0),
                    isPlottable: (byte)(rec.IsPlottable ? 1 : 0),
                    isUsed:      (byte)(rec.IsUsed      ? 1 : 0),
                    description: rec.Description ?? string.Empty);

                Hash h = _objects.Write(def.EncodeBlob());
                entries.Add(new HyobTreeEntry(rec.Name, HyobTreeEntryKind.Object, h));
                count++;
            }

            var tree = new HyobTree(entries);
            return _objects.Write(tree.EncodeBlob());
        }

        private Hash MirrorTextStyles(Database db, Transaction tx, out int count)
        {
            count = 0;
            var entries = new List<HyobTreeEntry>();
            var ts = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);

            foreach (ObjectId id in ts)
            {
                if (id.IsErased) continue;
                TextStyleTableRecord rec;
                try { rec = (TextStyleTableRecord)tx.GetObject(id, OpenMode.ForRead); }
                catch { continue; }

                string typeface = string.Empty;
                int pitchFam = 0;
                int charset = 0;
                byte bold = 0, italic = 0;
                try
                {
                    var fd = rec.Font;
                    if (fd != null)
                    {
                        typeface = fd.TypeFace ?? string.Empty;
                        pitchFam = fd.PitchAndFamily;
                        charset = fd.CharacterSet;
                        bold = (byte)(fd.Bold ? 1 : 0);
                        italic = (byte)(fd.Italic ? 1 : 0);
                    }
                }
                catch { /* 老版 SHX 字体无 Font 信息 */ }

                var def = new HyobTextStyleDef(
                    name: rec.Name,
                    fileName: rec.FileName ?? string.Empty,
                    bigFontFileName: rec.BigFontFileName ?? string.Empty,
                    textSize: rec.TextSize,
                    widthFactor: rec.XScale,
                    obliqueRad: rec.ObliquingAngle,
                    isVertical: (byte)(rec.IsVertical ? 1 : 0),
                    isShape: 0,
                    fontTypeface: typeface,
                    fontPitchFamily: pitchFam,
                    fontCharset: charset,
                    fontBold: bold,
                    fontItalic: italic);

                Hash h = _objects.Write(def.EncodeBlob());
                entries.Add(new HyobTreeEntry(rec.Name, HyobTreeEntryKind.Object, h));
                count++;
            }

            var tree = new HyobTree(entries);
            return _objects.Write(tree.EncodeBlob());
        }

        private Hash MirrorDimStyles(Database db, Transaction tx, out int count)
        {
            count = 0;
            var entries = new List<HyobTreeEntry>();
            var dt = (DimStyleTable)tx.GetObject(db.DimStyleTableId, OpenMode.ForRead);

            foreach (ObjectId id in dt)
            {
                if (id.IsErased) continue;
                DimStyleTableRecord rec;
                try { rec = (DimStyleTableRecord)tx.GetObject(id, OpenMode.ForRead); }
                catch { continue; }

                string txStyle = SafeReadTextStyleName(tx, rec.Dimtxsty);
                string ldrBlk = SafeReadBlockName(tx, rec.Dimldrblk);

                var def = new HyobDimStyleDef(
                    name: rec.Name,
                    dimScale: rec.Dimscale,
                    dimTextHeight: rec.Dimtxt,
                    dimArrowSize: rec.Dimasz,
                    dimExtOffset: rec.Dimexo,
                    dimExtExtension: rec.Dimexe,
                    dimBaselineDistance: rec.Dimdli,
                    textStyleName: txStyle,
                    ldrArrowBlockName: ldrBlk,
                    textColorIndex: rec.Dimclrt.ColorIndex,
                    dimLineColorIndex: rec.Dimclrd.ColorIndex,
                    extLineColorIndex: rec.Dimclre.ColorIndex,
                    linearScaleFactor: rec.Dimlfac,
                    decimalPlaces: rec.Dimdec,
                    tofl: (byte)(rec.Dimtofl ? 1 : 0),
                    tih:  (byte)(rec.Dimtih  ? 1 : 0),
                    toh:  (byte)(rec.Dimtoh  ? 1 : 0));

                Hash h = _objects.Write(def.EncodeBlob());
                entries.Add(new HyobTreeEntry(rec.Name, HyobTreeEntryKind.Object, h));
                count++;
            }

            var tree = new HyobTree(entries);
            return _objects.Write(tree.EncodeBlob());
        }

        private Hash MirrorBlocks(Database db, Transaction tx, out int count)
        {
            count = 0;
            var entries = new List<HyobTreeEntry>();
            var bt = (BlockTable)tx.GetObject(db.BlockTableId, OpenMode.ForRead);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (ObjectId id in bt)
            {
                if (id.IsErased) continue;
                BlockTableRecord rec;
                try { rec = (BlockTableRecord)tx.GetObject(id, OpenMode.ForRead); }
                catch { continue; }

                string name = rec.Name ?? string.Empty;
                if (string.IsNullOrEmpty(name)) continue;
                if (!seen.Add(name)) continue;

                uint entCount = 0;
                try
                {
                    foreach (ObjectId _ in rec) entCount++;
                }
                catch { }

                string pathName = string.Empty;
                try { pathName = rec.PathName ?? string.Empty; }
                catch { }

                var def = new HyobBlockDef(
                    name: name,
                    description: rec.Comments ?? string.Empty,
                    isLayout: (byte)(rec.IsLayout ? 1 : 0),
                    isFromExternalReference: (byte)(rec.IsFromExternalReference ? 1 : 0),
                    isAnonymous: (byte)(rec.IsAnonymous ? 1 : 0),
                    ox: rec.Origin.X, oy: rec.Origin.Y, oz: rec.Origin.Z,
                    units: (byte)rec.Units,
                    entityCount: entCount,
                    pathName: pathName);

                Hash h = _objects.Write(def.EncodeBlob());
                entries.Add(new HyobTreeEntry(name, HyobTreeEntryKind.Object, h));
                count++;
            }

            var tree = new HyobTree(entries);
            return _objects.Write(tree.EncodeBlob());
        }

        private static string SafeReadLinetypeName(Transaction tx, ObjectId id)
        {
            if (id.IsNull || id.IsErased) return string.Empty;
            try
            {
                var rec = tx.GetObject(id, OpenMode.ForRead) as LinetypeTableRecord;
                return rec?.Name ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string SafeReadTextStyleName(Transaction tx, ObjectId id)
        {
            if (id.IsNull || id.IsErased) return string.Empty;
            try
            {
                var rec = tx.GetObject(id, OpenMode.ForRead) as TextStyleTableRecord;
                return rec?.Name ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static string SafeReadBlockName(Transaction tx, ObjectId id)
        {
            if (id.IsNull || id.IsErased) return string.Empty;
            try
            {
                var rec = tx.GetObject(id, OpenMode.ForRead) as BlockTableRecord;
                return rec?.Name ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        /// <summary>LineWeight 枚举 → 毫米。-3 ByLayer / -2 ByBlock / -1 默认 → 转 0；其他枚举值 = 实际值×0.01mm。</summary>
        private static double LineWeightToMm(LineWeight lw)
        {
            int v = (int)lw;
            if (v < 0) return v;
            return v / 100.0;
        }
    }
}
