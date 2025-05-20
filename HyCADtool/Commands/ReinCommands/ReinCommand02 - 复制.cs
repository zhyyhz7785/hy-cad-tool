using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
[assembly: CommandClass(typeof(HyCADTool.Commands.HyCommand))]
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("gd")]
        ///截断钢筋
        public static void ModifyPolyline()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            double d = Reinforcement.HookLength;
            try
            {
                var pLResult = HyTool.GetPolylineInfo("\n请选择需要打断的钢筋");
                if (pLResult == null)
                {
                    ed.WriteMessage("\n未选择有效的多段线，操作取消。");
                    return;
                }
                var pl = pLResult.Value.Polyline;
                var segment = pLResult.Value.SelectedSegment;
                var closePoint = pLResult.Value.ClosestPoint;
                var para = pLResult.Value.Parameter;
                var index = (int)Math.Floor(para);
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline lowerPl = new Polyline();
                    Polyline upperPl = new Polyline();
                    Polyline newPl = new Polyline();
                    Polyline originalPl = tr.GetObject(pl.ObjectId, OpenMode.ForWrite) as Polyline;
                    Point3d ptEnd = segment.EndPoint;
                    Point3d Pstart = segment.StartPoint;
                    // 添加下部多段线的顶点（从起点到分割点）
                    // 添加下部多段线的顶点（从起点到分割点）
                    if (!originalPl.Closed)
                    {
                        int k = 0;
                        for (int i = 0; i <= index; i++)
                        {
                            Point3d pt = originalPl.GetPoint3dAt(i);
                            double startWidth = originalPl.GetStartWidthAt(i);
                            double endWidth = originalPl.GetEndWidthAt(i);
                            upperPl.AddVertexAt(k, new Point2d(pt.X, pt.Y), originalPl.GetBulgeAt(i), startWidth, endWidth);
                            k++;
                        }
                        // 添加截断点，同时设置线宽
                        if (segment.StartPoint.Y > segment.EndPoint.Y)
                        {
                            upperPl.AddVertexAt(index + 1, new Point2d(ptEnd.X, ptEnd.Y + d), 0, originalPl.GetStartWidthAt(index), originalPl.GetEndWidthAt(index));
                        }
                        // 添加上部多段线的顶点（从分割点到终点）
                        int j = 0;
                        for (int i = index + 1; i < originalPl.NumberOfVertices; i++)
                        {
                            Point3d pt = originalPl.GetPoint3dAt(i);
                            double startWidth = originalPl.GetStartWidthAt(i);
                            double endWidth = originalPl.GetEndWidthAt(i);
                            lowerPl.AddVertexAt(j, new Point2d(pt.X, pt.Y), originalPl.GetBulgeAt(i), startWidth, endWidth);
                            j++;
                        }
                        if (segment.StartPoint.Y < segment.EndPoint.Y)
                        {
                            lowerPl.AddVertexAt(0, new Point2d(Pstart.X, Pstart.Y + d), 0, originalPl.GetStartWidthAt(index), originalPl.GetEndWidthAt(index));
                        }
                    }
                    else
                    {
                        if (segment.StartPoint.Y < segment.EndPoint.Y)
                        {
                            newPl = originalPl.BreakClosedPoly(index);
                            newPl.SetPointAt(0, new Point2d(Pstart.X, Pstart.Y + d));
                        }
                        if (segment.StartPoint.Y > segment.EndPoint.Y)
                        {
                            newPl = originalPl.BreakClosedPoly(index + 1);
                            newPl.SetPointAt(newPl.NumberOfVertices - 1, new Point2d(ptEnd.X, ptEnd.Y + d));
                        }
                    }
                    // 6. 将新的实体添加到图形中
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                    // 删除原始多段线
                    // 添加新的多段线实体
                    if (newPl.NumberOfVertices > 0)
                    {
                        btr.AppendEntity(newPl);
                        tr.AddNewlyCreatedDBObject(newPl, true);
                        originalPl.Erase();
                    }
                    if (lowerPl.NumberOfVertices > 0)
                    {
                        btr.AppendEntity(lowerPl);
                        tr.AddNewlyCreatedDBObject(lowerPl, true);
                        originalPl.Erase();
                    }
                    if (upperPl.NumberOfVertices > 0)
                    {
                        btr.AppendEntity(upperPl);
                        tr.AddNewlyCreatedDBObject(upperPl, true);
                        originalPl.Erase();
                    }
                    // 提交事务
                    tr.Commit();
                    ed.WriteMessage("\n操作完成。");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n出错：" + ex.Message);
            }
        }
    }
}
