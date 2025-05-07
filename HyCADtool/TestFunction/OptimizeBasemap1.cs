using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool
{
    public static partial class TestFunction
    {
        // CommandMethod 特性表明这是一个 AutoCAD 命令
        [CommandMethod("OptimizeBasemap")]
        public static List<ObjectId> OptimizeBasemap()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            // 获取用户选择的图形对象
            PromptSelectionOptions selOpts = new PromptSelectionOptions();
            selOpts.MessageForAdding = "\n请选择图形对象: ";
            PromptSelectionResult selRes = ed.GetSelection(selOpts);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n用户取消了选择.");
                return null;
            }
            // 过滤出特定图层的对象
            string[] targetLayers = { "砼墙", "柱", "板元", "筏板板元配筋标注" };
            List<ObjectId> objectsToKeep = new List<ObjectId>();
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selObj in selRes.Value)
                {
                    if (selObj != null)
                    {
                        Entity ent = trans.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent != null && targetLayers.Contains(ent.Layer))
                        {
                            objectsToKeep.Add(selObj.ObjectId);
                        }
                        else if (ent != null)
                        {
                            ent.UpgradeOpen();
                            ent.Erase();
                        }
                    }
                }
                trans.Commit();
            }
            // 执行purge命令
            ed.Command("_.purge", "_all", "*", "_n");
            ed.WriteMessage("\n优化底图完成.");
            return objectsToKeep;
        }
        public static SelectionSet CreateSelectionSetFromIds(this List<ObjectId> objectIds)
        {
            // 创建新的 SelectionSet
            SelectionSet newSelSet = SelectionSet.FromObjectIds(objectIds.ToArray());
            return newSelSet;
        }
    }
}
