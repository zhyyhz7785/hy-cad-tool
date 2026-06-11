using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.SpongeCity.Domain.Models;
using HyCADTool.Features.SpongeCity.Infrastructure;
using HyCADTool.Features.SpongeCity.ViewModels;
using HyCADTool.Shell.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.SpongeCity.Commands
{
    /// <summary>
    /// hyscWA：
    /// 1) 在 vm.LayerPolylines 每条 Polyline 形心位置写 DBText（图层=「{Name}-文字」）；
    /// 2) 在 vm.AnchorPoints[layer] 的样线 a 端点旁写「{Name}: NN.NN m²」合计文字。
    /// 文字字高 = 5 * Scale，与项目其他注记保持一致。
    /// </summary>
    public class WriteSurfaceAreaCommand
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
                ed.WriteMessage("\n[hyscWA] 海绵面板尚未打开。请先 HYSpongeCity 启动面板。");
                return;
            }

            double scale = ReadScale();
            double textHeight = 5 * scale;
            int wroteCentroid = 0;
            int wroteTotal = 0;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (var entry in SurfaceCatalog.Entries)
                    {
                        var item = vm.Surfaces.FirstOrDefault(s => s.Code == entry.Code);
                        if (item == null) continue;

                        SurfaceLayerService.EnsureLayer(tr, db, entry.TextLayerName, entry.AciColor);

                        // 1) 每条形心文字
                        if (vm.LayerPolylines.TryGetValue(item.LayerName, out var polyList))
                        {
                            foreach (var rec in polyList)
                            {
                                var dbt = new DBText
                                {
                                    Position = rec.Centroid,
                                    Height = textHeight,
                                    TextString = $"{rec.Area:F2} m²",
                                    Layer = entry.TextLayerName,
                                    HorizontalMode = TextHorizontalMode.TextCenter,
                                    VerticalMode = TextVerticalMode.TextVerticalMid,
                                    AlignmentPoint = rec.Centroid,
                                };
                                dbt.AdjustAlignment(db);
                                ms.AppendEntity(dbt);
                                tr.AddNewlyCreatedDBObject(dbt, true);
                                wroteCentroid++;
                            }
                        }

                        // 2) 合计文字（贴在样线 a 右端；无 a 则跳过）
                        if (vm.AnchorPoints.TryGetValue(entry.Name, out var anchor))
                        {
                            var totalText = new DBText
                            {
                                Position = new Point3d(anchor.X + textHeight, anchor.Y - textHeight * 0.5, 0),
                                Height = textHeight,
                                TextString = $"{entry.Name}: {item.Area:F2} m²",
                                Layer = entry.TextLayerName,
                                HorizontalMode = TextHorizontalMode.TextLeft,
                                VerticalMode = TextVerticalMode.TextBottom,
                                AlignmentPoint = new Point3d(anchor.X + textHeight, anchor.Y - textHeight * 0.5, 0),
                            };
                            totalText.AdjustAlignment(db);
                            ms.AppendEntity(totalText);
                            tr.AddNewlyCreatedDBObject(totalText, true);
                            wroteTotal++;
                        }
                    }

                    tr.Commit();
                }

                ed.WriteMessage($"\n[hyscWA] 已写：形心文字 {wroteCentroid} 条，合计文字 {wroteTotal} 条。");
                vm.StatusMessage = $"已写面积文字：形心 {wroteCentroid} + 合计 {wroteTotal}。";
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[hyscWA] 失败: {ex.Message}");
                vm.StatusMessage = "hyscWA 失败：" + ex.Message;
            }
        }

        private static double ReadScale()
        {
            try
            {
                var s = SettingsPanelViewModel.Current?.Scale ?? 50.0;
                return s <= 0 ? 50.0 : s;
            }
            catch { return 50.0; }
        }
    }
}
