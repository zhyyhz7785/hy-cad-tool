using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool
{
    public static partial class BaseRein
    {
        // 新方法：根据输入的字符串筛选并返回字典，字典的键为输入的字符串，值为 ObjectId[]
        public static Dictionary<string, ObjectId[]> GetObjectIdsByLayerOrLinetype(ObjectId[] objectIds, string[] inputs)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            Dictionary<string, List<ObjectId>> resultDictionary = new Dictionary<string, List<ObjectId>>();
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (string input in inputs)
                    {
                        if (!resultDictionary.ContainsKey(input))
                        {
                            resultDictionary[input] = new List<ObjectId>();
                        }
                        foreach (ObjectId objId in objectIds)
                        {
                            Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (ent != null)
                            {
                                // 过滤条件：图层名称或线型名称匹配
                                bool isMatchingLayer = ent.Layer.Equals(input, StringComparison.OrdinalIgnoreCase);
                                bool isMatchingLinetype = ent.Linetype.Equals(input, StringComparison.OrdinalIgnoreCase);
                                if (isMatchingLayer || isMatchingLinetype)
                                {
                                    resultDictionary[input].Add(ent.ObjectId);
                                }
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
            // 转换为字典并返回
            return resultDictionary.ToDictionary(k => k.Key, k => k.Value.ToArray());
        }
    }
}
