using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Core;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("hyef_Base_ConstructBaseData")]
        public static void ConstructBaseDataFilterCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            CoreMethods.ConstructBaseDataFilter(db, ed);
        }
        [CommandMethod("hyef_Base_HighlightBoltData")]
        public static void HighlightBoltDataCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            var peo = new PromptEntityOptions("\n请选择一个多段线: ");
            peo.SetRejectMessage("\n必须选择多段线！");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;
            var pline = db.TransactionManager.StartTransaction().GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
            if (pline == null)
            {
                ed.WriteMessage("\n未选择多段线，操作取消！");
                return;
            }
            CoreMethods.HighlightBoltData(db, ed, pline);
        }
        [CommandMethod("hyef_Axis_Construct")]
        public static void ConstructAxisCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            var axes = CoreMethods.ConstructAxis(doc.Database, ed);
            if (axes != null && axes.Count > 0)
            {
                ed.WriteMessage($"\n成功构造 {axes.Count} 条轴线！");
            }
            else
            {
                ed.WriteMessage("\n轴线构造失败或未选择任何直线！");
            }
        }
        [CommandMethod("hyef_Axis_Initialize")]
        public static void InitializeAxisCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            var axis = CoreMethods.InitializeAxis(doc.Database, ed);
            if (axis != null)
            {
                ed.WriteMessage($"\n轴线初始化完成，底座数量: {axis.Bases.Count}");
            }
            else
            {
                ed.WriteMessage("\n轴线初始化失败或直线没有附加数据！");
            }
        }
        [CommandMethod("hyef_Axis_Display")]
        public static void DisplayStructureCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            var axis = CoreMethods.GetAxisFromLine(doc.Database, ed);
            if (axis == null)
            {
                ed.WriteMessage("\n无法读取轴线数据或直线没有附加数据，无法显示结构！");
                return;
            }
            CoreMethods.DisplayStructure(doc.Database, ed, axis);
            ed.WriteMessage("\n结构高亮显示完成！");
        }
        [CommandMethod("hyef_Axis_CreateTable")]
        public static void CreateAxisTableCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            var axis = CoreMethods.GetAxisFromLine(db, ed);
            if (axis == null)
            {
                ed.WriteMessage("\n未选择轴线或轴线数据无效，操作取消！");
                return;
            }
            CoreMethods.CreateAxisTable(db, ed, axis);
            ed.WriteMessage("\n轴线表格创建完成！");
        }
    }
}