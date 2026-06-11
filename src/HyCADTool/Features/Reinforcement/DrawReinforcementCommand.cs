using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Shell.Contracts;
using HyCADTool.Features.Reinforcement.Domain;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Converters;
using HyCADTool.App.Bootstrap;
using HyCADTool.Shell.ViewModels;
using System;
using System.Collections.Generic;
using HyCAD.Geometry.Interfaces;

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

            var vm = SettingsPanelViewModel.Current;
            var parameters = vm != null ? vm.CreateReinParameters() : ReinParameters.CreateDefault();

            if (!parameters.IsValid(out string paramError))
            {
                ed.WriteMessage($"\n钢筋参数无效：{paramError}");
                return;
            }

            vm?.EnsureStylesApplied();

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

            var rawBoundaries = new List<Polyline2D>(total);
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var oid in objectIds)
                {
                    var entity = tr.GetObject(oid, OpenMode.ForRead) as Polyline;
                    if (entity == null) continue;

                    if (HasArcSegments(entity))
                        ed.WriteMessage("\n提示：含弧段边界将按弦线近似处理。");

                    var boundary = entity.ToDomainPolyline();
                    boundary.RemoveDuplicateVertices();
                    boundary.IsClosed = true;
                    rawBoundaries.Add(boundary);
                }
                tr.Commit();
            }

            if (rawBoundaries.Count == 0)
            {
                ed.WriteMessage("\n未找到有效的闭合多段线。");
                return;
            }

            var classified = ReinRegionBuilder.ClassifyBoundaries(rawBoundaries);
            var regions = ReinRegionBuilder.BuildRegions(classified);

            if (regions.Count == 0)
            {
                ed.WriteMessage("\n未找到有效的外轮廓（嵌套孔洞需与外轮廓一同选择）。");
                return;
            }

            int successCount = 0;
            int rebarTotal = 0;
            foreach (var (region, reinBoundaries) in regions)
            {
                foreach (var boundary in reinBoundaries)
                {
                    try
                    {
                        var result = ReinforcementUtils.GenerateAll(
                            boundary, parameters, _offsetService, _intersectionService, region);

                        int rebarCount = result.FinalReinforcements?.Length ?? 0;
                        if (rebarCount == 0)
                        {
                            ed.WriteMessage($"\n  跳过：偏移无效或边界退化");
                            continue;
                        }

                        _reinService.DrawReinforcement(result, parameters);
                        successCount++;
                        rebarTotal += rebarCount;
                        ed.WriteMessage($"\n  [{successCount}] {rebarCount} 根钢筋");
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n  失败：{ex.Message}");
                    }
                }
            }

            ed.WriteMessage($"\n配筋完成：{successCount} 次写入，共 {rebarTotal} 根钢筋");
        }

        private static bool HasArcSegments(Polyline poly)
        {
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                if (Math.Abs(poly.GetBulgeAt(i)) > 1e-6)
                    return true;
            }
            return false;
        }
    }
}
