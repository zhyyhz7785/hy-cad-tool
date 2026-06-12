using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.G101.Domain.Geometry;

namespace HyCADTool.Features.G101.Services
{
    /// <summary>
    /// DetailSketch → AutoCAD 实体渲染。
    /// 必须在命令上下文（_HyExec 路由）中调用；内部单文档锁 + 单事务批量写入，
    /// 图层在同一事务内创建，避免 modeless 面板下 eLockViolation/eKeyNotFound。
    /// </summary>
    public static class DetailSketchRenderer
    {
        private const string LayerConcrete = "G101-混凝土";
        private const string LayerRebar = "G101-钢筋";
        private const string LayerDim = "G101-标注";
        private const string LayerText = "G101-文字";

        public static ObjectIdCollection Render(DetailSketch sketch, Point3d insertPoint, double scale = 1.0)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ids = new ObjectIdCollection();
            var ox = insertPoint.X + sketch.Origin.X * scale;
            var oy = insertPoint.Y + sketch.Origin.Y * scale;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                EnsureLayers(db, tr);

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                void Append(Entity ent)
                {
                    ids.Add(btr.AppendEntity(ent));
                    tr.AddNewlyCreatedDBObject(ent, true);
                }

                foreach (var pl in sketch.Polylines)
                {
                    var acPl = new Polyline();
                    for (int i = 0; i < pl.Points.Count; i++)
                    {
                        var p = pl.Points[i];
                        acPl.AddVertexAt(i, new Point2d(ox + p.X * scale, oy + p.Y * scale), 0, 0, 0);
                    }
                    if (pl.Closed) acPl.Closed = true;
                    acPl.Layer = MapLayer(pl.Layer);
                    acPl.Color = MapColor(pl.LineKind);
                    if (pl.LineKind == SketchLineKind.Rebar)
                        acPl.ConstantWidth = System.Math.Max(0.5, pl.LineWeight * scale);
                    Append(acPl);
                }

                foreach (var arc in sketch.Arcs)
                {
                    var a = new Arc(
                        new Point3d(ox + arc.Center.X * scale, oy + arc.Center.Y * scale, 0),
                        arc.Radius * scale,
                        arc.StartAngleDeg * System.Math.PI / 180.0,
                        arc.EndAngleDeg * System.Math.PI / 180.0);
                    a.Layer = MapLayer(arc.Layer);
                    a.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                    Append(a);
                }

                foreach (var t in sketch.Texts)
                {
                    var dbText = new DBText
                    {
                        Position = new Point3d(ox + t.Position.X * scale, oy + t.Position.Y * scale, 0),
                        TextString = t.Content,
                        Height = t.Height * scale,
                        Rotation = t.RotationDeg * System.Math.PI / 180.0,
                        Layer = LayerText
                    };
                    Append(dbText);
                }

                foreach (var d in sketch.Dimensions)
                {
                    var dim = new AlignedDimension(
                        new Point3d(ox + d.P1.X * scale, oy + d.P1.Y * scale, 0),
                        new Point3d(ox + d.P2.X * scale, oy + d.P2.Y * scale, 0),
                        new Point3d(ox + d.DimLinePoint.X * scale, oy + d.DimLinePoint.Y * scale, 0),
                        string.IsNullOrEmpty(d.OverrideText) ? string.Empty : d.OverrideText,
                        db.Dimstyle);
                    dim.Layer = LayerDim;
                    Append(dim);
                }

                if (!string.IsNullOrEmpty(sketch.Title))
                {
                    var title = new DBText
                    {
                        Position = new Point3d(ox, oy + 20 * scale, 0),
                        TextString = sketch.Title,
                        Height = 3.5 * scale,
                        Layer = LayerText
                    };
                    Append(title);
                }

                tr.Commit();
            }

            return ids;
        }

        private static string MapLayer(SketchLayerKind kind)
        {
            switch (kind)
            {
                case SketchLayerKind.Rebar: return LayerRebar;
                case SketchLayerKind.Dimension: return LayerDim;
                case SketchLayerKind.Text: return LayerText;
                default: return LayerConcrete;
            }
        }

        private static Color MapColor(SketchLineKind kind)
        {
            switch (kind)
            {
                case SketchLineKind.Rebar: return Color.FromColorIndex(ColorMethod.ByAci, 1);
                case SketchLineKind.Dimension: return Color.FromColorIndex(ColorMethod.ByAci, 4);
                default: return Color.FromColorIndex(ColorMethod.ByAci, 7);
            }
        }

        /// <summary>在当前事务内确保 G101 图层存在（调用方已持有文档锁）。</summary>
        private static void EnsureLayers(Database db, Transaction tr)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (var name in new[] { LayerConcrete, LayerRebar, LayerDim, LayerText })
            {
                if (lt.Has(name)) continue;
                if (!lt.IsWriteEnabled) lt.UpgradeOpen();
                var ltr = new LayerTableRecord { Name = name };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }
    }
}
