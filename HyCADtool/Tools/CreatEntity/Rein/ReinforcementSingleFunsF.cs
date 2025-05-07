using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Tools;
using System;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;
namespace HyCADTool
{
    public static partial class Reinforcement
    {
        // 假设在此处声明一个全局常量，弯钩长度300
        private const double HookL = 300.0; // L=300
                                            //[CommandMethod("ge1")]
        public static void GExtend()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            double d = Reinforcement.ProtectionThickness; // 保护层厚度
            try
            {
                // 步骤1：选择边界Polyline
                PromptEntityOptions boundaryOptions = new PromptEntityOptions("\n请选择边界Polyline对象：");
                boundaryOptions.SetRejectMessage("\n请选择一个Polyline实体作为边界。");
                boundaryOptions.AddAllowedClass(typeof(Polyline), false);
                PromptEntityResult boundaryRes = ed.GetEntity(boundaryOptions);
                if (boundaryRes.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择任何边界实体。");
                    return;
                }
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
                ed.WriteMessage("\n边界Polyline已选择。现在选择需要延伸的Polyline。按ESC退出。");
                // 步骤2：循环选择并延伸Polyline
                while (true)
                {
                    PromptEntityOptions plineOptions = new PromptEntityOptions("\n请选择要延伸的Polyline（ESC退出）：");
                    plineOptions.SetRejectMessage("\n请选择一个Polyline实体。");
                    plineOptions.AddAllowedClass(typeof(Polyline), false);
                    plineOptions.AllowNone = true;
                    PromptEntityResult plineRes = ed.GetEntity(plineOptions);
                    if (plineRes.Status == PromptStatus.Cancel || plineRes.Status == PromptStatus.None)
                    {
                        ed.WriteMessage("\n命令已结束。");
                        break;
                    }
                    if (plineRes.Status == PromptStatus.OK)
                    {
                        using (Transaction tr = db.TransactionManager.StartTransaction())
                        {
                            Polyline pline = tr.GetObject(plineRes.ObjectId, OpenMode.ForWrite) as Polyline;
                            if (pline == null)
                            {
                                ed.WriteMessage("\n选择的对象不是Polyline，跳过。");
                                tr.Commit();
                                continue;
                            }
                            // 获取选择点在Polyline上的最近点
                            Point3d selPt = plineRes.PickedPoint;
                            Point3d closestPoint = pline.GetClosestPointTo(selPt, false);
                            double param = pline.GetParameterAtPoint(closestPoint);
                            int index = (int)Math.Floor(param);
                            // 执行延伸操作（请根据您的实际逻辑实现此方法）
                            ExtendPolylineSegmentToPoly(pline, closestPoint, boundaryPline, d, index);
                            // 延伸完成后，删除延伸方向最后一个点
                            if (pline.NumberOfVertices > 1)
                            {
                                // 假设延伸是从Polyline的末端延伸（根据实际情况调整逻辑）
                                // 如果需要判断是首点还是末点延伸，可根据index或延伸方向决定删除哪一个点。
                                pline.RemoveVertexAt(pline.NumberOfVertices - 1);
                            }
                            // 添加垂直弯钩（Hook）
                            // 这里传入true表示为垂直钩，根据您的HookJig实现确定
                            pline.AddAnchor(tr, true, Reinforcement.HookL);
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
        // 请根据您的实际情况实现此方法
        // ExtendPolylineSegmentToPoly负责将Polyline延伸到boundaryPline
        // 您需要在此处实现具体的延伸逻辑
    }
}
