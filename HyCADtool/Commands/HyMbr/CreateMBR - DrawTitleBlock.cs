using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass.TitleBlock;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        // 命令入口：调用工厂与绘图器完成图框绘制
        [CommandMethod("HYMBRD_DrawTitleBlock")]
        public static void DrawTitleBlockCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            // 1. 图幅选择
            PromptKeywordOptions sizeOpts = new PromptKeywordOptions("\n请选择图幅")
            {
                AllowNone = false
            };
            sizeOpts.Keywords.Add("A0");
            sizeOpts.Keywords.Add("A1");
            sizeOpts.Keywords.Add("A2");
            sizeOpts.Keywords.Add("A3");
            sizeOpts.Keywords.Add("A4");
            sizeOpts.Keywords.Default = "A3";
            PromptResult sizeRes = ed.GetKeywords(sizeOpts);
            if (sizeRes.Status != PromptStatus.OK) return;
            // 2. 长边放大比例
            PromptDoubleOptions scaleOpts = new PromptDoubleOptions("\n请输入图框长边方向放大比例 [默认1.0]：")
            {
                AllowNegative = false,
                AllowZero = false,
                DefaultValue = 1.0
            };
            PromptDoubleResult scaleRes = ed.GetDouble(scaleOpts);
            if (scaleRes.Status != PromptStatus.OK) return;
            // 3. 用户选择插入点（右下角对齐）
            PromptPointResult ptRes = ed.GetPoint("\n请选择图框右下角插入点：");
            if (ptRes.Status != PromptStatus.OK) return;
            Point3d basePoint = ptRes.Value;
            // 4. 创建图框
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.PaperSpace], OpenMode.ForWrite);
                ITitleBlock tb = TitleBlockFactory.Create(sizeRes.StringResult, scaleRes.Value);
                TitleBlockDrawer.DrawTitleBlock(tr, btr, tb, db, basePoint);
                tr.Commit();
                ed.WriteMessage($"\n成功绘制图框：{tb.Name}, 放大比例：{scaleRes.Value}");
            }
        }
    }
}