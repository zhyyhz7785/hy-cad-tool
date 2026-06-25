using System;
using System.Globalization;
using Autodesk.AutoCAD.DatabaseServices;
using HyCAD.Tables.Structure;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>
    /// 表格模块 AutoCAD XData（RegApp HY_TABLE）。
    /// </summary>
    public static class HyTableXdata
    {
        public const string RegAppName = "HY_TABLE";
        public const string KeyTableId = "TABLE_ID";
        public const string KeyKind = "KIND";
        public const string KeySchema = "SCHEMA";
        public const string KeyCell = "CELL";

        public const string KindCarrier = "Carrier";
        public const string KindGrid = "Grid";
        public const string KindText = "Text";

        public const string SchemaVersion = "1";
        public const string ExtensionJsonKey = "HyTable_JSON";

        public static void EnsureRegApp(Transaction tr, Database db)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));

            var regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (regAppTable.Has(RegAppName))
                return;

            regAppTable.UpgradeOpen();
            using (var record = new RegAppTableRecord { Name = RegAppName })
            {
                regAppTable.Add(record);
                tr.AddNewlyCreatedDBObject(record, true);
            }
        }

        public static void WriteCarrier(Transaction tr, Database db, DBObject obj, Guid tableId)
        {
            WriteCore(tr, db, obj, tableId, KindCarrier, null);
        }

        public static void WriteMember(
            Transaction tr,
            Database db,
            DBObject obj,
            Guid tableId,
            string kind,
            CellAddr cell)
        {
            WriteCore(tr, db, obj, tableId, kind, FormatCell(cell));
        }

        /// <summary>写入表级成员 XData（无 CELL 键，如网格线 / 斜线）。</summary>
        public static void WriteTableEntity(
            Transaction tr,
            Database db,
            DBObject obj,
            Guid tableId,
            string kind)
        {
            WriteCore(tr, db, obj, tableId, kind, null);
        }

        public static Guid ReadTableId(DBObject obj)
        {
            var raw = ReadStringValue(obj, KeyTableId);
            if (string.IsNullOrWhiteSpace(raw))
                return Guid.Empty;
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }

        public static string ReadKind(DBObject obj)
        {
            return ReadStringValue(obj, KeyKind);
        }

        public static bool TryReadCell(DBObject obj, out CellAddr cell)
        {
            cell = default;
            var raw = ReadStringValue(obj, KeyCell);
            if (string.IsNullOrEmpty(raw))
                return false;

            var parts = raw.Split(',');
            if (parts.Length != 2)
                return false;

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var row))
                return false;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var col))
                return false;

            cell = new CellAddr(row, col);
            return true;
        }

        public static bool IsHyTableEntity(DBObject obj)
        {
            if (obj == null)
                return false;
            var rb = obj.GetXDataForApplication(RegAppName);
            if (rb == null)
                return false;
            rb.Dispose();
            return true;
        }

        private static void WriteCore(
            Transaction tr,
            Database db,
            DBObject obj,
            Guid tableId,
            string kind,
            string cellKey)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (tableId == Guid.Empty)
                throw new ArgumentException("tableId cannot be empty", nameof(tableId));

            EnsureRegApp(tr, db);
            if (!obj.IsWriteEnabled)
                obj.UpgradeOpen();

            var values = new System.Collections.Generic.List<TypedValue>
            {
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeyTableId),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, tableId.ToString("D")),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeyKind),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, kind ?? string.Empty),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeySchema),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, SchemaVersion),
            };

            if (!string.IsNullOrEmpty(cellKey))
            {
                values.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeyCell));
                values.Add(new TypedValue((int)DxfCode.ExtendedDataAsciiString, cellKey));
            }

            var rb = new ResultBuffer(values.ToArray());
            obj.XData = rb;
            rb.Dispose();
        }

        private static string FormatCell(CellAddr cell) =>
            cell.Row.ToString(CultureInfo.InvariantCulture) + "," +
            cell.Col.ToString(CultureInfo.InvariantCulture);

        private static string ReadStringValue(DBObject obj, string key)
        {
            if (obj == null)
                return null;

            var rb = obj.GetXDataForApplication(RegAppName);
            if (rb == null)
                return null;

            try
            {
                var values = rb.AsArray();
                for (var i = 1; i + 1 < values.Length; i++)
                {
                    if (values[i].TypeCode != (int)DxfCode.ExtendedDataAsciiString)
                        continue;
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
