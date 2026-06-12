using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.Reinforcement.Domain.Components;
using HyCADTool.Shared.AutoCAD.Xdata;
using System;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 点击预览实体循环切换构件类型（hyCompSw）。
    /// </summary>
    public sealed class SwitchComponentTypeCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            if (ComponentSession.CurrentSessionId == Guid.Empty)
            {
                ed.WriteMessage("\n请先执行构件识别。");
                return;
            }

            var peo = new PromptEntityOptions("\n选择构件预览区域（Hatch/文字）:");
            peo.SetRejectMessage("\n请选择构件预览实体。");
            peo.AddAllowedClass(typeof(Hatch), true);
            peo.AddAllowedClass(typeof(DBText), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as Entity;
                if (ent == null || !HyComponentXdata.TryReadPreview(ent, out _, out Guid regionId, out ComponentType oldType))
                {
                    ed.WriteMessage("\n所选实体不是构件预览。");
                    return;
                }

                ComponentType newType = oldType.Next();
                UpdateAllEntitiesForRegion(tr, doc.Database, regionId, newType);
                tr.Commit();
                ed.WriteMessage($"\n区域类型：{oldType.DisplayName()} → {newType.DisplayName()}");
            }
        }

        private static void UpdateAllEntitiesForRegion(Transaction tr, Database db, Guid regionId, ComponentType newType)
        {
            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);
            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                if (ent == null) continue;
                if (!HyComponentXdata.TryReadPreview(ent, out _, out Guid rid, out _))
                    continue;
                if (rid != regionId) continue;

                HyComponentXdata.UpdateType(ent, newType);
                ent.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByAci, newType.ColorIndex());
                if (ent is DBText text)
                    text.TextString = newType.DisplayName();
            }

            ComponentSession.UpdateRegionType(regionId, newType);
        }
    }
}
