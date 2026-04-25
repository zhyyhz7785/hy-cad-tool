using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.AnchorBolt
{
    /// <summary>
    /// 螺栓对齐命令（对应旧命令 hyabA_Align）
    /// 流程：选螺栓 → 选多段线 → 垂直投影到多段线交点
    /// </summary>
    public class AnchorBoltAlignCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                // 选择螺栓
                var boltRes = ed.GetSelection(
                    new PromptSelectionOptions { MessageForAdding = "\n请选择螺栓（圆或块）：" },
                    new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "CIRCLE,INSERT") }));
                if (boltRes.Status != PromptStatus.OK) return;

                // 选择多段线
                var polyRes = ed.GetSelection(
                    new PromptSelectionOptions { MessageForAdding = "\n请选择多段线：" },
                    new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE") }));
                if (polyRes.Status != PromptStatus.OK) return;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bolts = new List<(Entity Entity, Point3d Center, Point3d? Target)>();

                    foreach (ObjectId boltId in boltRes.Value.GetObjectIds())
                    {
                        var ent = tr.GetObject(boltId, OpenMode.ForWrite) as Entity;
                        if (ent == null) continue;

                        Point3d center;
                        if (ent is Circle c) center = c.Center;
                        else if (ent is BlockReference br) center = br.Position;
                        else continue;

                        // 垂直射线求交
                        var rayUp = new Line(center, center - Vector3d.YAxis * 1e6);
                        var rayDown = new Line(center, center + Vector3d.YAxis * 1e6);
                        Point3d? upMax = null; double maxY = double.MinValue;
                        Point3d? downMin = null; double minY = double.MaxValue;

                        foreach (ObjectId polyId in polyRes.Value.GetObjectIds())
                        {
                            var poly = tr.GetObject(polyId, OpenMode.ForRead) as Polyline;
                            if (poly == null) continue;

                            var upPts = new Point3dCollection();
                            rayUp.IntersectWith(poly, Intersect.ExtendBoth, upPts, IntPtr.Zero, IntPtr.Zero);
                            foreach (Point3d pt in upPts)
                                if (pt.Y > maxY) { maxY = pt.Y; upMax = pt; }

                            var downPts = new Point3dCollection();
                            rayDown.IntersectWith(poly, Intersect.ExtendBoth, downPts, IntPtr.Zero, IntPtr.Zero);
                            foreach (Point3d pt in downPts)
                                if (pt.Y < minY) { minY = pt.Y; downMin = pt; }
                        }

                        bolts.Add((ent, center, upMax ?? downMin));
                    }

                    // 执行移动
                    foreach (var bolt in bolts)
                    {
                        if (bolt.Target.HasValue)
                            bolt.Entity.TransformBy(Matrix3d.Displacement(bolt.Target.Value - bolt.Center));
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n完成处理 {bolts.Count} 个螺栓。");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生异常: {ex.Message}");
            }
        }
    }
}
