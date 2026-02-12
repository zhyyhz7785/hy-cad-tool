using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interactive;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 标高符号交互绘制命令（对应旧命令 bg）
    /// 流程：选基点 → Jig 拖动 → 左键放置 → 继续 → ESC 退出
    /// </summary>
    public class DrawElevationCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                var vm = ViewModels.SettingsPanelViewModel.Current;
                double scale = vm != null ? vm.Scale : 40.0;
                double d = 2.0; // 构造参数（ElevationLength）

                // 选择基点
                PromptPointResult ppr = ed.GetPoint("\n选择基点: ");
                if (ppr.Status != PromptStatus.OK) return;
                Point3d basePoint = ppr.Value;

                // 确保图层/文字样式
                var (textStyleId, layerId) = ElevationService.EnsureStylesCreated();

                // 创建 Jig 并连续绘制
                var symbol = new ElevationSymbolJig(basePoint, scale, d, ed, textStyleId, layerId);
                bool continueDrawing = true;

                while (continueDrawing)
                {
                    PromptResult jigRes = ed.Drag(symbol);
                    switch (jigRes.Status)
                    {
                        case PromptStatus.OK:
                            using (var tr = db.TransactionManager.StartTransaction())
                            {
                                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                                symbol.AddToDatabase(tr, btr);
                                tr.Commit();
                            }
                            // 保持基点不变，创建新 Jig 继续
                            symbol = new ElevationSymbolJig(basePoint, scale, d, ed, textStyleId, layerId, symbol.State);
                            break;
                        case PromptStatus.Cancel:
                            continueDrawing = false;
                            break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n{ex.Message}");
            }
        }
    }
}
