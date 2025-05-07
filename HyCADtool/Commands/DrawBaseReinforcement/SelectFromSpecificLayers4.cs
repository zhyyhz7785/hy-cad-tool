using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
[assembly: CommandClass(typeof(HyCADTool.Command.DrawBaseReinforcement))]
namespace HyCADTool.Command
{
    public static partial class DrawBaseReinforcement
    {
        // CommandMethod 特性表明这是一个 AutoCAD 命令      
        public static Dictionary<DBText, ObjectId> SelectFromSpecificLayers()
        {
            // 获取当前文档、编辑器和数据库
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 创建选择过滤条件
            TypedValue[] filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.LayerName, "板元,筏板板元配筋标注"),
                new TypedValue((int)DxfCode.Start, "TEXT,MTEXT,LWPOLYLINE,POLYLINE")
            };
            SelectionFilter filter = new SelectionFilter(filterList);
            // 提示用户选择有限元网格中的对象
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择板元或筏板板元配筋标注图层中的对象: "
            };
            PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
            // 如果用户取消选择，输出消息并返回空字典
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n用户取消了选择。");
                return new Dictionary<DBText, ObjectId>();
            }
            // 获取选择集
            SelectionSet selSet = selRes.Value;
            // 处理选择集中的对象并返回结果
            return ProcessSelectedObjects(selSet);
        }
        private static Dictionary<DBText, ObjectId> ProcessSelectedObjects(SelectionSet selSet)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            // 使用事务获取所有对象并存储在字典中
            Dictionary<ObjectId, DBObject> objectDict = new Dictionary<ObjectId, DBObject>();
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selObj in selSet)
                {
                    if (selObj != null)
                    {
                        DBObject obj = trans.GetObject(selObj.ObjectId, OpenMode.ForRead);
                        objectDict[selObj.ObjectId] = obj;
                    }
                }
                trans.Commit();
            }
            // 提取包含数值的文字对象
            List<DBText> rebarTexts = objectDict.Values.OfType<DBText>()
                .Where(textObj => textObj.Layer == "筏板板元配筋标注" && double.TryParse(textObj.TextString, out _)).ToList();
            // 并行处理找到每个文字对象对应的网格对象ID
            ConcurrentDictionary<DBText, ObjectId> result = new ConcurrentDictionary<DBText, ObjectId>();
            Parallel.ForEach(rebarTexts, text =>
            {
                foreach (var kvp in objectDict)
                {
                    if (kvp.Value is Polyline polyline && polyline.Layer == "板元" && IsPointInsidePolyline(polyline, text.AlignmentPoint))
                    {
                        result[text] = kvp.Key;
                        break;
                    }
                }
            });
            // 转换为普通字典
            Dictionary<DBText, ObjectId> resultDict = result.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            // 高亮显示结果
            EtGpt.HighlightSelection(ed, result);          
            return resultDict;
        }
    }
}
