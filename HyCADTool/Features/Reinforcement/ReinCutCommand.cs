using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Interfaces;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Domain.ValueObjects.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shared.Bootstrap;
using HyCADTool.Presentation.ViewModels;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 截断钢筋命令（对应旧命令 gd → ModifyPolyline）
    /// 流程：选择钢筋多段线 → 在点击处线段的 Y 较低顶点分割 → 上半段首端缩短 50mm → 替换原线
    /// 用途：在钢筋搭接处制造断口，表示接头位置
    /// </summary>
    public class ReinCutCommand
    {
        private readonly ILayerService _layerService;

        private static string LayerLineRein => UserLayerNameResolver.Get(LayerSemanticIds.ReinLine, LayerBuiltinDefaults.ReinLine);

        /// <summary>搭接断口距离（mm），结构制图惯例</summary>
        private const double SpliceGap = 50.0;

        public ReinCutCommand()
        {
            _layerService = ServiceLocator.Resolve<ILayerService>();
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var vm = SettingsPanelViewModel.Current;
            double scale = vm?.Scale ?? 40.0;
            double reinWidth = (vm?.PolylineWidth ?? 0.4) * scale;

            try
            {
                // 1. 选择钢筋多段线
                var peo = new PromptEntityOptions("\n请选择一个多段线：");
                peo.SetRejectMessage("\n请选择一个多段线对象。");
                peo.AddAllowedClass(typeof(Polyline), true);

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                using (var trans = db.TransactionManager.StartTransaction())
                {
                    var originalPl = trans.GetObject(per.ObjectId, OpenMode.ForWrite) as Polyline;
                    if (originalPl == null || originalPl.NumberOfVertices < 2) return;

                    // 2. 找到点击处对应的线段索引
                    Point3d closestPt = originalPl.GetClosestPointTo(per.PickedPoint, false);
                    int segIndex = GetSegmentIndex(originalPl, closestPt);
                    if (segIndex < 0 || segIndex >= originalPl.NumberOfVertices - 1) return;

                    // 3. 确定分割顶点：该段两端点中 Y 较低者
                    Point3d pt1 = originalPl.GetPoint3dAt(segIndex);
                    Point3d pt2 = originalPl.GetPoint3dAt(segIndex + 1);
                    int splitIdx = pt1.Y < pt2.Y ? segIndex : segIndex + 1;

                    // 4. 分割为上下两段
                    var lowerPl = ExtractSubPolyline(originalPl, 0, splitIdx);
                    var upperPl = ExtractSubPolyline(originalPl, splitIdx, originalPl.NumberOfVertices - 1);

                    // 5. 上半段首端缩短（制造搭接断口）
                    ShortenStart(upperPl, SpliceGap);

                    lowerPl.ApplyReinforcementWidth(reinWidth);
                    upperPl.ApplyReinforcementWidth(reinWidth);

                    // 6. 替换原多段线
                    var btr = (BlockTableRecord)trans.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    originalPl.Erase();

                    btr.AppendEntity(lowerPl);
                    trans.AddNewlyCreatedDBObject(lowerPl, true);

                    btr.AppendEntity(upperPl);
                    trans.AddNewlyCreatedDBObject(upperPl, true);

                    trans.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取点所在的线段索引（Floor(parameter)）
        /// </summary>
        private static int GetSegmentIndex(Polyline poly, Point3d pt)
        {
            double param = poly.GetParameterAtPoint(pt);
            int idx = (int)Math.Floor(param);
            // 钳制到有效范围
            if (idx >= poly.NumberOfVertices - 1)
                idx = poly.NumberOfVertices - 2;
            return Math.Max(0, idx);
        }

        /// <summary>
        /// 提取多段线从 startIdx 到 endIdx 的子段
        /// </summary>
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

        /// <summary>
        /// 缩短多段线首端：沿第一段方向将起点向终点移动指定距离
        /// </summary>
        private static void ShortenStart(Polyline poly, double distance)
        {
            if (poly.NumberOfVertices < 2) return;

            Vector2d dir = (poly.GetPoint2dAt(1) - poly.GetPoint2dAt(0)).GetNormal();
            Point2d newStart = poly.GetPoint2dAt(0) + dir * distance;
            poly.SetPointAt(0, newStart);
        }
    }
}
