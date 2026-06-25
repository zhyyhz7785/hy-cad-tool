using System;
using Autodesk.AutoCAD.DatabaseServices;
using HyCAD.Tables.Layout;
using HyCAD.Tables.Structure;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>
    /// PhotoSlot 占位：内缩矩形 + 居中标签（AC8）。
    /// </summary>
    internal static class AcadTablePhotoSlotRenderer
    {
        internal readonly struct PhotoSlotPlaceholder
        {
            public PhotoSlotPlaceholder(Polyline innerFrame, DBText label)
            {
                InnerFrame = innerFrame;
                Label = label ?? throw new ArgumentNullException(nameof(label));
            }

            public Polyline InnerFrame { get; }
            public DBText Label { get; }
        }

        internal static bool TryGetInnerRect(LayoutRect bounds, double inset, out LayoutRect inner)
        {
            var innerLeft = bounds.Left + inset;
            var innerRight = bounds.Right - inset;
            var innerTop = bounds.Top - inset;
            var innerBottom = bounds.Bottom + inset;

            if (innerRight <= innerLeft || innerTop <= innerBottom)
            {
                inner = default;
                return false;
            }

            inner = new LayoutRect(innerLeft, innerTop, innerRight, innerBottom);
            return true;
        }

        internal static LayoutRect GetLabelBounds(LayoutRect bounds, double inset)
        {
            return TryGetInnerRect(bounds, inset, out var inner) ? inner : bounds;
        }

        internal static PhotoSlotPlaceholder CreatePlaceholder(
            LayoutRect bounds,
            AcadTableRenderOptions options,
            Database database,
            Transaction tr)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var inset = options.PhotoSlotInsetMm;
            Polyline innerFrame = null;
            if (TryGetInnerRect(bounds, inset, out var inner))
                innerFrame = CreateInnerFrame(inner, options, database, tr);

            var labelBounds = GetLabelBounds(bounds, inset);
            var label = CreateLabel(labelBounds, options, database);
            return new PhotoSlotPlaceholder(innerFrame, label);
        }

        private static Polyline CreateInnerFrame(
            LayoutRect inner,
            AcadTableRenderOptions options,
            Database database,
            Transaction tr)
        {
            var z = options.ZElevation;
            var polyline = new Polyline(4);
            polyline.AddVertexAt(0, new Autodesk.AutoCAD.Geometry.Point2d(inner.Left, inner.Top), 0, 0, 0);
            polyline.AddVertexAt(1, new Autodesk.AutoCAD.Geometry.Point2d(inner.Right, inner.Top), 0, 0, 0);
            polyline.AddVertexAt(2, new Autodesk.AutoCAD.Geometry.Point2d(inner.Right, inner.Bottom), 0, 0, 0);
            polyline.AddVertexAt(3, new Autodesk.AutoCAD.Geometry.Point2d(inner.Left, inner.Bottom), 0, 0, 0);
            polyline.Closed = true;
            polyline.Layer = options.EffectivePhotoSlotInnerLayerName;
            polyline.Elevation = z;

            var width = Math.Max(options.EffectivePhotoSlotInnerBorderWidthMm, options.MinBorderWidthMm);
            polyline.ConstantWidth = width;

            if (options.PhotoSlotUseDashedInnerBorder)
                TryApplyDashedLinetype(polyline, database, tr);

            return polyline;
        }

        private static DBText CreateLabel(
            LayoutRect labelBounds,
            AcadTableRenderOptions options,
            Database database)
        {
            var height = options.PhotoSlotLabelTextHeightMm > 0
                ? options.PhotoSlotLabelTextHeightMm
                : options.DefaultTextHeightMm;

            var center = labelBounds.Center;
            var text = new DBText
            {
                Height = height,
                TextString = options.PhotoSlotLabelText ?? "照片",
                Layer = options.EffectivePhotoSlotTextLayerName,
            };

            AcadTableTextMapper.ApplyAtPoint(
                text,
                center.X,
                center.Y,
                TextAlign.Center,
                TextAlign.Center,
                options.ZElevation,
                database);

            return text;
        }

        private static void TryApplyDashedLinetype(Entity entity, Database database, Transaction tr)
        {
            if (database == null || tr == null)
                return;

            try
            {
                var lt = (LinetypeTable)tr.GetObject(database.LinetypeTableId, OpenMode.ForRead);
                if (lt.Has("DASHED"))
                    entity.LinetypeId = lt["DASHED"];
            }
            catch
            {
                // 回退实线
            }
        }
    }
}
