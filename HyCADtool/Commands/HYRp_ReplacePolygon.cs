//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//using HyCADTool.Tools;
//using System;

//namespace HyCADTool.Command
//{
//    public static partial class HyCommand
//    {
//        [CommandMethod("HY_ReplacePolygonByIntersection")]
//        public static void ReplacePolygonByIntersection()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Editor ed = doc.Editor;
//            Database db = doc.Database;

//            PromptEntityOptions peo = new PromptEntityOptions("\n请选择第一个闭合多段线：");
//            peo.SetRejectMessage("只能选择闭合的多段线！");
//            peo.AddAllowedClass(typeof(Polyline), true);
//            var res1 = ed.GetEntity(peo);
//            if (res1.Status != PromptStatus.OK) return;

//            peo.Message = "\n请选择第二个闭合多段线：";
//            var res2 = ed.GetEntity(peo);
//            if (res2.Status != PromptStatus.OK) return;

//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                var pline1 = tr.GetObject(res1.ObjectId, OpenMode.ForWrite) as Polyline;
//                var pline2 = tr.GetObject(res2.ObjectId, OpenMode.ForRead) as Polyline;

//                if (!pline1.Closed || !pline2.Closed)
//                {
//                    ed.WriteMessage("\n两个多段线必须是闭合的！");
//                    return;
//                }

//                string targetLayer = pline1.Layer;

//                // ========== 步骤 1：检查是否满足边平移条件 ==========
//                //bool moved = GeometryUtils.TryAlignOneEdgeToAnother(pline1, pline2, 500);

//                // ========== 步骤 2：执行交集替换逻辑（保留原有） ==========
//                Region region1 = GeometryUtils.CreateRegionFromPolyline(pline1);
//                Region region2 = GeometryUtils.CreateRegionFromPolyline(pline2);

//                if (region1 == null || region2 == null)
//                {
//                    ed.WriteMessage("\n创建 Region 失败！");
//                    return;
//                }

//                double area2 = region2.Area;
//                region1.BooleanOperation(BooleanOperationType.BoolIntersect, region2);
//                Region regionIntersect = region1;
//                double areaIntersect = regionIntersect?.Area ?? 0;

//                if (areaIntersect < Tolerance.Global.EqualPoint)
//                {
//                    ed.WriteMessage("\n无有效交集。");
//                    region1.Dispose();
//                    region2.Dispose();
//                    return;
//                }

//                pline1.Erase();
//                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

//                if (areaIntersect >= 0.7 * area2)
//                {
//                    Polyline newPl = pline2.Clone() as Polyline;
//                    newPl.Layer = targetLayer;
//                    btr.AppendEntity(newPl);
//                    tr.AddNewlyCreatedDBObject(newPl, true);
//                }
//                else
//                {
//                    DBObjectCollection boundaries = new DBObjectCollection();
//                    regionIntersect.Explode(boundaries);

//                    foreach (object obj in boundaries)
//                    {
//                        if (obj is Line line)
//                        {
//                            Polyline pl = GeometryUtils.ConvertCurveToPolyline(line);
//                            if (pl != null)
//                            {
//                                pl.Layer = targetLayer;
//                                btr.AppendEntity(pl);
//                                tr.AddNewlyCreatedDBObject(pl, true);
//                            }
//                        }

//                        if (obj is IDisposable d) d.Dispose();
//                    }
//                }

//                region1.Dispose();
//                region2.Dispose();
//                tr.Commit();
//            }
//        }
//    }
//}
