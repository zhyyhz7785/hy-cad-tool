using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Models;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Tables;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.AutoCadMirror
{
    /// <summary>
    /// hyob → AutoCAD <see cref="LayerTable"/> 反向同步（M9-C-1）。<see cref="TablesMirror"/> 的逆向。
    ///
    /// 策略（保守 + 字段幂等）：
    ///   - target 有 / DB 无 → 创建 LayerTableRecord，拷贝完整字段
    ///   - target 有 / DB 有 / 字段不同 → 更新色 / 线型 / 线宽 / on/freeze/lock/plottable / description
    ///   - target 无 / DB 有 → 不动（保留 DB 已有 layer，避免误删用户层）
    ///   - <c>IsUsed</c>: 只读字段（由引用计算），跳过
    ///   - LinetypeName 在 DB 不存在 → 回落 Continuous（DB 自带，永远存在）
    ///
    /// 调用方负责事务；本类只在调用方提供的 <see cref="Transaction"/> 中读写。
    /// </summary>
    internal sealed class LayerReverseMirror
    {
        private readonly HyobObjectStore _objects;

        public LayerReverseMirror(HyobObjectStore objects)
        {
            _objects = objects ?? throw new ArgumentNullException(nameof(objects));
        }

        public sealed class LayerStats
        {
            public int Added { get; internal set; }
            public int Updated { get; internal set; }
            public int Skipped { get; internal set; }
        }

        public LayerStats Sync(Database db, Transaction tx, Hash targetRootTree)
        {
            var stats = new LayerStats();
            if (targetRootTree.IsZero) return stats;

            var targetLayers = LoadTargetLayers(targetRootTree);
            if (targetLayers.Count == 0) return stats;

            var lt = (LayerTable)tx.GetObject(db.LayerTableId, OpenMode.ForWrite);
            var ltt = (LinetypeTable)tx.GetObject(db.LinetypeTableId, OpenMode.ForRead);

            foreach (var def in targetLayers)
            {
                if (string.IsNullOrEmpty(def.Name)) { stats.Skipped++; continue; }

                ObjectId linetypeId = ResolveLinetypeId(ltt, def.LinetypeName, db);

                if (lt.Has(def.Name))
                {
                    var rec = (LayerTableRecord)tx.GetObject(lt[def.Name], OpenMode.ForWrite);
                    if (ApplyDefToRecord(rec, def, linetypeId)) stats.Updated++;
                }
                else
                {
                    var rec = new LayerTableRecord { Name = def.Name };
                    ApplyDefToRecord(rec, def, linetypeId);
                    lt.Add(rec);
                    tx.AddNewlyCreatedDBObject(rec, true);
                    stats.Added++;
                }
            }

            return stats;
        }

        /// <summary>
        /// 把 <paramref name="def"/> 的字段写到 <paramref name="rec"/>。返回 <c>true</c> 表示真有字段改变。
        /// 对**新建**记录而言函数总返回 true（caller 不依赖该值，只是用作 update 计数判定）。
        /// </summary>
        private static bool ApplyDefToRecord(LayerTableRecord rec, HyobLayerDef def, ObjectId linetypeId)
        {
            bool dirty = false;

            short ci = (short)def.ColorIndex;
            if (rec.Color == null || rec.Color.ColorIndex != ci)
            {
                rec.Color = Color.FromColorIndex(ColorMethod.ByAci, ci);
                dirty = true;
            }

            if (!linetypeId.IsNull && rec.LinetypeObjectId != linetypeId)
            {
                rec.LinetypeObjectId = linetypeId;
                dirty = true;
            }

            var lw = MmToLineWeight(def.LineweightMm);
            if (rec.LineWeight != lw)
            {
                rec.LineWeight = lw;
                dirty = true;
            }

            bool wantOff = def.IsOff != 0;
            if (rec.IsOff != wantOff) { rec.IsOff = wantOff; dirty = true; }

            bool wantFrozen = def.IsFrozen != 0;
            if (rec.IsFrozen != wantFrozen) { rec.IsFrozen = wantFrozen; dirty = true; }

            bool wantLocked = def.IsLocked != 0;
            if (rec.IsLocked != wantLocked) { rec.IsLocked = wantLocked; dirty = true; }

            bool wantPlot = def.IsPlottable != 0;
            if (rec.IsPlottable != wantPlot) { rec.IsPlottable = wantPlot; dirty = true; }

            string desc = def.Description ?? string.Empty;
            if ((rec.Description ?? string.Empty) != desc)
            {
                rec.Description = desc;
                dirty = true;
            }

            return dirty;
        }

        private static ObjectId ResolveLinetypeId(LinetypeTable ltt, string name, Database db)
        {
            if (string.IsNullOrEmpty(name))
            {
                return ltt.Has("Continuous") ? ltt["Continuous"] : db.ContinuousLinetype;
            }
            if (ltt.Has(name)) return ltt[name];
            return ltt.Has("Continuous") ? ltt["Continuous"] : db.ContinuousLinetype;
        }

        /// <summary>
        /// 读 target 的 <c>tables/layers/*</c> 子树，解码所有 <see cref="HyobLayerDef"/>。
        /// </summary>
        private List<HyobLayerDef> LoadTargetLayers(Hash rootTree)
        {
            var result = new List<HyobLayerDef>();
            if (!_objects.TryRead(rootTree, out var rootBlob)) return result;
            HyobTree root;
            try { root = HyobTree.Decode(rootBlob); } catch { return result; }
            if (!root.TryFind("tables", out var tablesEntry)
                || tablesEntry.Kind != HyobTreeEntryKind.Tree) return result;

            if (!_objects.TryRead(tablesEntry.Hash, out var tBlob)) return result;
            HyobTree tables;
            try { tables = HyobTree.Decode(tBlob); } catch { return result; }
            if (!tables.TryFind("layers", out var layersEntry)
                || layersEntry.Kind != HyobTreeEntryKind.Tree) return result;

            if (!_objects.TryRead(layersEntry.Hash, out var lBlob)) return result;
            HyobTree layers;
            try { layers = HyobTree.Decode(lBlob); } catch { return result; }

            foreach (var e in layers.Entries)
            {
                if (e.Kind != HyobTreeEntryKind.Object) continue;
                if (!_objects.TryRead(e.Hash, out var defBlob)) continue;
                try { result.Add(HyobLayerDef.Decode(defBlob)); }
                catch { /* schema 异常静默跳过单条 */ }
            }
            return result;
        }

        /// <summary>毫米 → LineWeight 枚举。负值（-1/-2/-3）保留语义；非负 mm × 100 取最近枚举。</summary>
        internal static LineWeight MmToLineWeight(double mm)
        {
            if (mm < 0)
            {
                int neg = (int)System.Math.Round(mm);
                if (neg == -3) return LineWeight.ByLayer;
                if (neg == -2) return LineWeight.ByBlock;
                return LineWeight.ByLineWeightDefault;
            }
            int hundredths = (int)System.Math.Round(mm * 100.0);
            if (hundredths < 0) hundredths = 0;
            if (hundredths > 211) hundredths = 211; // AutoCAD LineWeight211 = 2.11mm 是上限
            // 不是任意 hundredths 都对应有效枚举，但 LineWeight 是 short 的强类型；越界值 AutoCAD 会自动 clamp。
            return (LineWeight)hundredths;
        }
    }
}
