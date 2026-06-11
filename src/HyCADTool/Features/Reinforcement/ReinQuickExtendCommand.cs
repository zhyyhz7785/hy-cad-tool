using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shell.ViewModels;
using HyCADTool.Shell.Configuration;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shared.AutoCAD.Interactive;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shell.Configuration.User;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 延伸至相交钢筋并加 15d 方向弯钩（对应旧命令 ge1）。
    /// 流程：选钢筋 → 按点击端删弯钩 → 沿主筋方向对「01-hy-1配筋-钢筋线」求交（含自身其它段）
    /// → 相交线朝自身侧偏移保护层再求交得末端 → HookJig 沿相交线方向画 15d 弯钩。
    /// </summary>
    public class ReinQuickExtendCommand
    {
        private static string LayerLineRein =>
            UserLayerNameResolver.Get(LayerSemanticIds.ReinLine, LayerBuiltinDefaults.ReinLine);

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var vm = SettingsPanelViewModel.Current;
            double scale = ScaleResolver.GetScale();
            double protectionThickness = (vm?.ProtectionThickness ?? 1.0) * scale;
            double rebarDiameter = vm?.RebarDiameter ?? 14.0;
            double hookLength15d = 15.0 * rebarDiameter;
            double reinWidth = (vm?.PolylineWidth ?? 0.4) * scale;
            double hookHint = ScaleResolver.GetAnchorageLength();

            vm?.EnsureStylesApplied();

            try
            {
                var peo = new PromptEntityOptions("\n请选择钢筋多段线（点击靠近要延伸的一端）：");
                peo.SetRejectMessage("\n请选择一个多段线对象。");
                peo.AddAllowedClass(typeof(Polyline), true);
                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                using (var trans = db.TransactionManager.StartTransaction())
                {
                    var poly = trans.GetObject(per.ObjectId, OpenMode.ForWrite) as Polyline;
                    if (poly == null || poly.NumberOfVertices < 2)
                    {
                        trans.Abort();
                        return;
                    }

                    bool extendFromStart = ReinExtendCommand.IsPickedNearStart(poly, per.PickedPoint);

                    if (!ReinExtendCommand.TryGetHookSpan(poly, hookHint, extendFromStart, out int mainTipIndex, out int mainPrevIndex))
                    {
                        ed.WriteMessage("\n无法识别钢筋末端。");
                        trans.Abort();
                        return;
                    }

                    if (extendFromStart)
                        ReinExtendCommand.RemoveStartHookVertices(poly, mainTipIndex);
                    else
                        ReinExtendCommand.RemoveEndHookVertices(poly, mainTipIndex);

                    if (poly.NumberOfVertices < 2)
                    {
                        trans.Abort();
                        return;
                    }

                    int extIdx = extendFromStart ? 0 : poly.NumberOfVertices - 1;
                    int adjIdx = extendFromStart ? 1 : extIdx - 1;
                    Point3d endPt = poly.GetPoint3dAt(extIdx);
                    Point3d adjPt = poly.GetPoint3dAt(adjIdx);
                    Vector3d extDir = endPt - adjPt;
                    if (extDir.Length < 1e-6)
                    {
                        ed.WriteMessage("\n端点与相邻点重合，无法延伸。");
                        trans.Abort();
                        return;
                    }
                    extDir = extDir.GetNormal();

                    int skipSeg = ReinExtendCommand.GetSkipAdjacentSegmentIndex(poly, extIdx);
                    if (!ReinExtendCommand.FindNearestForwardRayHit(
                            db, trans, endPt, extDir, per.ObjectId, poly, skipSeg, LayerLineRein, out var hit))
                    {
                        ed.WriteMessage("\n未找到射线方向上的钢筋线。");
                        trans.Abort();
                        return;
                    }

                    if (!ReinExtendCommand.TryGetOffsetExtensionPoint(
                            endPt, extDir, hit, protectionThickness, out Point3d extensionPt))
                    {
                        ed.WriteMessage("\n偏移保护层后无法确定延伸终点。");
                        trans.Abort();
                        return;
                    }

                    if (endPt.DistanceTo(extensionPt) < 1e-6)
                    {
                        ed.WriteMessage("\n延伸距离过短，无法延伸。");
                        trans.Abort();
                        return;
                    }

                    poly.SetPointAt(extIdx, new Point2d(extensionPt.X, extensionPt.Y));

                    var hookJig = new DirectionalHookJig(poly, extensionPt, hit.SegmentDir, hookLength15d);
                    var pr = ed.Drag(hookJig);

                    if (pr.Status == PromptStatus.OK || pr.Status == PromptStatus.None)
                    {
                        double targetWidth = hookJig.SourceWidth > 0 ? hookJig.SourceWidth : reinWidth;
                        poly.ApplyReinforcementWidth(targetWidth);
                        trans.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }
    }
}
