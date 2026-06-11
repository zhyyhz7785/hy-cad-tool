using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.SpongeCity.Domain.Models;
using HyCADTool.Features.SpongeCity.Infrastructure;
using HyCADTool.Features.SpongeCity.ViewModels;
using HyCADTool.Shell.ViewModels;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.SpongeCity.Commands
{
    /// <summary>
    /// hyscDL：在拾取基点为 10 类下垫面图层画样线（含图层名文字 + 合计文字锚点 a）。
    /// 行为：从基点向上每行 5*Scale，写一条 2 m 长 Line + 200 mm 标尺线 a + 顶上写图层名 DBText。
    /// 配合 hyscWA 在 a 旁边写「类别: NN m²」合计文字。
    /// </summary>
    public class DrawSurfaceLayerLinesCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var db = doc.Database;
            var ed = doc.Editor;

            var ptResult = ed.GetPoint("\n[hyscDL] 选择基点（10 类下垫面图层的样线起点）: ");
            if (ptResult.Status != PromptStatus.OK) return;

            double scale = ReadScale();
            double lineLength = 2000;     // 实际 mm（不乘 scale）
            double rulerLength = 200;     // 标尺线 a
            double rowGap = 250 * scale;  // 行距按 scale 缩放（避免不同图纸尺寸太挤）
            double textHeight = 5 * scale;

            double startX = ptResult.Value.X;
            double startY = ptResult.Value.Y;

            var vm = SpongeCityPanelViewModel.Current;
            if (vm != null) vm.AnchorPoints.Clear();

            int created = 0;
            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (var entry in SurfaceCatalog.Entries)
                    {
                        SurfaceLayerService.EnsureLayer(tr, db, entry.Name, entry.AciColor);
                        SurfaceLayerService.EnsureLayer(tr, db, entry.TextLayerName, entry.AciColor);

                        // 主线
                        var line = new Line(
                            new Point3d(startX, startY, 0),
                            new Point3d(startX + lineLength, startY, 0))
                        {
                            Layer = entry.Name,
                        };
                        ms.AppendEntity(line);
                        tr.AddNewlyCreatedDBObject(line, true);

                        // 标尺线 a（紧跟主线右端，作为合计文字锚点）
                        var anchorStart = new Point3d(startX + lineLength + textHeight, startY, 0);
                        var anchorEnd = new Point3d(anchorStart.X + rulerLength, startY, 0);
                        var ruler = new Line(anchorStart, anchorEnd)
                        {
                            Layer = entry.Name,
                        };
                        ms.AppendEntity(ruler);
                        tr.AddNewlyCreatedDBObject(ruler, true);

                        // 图层名文字（线上方）
                        var text = new DBText
                        {
                            Position = new Point3d(startX, startY + textHeight * 0.4, 0),
                            Height = textHeight,
                            TextString = entry.Name,
                            Layer = entry.TextLayerName,
                            HorizontalMode = TextHorizontalMode.TextLeft,
                            VerticalMode = TextVerticalMode.TextBottom,
                            AlignmentPoint = new Point3d(startX, startY + textHeight * 0.4, 0),
                        };
                        text.AdjustAlignment(db);
                        ms.AppendEntity(text);
                        tr.AddNewlyCreatedDBObject(text, true);

                        // 缓存锚点
                        if (vm != null)
                            vm.AnchorPoints[entry.Name] = anchorEnd;

                        startY += rowGap;
                        created++;
                    }

                    tr.Commit();
                }

                ed.WriteMessage($"\n[hyscDL] 已在 {created} 个下垫面图层绘制样线（基点 ({ptResult.Value.X:F0},{ptResult.Value.Y:F0})，行距 {rowGap:F0}）。");
                if (vm != null) vm.StatusMessage = $"已画样线：{created} 类，行距 {rowGap:F0}。";
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[hyscDL] 失败: {ex.Message}");
                if (vm != null) vm.StatusMessage = "hyscDL 失败：" + ex.Message;
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
