using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Interfaces;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shared.AutoCAD.Interactive;
using HyCADTool.Domain.ValueObjects.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shared.AutoCAD.Utilities;
using HyCADTool.App.Bootstrap;
using HyCADTool.Presentation.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 钢筋添加锚固命令（对应旧命令 g1/g2）
    /// g1: 单侧弯钩（isVertical=false）—— 弯钩角度 135°/225°
    /// g2: 竖向弯钩（isVertical=true）—— 弯钩角度 90°/270°
    /// 流程：选择多段线 → 根据点击位置调整方向 → HookJig 交互式添加弯钩
    /// </summary>
    public class ReinAddAnchorCommand
    {
        private readonly ILayerService _layerService;
        private readonly bool _isVertical;

        private static string LayerLineRein => UserLayerNameResolver.Get(LayerSemanticIds.ReinLine, LayerBuiltinDefaults.ReinLine);

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="isVertical">true = g2（竖向弯钩 90°/270°），false = g1（斜向弯钩 135°/225°）</param>
        public ReinAddAnchorCommand(bool isVertical)
        {
            _layerService = ServiceLocator.Resolve<ILayerService>();
            _isVertical = isVertical;
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            _layerService.SetCurrentLayer(LayerLineRein);

            var vm = SettingsPanelViewModel.Current;
            double scale = vm?.Scale ?? 40.0;
            double hookLength = (vm?.HookLength ?? 1.0) * scale; // 绿色参数 × Scale
            double reinWidth = (vm?.PolylineWidth ?? 0.4) * scale;

            #region agent log
            AgentDebugLogger.Log("initial", "H1", "ReinAddAnchorCommand.Execute", "g1/g2 width parameters",
                new
                {
                    isVertical = _isVertical,
                    hasViewModel = vm != null,
                    scale,
                    polylineWidth = vm?.PolylineWidth,
                    hookLength,
                    reinWidth
                });
            #endregion

            // 确保样式已同步
            vm?.EnsureStylesApplied();

            try
            {
                // 选择多段线
                var peo = new PromptEntityOptions("\n请选择一条多段线:");
                peo.SetRejectMessage("\n请选择一条多段线。");
                peo.AddAllowedClass(typeof(Polyline), true);
                peo.AllowNone = false;

                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                using (var trans = db.TransactionManager.StartTransaction())
                {
                    var polyline = trans.GetObject(per.ObjectId, OpenMode.ForWrite) as Polyline;
                    if (polyline == null) return;

                    // 反转多段线使末端靠近用户点击位置（弯钩添加在末端）
                    OrientEndToPickedPoint(polyline, per.PickedPoint);

                    // 交互式添加弯钩（HookJig 实时预览）
                    var jig = new HookJig(polyline, hookLength, _isVertical);
                    var pr = ed.Drag(jig);

                    if (pr.Status == PromptStatus.OK)
                    {
                        polyline.ApplyReinforcementWidth(reinWidth);
                        trans.Commit();
                    }
                    // 用户取消 → 不提交，事务自动回滚（含方向反转）
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 反转多段线使末端靠近用户点击位置（对应旧 FindRightPolyline）
        /// HookJig 在多段线末端添加弯钩，所以需要确保末端在用户期望的位置
        /// </summary>
        private static void OrientEndToPickedPoint(Polyline polyline, Point3d pickedPoint)
        {
            var closestPoint = polyline.GetClosestPointTo(pickedPoint, false);
            double distToStart = closestPoint.DistanceTo(polyline.StartPoint);
            double distToEnd = closestPoint.DistanceTo(polyline.EndPoint);

            // 点击位置更靠近起点 → 反转，使原起点变为末端
            if (distToStart < distToEnd)
            {
                polyline.ReverseCurve();
            }
        }
    }
}
