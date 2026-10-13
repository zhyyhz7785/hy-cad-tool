using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Utilities;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Features.Road.CrossSection.Services.FillRendering
{
    /// <summary>
    /// 在 ModelSpace 中按 <see cref="LayerFillSettings"/> 创建关联 Hatch 与/或 <see cref="BlockReference"/>。
    /// </summary>
    public static class LayerFillApplier
    {
        public static int ApplyHatch(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateGuid,
            Polyline boundary,
            LayerFillSettings fill,
            string layerName)
        {
            if (fill == null || !fill.PatternEnabled || string.IsNullOrWhiteSpace(fill.PatternName) || boundary == null)
                return 0;
            if (tr == null || ms == null || db == null) return 0;

            // boundary 必须先进 Database（有 ObjectId），否则下面 AppendLoop 会拿不到合法的 Loop Id。
            // 调用方已经入库的轮廓归调用方，失败时不删。本方法自己补进去的，填充失败就擦掉。
            bool addedBoundary = false;
            if (boundary.ObjectId.IsNull)
            {
                try
                {
                    ms.AppendEntity(boundary);
                    tr.AddNewlyCreatedDBObject(boundary, true);
                    addedBoundary = true;
                }
                catch
                {
                    return 0;
                }
            }

            string pat = fill.PatternName.Trim();
            var hatch = new Hatch
            {
                Layer = string.IsNullOrEmpty(layerName) ? boundary.Layer : layerName,
            };
            if (fill.ColorIndex >= 0)
                hatch.ColorIndex = fill.ColorIndex;
            try
            {
                hatch.SetHatchPattern(HatchPatternType.PreDefined, pat);
            }
            catch
            {
                hatch.Dispose();
                EraseIfAdded(tr, addedBoundary, boundary);
                return 0;
            }
            try
            {
                double sc = fill.PatternScale > 1e-9 ? fill.PatternScale : 1.0;
                hatch.PatternSpace = 1.0 * sc;
            }
            catch
            {
                // 预定义团块/版本差异可能不暴露 PatternSpace
            }
            hatch.PatternAngle = fill.PatternAngle * (Math.PI / 180.0);

            // ─── AutoCAD 规范：必须先把 Hatch 加进 Database，才能设置 Associative / AppendLoop / EvaluateHatch。
            //     否则 set_Associative 抛 eNotInDatabase（Hatch.SetReactors 需要 db 绑定）。
            try
            {
                ms.AppendEntity(hatch);
                tr.AddNewlyCreatedDBObject(hatch, true);
            }
            catch
            {
                hatch.Dispose();
                EraseIfAdded(tr, addedBoundary, boundary);
                return 0;
            }

            try
            {
                hatch.Associative = true;
                var loop = new ObjectIdCollection { boundary.ObjectId };
                hatch.AppendLoop(HatchLoopTypes.Outermost, loop);
                hatch.EvaluateHatch(true);
            }
            catch
            {
                // 任一步失败（图案不识别 / 边界拓扑问题 / 关联建立失败）都把 hatch 擦掉。
                // 调用方自己的轮廓留下；本方法临时补进库的边界一起擦掉。
                LockedLayerErase.TryErase(tr, hatch, null);
                EraseIfAdded(tr, addedBoundary, boundary);
                return 0;
            }

            HyRoadXdata.Write(tr, db, hatch, templateGuid, RoadStandardSectionDrawService.DrawingKind, SchemaVersion.Current);
            return 1;
        }

        private static void EraseIfAdded(Transaction tr, bool added, Polyline boundary)
        {
            if (!added || boundary == null)
                return;
            LockedLayerErase.TryErase(tr, boundary, null);
        }

        public static int ApplyBlock(
            Transaction tr,
            BlockTableRecord ms,
            Database db,
            Guid templateGuid,
            Point3d centerWcs,
            LayerFillSettings fill,
            string layerName)
        {
            if (fill == null || !fill.BlockEnabled) return 0;
            var name = (fill.BlockName ?? string.Empty).Trim();
            if (name.Length == 0) return 0;
            if (tr == null || ms == null || db == null) return 0;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(name))
            {
                try
                {
                    var doc = Application.DocumentManager.MdiActiveDocument;
                    doc?.Editor?.WriteMessage($"\n[HyRoad] 未找到图块「{name}」，已跳过结构层图块。");
                }
                catch
                {
                    // 非 CAD 环境
                }
                return 0;
            }
            var id = bt[name];
            var br = new BlockReference(centerWcs, id)
            {
                Layer = layerName,
                ColorIndex = 256,
            };
            double k = fill.BlockScale > 1e-9 ? fill.BlockScale : 1.0;
            br.ScaleFactors = new Scale3d(k, k, k);
            br.Rotation = fill.BlockRotation * (Math.PI / 180.0);
            ms.AppendEntity(br);
            tr.AddNewlyCreatedDBObject(br, true);
            HyRoadXdata.Write(tr, db, br, templateGuid, RoadStandardSectionDrawService.DrawingKind, SchemaVersion.Current);
            return 1;
        }
    }
}
