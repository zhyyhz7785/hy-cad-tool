using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.SpongeCity.Domain.Models;
using HyCADTool.Features.SpongeCity.Infrastructure;
using HyCADTool.Features.SpongeCity.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.SpongeCity.Commands
{
    /// <summary>
    /// hyscRA：扫描 10 类下垫面图层上的闭合 Polyline，按图层汇总面积回填 SpongeCityPanelViewModel.Surfaces。
    /// 同时把每条 Polyline {Id, 形心, 面积} 缓存到 vm.LayerPolylines 供 hyscWA 写文字。
    /// </summary>
    public class ReadSurfaceAreaCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;
            var ed = doc.Editor;

            var vm = SpongeCityPanelViewModel.Current;
            if (vm == null)
            {
                ed.WriteMessage("\n[hyscRA] 海绵面板尚未打开。请先 HYSpongeCity 启动面板。");
                return;
            }

            int totalCount = 0;
            double totalArea = 0;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    vm.LayerPolylines.Clear();

                    // 用 vm.Surfaces 中的 LayerName（用户可能改过）作为权威列表
                    var layerToItem = vm.Surfaces.ToDictionary(s => s.LayerName, s => s);
                    foreach (var item in vm.Surfaces)
                    {
                        var hits = SurfaceLayerService.CollectClosedPolylines(tr, db, item.LayerName);
                        double sum = hits.Sum(h => h.Area) / 1e6; // mm² → m²
                        item.Area = sum;
                        vm.LayerPolylines[item.LayerName] = hits
                            .Select(h => new SpongeCityPanelViewModel.PolylineRecord
                            {
                                Id = h.Id,
                                Centroid = h.Centroid,
                                Area = h.Area / 1e6,
                            })
                            .ToList();
                        ed.WriteMessage($"\n[hyscRA] {item.LayerName}: {hits.Count} 条 / 合计 {sum:F2} m²");
                        totalCount += hits.Count;
                        totalArea += sum;
                    }

                    tr.Commit();
                }

                // 刷新 UI
                vm.ExecuteCalculate();
                vm.StatusMessage = $"已读面积：{totalCount} 条 / 合计 {totalArea:F2} m²。";
                ed.WriteMessage($"\n[hyscRA] 完成：{totalCount} 条 / 合计 {totalArea:F2} m²。");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[hyscRA] 失败: {ex.Message}");
                vm.StatusMessage = "hyscRA 失败：" + ex.Message;
            }
        }
    }
}
