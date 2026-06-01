using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Geometry;
using HYFEA.Core.Dofs;
using HYFEA.Core.Model;
using HYFEA.Core.Results;

namespace HyCADTool.Features.Fem.Renderer
{
    public sealed class BeamRenderOptions
    {
        public BeamRenderOptions(
            string layerName,
            double deflectionScale,
            bool drawDeflection,
            bool drawMoment,
            bool drawShear,
            bool drawReactions,
            UnitSystem units)
        {
            LayerName = layerName;
            DeflectionScale = deflectionScale;
            DrawDeflection = drawDeflection;
            DrawMoment = drawMoment;
            DrawShear = drawShear;
            DrawReactions = drawReactions;
            Units = units;
        }

        public string LayerName { get; }
        public double DeflectionScale { get; }
        public bool DrawDeflection { get; }
        public bool DrawMoment { get; }
        public bool DrawShear { get; }
        public bool DrawReactions { get; }
        public UnitSystem Units { get; }
    }

    /// <summary>将位移与简化 M/V/R 结果写入当前空间（HYFEA-Result）。</summary>
    public static class BeamResultRenderer
    {
        /// <summary>轴线节点坐标与 <see cref="Integration.HyfeaGeometryMapper.MapBeam"/> 输出一致（MmN=mm，SI=m）。</summary>
        public static void Render(Document doc, IReadOnlyList<Point2D> axisNodesFem, FemResult result, BeamRenderOptions opt)
        {
            if (result.Displacements == null)
                return;

            double toCad = opt.Units == UnitSystem.SI ? 1000.0 : 1.0;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                EnsureLayer(tr, db, opt.LayerName);

                int n = axisNodesFem.Count;
                if (opt.DrawDeflection && n >= 2)
                {
                    var pl = new Polyline();
                    for (int i = 0; i < n; i++)
                    {
                        var nid = new NodeId(i);
                        double ux = result.Displacements.Get(nid, DofType.UX);
                        double uy = result.Displacements.Get(nid, DofType.UY);
                        double x = axisNodesFem[i].X * toCad + ux * opt.DeflectionScale * toCad;
                        double y = axisNodesFem[i].Y * toCad + uy * opt.DeflectionScale * toCad;
                        pl.AddVertexAt(i, new Point2d(x, y), 0, 0, 0);
                    }

                    pl.Layer = opt.LayerName;
                    pl.ColorIndex = 3;
                    ms.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);
                }

                if (opt.DrawReactions && result.Reactions != null)
                {
                    double th = 150 * toCad / 1000.0;
                    for (int i = 0; i < n; i++)
                    {
                        var nid = new NodeId(i);
                        bool hx = result.Reactions.TryGet(nid, DofType.UX, out var rx);
                        bool hy = result.Reactions.TryGet(nid, DofType.UY, out var ry);
                        if (!hx && !hy) continue;
                        if (!hx) rx = 0;
                        if (!hy) ry = 0;
                        double x = axisNodesFem[i].X * toCad;
                        double y = axisNodesFem[i].Y * toCad;
                        var mt = new MText
                        {
                            Location = new Point3d(x, y - 200 * toCad / 1000.0, 0),
                            Contents = $"Rx={rx:F0}\nRy={ry:F0}",
                            TextHeight = th,
                            Layer = opt.LayerName,
                        };
                        ms.AppendEntity(mt);
                        tr.AddNewlyCreatedDBObject(mt, true);
                    }
                }

                tr.Commit();
            }

            if ((opt.DrawMoment || opt.DrawShear) && result.BeamEndForces != null && axisNodesFem.Count >= 2)
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    EnsureLayer(tr, db, opt.LayerName);

                    var map = result.BeamEndForces.AsReadOnly();
                    if (opt.DrawMoment)
                    {
                        var plM = new Polyline();
                        int vi = 0;
                        for (int seg = 1; seg < axisNodesFem.Count; seg++)
                        {
                            var eid = new ElementId(seg);
                            if (!map.TryGetValue(eid, out var end))
                                continue;
                            var a = axisNodesFem[seg - 1];
                            var b = axisNodesFem[seg];
                            double mx = (a.X + b.X) / 2 * toCad;
                            double my = (a.Y + b.Y) / 2 * toCad;
                            double dx = b.X - a.X;
                            double dy = b.Y - a.Y;
                            double len = Math.Sqrt(dx * dx + dy * dy);
                            if (len < 1e-15) continue;
                            double nx = -dy / len;
                            double ny = dx / len;
                            double mAbs = Math.Max(Math.Abs(end.MA), Math.Abs(end.MB));
                            double off = Math.Min(3000 * toCad / 1000.0, mAbs * 1e-9 * toCad);
                            plM.AddVertexAt(vi++, new Point2d(mx + nx * off, my + ny * off), 0, 0, 0);
                        }

                        if (vi >= 2)
                        {
                            plM.Layer = opt.LayerName;
                            plM.ColorIndex = 1;
                            ms.AppendEntity(plM);
                            tr.AddNewlyCreatedDBObject(plM, true);
                        }
                    }

                    if (opt.DrawShear)
                    {
                        var plV = new Polyline();
                        int vi = 0;
                        for (int seg = 1; seg < axisNodesFem.Count; seg++)
                        {
                            var eid = new ElementId(seg);
                            if (!map.TryGetValue(eid, out var end))
                                continue;
                            var a = axisNodesFem[seg - 1];
                            var b = axisNodesFem[seg];
                            double mx = (a.X + b.X) / 2 * toCad;
                            double my = (a.Y + b.Y) / 2 * toCad;
                            double dx = b.X - a.X;
                            double dy = b.Y - a.Y;
                            double len = Math.Sqrt(dx * dx + dy * dy);
                            if (len < 1e-15) continue;
                            double nx = -dy / len;
                            double ny = dx / len;
                            double vAbs = Math.Max(Math.Abs(end.VA), Math.Abs(end.VB));
                            double off = Math.Min(2500 * toCad / 1000.0, vAbs * 1e-6 * toCad);
                            plV.AddVertexAt(vi++, new Point2d(mx - nx * off, my - ny * off), 0, 0, 0);
                        }

                        if (vi >= 2)
                        {
                            plV.Layer = opt.LayerName;
                            plV.ColorIndex = 5;
                            ms.AppendEntity(plV);
                            tr.AddNewlyCreatedDBObject(plV, true);
                        }
                    }

                    tr.Commit();
                }
            }

            doc.Editor.Regen();
        }

        private static void EnsureLayer(Transaction tr, Database db, string name)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;
            lt.UpgradeOpen();
            var lr = new LayerTableRecord
            {
                Name = name,
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 7),
            };
            lt.Add(lr);
            tr.AddNewlyCreatedDBObject(lr, true);
        }
    }
}
