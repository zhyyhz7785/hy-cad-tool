using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.Pile.Domain.Entities;
using HyCADTool.Features.Pile.Domain.Services;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Features.Pile.Services;
using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Pile.ViewModels;
using HyCADTool.Presentation.ViewModels;
using System;
using System.Linq;

namespace HyCADTool.Features.Pile
{
    /// <summary>
    /// 绘制桩命令（编排 Domain + Infrastructure，不依赖旧项目）
    /// 流程：选择矩形多段线 → PileLayoutService 计算 → PileDrawingService 绘制
    /// </summary>
    public class DrawPilesCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var vm = PilePanelViewModel.Current;
            if (vm == null)
            {
                ed.WriteMessage("\n桩基面板未初始化");
                return;
            }

            // Scale 统一从设置面板读取
            var scale = SettingsPanelViewModel.Current?.Scale ?? 40.0;

            try
            {
                // 1. 选择闭合矩形多段线
                var selResult = ed.GetEntity("\n选择闭合矩形多段线作为桩基边界: ");
                if (selResult.Status != PromptStatus.OK) return;

                double x0, y0, width, height;
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    var polyline = tr.GetObject(selResult.ObjectId, OpenMode.ForRead) as Polyline;
                    if (polyline == null || !polyline.Closed)
                    {
                        ed.WriteMessage("\n选择的不是闭合多段线。");
                        tr.Commit();
                        return;
                    }

                    // 提取矩形边界（取 AABB 包围盒）
                    var ext = polyline.GeometricExtents;
                    x0 = ext.MinPoint.X;
                    y0 = ext.MinPoint.Y;
                    width = ext.MaxPoint.X - ext.MinPoint.X;
                    height = ext.MaxPoint.Y - ext.MinPoint.Y;
                    tr.Commit();
                }

                if (width <= 0 || height <= 0)
                {
                    ed.WriteMessage("\n多段线边界无效。");
                    return;
                }

                // 2. Domain 计算（从 DI 容器获取服务）
                var layoutService = ServiceLocator.Resolve<PileLayoutService>();
                var sectionType = vm.IsCirclePile ? PileSectionType.Circle : PileSectionType.Square;
                var arrangementType = vm.IsRectangular ? PileArrangementType.Rectangle : PileArrangementType.Circular;

                int manualNX = vm.ManualControl ? vm.NX : 0;
                int manualNY = vm.ManualControl ? vm.NY : 0;

                var result = layoutService.Calculate(
                    x0, y0, width, height,
                    sectionType, vm.DiameterOrEdge,
                    vm.InputDisplacementRate, vm.PileArrangeRate,
                    (vm.MarginUp, vm.MarginDown, vm.MarginLeft, vm.MarginRight),
                    arrangementType, scale,
                    manualNX, manualNY);

                if (result.Error != null)
                {
                    ed.WriteMessage($"\n计算失败: {result.Error}");
                    return;
                }

                // 3. Infrastructure 绘制（从 DI 容器获取服务）
                var drawingService = ServiceLocator.Resolve<PileDrawingService>();
                drawingService.Draw(result, vm.PileElevation);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n绘制桩失败: {ex.Message}");
            }
        }
    }
}
