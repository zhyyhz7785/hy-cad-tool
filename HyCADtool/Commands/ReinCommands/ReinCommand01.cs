using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass.Jig;
using HyCADTool.Tools;
using System;
using static HyCADTool.Tools.Et;
[assembly: CommandClass(typeof(HyCADTool.Commands.HyCommand))]
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        public static double HookLength { get; private set; }
        [CommandMethod("g1")]
        public static void ReinAddAnchor1()
        {
            Et.SetCurrentLayer("01_hy_1钢筋_线钢筋");
            HookLength = Reinforcement.HookLength;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                // 1. 选择多段线，并获取用户点击的点
                PromptEntityOptions peo = new PromptEntityOptions("\n请选择一条多段线:");
                peo.SetRejectMessage("\n请选择一条多段线。");
                peo.AddAllowedClass(typeof(Polyline), true);
                peo.AllowNone = false;
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                    return;
                ObjectId polylineId = per.ObjectId;
                Point3d pickedPoint = per.PickedPoint;
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    // 打开多段线
                    Polyline polyline = trans.GetObject(polylineId, OpenMode.ForWrite) as Polyline;
                    polyline = FindRightPolyline(polyline, pickedPoint, trans);
                    polyline.AddAnchor(trans, false, Reinforcement.HookLength);
                    // 提交事务
                    trans.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
        [CommandMethod("g2")]
        public static void ReinAddAnchor2()
        {
            Et.SetCurrentLayer("01_hy_1钢筋_线钢筋");
            HookLength = Reinforcement.HookLength;
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                // 1. 选择多段线，并获取用户点击的点
                PromptEntityOptions peo = new PromptEntityOptions("\n请选择一条多段线:");
                peo.SetRejectMessage("\n请选择一条多段线。");
                peo.AddAllowedClass(typeof(Polyline), true);
                peo.AllowNone = false;
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                    return;
                ObjectId polylineId = per.ObjectId;
                Point3d pickedPoint = per.PickedPoint;
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    // 打开多段线
                    Polyline polyline = trans.GetObject(polylineId, OpenMode.ForWrite) as Polyline;
                    polyline = FindRightPolyline(polyline, pickedPoint, trans);
                    polyline.AddAnchor(trans, true, Reinforcement.HookLength);
                    // 提交事务
                    trans.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
        public static Polyline FindRightPolyline(Polyline polyline, Point3d pickedPoint, Transaction trans)
        {
            if (polyline == null)
                throw new ArgumentNullException(nameof(polyline), "Polyline cannot be null.");
            if (trans == null)
                throw new ArgumentNullException(nameof(trans), "Transaction cannot be null.");
            // 找到距离选中点最近的点
            Point3d closestPoint = polyline.GetClosestPointTo(pickedPoint, false);
            // 获取起点和终点
            Point3d startPt = polyline.StartPoint;
            Point3d endPt = polyline.EndPoint;
            // 计算最近点与起点、终点的距离
            double distToStart = closestPoint.DistanceTo(startPt);
            double distToEnd = closestPoint.DistanceTo(endPt);
            // 如果最近点更靠近终点，反转多段线
            if (distToStart < distToEnd)
            {
                polyline.ReverseCurve();
                Document doc = Application.DocumentManager.MdiActiveDocument;
                doc.Editor.WriteMessage("\n多段线已反转方向。\n");
            }
            return polyline;
        }
        [CommandMethod("gg")]
        public static void MyPolyline()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Et.SetCurrentLayer("01_hy_1钢筋_线钢筋");
            PolylineJig jig = new PolylineJig();
            jig._offsetDistance = -Reinforcement.ProtectionThickness;
            double hookLength = Reinforcement.HookLength;
            if (jig.StartJig() == PromptStatus.OK && jig._points.Count > 1)
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    Autodesk.AutoCAD.DatabaseServices.Polyline polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
                    for (int i = 0; i < jig._points.Count; i++)
                    {
                        polyline.AddVertexAt(i, new Point2d(jig._points[i].X, jig._points[i].Y), 0, 0, 0);
                    }
                    // 生成偏移曲线
                    var offsetCurves = polyline.GetOffsetCurves(jig._offsetDistance);
                    Polyline polylineConverted = null;
                    // 添加偏移后的多段线到图形中
                    foreach (Entity ent in offsetCurves)
                    {
                        if (ent is Polyline poly)
                        {
                            // 如果是 Polyline，直接使用
                            polylineConverted = poly;
                        }
                        polylineConverted.AddAnchor(trans, hookLength);
                        btr.AppendEntity(polylineConverted);
                        trans.AddNewlyCreatedDBObject(polylineConverted, true);
                    }
                    trans.Commit();
                }
            }
        }
    }
}
