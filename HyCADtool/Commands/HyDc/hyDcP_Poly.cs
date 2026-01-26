using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("hyDcP_Poly")]
        public static void CreatePadFromPolylines() // 方法名稍作调整以反映多选
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            double d = 100.0; // 默认厚度 100
            var layerId = Tools.ZTools.CreateLayer("00_hy_垫层", 7); // 假设 EtGpt 已定义
            try
            {
                // 提示用户选择多条多段线
                PromptSelectionOptions selOpts = new PromptSelectionOptions();
                selOpts.MessageForAdding = "\n请选择一条或多条多段线以创建垫层: ";
                selOpts.SingleOnly = false; // 允许多选
                TypedValue[] filter = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
                };
                SelectionFilter sf = new SelectionFilter(filter);
                PromptSelectionResult selRes = ed.GetSelection(selOpts, sf);
                if (selRes.Status != PromptStatus.OK) return;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    // 遍历所有选择的多段线
                    foreach (ObjectId objId in selRes.Value.GetObjectIds())
                    {
                        Polyline pline = tr.GetObject(objId, OpenMode.ForRead) as Polyline;
                        if (pline == null) continue;
                        // 获取所有顶点
                        List<Point2d> vertices = new List<Point2d>();
                        for (int i = 0; i < pline.NumberOfVertices; i++)
                        {
                            vertices.Add(pline.GetPoint2dAt(i));
                        }
                        // 找到 X 坐标最小和最大的点
                        Point2d minXPoint = vertices.OrderBy(p => p.X).ThenBy(p => p.Y).First();
                        Point2d maxXPoint = vertices.OrderBy(p => p.X).ThenBy(p => p.Y).Last();
                        int minXIndex = vertices.FindIndex(v => v.X == minXPoint.X && v.Y == minXPoint.Y);
                        int maxXIndex = vertices.FindIndex(v => v.X == maxXPoint.X && v.Y == maxXPoint.Y);
                        // 创建从 minXPoint 到 maxXPoint 的子多段线
                        Polyline segmentedPline = new Polyline();
                        int startIndex = Math.Min(minXIndex, maxXIndex);
                        int endIndex = Math.Max(minXIndex, maxXIndex);
                        int vertexIndex = 0;
                        if (pline.Closed && minXIndex > maxXIndex)
                        {
                            for (int i = minXIndex; i < pline.NumberOfVertices; i++)
                                segmentedPline.AddVertexAt(vertexIndex++, pline.GetPoint2dAt(i), 0, 0, 0);
                            for (int i = 0; i <= maxXIndex; i++)
                                segmentedPline.AddVertexAt(vertexIndex++, pline.GetPoint2dAt(i), 0, 0, 0);
                        }
                        else
                        {
                            for (int i = startIndex; i <= endIndex; i++)
                                segmentedPline.AddVertexAt(vertexIndex++, pline.GetPoint2dAt(i), 0, 0, 0);
                        }
                        segmentedPline.LayerId = pline.LayerId;
                        // 为 segmentedPline 的每个线段生成垫层
                        for (int i = 0; i < segmentedPline.NumberOfVertices - 1; i++)
                        {
                            Point3d startPoint = segmentedPline.GetPoint3dAt(i);
                            Point3d endPoint = segmentedPline.GetPoint3dAt(i + 1);
                            var line = new Line(startPoint, endPoint);
                            Vector3d lineVector = line.EndPoint - line.StartPoint;
                            double angle = lineVector.GetAngleTo(Vector3d.XAxis) * 180 / Math.PI;
                            if (lineVector.Y < 0) angle = -angle;
                            ed.WriteMessage($"\n处理多段线 {objId}: 线段 {i}: 起点={startPoint}, 终点={endPoint}, 角度={angle}");
                            if (angle >= -30 && angle <= 30)
                            {
                                ed.WriteMessage("\n生成垫层");
                                var pad = CreatePadFromLineSegment(line, segmentedPline, i, d, layerId);
                                btr.AppendEntity(pad);
                                tr.AddNewlyCreatedDBObject(pad, true);
                            }
                            else
                            {
                                ed.WriteMessage("\n角度超出范围，跳过");
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
        public static Polyline CreatePadFromLineSegment(Line line, Polyline pline, int segmentIndex, double d = 100.0, ObjectId? layerId = null)
        {
            Vector3d lineVector = line.EndPoint - line.StartPoint;
            Vector3d padVector = lineVector.RotateBy(-Math.PI / 2, Vector3d.ZAxis).GetNormal() * d; // 内侧方向（右侧）
            Vector3d extensionVector = lineVector.GetNormal() * d; // 延伸方向
            int vertexCount = pline.NumberOfVertices;
            Point3d prevPoint = segmentIndex > 0 ? pline.GetPoint3dAt(segmentIndex - 1) : pline.GetPoint3dAt(vertexCount - 1);
            Point3d nextPoint = segmentIndex < vertexCount - 2 ? pline.GetPoint3dAt(segmentIndex + 2) : pline.GetPoint3dAt(0);
            Vector3d toPrev = prevPoint - line.StartPoint;
            Vector3d toNext = nextPoint - line.EndPoint;
            double crossStart = lineVector.CrossProduct(toPrev).Z;
            double crossEnd = lineVector.CrossProduct(toNext).Z;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            ed.WriteMessage($"\n起点叉积={crossStart}, 终点叉积={crossEnd}");
            // 反转延伸逻辑：内侧（右侧，叉积 >= 0）延伸，外侧（左侧，叉积 < 0）不延伸
            Point3d extendedStart = crossStart >= 0 ? line.StartPoint - extensionVector : line.StartPoint;
            Point3d extendedEnd = crossEnd >= 0 ? line.EndPoint + extensionVector : line.EndPoint;
            Point3d p1 = extendedStart;
            Point3d p2 = extendedEnd;
            Point3d p3 = extendedEnd + padVector;
            Point3d p4 = extendedStart + padVector;
            Polyline pad = new Polyline();
            pad.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
            pad.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
            pad.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
            pad.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
            pad.Closed = true;
            if (layerId.HasValue)
            {
                pad.LayerId = layerId.Value;
            }
            return pad;
        }
    }
}