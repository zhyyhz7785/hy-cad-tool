using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.Utilities;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Converters;
using HyCADTool.Refactored.Infrastructure.Configuration;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 绘制钢筋命令（对应旧命令 gj → Reinforcement.Rein）
    /// 编排流程：选择边界 → Domain 计算 → Infrastructure 写入图纸
    /// </summary>
    public class DrawReinforcementCommand
    {
        private readonly IReinService _reinService;
        private readonly IPolygonOffsetService _offsetService;
        private readonly ILineIntersectionService _intersectionService;

        public DrawReinforcementCommand()
        {
            _reinService = ServiceLocator.Resolve<IReinService>();
            _offsetService = ServiceLocator.Resolve<IPolygonOffsetService>();
            _intersectionService = ServiceLocator.Resolve<ILineIntersectionService>();
        }

        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 1. 获取参数（优先从设置面板，否则用默认值）
            var vm = ViewModels.SettingsPanelViewModel.Current;
            var parameters = vm != null ? vm.CreateReinParameters() : ReinParameters.CreateDefault();

            // 2. 选择边界多段线
            var selResult = ed.GetEntity("\n选择混凝土边界多段线: ");
            if (selResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var entity = tr.GetObject(selResult.ObjectId, OpenMode.ForRead) as Polyline;
                if (entity == null)
                {
                    ed.WriteMessage("\n选择的不是多段线。");
                    return;
                }

                // 3. 预处理：转换为 Domain 对象
                // 旧代码 SetPolyLineClockWise 实际确保逆时针（命名误导），偏移用负值向内
                var boundary = entity.ToDomainPolyline();
                boundary.RemoveDuplicateVertices();
                boundary.SetCounterClockwise();
                boundary.IsClosed = true;

                tr.Commit();

                // 4. 应用样式
                _reinService.ApplyStyle(parameters);

                // 5. Domain 计算（平台无关的纯算法）
                var result = ReinforcementUtils.GenerateAll(
                    boundary, parameters, _offsetService, _intersectionService);

                // 6. 写入图纸（Infrastructure 层）
                _reinService.DrawReinforcement(result, parameters);

                ed.WriteMessage($"\n配筋完成：{result.FinalReinforcements?.Length ?? 0} 根钢筋");
            }
        }
    }
}
