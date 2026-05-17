using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Features.TextEdit.Services
{
    /// <summary>读取/写回 MText、DBText、MLeader(MText)、Dimension 的文字字段。</summary>
    public static class HyEdTextAccessor
    {
        public static bool TryRead(ObjectId id, out string text, out HyEdEntityKind kind, out string handleHex)
        {
            text = string.Empty;
            kind = HyEdEntityKind.Unknown;
            handleHex = string.Empty;

            if (id.IsNull || !id.IsValid)
                return false;

            var db = id.Database;
            if (db == null)
                return false;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var obj = tr.GetObject(id, OpenMode.ForRead, false);
                if (!(obj is Entity ent))
                {
                    tr.Commit();
                    return false;
                }

                handleHex = ent.Handle.ToString();
                if (!HyEdEntityProbe.TryGetKind(ent, out kind))
                {
                    tr.Commit();
                    return false;
                }

                text = ReadText(ent, kind);
                tr.Commit();
                return true;
            }
        }

        public static bool Write(ObjectId id, string newText, HyEdEntityKind kind)
        {
            if (id.IsNull || !id.IsValid || kind == HyEdEntityKind.Unknown)
                return false;

            var db = id.Database;
            if (db == null)
                return false;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var obj = tr.GetObject(id, OpenMode.ForWrite, false);
                if (!(obj is Entity ent))
                {
                    tr.Abort();
                    return false;
                }

                if (!HyEdEntityProbe.TryGetKind(ent, out var verifyKind) || verifyKind != kind)
                {
                    tr.Abort();
                    return false;
                }

                WriteText(ent, kind, newText ?? string.Empty);
                tr.Commit();
                return true;
            }
        }

        private static string ReadText(Entity ent, HyEdEntityKind kind)
        {
            switch (kind)
            {
                case HyEdEntityKind.MText:
                    return ((MText)ent).Contents ?? string.Empty;

                case HyEdEntityKind.DBText:
                    return ((DBText)ent).TextString ?? string.Empty;

                case HyEdEntityKind.MLeader:
                    return ((MLeader)ent).MText?.Contents ?? string.Empty;

                case HyEdEntityKind.Dimension:
                    return ((Dimension)ent).DimensionText ?? string.Empty;

                default:
                    return string.Empty;
            }
        }

        private static void WriteText(Entity ent, HyEdEntityKind kind, string newText)
        {
            switch (kind)
            {
                case HyEdEntityKind.MText:
                    ((MText)ent).Contents = newText;
                    break;

                case HyEdEntityKind.DBText:
                    ((DBText)ent).TextString = newText;
                    break;

                case HyEdEntityKind.MLeader:
                    {
                        var ml = (MLeader)ent;
                        var mt = ml.MText;
                        if (mt == null)
                            throw new InvalidOperationException("MLeader 无 MText 内容。");
                        mt.UpgradeOpen();
                        mt.Contents = newText;
                        ml.MText = mt;
                        break;
                    }

                case HyEdEntityKind.Dimension:
                    // 空串：清除替代，显示测量值（与 AutoCAD 行为一致）
                    ((Dimension)ent).DimensionText = string.IsNullOrEmpty(newText) ? string.Empty : newText;
                    break;
            }
        }
    }
}
