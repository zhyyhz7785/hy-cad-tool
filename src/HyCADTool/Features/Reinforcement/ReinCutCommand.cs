using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shell.ViewModels;
using HyCADTool.Shell.Configuration;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 截断钢筋命令（对应旧命令 gd → ModifyPolyline）
    /// 流程：选择钢筋多段线 → 在点击处线段的 Y 较低顶点分割 → 上半段首端缩短 50mm → 替换原线
    /// </summary>
    public class ReinCutCommand
    {
        /// <summary>搭接断口距离（mm），结构制图惯例</summary>
        private const double SpliceGap = 50.0;

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var vm = SettingsPanelViewModel.Current;
            double scale = ScaleResolver.GetScale();
            double panelWidth = (vm?.PolylineWidth ?? 0.4) * scale;

            try
            {
                var peo = new PromptEntityOptions("\n请选择一个多段线：");
                peo.SetRejectMessage("\n请选择一个多段线对象。");
                peo.AddAllowedClass(typeof(Polyline), true);

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                using (var trans = db.TransactionManager.StartTransaction())
                {
                    var originalPl = trans.GetObject(per.ObjectId, OpenMode.ForWrite) as Polyline;
                    if (originalPl == null || originalPl.NumberOfVertices < 2) return;

                    string sourceLayer = originalPl.Layer;
                    double sourceWidth = ReinforcementWidthExtensions.ResolveEffectiveWidth(originalPl);
                    double targetWidth = sourceWidth > 0 ? sourceWidth : panelWidth;

                    Point3d closestPt = originalPl.GetClosestPointTo(per.PickedPoint, false);
                    int segIndex = GetSegmentIndex(originalPl, closestPt);
                    if (segIndex < 0 || segIndex >= originalPl.NumberOfVertices - 1) return;

                    Point3d pt1 = originalPl.GetPoint3dAt(segIndex);
                    Point3d pt2 = originalPl.GetPoint3dAt(segIndex + 1);
                    int splitIdx = pt1.Y < pt2.Y ? segIndex : segIndex + 1;

                    var lowerPl = ExtractSubPolyline(originalPl, 0, splitIdx);
                    var upperPl = ExtractSubPolyline(originalPl, splitIdx, originalPl.NumberOfVertices - 1);

                    if (upperPl.NumberOfVertices >= 2)
                        ShortenStart(upperPl, SpliceGap);

                    var btr = (BlockTableRecord)trans.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    originalPl.Erase();

                    int written = 0;
                    if (lowerPl.NumberOfVertices >= 2)
                    {
                        lowerPl.Layer = sourceLayer;
                        lowerPl.ApplyReinforcementWidth(targetWidth);
                        btr.AppendEntity(lowerPl);
                        trans.AddNewlyCreatedDBObject(lowerPl, true);
                        written++;
                    }
                    else
                    {
                        lowerPl.Dispose();
                    }

                    if (upperPl.NumberOfVertices >= 2)
                    {
                        upperPl.Layer = sourceLayer;
                        upperPl.ApplyReinforcementWidth(targetWidth);
                        btr.AppendEntity(upperPl);
                        trans.AddNewlyCreatedDBObject(upperPl, true);
                        written++;
                    }
                    else
                    {
                        upperPl.Dispose();
                    }

                    if (written == 0)
                    {
                        ed.WriteMessage("\n截断后无有效线段，操作已取消。");
                        trans.Abort();
                        return;
                    }

                    trans.Commit();
                    ed.WriteMessage($"\n截断完成：生成 {written} 段钢筋。");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }

        private static int GetSegmentIndex(Polyline poly, Point3d pt)
        {
            double param = poly.GetParameterAtPoint(pt);
            int idx = (int)Math.Floor(param);
            if (idx >= poly.NumberOfVertices - 1)
                idx = poly.NumberOfVertices - 2;
            return Math.Max(0, idx);
        }

        private static Polyline ExtractSubPolyline(Polyline source, int startIdx, int endIdx)
        {
            var sub = new Polyline();
            for (int i = startIdx; i <= endIdx; i++)
            {
                Point3d pt = source.GetPoint3dAt(i);
                sub.AddVertexAt(sub.NumberOfVertices, new Point2d(pt.X, pt.Y), 0, 0, 0);
            }
            return sub;
        }

        private static void ShortenStart(Polyline poly, double distance)
        {
            if (poly.NumberOfVertices < 2) return;

            Vector2d dir = (poly.GetPoint2dAt(1) - poly.GetPoint2dAt(0)).GetNormal();
            Point2d newStart = poly.GetPoint2dAt(0) + dir * distance;
            poly.SetPointAt(0, newStart);
        }
    }
}
