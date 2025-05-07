using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
namespace HyCADTool
{
    public static partial class Reinforcement
    {
        // 定义命令，命名为"HyExtendQuick"
        //[CommandMethod("ge1")]
        public static void QuickExtend()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            double d = Reinforcement.ProtectionThickness;
            try
            {
                // 步骤1：选择边界实体（仅限Polyline）
                PromptEntityOptions boundaryOptions = new PromptEntityOptions("\n请选择边界Polyline对象：");
                boundaryOptions.SetRejectMessage("\n请选择一个Polyline实体作为边界。");
                boundaryOptions.AddAllowedClass(typeof(Polyline), false);
                PromptEntityResult boundaryRes = ed.GetEntity(boundaryOptions);
                if (boundaryRes.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择任何边界实体。");
                    return;
                }
                // 获取边界实体
                Polyline boundaryPline;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    boundaryPline = tr.GetObject(boundaryRes.ObjectId, OpenMode.ForRead) as Polyline;
                    if (boundaryPline == null)
                    {
                        ed.WriteMessage("\n选择的边界实体不是Polyline。");
                        tr.Commit();
                        return;
                    }
                    tr.Commit();
                }
                ed.WriteMessage("\n边界Polyline已选择。现在可以选择要延伸的Polyline。每选择一个Polyline，完成一次延伸。按ESC键退出。");
                // 步骤2：循环选择并延伸Polyline
                while (true)
                {
                    // 提示选择要延伸的Polyline
                    PromptEntityOptions plineOptions = new PromptEntityOptions("\n请选择要延伸的Polyline（ESC退出）：");
                    plineOptions.SetRejectMessage("\n请选择一个Polyline实体。");
                    plineOptions.AddAllowedClass(typeof(Polyline), false);
                    plineOptions.AllowNone = true; // 允许用户按ESC退出
                    PromptEntityResult plineRes = ed.GetEntity(plineOptions);
                    ObjectId plId = plineRes.ObjectId;
                    Point3d selPt = plineRes.PickedPoint;
                    if (plineRes.Status == PromptStatus.Cancel || plineRes.Status == PromptStatus.None)
                    {
                        ed.WriteMessage("\n命令已取消或完成。");
                        break;
                    }
                    if (plineRes.Status == PromptStatus.OK)
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            Polyline pline = tr.GetObject(plineRes.ObjectId, OpenMode.ForWrite) as Polyline;
                            // 获取选择点在多段线上的最近点
                            Point3d closestPoint = pline.GetClosestPointTo(selPt, false);
                            // 获取最近点对应的线段索引
                            double param = pline.GetParameterAtPoint(closestPoint);
                            var index = (int)Math.Floor(param);
                            if (pline == null)
                            {
                                ed.WriteMessage("\n选择的对象不是Polyline，跳过。");
                                tr.Commit();
                                continue;
                            }
                            // 执行延伸操作
                            ExtendPolylineSegmentToPoly(pline, closestPoint, boundaryPline, d, index);
                            tr.Commit();
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误：{ex.Message}");
            }
        }
    }
}
