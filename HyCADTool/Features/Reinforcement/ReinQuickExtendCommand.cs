using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Interfaces;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shared.Bootstrap;
using HyCADTool.Presentation.ViewModels;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 快速延伸钢筋至边界命令（对应旧命令 ge1 → QuickExtend）
    /// 流程：选择边界多段线 → 循环选择钢筋多段线 → 每根延伸至边界减去保护层厚度 → ESC 退出
    /// </summary>
    public class ReinQuickExtendCommand
    {
        private readonly ILayerService _layerService;

        public ReinQuickExtendCommand()
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
            double protectionThickness = (vm?.ProtectionThickness ?? 1.0) * scale; // 绿色参数 × Scale
            double reinWidth = (vm?.PolylineWidth ?? 0.4) * scale;

            // 确保样式已同步
            vm?.EnsureStylesApplied();

            try
            {
                // 1. 选择边界多段线
                var boundaryOpt = new PromptEntityOptions("\n请选择边界Polyline对象：");
                boundaryOpt.SetRejectMessage("\n请选择一个Polyline实体作为边界。");
                boundaryOpt.AddAllowedClass(typeof(Polyline), false);

                var boundaryRes = ed.GetEntity(boundaryOpt);
                if (boundaryRes.Status != PromptStatus.OK) return;

                ObjectId boundaryId = boundaryRes.ObjectId;

                // 2. 循环选择钢筋多段线并延伸
                while (true)
                {
                    var plineOpt = new PromptEntityOptions("\n请选择要延伸的Polyline（ESC退出）：");
                    plineOpt.SetRejectMessage("\n请选择一个Polyline实体。");
                    plineOpt.AddAllowedClass(typeof(Polyline), false);
                    plineOpt.AllowNone = true;

                    var plineRes = ed.GetEntity(plineOpt);
                    if (plineRes.Status == PromptStatus.Cancel || plineRes.Status == PromptStatus.None)
                        break;
                    if (plineRes.Status != PromptStatus.OK)
                        continue;

                    using (var trans = db.TransactionManager.StartTransaction())
                    {
                        var pline = trans.GetObject(plineRes.ObjectId, OpenMode.ForWrite) as Polyline;
                        var boundary = trans.GetObject(boundaryId, OpenMode.ForRead) as Polyline;
                        if (pline == null || boundary == null || pline.NumberOfVertices < 2)
                        {
                            trans.Abort();
                            continue;
                        }

                        // 计算点击处的线段索引
                        Point3d closestPt = pline.GetClosestPointTo(plineRes.PickedPoint, false);
                        double param = pline.GetParameterAtPoint(closestPt);
                        int segIndex = Math.Min((int)Math.Floor(param), pline.NumberOfVertices - 2);

                        // 延伸至边界减去保护层厚度
                        ReinExtendCommand.ExtendSegmentToBoundary(
                            pline, segIndex, closestPt, boundary, protectionThickness);

                        pline.ApplyReinforcementWidth(reinWidth);

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
