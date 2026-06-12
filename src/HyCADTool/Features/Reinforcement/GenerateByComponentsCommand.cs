using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.App.Bootstrap;
using HyCADTool.Features.Reinforcement.Domain;
using HyCADTool.Features.Reinforcement.Domain.Components;
using HyCADTool.Features.Reinforcement.Services;
using HyCADTool.Shell.ViewModels;
using HyCAD.Geometry.Interfaces;
using System;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 按已识别构件分区配筋（hyCompGen）。
    /// </summary>
    public sealed class GenerateByComponentsCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            if (ComponentSession.CurrentSessionId == Guid.Empty || ComponentSession.Regions.Count == 0)
            {
                ed.WriteMessage("\n请先执行构件识别。");
                return;
            }

            var vm = SettingsPanelViewModel.Current;
            var reinParams = vm != null ? vm.CreateReinParameters() : ReinParameters.CreateDefault();
            var compParams = vm != null ? vm.CreateComponentParameters() : new ComponentParameters();

            if (!reinParams.IsValid(out string paramError))
            {
                ed.WriteMessage($"\n钢筋参数无效：{paramError}");
                return;
            }

            vm?.EnsureStylesApplied();

            var reinService = ServiceLocator.Resolve<IReinService>();
            var offsetService = ServiceLocator.Resolve<IPolygonOffsetService>();
            var intersectionService = ServiceLocator.Resolve<ILineIntersectionService>();

            int success = 0;
            int rebarTotal = 0;

            foreach (var region in ComponentSession.ReinRegions)
            {
                foreach (var boundary in region.AllRings)
                {
                    try
                    {
                        var result = ReinforcementUtils.GenerateAllWithComponents(
                            boundary,
                            reinParams,
                            compParams,
                            ComponentSession.Regions,
                            region,
                            offsetService,
                            intersectionService);

                        int count = result.FinalReinforcements?.Length ?? 0;
                        if (count == 0) continue;

                        reinService.DrawReinforcement(result, reinParams);
                        success++;
                        rebarTotal += count;
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n配筋失败：{ex.Message}");
                    }
                }
            }

            ComponentPreviewService.EraseSession(ComponentSession.CurrentSessionId);
            ComponentSession.Clear();
            ed.WriteMessage($"\n按构件配筋完成：{success} 次写入，共 {rebarTotal} 根钢筋。");
        }
    }
}
