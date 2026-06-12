using System;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.Reinforcement.Domain.Components;

namespace HyCADTool.Shared.AutoCAD.Xdata
{
    /// <summary>
    /// 构件识别预览实体的 XData（HY_COMPONENT）。
    /// </summary>
    public static class HyComponentXdata
    {
        public const string RegAppName = "HY_COMPONENT";
        public const string KindPreview = "ComponentPreview";

        public const string KeySession = "SESSION";
        public const string KeyRegionId = "REGION_ID";
        public const string KeyType = "TYPE";

        public static void EnsureRegApp(Transaction tr, Database db)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));

            var regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (regAppTable.Has(RegAppName)) return;

            regAppTable.UpgradeOpen();
            using (var record = new RegAppTableRecord { Name = RegAppName })
            {
                regAppTable.Add(record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
        }

        public static void WritePreview(Transaction tr, Database db, DBObject obj, Guid sessionId, Guid regionId, ComponentType type)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            EnsureRegApp(tr, db);
            if (!obj.IsWriteEnabled) obj.UpgradeOpen();

            var rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeySession),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, sessionId.ToString("D")),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeyRegionId),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, regionId.ToString("D")),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeyType),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, ((int)type).ToString()),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, "KIND"),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KindPreview));

            obj.XData = rb;
            rb.Dispose();
        }

        public static bool TryReadPreview(DBObject obj, out Guid sessionId, out Guid regionId, out ComponentType type)
        {
            sessionId = Guid.Empty;
            regionId = Guid.Empty;
            type = ComponentType.Slab;

            string kind = ReadString(obj, "KIND");
            if (!string.Equals(kind, KindPreview, StringComparison.Ordinal))
                return false;

            string sessionRaw = ReadString(obj, KeySession);
            string regionRaw = ReadString(obj, KeyRegionId);
            string typeRaw = ReadString(obj, KeyType);

            if (!Guid.TryParse(sessionRaw, out sessionId))
                return false;
            if (!Guid.TryParse(regionRaw, out regionId))
                return false;
            if (!int.TryParse(typeRaw, out int typeInt))
                return false;

            type = (ComponentType)typeInt;
            return true;
        }

        public static void UpdateType(DBObject obj, ComponentType type)
        {
            if (obj == null || !TryReadPreview(obj, out Guid sessionId, out Guid regionId, out _))
                return;

            var rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeySession),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, sessionId.ToString("D")),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeyRegionId),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, regionId.ToString("D")),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeyType),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, ((int)type).ToString()),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, "KIND"),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KindPreview));

            if (!obj.IsWriteEnabled) obj.UpgradeOpen();
            obj.XData = rb;
            rb.Dispose();
        }

        private static string ReadString(DBObject obj, string key)
        {
            var rb = obj.GetXDataForApplication(RegAppName);
            if (rb == null) return null;
            try
            {
                var values = rb.AsArray();
                for (int i = 1; i + 1 < values.Length; i++)
                {
                    if (values[i].TypeCode != (int)DxfCode.ExtendedDataAsciiString) continue;
                    if (string.Equals(values[i].Value?.ToString(), key, StringComparison.Ordinal))
                        return values[i + 1].Value?.ToString();
                }
                return null;
            }
            finally
            {
                rb.Dispose();
            }
        }
    }
}
