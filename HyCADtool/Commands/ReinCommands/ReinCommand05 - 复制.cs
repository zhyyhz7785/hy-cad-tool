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
        // 使用静态字段来存储当前默认的bendingLineLength，每次用户修改后更新
        private static double currentBendingLineLength = Reinforcement.BendingLineLength;
        // 延伸柱子钢筋锚固到基础，并设置弯钩
        [CommandMethod("ge1")]
        public static void QuickExtend()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            double d = Reinforcement.ProtectionThickness;
            double bendingLineLength = currentBendingLineLength; // 从静态变量获取当前默认值
                                                                 // 步骤1：选择边界多段线
            var plinfo1 = HyTool.GetPolylineInfo("\n请选择边界Polyline对象（右键点击或按 ESC 取消）：");
            if (!plinfo1.HasValue)
            {
                ed.WriteMessage("\n用户取消了边界Polyline的选择。操作已中止。");
                return;
            }
            Polyline boundaryPline = plinfo1.Value.Polyline;
            LineSegment3d boundaryLineSegment3d = plinfo1.Value.SelectedSegment;
            // 使用关键字控制是否修改bendingLineLength
            PromptKeywordOptions pko = new PromptKeywordOptions($"\n是否修改弯曲长度？")
            {
                Message = "\n输入关键字 (或直接回车使用默认值)：",
                AllowNone = true // 允许直接回车使用None状态
            };
            pko.Keywords.Add("Modify", "Mo", "Modify(修改弯曲长度)");
            pko.Keywords.Add("Default", "De", "Default(使用当前默认值)");
            // 不强制用户选择关键字, 用户可直接回车继续
            PromptResult pkr = ed.GetKeywords(pko);
            if (pkr.Status == PromptStatus.None)
            {
                // 用户直接回车表示不修改弯曲长度，使用当前默认值
                ed.WriteMessage($"\n使用默认弯曲长度：{bendingLineLength}");
            }
            else if (pkr.Status == PromptStatus.OK)
            {
                if (pkr.StringResult == "Modify")
                {
                    // 用户选择修改弯曲长度
                    PromptDoubleOptions pdo = new PromptDoubleOptions($"\n请输入新的弯曲长度(当前默认:{bendingLineLength})：");
                    pdo.AllowNone = false;
                    pdo.DefaultValue = bendingLineLength;
                    var pdr = ed.GetDouble(pdo);
                    if (pdr.Status == PromptStatus.OK)
                    {
                        bendingLineLength = pdr.Value;
                        // 用户修改成功后，将静态字段更新为新值，以便下次作为默认值使用
                        currentBendingLineLength = bendingLineLength;
                        ed.WriteMessage($"\n弯曲长度已修改为：{bendingLineLength}");
                    }
                    else
                    {
                        // 用户取消输入长度，则依旧使用默认值
                        ed.WriteMessage("\n未修改弯曲长度，继续使用默认值。");
                    }
                }
                else if (pkr.StringResult == "Default")
                {
                    // 用户选择Default关键字，不修改弯曲长度
                    ed.WriteMessage($"\n使用当前默认弯曲长度：{bendingLineLength}");
                }
            }
            else
            {
                // 用户按ESC取消
                ed.WriteMessage("\n用户取消操作。");
                return;
            }
            // 步骤2：循环选择并延伸Polyline
            while (true)
            {
                var plinfo2 = HyTool.GetPolylineInfo("\n请选择需要延伸的Polyline对象：");
                if (!plinfo2.HasValue)
                {
                    ed.WriteMessage("\n用户取消了延伸Polyline的选择。操作已中止。");
                    return;
                }
                Polyline ExtendPline = plinfo2.Value.Polyline;
                LineSegment3d ExtendSeg = plinfo2.Value.SelectedSegment;
                Point3d ExtendP = plinfo2.Value.ClosestPoint;
                double para = plinfo2.Value.Parameter;
                int index = (int)Math.Floor(para);
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline pline = tr.GetObject(ExtendPline.ObjectId, OpenMode.ForWrite) as Polyline;
                    bool b = HyTool.IsClosestPointDirectionPositive(ExtendSeg, ExtendP);
                    Line line1 = new Line(boundaryLineSegment3d.StartPoint, boundaryLineSegment3d.EndPoint);
                    Line line2 = new Line(ExtendSeg.StartPoint, ExtendSeg.EndPoint);
                    Point3dCollection points = new Point3dCollection();
                    line1.IntersectWith(line2, Intersect.ExtendBoth, points, IntPtr.Zero, IntPtr.Zero);
                    if (points.Count == 0)
                    {
                        ed.WriteMessage("\n所选线段与边界线平行，无交点。操作终止。");
                        return;
                    }
                    var interP = points[0];
                    //if (b)
                    //{
                    //    // 正向延伸逻辑
                    //    int startRemove = index + 2;
                    //    if (startRemove < pline.NumberOfVertices)
                    //    {
                    //        for (int i = pline.NumberOfVertices - 1; i >= startRemove; i--)
                    //        {
                    //            pline.RemoveVertexAt(i);
                    //        }
                    //    }
                    //    Vector3d vec = (ExtendSeg.EndPoint - ExtendSeg.StartPoint).GetNormal();
                    //    Point3d p = interP - vec * d;
                    //    pline.SetPointAt(index + 1, p.ConvertPoint3dTo2d());
                    //    pline.AddAnchor(tr, boundaryLineSegment3d, bendingLineLength);
                    //}
                    //else
                    //{
                    //    // 反向延伸逻辑
                    //    int endRemove = index - 1;
                    //    if (endRemove >= 0)
                    //    {
                    //        for (int i = endRemove; i >= 0; i--)
                    //        {
                    //            pline.RemoveVertexAt(i);
                    //        }
                    //    }
                    //    Vector3d vec = -(ExtendSeg.EndPoint - ExtendSeg.StartPoint).GetNormal();
                    //    Point3d p = interP - vec * d;
                    //    pline.SetPointAt(0, p.ConvertPoint3dTo2d());
                    //    pline.ReverseCurve();
                    //    pline.AddAnchor(tr, boundaryLineSegment3d, bendingLineLength);
                    //}
                    if (b)
                    {
                        // 删除延伸点之后的所有点
                        for (int i = index + 2; i < pline.NumberOfVertices; i++)
                        {
                            pline.RemoveVertexAt(i);
                        }
                        Vector3d vec = (ExtendSeg.EndPoint - ExtendSeg.StartPoint).GetNormal();
                        var p = interP - vec * d;
                        pline.SetPointAt(index + 1, p.ConvertPoint3dTo2d());
                        pline.AddAnchor(tr, boundaryLineSegment3d, bendingLineLength);
                    }
                    else
                    {
                        // 删除延伸点之后的所有点
                        for (int i = index - 1; i < 0; i--)
                        {
                            pline.RemoveVertexAt(i);
                        }
                        Vector3d vec = -(ExtendSeg.EndPoint - ExtendSeg.StartPoint).GetNormal();
                        var p = interP - vec * d;
                        pline.SetPointAt(index - 1, p.ConvertPoint3dTo2d());
                        pline.ReverseCurve();
                        pline.AddAnchor(tr, boundaryLineSegment3d, bendingLineLength);
                    }
                    ed.WriteMessage("\n延伸操作完成。");
                    tr.Commit();
                }
            }
        }
    }
}
