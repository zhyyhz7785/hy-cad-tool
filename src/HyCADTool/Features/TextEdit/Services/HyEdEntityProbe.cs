using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Features.TextEdit.Services
{
    /// <summary>判断 ObjectId 是否可被 hyed 编辑。</summary>
    public static class HyEdEntityProbe
    {
        public static bool TryGetKind(Entity ent, out HyEdEntityKind kind)
        {
            kind = HyEdEntityKind.Unknown;

            switch (ent)
            {
                case MText _:
                    kind = HyEdEntityKind.MText;
                    return true;

                case DBText _:
                    kind = HyEdEntityKind.DBText;
                    return true;

                case Dimension _:
                    kind = HyEdEntityKind.Dimension;
                    return true;

                case MLeader ml:
                    if (ml.ContentType == ContentType.MTextContent && ml.MText != null)
                    {
                        kind = HyEdEntityKind.MLeader;
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }
    }
}
