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
    /// Layer / TextStyle 处理：建好 entity 后由调用方做 layer 赋值；TextStyle 由本类内部按名查
    /// <see cref="TextStyleTable"/>，找不到回落 STANDARD 或保持默认 ObjectId.Null（AutoCAD 自动落到当前 STYLE）。
    /// 本类不创建 Layer / Style / Block 表条目；缺什么由 M9-C 反向同步表。
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
        ///
        /// <paramref name="db"/> / <paramref name="tx"/> 用于查 TextStyle 表；可传 null（DBText/MText 退化成默认样式）。
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
                    poly.Elevation = p.Elevation;
                    poly.Normal = new Vector3d(p.NormalX, p.NormalY, p.NormalZ);
                    if (p.ConstantWidth != 0) poly.ConstantWidth = p.ConstantWidth;
                    entity = poly;
                    layer = p.Layer;
                    return true;
                }
                case HyobObjectKind.DBText:
                {
                    var t = HyobDBText.Decode(blob);
                    var dbt = new DBText
                    {
                        TextString = t.TextString,
                        Position = new Point3d(t.PosX, t.PosY, t.PosZ),
                        Height = t.Height,
                        Rotation = t.RotationRad,
                        WidthFactor = t.WidthFactor != 0 ? t.WidthFactor : 1.0,
                        Oblique = t.ObliqueRad,
                        Thickness = t.Thickness,
                        Normal = new Vector3d(t.NormalX, t.NormalY, t.NormalZ),
                        HorizontalMode = (TextHorizontalMode)t.HorizontalMode,
                        VerticalMode = (TextVerticalMode)t.VerticalMode,
                        AlignmentPoint = new Point3d(t.AlignX, t.AlignY, t.AlignZ),
                    };
                    var styleId = ResolveTextStyleId(t.TextStyleName, db, tx);
                    if (!styleId.IsNull) dbt.TextStyleId = styleId;
                    entity = dbt;
                    layer = t.Layer;
                    return true;
                }
                case HyobObjectKind.MText:
                {
                    var m = HyobMText.Decode(blob);
                    var direction = (m.DirX == 0 && m.DirY == 0 && m.DirZ == 0)
                        ? new Vector3d(1, 0, 0)
                        : new Vector3d(m.DirX, m.DirY, m.DirZ);
                    var mt = new MText
                    {
                        Contents = m.Contents,
                        Location = new Point3d(m.LocX, m.LocY, m.LocZ),
                        TextHeight = m.TextHeight,
                        Width = m.Width,
                        Rotation = m.RotationRad,
                        Normal = new Vector3d(m.NormalX, m.NormalY, m.NormalZ),
                        Direction = direction,
                        Attachment = (AttachmentPoint)m.Attachment,
                        LineSpacingStyle = (LineSpacingStyle)m.LineSpacingStyle,
                        LineSpacingFactor = m.LineSpacingFactor != 0 ? m.LineSpacingFactor : 1.0,
                        BackgroundFill = m.BackgroundFill != 0,
                        BackgroundScaleFactor = m.BackgroundScaleFactor != 0 ? m.BackgroundScaleFactor : 1.5,
                    };
                    var styleId = ResolveTextStyleId(m.TextStyleName, db, tx);
                    if (!styleId.IsNull) mt.TextStyleId = styleId;
                    entity = mt;
                    layer = m.Layer;
                    return true;
                }
                default:
                    return false;
            }
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
