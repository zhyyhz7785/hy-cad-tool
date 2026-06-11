using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Features.Road.Intersections.ViewModels;
using HyCADTool.Features.Road.Intersections.Views;
using HyCADTool.Shell.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using HyCADTool.Features.Road.PlanAlignment.Services;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCADTool.Features.Road.PlanAlignment.Commands;
using HyCADTool.Features.Road.CrossSection.Commands;
namespace HyCADTool.Features.Road
{
    /// <summary>
    /// 人行横道参数模态窗：先编辑再落图；确定时写回 <see cref="SettingsPanelViewModel"/> 道路横道四项。
    /// </summary>
    public static class CrosswalkDrawParamsDialog
    {
        public static bool TryShow(out double gap, out double width, out double stop, out double spacing)
        {
            gap = width = stop = spacing = 0;

            var settings = SettingsPanelViewModel.Current;
            var vm = new CrosswalkDrawParamsViewModel(
                settings?.RoadGapWidth ?? Crosswalk.DefaultGapWidth,
                settings?.RoadCrosswalkWidth ?? Crosswalk.DefaultWidth,
                settings?.RoadStopLineDistance ?? Crosswalk.DefaultStopLineDistance,
                settings?.RoadStripeSpacing ?? Crosswalk.DefaultStripeSpacing);

            var window = new CrosswalkDrawParamsWindow(vm);
            bool? result = null;
            vm.CloseRequested += (_, dialogResult) => { result = dialogResult; };

            AcApp.ShowModalWindow(window);

            if (result != true)
                return false;

            gap = vm.GapWidth;
            width = vm.CrosswalkWidth;
            stop = vm.StopLineDistance;
            spacing = vm.StripeSpacing;

            if (settings != null)
            {
                settings.RoadGapWidth = gap;
                settings.RoadCrosswalkWidth = width;
                settings.RoadStopLineDistance = stop;
                settings.RoadStripeSpacing = spacing;
            }

            return true;
        }
    }
}
