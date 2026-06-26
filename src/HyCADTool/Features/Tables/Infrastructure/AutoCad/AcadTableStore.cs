using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using HyCAD.Tables;
using HyCAD.Tables.Serialization;
using HyCAD.Tables.Structure;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>
    /// HyTable CAD 真相源：Carrier JSON、成员 XData、Group 分组。
    /// </summary>
    public static class AcadTableStore
    {
        public static TableCadHandle Attach(
            Transaction tr,
            Database db,
            TableGrid grid,
            ObjectId carrierId,
            IReadOnlyList<(ObjectId Id, string Kind, CellAddr? Cell)> members)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (carrierId.IsNull)
                throw new ArgumentException("carrierId is null", nameof(carrierId));

            var carrier = (Entity)tr.GetObject(carrierId, OpenMode.ForWrite);
            var json = TableJson.SerializeGrid(grid);
            ExtensionDictionaryService.WriteLongString(tr, carrier, json, HyTableXdata.ExtensionJsonKey);
            HyTableXdata.WriteCarrier(tr, db, carrier, grid.Id);

            var allIds = new ObjectIdCollection { carrierId };
            if (members != null)
            {
                foreach (var member in members)
                {
                    var obj = tr.GetObject(member.Id, OpenMode.ForWrite);
                    if (member.Cell.HasValue)
                        HyTableXdata.WriteMember(tr, db, obj, grid.Id, member.Kind, member.Cell.Value);
                    else
                        HyTableXdata.WriteTableEntity(tr, db, obj, grid.Id, member.Kind);
                    allIds.Add(member.Id);
                }
            }

            var groupId = CreateGroup(tr, db, grid.Id, allIds);
            return new TableCadHandle(groupId, carrierId, grid.Id, allIds.Count);
        }

        /// <summary>
        /// 从 Carrier、成员或 Group 读回 <see cref="TableGrid"/>。
        /// </summary>
        public static bool TryResolveTableGrid(
            Transaction tr,
            Database db,
            DBObject picked,
            out TableGrid grid,
            out string error)
        {
            grid = null;
            error = null;

            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (picked == null)
            {
                error = "未选择实体。";
                return false;
            }

            var carrierId = ResolveCarrierId(tr, db, picked);
            if (carrierId.IsNull)
            {
                error = "非 HyTable 实体（无 HY_TABLE 标识或找不到载体）。";
                return false;
            }

            var carrier = tr.GetObject(carrierId, OpenMode.ForRead) as Entity;
            if (carrier == null)
            {
                error = "载体实体无效。";
                return false;
            }

            var json = ExtensionDictionaryService.ReadLongString(tr, carrier, HyTableXdata.ExtensionJsonKey);
            if (string.IsNullOrEmpty(json))
            {
                error = "载体无 HyTable_JSON（可能未 Attach 或已 Explode）。";
                return false;
            }

            try
            {
                grid = TableJson.DeserializeGrid(json);
                return true;
            }
            catch (Exception ex)
            {
                error = "JSON 反序列化失败：" + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 解析 HyTable 载体 ObjectId（Carrier / 成员 / Group）。
        /// </summary>
        public static bool TryResolveCarrierId(
            Transaction tr,
            Database db,
            DBObject picked,
            out ObjectId carrierId)
        {
            carrierId = ResolveCarrierId(tr, db, picked);
            return !carrierId.IsNull;
        }

        private static ObjectId ResolveCarrierId(Transaction tr, Database db, DBObject picked)
        {
            if (picked is Group group)
                return FindCarrierInGroup(tr, group);

            var entity = picked as Entity;
            if (entity == null || !HyTableXdata.IsHyTableEntity(entity))
                return ObjectId.Null;

            var kind = HyTableXdata.ReadKind(entity);
            if (string.Equals(kind, HyTableXdata.KindCarrier, StringComparison.Ordinal))
                return entity.ObjectId;

            var tableId = HyTableXdata.ReadTableId(entity);
            if (tableId == Guid.Empty)
                return ObjectId.Null;

            return FindCarrierViaGroup(tr, db, entity.ObjectId, tableId);
        }

        private static ObjectId FindCarrierViaGroup(Transaction tr, Database db, ObjectId memberId, Guid tableId)
        {
            var groupDict = tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead) as DBDictionary;
            if (groupDict == null)
                return ObjectId.Null;

            foreach (DBDictionaryEntry entry in groupDict)
            {
                var group = tr.GetObject(entry.Value, OpenMode.ForRead) as Group;
                if (group == null || !GroupContains(group, memberId))
                    continue;

                var carrierId = FindCarrierInGroup(tr, group, tableId);
                if (!carrierId.IsNull)
                    return carrierId;
            }

            return ObjectId.Null;
        }

        private static ObjectId FindCarrierInGroup(Transaction tr, Group group, Guid tableId = default)
        {
            if (group == null)
                return ObjectId.Null;

            foreach (ObjectId id in group.GetAllEntityIds())
            {
                var obj = tr.GetObject(id, OpenMode.ForRead);
                if (!string.Equals(HyTableXdata.ReadKind(obj), HyTableXdata.KindCarrier, StringComparison.Ordinal))
                    continue;

                if (tableId != Guid.Empty && HyTableXdata.ReadTableId(obj) != tableId)
                    continue;

                return id;
            }

            return ObjectId.Null;
        }

        private static bool GroupContains(Group group, ObjectId entityId)
        {
            foreach (ObjectId id in group.GetAllEntityIds())
            {
                if (id == entityId)
                    return true;
            }

            return false;
        }

        private static ObjectId CreateGroup(Transaction tr, Database db, Guid tableId, ObjectIdCollection entityIds)
        {
            var groupDict = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForWrite);
            var groupName = BuildGroupName(tableId);

            if (groupDict.Contains(groupName))
            {
                var existing = tr.GetObject(groupDict.GetAt(groupName), OpenMode.ForWrite);
                existing.Erase();
            }

            var group = new Group();
            foreach (ObjectId id in entityIds)
                group.Append(id);

            groupDict.SetAt(groupName, group);
            tr.AddNewlyCreatedDBObject(group, true);
            return group.ObjectId;
        }

        public static string BuildGroupName(Guid tableId) =>
            "*HyTable-" + tableId.ToString("N");

        /// <summary>
        /// 删除 HyTable 组内全部实体及组本身（AC11 原位再发布）。
        /// </summary>
        public static bool TryEraseTable(Transaction tr, Database db, Guid tableId, out string error)
        {
            error = null;
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (db == null)
                throw new ArgumentNullException(nameof(db));

            var groupName = BuildGroupName(tableId);
            var groupDict = tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead) as DBDictionary;
            if (groupDict == null || !groupDict.Contains(groupName))
            {
                // 组已不存在视为已删除（原位再发布 / 外部擦除）
                return true;
            }

            var group = tr.GetObject(groupDict.GetAt(groupName), OpenMode.ForWrite) as Group;
            if (group == null)
            {
                error = "表格组无效。";
                return false;
            }

            foreach (ObjectId id in group.GetAllEntityIds())
            {
                var obj = tr.GetObject(id, OpenMode.ForWrite, false);
                if (obj != null && !obj.IsErased)
                    obj.Erase();
            }

            group.Erase();
            return true;
        }
    }
}
