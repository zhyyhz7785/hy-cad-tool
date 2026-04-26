using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Pad
{
    /// <summary>
    /// 根据多段线创建垫层（hyDcP_Poly）
    /// 对每个近水平线段（±30°）生成带延伸的垫层矩形
    /// </summary>
    public class CreatePadFromPolylineCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            double d = 100.0;

            try
            {
                var selOpts = new PromptSelectionOptions
                {
                    MessageForAdding = "\n请选择多段线以创建垫层: "
                };
                var filter = new SelectionFilter(new[] {
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
                });
                var selRes = ed.GetSelection(selOpts, filter);
                if (selRes.Status != PromptStatus.OK) return;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 图层已在 PluginInitializer 统一创建
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (ObjectId objId in selRes.Value.GetObjectIds())
                    {
                        var pline = tr.GetObject(objId, OpenMode.ForRead) as Polyline;
                        if (pline == null) continue;

                        // 获取顶点
                        var verts = new List<Point2d>();
                        for (int i = 0; i < pline.NumberOfVertices; i++)
                            verts.Add(pline.GetPoint2dAt(i));

                        // 找 X 最小/最大点
                        var minX = verts.OrderBy(p => p.X).ThenBy(p => p.Y).First();
                        var maxX = verts.OrderByDescending(p => p.X).ThenByDescending(p => p.Y).First();
                        int minIdx = verts.FindIndex(v => v.X == minX.X && v.Y == minX.Y);
                        int maxIdx = verts.FindIndex(v => v.X == maxX.X && v.Y == maxX.Y);

                        // 提取子段
                        var seg = new Polyline();
                        int vi = 0;
                        int startIdx = Math.Min(minIdx, maxIdx);
                        int endIdx = Math.Max(minIdx, maxIdx);

                        if (pline.Closed && minIdx > maxIdx)
                        {
                            for (int i = minIdx; i < pline.NumberOfVertices; i++)
                                seg.AddVertexAt(vi++, pline.GetPoint2dAt(i), 0, 0, 0);
                            for (int i = 0; i <= maxIdx; i++)
                                seg.AddVertexAt(vi++, pline.GetPoint2dAt(i), 0, 0, 0);
                        }
                        else
                        {
                            for (int i = startIdx; i <= endIdx; i++)
                                seg.AddVertexAt(vi++, pline.GetPoint2dAt(i), 0, 0, 0);
                        }

                        // 对每个线段生成垫层
                        for (int i = 0; i < seg.NumberOfVertices - 1; i++)
                        {
                            var sp = seg.GetPoint3dAt(i);
                            var ep = seg.GetPoint3dAt(i + 1);
                            var lineVec = ep - sp;
                            double angle = lineVec.GetAngleTo(Vector3d.XAxis) * 180.0 / Math.PI;
                            if (lineVec.Y < 0) angle = -angle;

                            if (angle < -30 || angle > 30) continue;

                            var padVec = lineVec.RotateBy(-Math.PI / 2, Vector3d.ZAxis).GetNormal() * d;
                            var extVec = lineVec.GetNormal() * d;

                            // 判断延伸方向
                            Point3d prevPt = i > 0 ? seg.GetPoint3dAt(i - 1) : seg.GetPoint3dAt(seg.NumberOfVertices - 1);
                            Point3d nextPt = i < seg.NumberOfVertices - 2 ? seg.GetPoint3dAt(i + 2) : seg.GetPoint3dAt(0);

                            double crossStart = lineVec.CrossProduct(prevPt - sp).Z;
                            double crossEnd = lineVec.CrossProduct(nextPt - ep).Z;

                            Point3d p1 = crossStart >= 0 ? sp - extVec : sp;
                            Point3d p2 = crossEnd >= 0 ? ep + extVec : ep;
                            Point3d p3 = p2 + padVec;
                            Point3d p4 = p1 + padVec;

                            var pad = new Polyline();
                            pad.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
                            pad.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
                            pad.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
                            pad.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
                            pad.Closed = true;
                            pad.Layer = UserLayerNameResolver.Get(LayerSemanticIds.Cushion, LayerBuiltinDefaults.Cushion);

                            ms.AppendEntity(pad);
                            tr.AddNewlyCreatedDBObject(pad, true);
                        }
                    }

                    tr.Commit();
                }

                ed.WriteMessage("\n垫层创建完成");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n创建失败: {ex.Message}");
            }
        }
    }
}
