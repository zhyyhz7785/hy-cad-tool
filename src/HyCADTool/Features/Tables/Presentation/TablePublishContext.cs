using System;
using Autodesk.AutoCAD.Geometry;

namespace HyCADTool.Features.Tables.Presentation
{
    /// <summary>
    /// AC11：面板与 CAD 实例之间的发布上下文（载体、插入点）。
    /// </summary>
    public sealed class TablePublishContext
    {
        public TablePublishContext(Guid tableId, Point3d insertionPoint, string carrierHandle = null)
        {
            TableId = tableId;
            InsertionPoint = insertionPoint;
            CarrierHandle = carrierHandle;
        }

        public Guid TableId { get; }

        public Point3d InsertionPoint { get; }

        public string CarrierHandle { get; set; }
    }
}
