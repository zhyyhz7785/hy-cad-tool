using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.HelpClass;
using HyCADTool.Log;
using System.Collections.Generic;
namespace HyCADTool
{
    public static partial class BaseRein
    {
        public static ObjectId[] SelectPillarPiers()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            List<ObjectId> selectedObjectIds = new List<ObjectId>();
            try
            {
                SimpleLogger.StartTiming("过滤器设置和选择操作");
                // 创建选择过滤器，允许选择 Polyline, DBText, MText
                var filter = AcTv.And(AcTv.GetLayerFilter("柱"), AcTv.Polyline, "ByLayer".GetLinetypeFilter()).Getfilter();
                PromptSelectionOptions opts = new PromptSelectionOptions
                {
                    MessageForAdding = "请选择柱墩，及配筋网格: ",
                    AllowDuplicates = false
                };
                PromptSelectionResult res = ed.GetSelection(opts, filter);
                SimpleLogger.StopTiming("过滤器设置和选择操作");
                if (res.Status == PromptStatus.OK)
                {
                    SelectionSet selSet = res.Value;
                    SimpleLogger.StartTiming("事务处理");
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (SelectedObject selObj in selSet)
                        {
                            if (selObj != null)
                            {
                                Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                                if (ent is Polyline)
                                {
                                    selectedObjectIds.Add(ent.ObjectId);
                                }
                            }
                        }
                        tr.Commit();
                    }
                    SimpleLogger.StopTiming("事务处理");
                    // 将选中的对象ID设置为隐含选择集
                    if (selectedObjectIds.Count > 0)
                    {
                        ed.SetImpliedSelection(selectedObjectIds.ToArray());
                    }
                    // 返回选中的对象ID
                    return selectedObjectIds.ToArray();
                }
                else
                {
                    ed.WriteMessage("\n没有选择多段线、DBText 或 MText。");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
                SimpleLogger.Log($"错误: {ex.Message}");
            }
            // 如果未成功选择对象，返回空数组
            return new ObjectId[0];
        }
    }
}
