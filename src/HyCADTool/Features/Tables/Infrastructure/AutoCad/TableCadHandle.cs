using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>
    /// AutoCAD 侧表格实例句柄（Group + Carrier + Domain Id）。
    /// </summary>
    public sealed class TableCadHandle
    {
        public TableCadHandle(ObjectId groupId, ObjectId carrierId, Guid tableId, int entityCount)
        {
            GroupId = groupId;
            CarrierId = carrierId;
            TableId = tableId;
            EntityCount = entityCount;
        }

        public ObjectId GroupId { get; }

        public ObjectId CarrierId { get; }

        public Guid TableId { get; }

        public int EntityCount { get; }
    }
}
