using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shell.ViewModels;
using HyCADTool.Shell.Configuration;
using System;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 外部钢筋生成命令（ggj）
    /// 选择闭合多段线 → 外偏移 → 生成线钢筋（含弯钩）
    /// </summary>
    public class ReinOutsideCommand
    {
        private static string LayerLineRein => UserLayerNameResolver.Get(LayerSemanticIds.ReinLineExternal, LayerBuiltinDefaults.ReinLineExternal);

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var vm = SettingsPanelViewModel.Current;
            double scale = ScaleResolver.GetScale();
            double protectionThickness = (vm?.ProtectionThickness ?? 1.0) * scale;
            double anchorageLength = ScaleResolver.GetAnchorageLength();
            double hookLength = (vm?.HookLength ?? 1.0) * scale;
            double reinWidth = (vm?.PolylineWidth ?? 0.4) * scale;

            vm?.EnsureStylesApplied();

            var peo = new PromptEntityOptions("\n请选择一个闭合 Polyline:");
            peo.SetRejectMessage("\n请选择一个 Polyline。");
            peo.AddAllowedClass(typeof(Polyline), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var source = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                    if (source == null || !source.Closed)
                    {
                        ed.WriteMessage("\n请选择一个闭合 Polyline");
                        return;
                    }

                    var workPoly = source.Clone() as Polyline;
                    if (workPoly == null) return;

                    workPoly.EnsureClockwise();

                    DBObjectCollection offsets = workPoly.GetOffsetCurves(-protectionThickness);
                    workPoly.Dispose();

                    if (offsets == null || offsets.Count == 0)
                    {
                        ed.WriteMessage("\n偏移失败");
                        DisposeOffsetCurves(offsets);
                        return;
                    }

                    Polyline boundary = null;
                    foreach (Entity ent in offsets)
                    {
                        if (boundary == null && ent is Polyline p)
                            boundary = p;
                        else
                            ent?.Dispose();
                    }

                    if (boundary == null)
                    {
                        ed.WriteMessage("\n偏移结果不是多段线");
                        DisposeOffsetCurves(offsets);
                        return;
                    }

                    var lines = PolyToLines(boundary);
                    boundary.Dispose();

                    var reinPolys = new List<Polyline>();
                    foreach (var line in lines)
                    {
                        try
                        {
                            var dir = (line.EndPoint - line.StartPoint).GetNormal();
                            var start = line.StartPoint - dir * anchorageLength;
                            var end = line.EndPoint + dir * anchorageLength;

                            var plane = new Plane(Point3d.Origin, Vector3d.ZAxis);
                            var rein = new Polyline();
                            rein.AddVertexAt(0, start.Convert2d(plane), 0, 0, 0);
                            rein.AddVertexAt(1, end.Convert2d(plane), 0, 0, 0);

                            var hookStart = CalculateHook(rein.GetPoint3dAt(1), rein.GetPoint3dAt(0), hookLength);
                            rein.AddVertexAt(0, hookStart.Point3dTo2d(), 0, 0, 0);

                            int last = rein.NumberOfVertices - 1;
                            var hookEnd = CalculateHook(rein.GetPoint3dAt(last - 1), rein.GetPoint3dAt(last), hookLength);
                            rein.AddVertexAt(rein.NumberOfVertices, hookEnd.Point3dTo2d(), 0, 0, 0);
                            rein.ApplyReinforcementWidth(reinWidth);

                            reinPolys.Add(rein);
                        }
                        finally
                        {
                            line.Dispose();
                        }
                    }

                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    foreach (var rein in reinPolys)
                    {
                        rein.Layer = LayerLineRein;
                        btr.AppendEntity(rein);
                        tr.AddNewlyCreatedDBObject(rein, true);
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n外部钢筋生成完成，共 {reinPolys.Count} 根");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n生成失败: {ex.Message}");
            }
        }

        private static void DisposeOffsetCurves(DBObjectCollection offsets)
        {
            if (offsets == null) return;
            foreach (Entity ent in offsets)
                ent?.Dispose();
        }

        private static List<Line> PolyToLines(Polyline poly)
        {
            var lines = new List<Line>();
            int count = poly.NumberOfVertices;
            int limit = poly.Closed ? count : count - 1;
            for (int i = 0; i < limit; i++)
            {
                var sp = poly.GetPoint3dAt(i);
                var ep = poly.GetPoint3dAt((i + 1) % count);
                if (sp.DistanceTo(ep) > Tolerance.Global.EqualPoint)
                    lines.Add(new Line(sp, ep));
            }
            return lines;
        }

        private static Point3d CalculateHook(Point3d startPt, Point3d endPt, double hookLength)
        {
            var dir = (endPt - startPt).GetNormal();
            double angle = Math.PI * 3.0 / 4.0;
            var hookDir = dir.RotateBy(angle, Vector3d.ZAxis);
            return endPt + hookDir * hookLength;
        }
    }
}
