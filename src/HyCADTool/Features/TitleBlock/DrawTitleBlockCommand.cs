using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.TitleBlock.Services;
using HyCADTool.Shared.AutoCAD.Services;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.TitleBlock
{
    /// <summary>
    /// 在图纸空间绘制标准图框（HYMBRD）
    /// </summary>
    public class DrawTitleBlockCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 图幅选择
            var sizeOpts = new PromptKeywordOptions("\n请选择图幅") { AllowNone = false };
            sizeOpts.Keywords.Add("A0");
            sizeOpts.Keywords.Add("A1");
            sizeOpts.Keywords.Add("A2");
            sizeOpts.Keywords.Add("A3");
            sizeOpts.Keywords.Add("A4");
            sizeOpts.Keywords.Default = "A3";
            var sizeRes = ed.GetKeywords(sizeOpts);
            if (sizeRes.Status != PromptStatus.OK) return;

            // 长边放大比例
            var scaleOpts = new PromptDoubleOptions("\n请输入长边方向放大比例 [默认1.0]：")
            {
                AllowNegative = false, AllowZero = false, DefaultValue = 1.0
            };
            var scaleRes = ed.GetDouble(scaleOpts);
            if (scaleRes.Status != PromptStatus.OK) return;

            // 插入点
            var ptRes = ed.GetPoint("\n请选择图框右下角插入点：");
            if (ptRes.Status != PromptStatus.OK) return;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ps = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.PaperSpace], OpenMode.ForWrite);

                    var tb = TitleBlockFactory.Create(sizeRes.StringResult, scaleRes.Value);
                    TitleBlockDrawer.Draw(tr, ps, tb, db, ptRes.Value);

                    tr.Commit();
                    ed.WriteMessage($"\n图框绘制完成: {tb.Name}, 放大 {scaleRes.Value}");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n绘制失败: {ex.Message}");
            }
        }
    }
}
