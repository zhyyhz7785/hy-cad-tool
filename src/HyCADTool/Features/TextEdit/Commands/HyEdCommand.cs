using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Features.TextEdit.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.TextEdit.Commands
{
    /// <summary>
    /// hyed：极简文字/标注替代文字编辑（绕开原生 IPE/TTF 扫描）。
    /// 亦由 <see cref="HyEdDoubleClickInterceptor"/> 通过 <c>_HYED_INTERNAL</c> 派发。
    /// </summary>
    public sealed class HyEdCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            var ed = doc.Editor;
            ObjectId target;

            var implied = ed.SelectImplied();
            if (implied.Status == PromptStatus.OK && implied.Value.Count == 1)
            {
                target = implied.Value.GetObjectIds()[0];
                if (!IsSupportedPick(doc.Database, target))
                {
                    ed.WriteMessage("\n[hyed] 当前选择不是支持的类型，请重新选择。\n");
                    ClearImpliedSafely(ed);
                    target = PromptForEntity(ed);
                    if (target.IsNull)
                        return;
                }
            }
            else
            {
                target = PromptForEntity(ed);
                if (target.IsNull)
                    return;
            }

            HyEdLauncher.LaunchEditor(doc, target);
        }

        private static ObjectId PromptForEntity(Editor ed)
        {
            var opts = new PromptEntityOptions("\n[hyed] 选择文字/引线/标注:")
            {
                AllowNone = false
            };
            opts.SetRejectMessage("\n仅支持 MText / DBText / MLeader(MText) / Dimension。");
            opts.AddAllowedClass(typeof(MText), exactMatch: false);
            opts.AddAllowedClass(typeof(DBText), exactMatch: false);
            opts.AddAllowedClass(typeof(MLeader), exactMatch: false);
            opts.AddAllowedClass(typeof(Dimension), exactMatch: false);

            PromptEntityResult r = ed.GetEntity(opts);
            return r.Status == PromptStatus.OK ? r.ObjectId : ObjectId.Null;
        }

        private static bool IsSupportedPick(Database db, ObjectId id)
        {
            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead, false) is Entity ent))
                    {
                        tr.Commit();
                        return false;
                    }

                    bool ok = HyEdEntityProbe.TryGetKind(ent, out _);
                    tr.Commit();
                    return ok;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void ClearImpliedSafely(Editor ed)
        {
            try
            {
                ed.SetImpliedSelection(new ObjectId[0]);
            }
            catch
            {
                /* ignore */
            }
        }
    }
}
