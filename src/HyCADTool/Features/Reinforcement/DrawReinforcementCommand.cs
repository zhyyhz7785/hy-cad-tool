using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Shell.Contracts;
using HyCADTool.Features.Reinforcement.Domain;
using HyCADTool.Shared.Geometry;
using HyCADTool.Shared.AutoCAD.Converters;
using HyCADTool.App.Bootstrap;
using HyCADTool.Presentation.ViewModels;
using System.Collections.Generic;
using HyCADTool.Shared.Geometry.Interfaces;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 绘制钢筋命令（对应旧命令 gj）
    /// 支持选择一个或多个多段线，逐个计算+写入
    /// 不使用并行——AutoCAD STA 线程模型 + 单例服务不安全
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

            // 1. 获取参数并确保样式已应用
            var vm = SettingsPanelViewModel.Current;
            var parameters = vm != null ? vm.CreateReinParameters() : ReinParameters.CreateDefault();
            
            // 确保样式已同步（即使不是从面板按钮触发，Scale 变化后也能正确应用）
            vm?.EnsureStylesApplied();

            // 2. 选择边界多段线（支持单选 / 框选多个，仅过滤 LWPOLYLINE）
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            });
            var selResult = ed.GetSelection(
                new PromptSelectionOptions { MessageForAdding = "\n选择混凝土边界多段线: " },
                filter);
            if (selResult.Status != PromptStatus.OK) return;

            var objectIds = selResult.Value.GetObjectIds();
            int total = objectIds.Length;
            ed.WriteMessage($"\n已选择 {total} 个多段线");

            // 3. 批量读取几何 → 转 Domain 对象
            var boundaries = new List<Polyline2D>(total);
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var oid in objectIds)
                {
                    var entity = tr.GetObject(oid, OpenMode.ForRead) as Polyline;
                    if (entity == null) continue;

                    var boundary = entity.ToDomainPolyline();
                    boundary.RemoveDuplicateVertices();
                    boundary.SetCounterClockwise();
                    boundary.IsClosed = true;
                    boundaries.Add(boundary);
                }
                tr.Commit();
            }

            if (boundaries.Count == 0)
            {
                ed.WriteMessage("\n未找到有效的闭合多段线。");
                return;
            }

            // 4. 逐个：Domain 计算 → 写入图纸（串行，安全）
            int successCount = 0;
            for (int i = 0; i < boundaries.Count; i++)
            {
                try
                {
                    var result = ReinforcementUtils.GenerateAll(
                        boundaries[i], parameters, _offsetService, _intersectionService);

                    _reinService.DrawReinforcement(result, parameters);

                    successCount++;
                    int rebarCount = result.FinalReinforcements?.Length ?? 0;
                    ed.WriteMessage($"\n  [{successCount}/{boundaries.Count}] {rebarCount} 根钢筋");
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n  [{i + 1}] 失败：{ex.Message}");
                }
            }

            ed.WriteMessage($"\n配筋完成：{successCount}/{total}");
        }
    }
}
