using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection
{
    /// <summary>
    /// 选择工具类，提供实体选择相关的辅助方法
    /// </summary>
    public static class SelectionHelper
    {
        /// <summary>
        /// 选择单个实体
        /// 从旧项目 HyCADtool/Tools/SelectTool/SelectEntityTool.cs 迁移
        /// </summary>
        /// <returns>选中的实体，如果取消或出错则返回 null</returns>
        public static Entity SelectSingleEntity()
        {
            // 每次调用都从 Application 获取当前活动文档
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;

            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\n请选择一个实体或按 ESC 退出\n");

                // 在未锁文档前获取用户选择
                PromptSelectionResult res = ed.GetSelection();
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n选择已取消或出错\n");
                    return null;
                }

                ObjectId[] ids = res.Value.GetObjectIds();
                if (ids.Length == 0)
                {
                    ed.WriteMessage("\n没有选中实体\n");
                    return null;
                }
                if (ids.Length > 1)
                {
                    ed.WriteMessage("\n选中多个实体，请只选择一个\n");
                    return null;
                }

                // 开始事务操作前加锁
                using (DocumentLock docLock = doc.LockDocument())
                using (Transaction trans = doc.TransactionManager.StartTransaction())
                {
                    Entity ent = trans.GetObject(ids[0], OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        ed.WriteMessage($"\n选中单个实体成功，类型为：{ent.GetType().Name}\n");
                        trans.Commit();
                        return ent;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n系统错误：{ex.Message}\n");
            }

            return null;
        }

        /// <summary>
        /// 选择多个实体（通用方法）
        /// </summary>
        /// <param name="message">提示消息</param>
        /// <returns>选中的 ObjectId 数组，如果取消或出错则返回空数组</returns>
        public static ObjectId[] SelectEntities(string message = "\n请选择实体：")
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return new ObjectId[0];

            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage(message);
                PromptSelectionResult res = ed.GetSelection();
                
                if (res.Status == PromptStatus.OK)
                {
                    return res.Value.GetObjectIds();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n选择出错：{ex.Message}\n");
            }

            return new ObjectId[0];
        }
    }
}
