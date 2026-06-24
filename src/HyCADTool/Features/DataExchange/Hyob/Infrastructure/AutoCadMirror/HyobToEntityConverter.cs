using System;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.DataExchange.Hyob.Domain.Codec;
using HyCADTool.Features.DataExchange.Hyob.Domain.Objects.Typed;
using HyCADTool.Features.DataExchange.Hyob.Domain.Repository;
using HyCADTool.Features.DataExchange.Hyob.Domain.Schemas;

namespace HyCADTool.Features.DataExchange.Hyob.Infrastructure.AutoCadMirror
{
    /// <summary>
    /// hyob → AutoCAD <see cref="Entity"/> 反向构造器。<see cref="EntityToHyobConverter"/> 的逆向。
    ///
    /// 当前白名单（M9-B）：
    ///   - <c>Line</c> / <c>Circle</c> / <c>Arc</c>           （M9-A，纯几何）
    ///   - <c>Polyline</c>                                     （M9-B，2D + bulge + closed + 全局线宽）
    ///   - <c>DBText</c> / <c>MText</c>                        （M9-B，需 Database 上下文做 TextStyle 解析）
    ///
    /// 未支持（继续 skip 不动 DWG）：BlockRef（依赖 Block 表）/ Dimension 9 subtype / MLeader（语义级，无完整顶点） /
    /// Hatch（pattern-only，缺边界几何） / Wipeout（forward 当前未实现） / Opaque（黑盒不可还原）。
    ///
    /// <para>
    /// TryBuild 仅设置内存安全字段；Elevation/Normal/TextStyleId/MText 背景等须在 Append 后由
    /// <see cref="ApplyPostAppend"/> 补写（Append 前设置会抛 eNotApplicable）。
    /// </para>
    /// </summary>
    internal static class HyobToEntityConverter
    {
        /// <summary>白名单 type_id 是否支持反向构造。</summary>
        public static bool IsSupported(HyobObjectKind kind)
        {
            switch (kind)
            {
                case HyobObjectKind.Line:
                case HyobObjectKind.Circle:
                case HyobObjectKind.Arc:
                case HyobObjectKind.Polyline:
                case HyobObjectKind.DBText:
                case HyobObjectKind.MText:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 把 hyob blob 还原成 AutoCAD Entity。返回 false 表示 type_id 不在白名单（调用方应跳过）。
        /// 抛 <see cref="InvalidDataException"/> 表示白名单内但 schema 不识别（这种是 Domain 错误，hyobI 自检会先暴露）。
        /// </summary>
        public static bool TryBuild(byte[] blob, Database db, Transaction tx, out Entity entity, out string layer)
        {
            entity = null;
            layer = null;
            if (blob == null) throw new ArgumentNullException(nameof(blob));

            var (header, _) = HyobObjectHeader.Decode(blob);
            switch (header.TypeId)
            {
                case HyobObjectKind.Line:
                {
                    var l = HyobLine.Decode(blob);
                    entity = new Line(
                        new Point3d(l.StartX, l.StartY, l.StartZ),
                        new Point3d(l.EndX,   l.EndY,   l.EndZ));
                    layer = l.Layer;
                    return true;
                }
                case HyobObjectKind.Circle:
                {
                    var c = HyobCircle.Decode(blob);
                    entity = new Circle(
                        new Point3d(c.CenterX, c.CenterY, c.CenterZ),
                        new Vector3d(c.NormalX, c.NormalY, c.NormalZ),
                        c.Radius);
                    layer = c.Layer;
                    return true;
                }
                case HyobObjectKind.Arc:
                {
                    var a = HyobArc.Decode(blob);
                    entity = new Arc(
                        new Point3d(a.CenterX, a.CenterY, a.CenterZ),
                        new Vector3d(a.NormalX, a.NormalY, a.NormalZ),
                        a.Radius,
                        a.StartAngleRad,
                        a.EndAngleRad);
                    layer = a.Layer;
                    return true;
                }
                case HyobObjectKind.Polyline:
                {
                    var p = HyobPolyline.Decode(blob);
                    var poly = new Polyline(p.Vertices.Count);
                    for (int i = 0; i < p.Vertices.Count; i++)
                    {
                        var v = p.Vertices[i];
                        poly.AddVertexAt(i, new Point2d(v.X, v.Y), v.Bulge, 0, 0);
                    }
                    poly.Closed = p.Closed;
                    entity = poly;
                    layer = p.Layer;
                    return true;
                }
                case HyobObjectKind.DBText:
                {
                    var t = HyobDBText.Decode(blob);
                    entity = new DBText
                    {
                        TextString = t.TextString,
                        Position = new Point3d(t.PosX, t.PosY, t.PosZ),
                        Height = t.Height,
                        Rotation = t.RotationRad,
                        WidthFactor = t.WidthFactor != 0 ? t.WidthFactor : 1.0,
                        Oblique = t.ObliqueRad,
                    };
                    layer = t.Layer;
                    return true;
                }
                case HyobObjectKind.MText:
                {
                    var m = HyobMText.Decode(blob);
                    entity = new MText
                    {
                        Contents = m.Contents,
                        Location = new Point3d(m.LocX, m.LocY, m.LocZ),
                        TextHeight = m.TextHeight,
                        Rotation = m.RotationRad,
                    };
                    layer = m.Layer;
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary>
        /// Append 到 Database 后补写需库内上下文才能设置的属性。
        /// 调用方须在 <see cref="Transaction.AddNewlyCreatedDBObject"/> 之后调用；单项失败静默跳过。
        /// </summary>
        public static void ApplyPostAppend(Entity entity, byte[] blob, Database db, Transaction tx)
        {
            if (entity == null || blob == null || db == null || tx == null) return;

            var (header, _) = HyobObjectHeader.Decode(blob);
            switch (header.TypeId)
            {
                case HyobObjectKind.Polyline:
                    ApplyPolylinePostAppend(entity, HyobPolyline.Decode(blob));
                    break;
                case HyobObjectKind.DBText:
                    ApplyDBTextPostAppend(entity, HyobDBText.Decode(blob), db, tx);
                    break;
                case HyobObjectKind.MText:
                    ApplyMTextPostAppend(entity, HyobMText.Decode(blob), db, tx);
                    break;
            }
        }

        private static void ApplyPolylinePostAppend(Entity entity, HyobPolyline p)
        {
            if (!(entity is Polyline poly)) return;
            try
            {
                if (p.Elevation != 0) poly.Elevation = p.Elevation;
                if (p.NormalX != 0 || p.NormalY != 0 || (p.NormalZ != 0 && p.NormalZ != 1))
                    poly.Normal = new Vector3d(p.NormalX, p.NormalY, p.NormalZ);
                if (p.ConstantWidth != 0)
                {
                    for (int i = 0; i < poly.NumberOfVertices; i++)
                    {
                        poly.SetStartWidthAt(i, p.ConstantWidth);
                        poly.SetEndWidthAt(i, p.ConstantWidth);
                    }
                    try { poly.ConstantWidth = p.ConstantWidth; } catch { }
                }
            }
            catch { }
        }

        private static void ApplyDBTextPostAppend(Entity entity, HyobDBText t, Database db, Transaction tx)
        {
            if (!(entity is DBText dbt)) return;
            try
            {
                if (t.Thickness != 0) dbt.Thickness = t.Thickness;
                if (t.NormalX != 0 || t.NormalY != 0 || (t.NormalZ != 0 && t.NormalZ != 1))
                    dbt.Normal = new Vector3d(t.NormalX, t.NormalY, t.NormalZ);
                var styleId = ResolveTextStyleId(t.TextStyleName, db, tx);
                if (!styleId.IsNull) dbt.TextStyleId = styleId;
                if (t.HorizontalMode != 0 || t.VerticalMode != 0)
                {
                    dbt.HorizontalMode = (TextHorizontalMode)t.HorizontalMode;
                    dbt.VerticalMode = (TextVerticalMode)t.VerticalMode;
                    dbt.AlignmentPoint = new Point3d(t.AlignX, t.AlignY, t.AlignZ);
                }
            }
            catch { }
        }

        private static void ApplyMTextPostAppend(Entity entity, HyobMText m, Database db, Transaction tx)
        {
            if (!(entity is MText mt)) return;

            try { if (m.Width != 0) mt.Width = m.Width; } catch { }

            if (m.BackgroundFill != 0)
            {
                try
                {
                    mt.BackgroundFill = true;
                    mt.BackgroundScaleFactor = m.BackgroundScaleFactor != 0 ? m.BackgroundScaleFactor : 1.5;
                    if (m.BackgroundColorArgb != 0)
                    {
                        int argb = unchecked((int)m.BackgroundColorArgb);
                        mt.BackgroundFillColor = Autodesk.AutoCAD.Colors.Color.FromColor(
                            System.Drawing.Color.FromArgb(argb));
                    }
                }
                catch { }
            }

            try
            {
                if (m.NormalX != 0 || m.NormalY != 0 || (m.NormalZ != 0 && m.NormalZ != 1))
                    mt.Normal = new Vector3d(m.NormalX, m.NormalY, m.NormalZ);
                var direction = (m.DirX == 0 && m.DirY == 0 && m.DirZ == 0)
                    ? new Vector3d(1, 0, 0)
                    : new Vector3d(m.DirX, m.DirY, m.DirZ);
                mt.Direction = direction;
                mt.Attachment = (AttachmentPoint)m.Attachment;
                mt.LineSpacingStyle = (LineSpacingStyle)m.LineSpacingStyle;
                mt.LineSpacingFactor = m.LineSpacingFactor != 0 ? m.LineSpacingFactor : 1.0;
                var styleId = ResolveTextStyleId(m.TextStyleName, db, tx);
                if (!styleId.IsNull) mt.TextStyleId = styleId;
            }
            catch { }
        }

        /// <summary>按名查 TextStyle 表；找不到返回 ObjectId.Null（调用方保留默认）。</summary>
        private static ObjectId ResolveTextStyleId(string name, Database db, Transaction tx)
        {
            if (db == null || tx == null || string.IsNullOrEmpty(name)) return ObjectId.Null;
            try
            {
                var tt = (TextStyleTable)tx.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                if (tt.Has(name)) return tt[name];
            }
            catch { }
            return ObjectId.Null;
        }
    }
}

