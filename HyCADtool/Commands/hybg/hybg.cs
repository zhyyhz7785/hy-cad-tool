using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Config;
using HyCADTool.HelpClass.ElevationSymbol;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("bg")]
        public static void DrawElevationWithJig()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            try
            {
                double scale = GetScale(ed);
                if (scale <= 0) return;
                double d = GetConstructionParameter(ed);
                if (d <= 0) return;
                Point3d? basePoint = GetBasePoint(ed);
                if (!basePoint.HasValue) // 非空判断
                {
                    return; // 如果用户取消选择基点，则退出命令
                }
                Point3d basePointValue = basePoint.Value; // 获取非空的 Point3d 值
                // 创建 ElevationSymbol 并传递初始 scale 和 d
                ElevationSymbol symbol = new ElevationSymbol(basePointValue, scale, d, ed);
                bool continueDrawing = true;
                while (continueDrawing)
                {
                    PromptResult jigRes = ed.Drag(symbol);
                    switch (jigRes.Status)
                    {
                        case PromptStatus.OK:
                            using (Transaction tr = db.TransactionManager.StartTransaction())
                            {
                                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                                symbol.AddToDatabase(tr, btr);
                                tr.Commit();
                            }
                            // 不更新 basePoint，保持初始基点
                            symbol = new ElevationSymbol(basePointValue, scale, d, ed, symbol.State); // 保持 basePoint 固定
                            break;
                        case PromptStatus.Cancel:
                            continueDrawing = false;
                            break;
                    }
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n{ex}\n");
            }
        }
        private static double GetScale(Editor ed)
        {
            PromptDoubleOptions pdo = new PromptDoubleOptions("\n输入比例 [默认50]: ");
            pdo.DefaultValue = BaseConfig.Scale; // 默认值来自 BaseConfig
            PromptDoubleResult pdr = ed.GetDouble(pdo);
            return pdr.Status == PromptStatus.OK ? pdr.Value : BaseConfig.Scale; // 返回输入值或默认值
        }
        private static double GetConstructionParameter(Editor ed)
        {
            PromptDoubleOptions dOpt = new PromptDoubleOptions("\n输入构造参数 d [默认2.0]: ");
            dOpt.DefaultValue = BaseConfig.ElevationLength; // 默认值来自 BaseConfig
            PromptDoubleResult dRes = ed.GetDouble(dOpt);
            return dRes.Status == PromptStatus.OK ? dRes.Value : BaseConfig.ElevationLength; // 返回输入值或默认值
        }
        private static Point3d? GetBasePoint(Editor ed)
        {
            PromptPointResult ppr = ed.GetPoint("\n选择基点: ");
            return ppr.Status == PromptStatus.OK ? ppr.Value : (Point3d?)null;
        }
    }
}