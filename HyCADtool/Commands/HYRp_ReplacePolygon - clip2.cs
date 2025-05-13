using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clipper2Lib;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("abrc")]
        public static void ReplacePolygonByIntersection()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            // 第一个多段线（待替换）
            PromptEntityOptions peo = new PromptEntityOptions("\n请选择第一个闭合多段线（被替换）：");
            peo.SetRejectMessage("只能选择闭合的多段线！");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult res1 = ed.GetEntity(peo);
            if (res1.Status != PromptStatus.OK) return;
            // 第二个多段线（参考）
            peo = new PromptEntityOptions("\n请选择第二个闭合多段线（作为参考）：");
            peo.SetRejectMessage("只能选择闭合的多段线！");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult res2 = ed.GetEntity(peo);
            if (res2.Status != PromptStatus.OK) return;
            // 替换面积比例阈值（可调）
            double areaThreshold = 0.7;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Polyline pline1 = tr.GetObject(res1.ObjectId, OpenMode.ForWrite) as Polyline;
                Polyline pline2 = tr.GetObject(res2.ObjectId, OpenMode.ForRead) as Polyline;
                if (pline1 == null || pline2 == null || !pline1.Closed || !pline2.Closed)
                {
                    ed.WriteMessage("\n两个多段线必须是闭合的！");
                    return;
                }
                ReplaceWithClipperLogic(tr, pline1, pline2, pline1.Layer, db, areaThreshold, ed);
                tr.Commit();
            }
        }
        private static void ReplaceWithClipperLogic(Transaction tr, Polyline pline1, Polyline pline2,
      string targetLayer, Database db, double threshold, Editor ed)
        {
            // Step 1: 转换为 Clipper 路径
            Path64 subj = GeometryUtils.ConvertToPath64(pline1);
            Path64 clip = GeometryUtils.ConvertToPath64(pline2);
            Paths64 result = Clipper.Intersect(new Paths64 { subj }, new Paths64 { clip }, FillRule.NonZero);
            if (result == null || result.Count == 0)
            {
                ed.WriteMessage("\n交集为空，跳过。\n");
                return;
            }
            double area2 = Math.Abs(Clipper.Area(clip)) / 1_000_000.0;
            double totalIntersectArea = result.Sum(p => Math.Abs(Clipper.Area(p))) / 1_000_000.0;
            if (totalIntersectArea < Tolerance.Global.EqualPoint)
            {
                ed.WriteMessage("\n交集面积太小，跳过。\n");
                return;
            }
            // Step 2: 删除原 pline1
            pline1.Erase();
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            Polyline newPline = null;
            // Step 3: 根据交集面积决定替换方式
            if (totalIntersectArea >= threshold * area2)
            {
                newPline = pline2.Clone() as Polyline;
                if (newPline != null)
                {
                    newPline.Layer = targetLayer;
                    btr.AppendEntity(newPline);
                    tr.AddNewlyCreatedDBObject(newPline, true);
                }
            }
            else
            {
                foreach (Path64 path in result)
                {
                    Polyline poly = GeometryUtils.CreatePolylineFromPath64(path);
                    if (poly != null)
                    {
                        poly.Layer = targetLayer;
                        btr.AppendEntity(poly);
                        tr.AddNewlyCreatedDBObject(poly, true);
                        if (newPline == null)
                            newPline = poly;
                    }
                }
            }
            ed.WriteMessage($"\n交集面积：{totalIntersectArea:F2}，原面积：{area2:F2}，替换比例 {(totalIntersectArea / area2):P2}");
            // TODO: 对齐逻辑将在后期实现（此处预留接口）
        }
    }
}
