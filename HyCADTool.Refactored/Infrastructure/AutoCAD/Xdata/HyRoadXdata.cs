using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata
{
    /// <summary>
    /// 道路模块的 AutoCAD XData 读写工具（对应决策 4 - DWG Xdata 作为稳定 GUID 载体）。
    ///
    /// 设计：
    /// - 在 <see cref="RegAppName"/>（"HY_ROAD"）命名空间下读写几个固定 Key（"ID" / "KIND" / "SCHEMA"）。
    /// - 使用 AutoCAD 原生 <c>XData</c> 机制（TypedValue 数组），而非 <c>ExtensionDictionary</c>，因为：
    ///     · GUID 与类型标签合计 ~60 字节，远在 XData 16KB 限制以内。
    ///     · XData 贴近对象、查询简单、导出 DXF 时保留，符合"对象附加数据"心智模型。
    /// - v1 只写 ID + KIND；v2 Blender 同步阶段可扩展 LOD / 材质指针等。
    ///
    /// 使用须知：
    /// - 首次写入任何对象前需要保证 RegApp 已注册（<see cref="EnsureRegApp"/>）。
    /// - 所有方法都在调用方提供的 <c>Transaction</c> 作用域内执行，本类不自主开启事务。
    /// </summary>
    public static class HyRoadXdata
    {
        /// <summary>RegApp 名称（HY_ROAD）。</summary>
        public const string RegAppName = "HY_ROAD";

        /// <summary>领域对象 GUID 的 Xdata Key。</summary>
        public const string KeyId = "ID";

        /// <summary>对象类型标签的 Xdata Key（如 "Alignment" / "Profile" / "Template" / "Corridor" / "RoadArm"）。</summary>
        public const string KeyKind = "KIND";

        /// <summary>Schema 版本 Key（对应 <c>SchemaVersion.Current</c>）。</summary>
        public const string KeySchema = "SCHEMA";

        /// <summary>
        /// 确保 <see cref="RegAppName"/> 已注册到数据库的 RegAppTable。
        /// 首次写入 Xdata 之前必须调用一次（幂等）。
        /// </summary>
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

        /// <summary>
        /// 读取道路对象的 GUID。若对象无 HY_ROAD Xdata 或格式异常，返回 <see cref="Guid.Empty"/>。
        /// </summary>
        public static Guid ReadId(Transaction tr, DBObject obj)
        {
            string raw = ReadStringValue(obj, KeyId);
            if (string.IsNullOrWhiteSpace(raw)) return Guid.Empty;
            return Guid.TryParse(raw, out var g) ? g : Guid.Empty;
        }

        /// <summary>
        /// 读取道路对象的类型标签（"Alignment" / "Profile" / ...）。不存在返回 <c>null</c>。
        /// </summary>
        public static string ReadKind(Transaction tr, DBObject obj)
        {
            return ReadStringValue(obj, KeyKind);
        }

        /// <summary>
        /// 写入 / 覆盖道路对象的 ID + KIND（+ SCHEMA）。
        /// 本方法会重写全部 HY_ROAD Xdata 段，不保留未识别的 Key。
        /// </summary>
        public static void Write(Transaction tr, Database db, DBObject obj, Guid id, string kind, string schemaVersion)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            if (id == Guid.Empty) throw new ArgumentException("id cannot be empty", nameof(id));

            EnsureRegApp(tr, db);

            if (!obj.IsWriteEnabled) obj.UpgradeOpen();

            var rb = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, RegAppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeyId),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, id.ToString("D")),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeyKind),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, kind ?? string.Empty),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, KeySchema),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, schemaVersion ?? string.Empty));

            obj.XData = rb;
            rb.Dispose();
        }

        /// <summary>
        /// 按 Key 读取 HY_ROAD Xdata 的字符串值。
        /// 未找到 RegApp 或 Key 不存在时返回 <c>null</c>。
        /// </summary>
        private static string ReadStringValue(DBObject obj, string key)
        {
            if (obj == null) return null;
            var rb = obj.GetXDataForApplication(RegAppName);
            if (rb == null) return null;

            try
            {
                var values = rb.AsArray();
                // values[0] 为 RegAppName，随后为 key/value 对
                for (int i = 1; i + 1 < values.Length; i++)
                {
                    if (values[i].TypeCode != (int)DxfCode.ExtendedDataAsciiString) continue;
                    if (string.Equals(values[i].Value?.ToString(), key, StringComparison.Ordinal))
                    {
                        return values[i + 1].Value?.ToString();
                    }
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
