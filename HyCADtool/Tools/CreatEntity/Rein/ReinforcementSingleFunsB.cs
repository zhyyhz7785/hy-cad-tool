using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Tools;
namespace HyCADTool
{
    public static partial class Reinforcement
    {
        //[CommandMethod("gd")]
        ///截断钢筋
        public static void ModifyPolyline()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                // 1. 选择一个多段线，并记录选择点位置
                PromptEntityOptions peo = new PromptEntityOptions("\n请选择一个多段线：");
                peo.SetRejectMessage("\n请选择一个多段线对象。");
                peo.AddAllowedClass(typeof(Polyline), true);
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                    return;
                ObjectId plId = per.ObjectId;
                Point3d selPt = per.PickedPoint;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline originalPl = tr.GetObject(plId, OpenMode.ForWrite) as Polyline;
                    // 2. 找到包含选择点的段索引
                    Point3d closestPt = originalPl.GetClosestPointTo(selPt, false);
                    int idx = Et.FindSegmentIndex(originalPl, closestPt);
                    if (idx == -1)
                    {
                        ed.WriteMessage("\n未能找到包含最近点的段。");
                        return;
                    }
                    // 3. 确定分割点，即该段两个端点中 Y 坐标较小的点
                    Point3d pt1 = originalPl.GetPoint3dAt(idx);
                    Point3d pt2 = originalPl.GetPoint3dAt(idx + 1);
                    Point3d splitPt;
                    int splitIdx;
                    int otherIdx;
                    if (pt1.Y < pt2.Y)
                    {
                        splitPt = pt1;
                        splitIdx = idx;
                        otherIdx = idx + 1;
                    }
                    else
                    {
                        splitPt = pt2;
                        splitIdx = idx + 1;
                        otherIdx = idx;
                    }
                    // 4. 分割多段线，创建上、下两段
                    Polyline lowerPl = new Polyline();
                    Polyline upperPl = new Polyline();
                    // 添加下部多段线的顶点（从起点到分割点）
                    for (int i = 0; i <= splitIdx; i++)
                    {
                        Point3d pt = originalPl.GetPoint3dAt(i);
                        lowerPl.AddVertexAt(lowerPl.NumberOfVertices, new Point2d(pt.X, pt.Y), 0, 0, 0);
                    }
                    // 添加上部多段线的顶点（从分割点到终点）
                    for (int i = splitIdx; i < originalPl.NumberOfVertices; i++)
                    {
                        Point3d pt = originalPl.GetPoint3dAt(i);
                        upperPl.AddVertexAt(upperPl.NumberOfVertices, new Point2d(pt.X, pt.Y), 0, 0, 0);
                    }
                    // 5. 缩短上部多段线的首段 50mm
                    double d = 50;
                    if (upperPl.NumberOfVertices >= 2)
                    {
                        Vector2d dir = upperPl.GetPoint2dAt(1) - upperPl.GetPoint2dAt(0);
                        dir = dir.GetNormal(); // 方向向量
                        Point2d newStartPt = upperPl.GetPoint2dAt(0) + dir * d;
                        upperPl.SetPointAt(0, newStartPt);
                    }
                    else
                    {
                        ed.WriteMessage("\n上部多段线顶点不足，无法缩短首段。");
                    }
                    // 6. 将新的实体添加到图形中
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                    // 删除原始多段线
                    originalPl.Erase();
                    // 添加下部多段线
                    btr.AppendEntity(lowerPl);
                    tr.AddNewlyCreatedDBObject(lowerPl, true);
                    // 添加上部多段线
                    btr.AppendEntity(upperPl);
                    tr.AddNewlyCreatedDBObject(upperPl, true);
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
