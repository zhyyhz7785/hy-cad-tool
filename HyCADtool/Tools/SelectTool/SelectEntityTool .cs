using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.HelpClass;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Tools
{
    //Cad查询数据命令   (setq ent (entsel)) (setq ent_data (car ent)) (setq ent_data (entget ent_data)) 
    public static partial class Tools
    {
        public enum CadType
        {
            Curve,
            Arc,
            Circle,
            Ellipse,
            Leader,
            Line,
            Polyline,
            Spline,
            Xline,
            BlockReference,
            Hatch,
            DBPoint,
            DBText,
            Dimension,
            MLeader,
            MText,
            Region,
            DetailSymbol,
            Mline,
        }
        #region 过滤选择 
        public static ObjectId[] SelectWithFilterAll(this SelectionFilter filter)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            using (DocumentLock docLock = doc.LockDocument())
            {
                // 清空预先选择集
                ed.SetImpliedSelection(new ObjectId[0]);
                // 使用选择过滤器获取选择集
                PromptSelectionResult res = ed.SelectAll(filter);
                // 检查选择结果状态
                if (res.Status == PromptStatus.OK)
                {
                    // 输出选择的对象数量
                    ed.WriteMessage($"选中了 {res.Value.Count} 个满足过滤条件的图形对象。\n");
                    // 获取当前的选择集
                    SelectionSet ss = res.Value;
                    // 将选择集设置为当前选择集，高亮显示图形对象
                    // ed.SetImpliedSelection(ss);
                    var ids = ss.GetObjectIds();
                    return ids;
                }
                else
                {
                    ed.WriteMessage("未能选中任何对象。\n");
                    return null;
                }
            }
        }        
        public static ObjectId[] SelectWithFilter(this SelectionFilter filter, Document doc, Editor ed)
        {
            try
            {
                // 锁定文档
                using (doc.LockDocument())
                {
                    // 开启事务
                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        // 清空预选集
                        ed.SetImpliedSelection(new ObjectId[0]);
                        // 创建PromptSelectionOptions对象，用于设置选择提示
                        PromptSelectionOptions opt = new PromptSelectionOptions
                        {
                            MessageForAdding = "\n请选择要过滤图形对象:"
                        };
                        // 使用选择过滤器获取选择集
                        PromptSelectionResult res = ed.GetSelection(opt, filter);
                        // 检查选择结果状态
                        if (res.Status == PromptStatus.OK)
                        {
                            // 输出选择的对象数量
                            ed.WriteMessage($"选中了 {res.Value.Count} 个满足过滤条件的图形对象。\n");
                            // 获取当前的选择集
                            SelectionSet ss = res.Value;
                            // 将选择集设置为当前选择集，高亮显示图形对象
                            // ed.SetImpliedSelection(ss);
                            // 提交事务
                            tr.Commit();
                            return ss.GetObjectIds();
                        }
                        else
                        {
                            ed.WriteMessage("未能选中任何对象。\n");
                            return null;
                        }
                    }
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"发生错误: {ex.Message}\n");
                return null;
            }
        }
        #endregion
        #region Id和Entity相互转换  显示或关闭    
        /// <summary>
        /// ids转换entities
        /// </summary>
        /// <param name="db"></param>
        /// <param name="ids"></param>
        /// <returns></returns>
        public static Entity[] IdsToEntitys(this IEnumerable<ObjectId> ids)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            using (DocumentLock acLckDocCur = doc.LockDocument())
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                try
                {
                    if (ids == null)
                    {
                        ed.WriteMessage($"\nId转换Entitie失败,ObjectId输入值为空");
                        return null;
                    }
                    if (ids.Count() >= 1)
                    {
                        var ents = new Entity[ids.Count()];
                        using (var trans = db.TransactionManager.StartTransaction())
                        {
                            int i = 0;
                            foreach (var id in ids)
                            {
                                var ent = id.GetObject(OpenMode.ForWrite) as Entity;
                                ents[i] = ent;
                                i++;
                            }
                            trans.Commit();
                        }
                        return ents;
                    }
                    ed.WriteMessage($"\nId转换Entitie失败,ObjectId输入值为空");
                    return null;
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\nIds转换Entities失败\n{ex}");
                    return null;
                }
            }
        }
        public static T[] IdsToEntitys<T>(this IEnumerable<ObjectId> ids) where T : Entity
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            using (DocumentLock acLckDocCur = doc.LockDocument())
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                try
                {
                    if (ids == null)
                    {
                        ed.WriteMessage($"\nId转换Entitie失败,ObjectId输入值为空");
                        return null;
                    }
                    if (ids.Count() >= 1)
                    {
                        var ents = new T[ids.Count()];
                        using (var trans = db.TransactionManager.StartTransaction())
                        {
                            int i = 0;
                            foreach (var id in ids)
                            {
                                var ent = id.GetObject(OpenMode.ForWrite) as T;
                                if (ent != null)
                                {
                                    ents[i] = ent;
                                    i++;
                                }
                            }
                            trans.Commit();
                        }
                        return ents;
                    }
                    ed.WriteMessage($"\nId转换Entitie失败,ObjectId输入值为空");
                    return null;
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\nIds转换Entities失败\n{ex}");
                    return null;
                }
            }
        }
        /// <summary>
        /// ids转换entities
        /// </summary>
        /// <param name="db"></param>
        /// <param name="ids"></param>
        /// <returns></returns>
        public static Entity IdToEntity(this ObjectId id)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                if (id == null)
                {
                    ed.WriteMessage($"\nId转换Entitie失败,ObjectId输入值为空");
                    return null;
                }
                using (var trans = db.TransactionManager.StartTransaction())
                {
                    var ent = id.GetObject(OpenMode.ForWrite) as Entity;
                    trans.Commit();
                    return ent;
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nIds转换Entities失败\n{ex}");
                return null;
            }
        }
        public static T IdToEntityT<T>(this ObjectId id) where T : Entity
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                if (id == null)
                {
                    ed.WriteMessage($"\nId转换Entitie失败,ObjectId输入值为空");
                    return null;
                }
                using (var trans = db.TransactionManager.StartTransaction())
                {
                    var ent = id.GetObject(OpenMode.ForWrite) as T;
                    trans.Commit();
                    return ent;
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nIds转换Entities失败\n{ex}");
                return null;
            }
        }
        /// <summary>
        /// 通过实体可迭代类型，返回实体的objectID[]
        /// </summary>
        /// <param name="db"></param>
        /// <param name="ents"></param>
        /// <returns></returns>
        public static ObjectId[] EntitysToIds(this IEnumerable<Entity> ents)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                if (ents == null)
                {
                    ed.WriteMessage($"\nEntities转换Ids失败,Entities输入值为空");
                    return null;
                }
                var ids = new ObjectId[ents.Count()];
                if (ents.Count() >= 0)
                {
                    int i = 0;
                    foreach (var ent in ents)
                    {
                        var id = ent.ObjectId;
                        ids[i++] = id;
                    }
                    return ids;
                }
                ed.WriteMessage($"\nEntities转换Ids失败,Entities输入值为空");
                return new ObjectId[0];
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nEntities转换Ids失败\n{ex}");
                return null;
            }
        }
        public static void EntityVisualOn(this Database db, ObjectId[] ids)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var doc = Application.DocumentManager.MdiActiveDocument;
            using (var lockDoc = doc.LockDocument())
            {
                try
                {
                    if (ids == null)
                    {
                        ed.WriteMessage($"\n输入值为空\n");
                    }
                    using (var trans = db.TransactionManager.StartTransaction())
                    {
                        foreach (var id in ids)
                        {
                            var ent = id.GetObject(OpenMode.ForWrite) as Entity;
                            ent.Visible = true;
                        }
                        trans.Commit();
                    }
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n{ex}\n");
                }
            }
        }
        public static void EntityVisualOff(this Database db, ObjectId[] ids)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var doc = Application.DocumentManager.MdiActiveDocument;
            using (var lockDoc = doc.LockDocument())
            {
                try
                {
                    if (ids == null)
                    {
                        ed.WriteMessage($"\n输入值为空\n");
                    }
                    using (var trans = db.TransactionManager.StartTransaction())
                    {
                        foreach (var id in ids)
                        {
                            var ent = id.GetObject(OpenMode.ForWrite) as Entity;
                            ent.Visible = false;
                        }
                        trans.Commit();
                    }
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n{ex}\n");
                }
            }
        }
        #endregion
        #region 选择单个实体 返回实体的实例
        /// <summary>
        /// 选择一个实体，获取图形的实例
        /// </summary>
        /// <returns></returns>
        public static Entity SelectSingleEntity(this Database db)
        {
            // 获取当前活动文档和编辑器
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 锁定文档以确保线程安全和文档一致性
            using (DocumentLock docLock = doc.LockDocument())
            {
                try
                {
                    // 提示用户选择一个实体
                    ed.WriteMessage("\n请选择一个实体或按 ESC 退出\n");
                    // 获取选择结果
                    PromptSelectionResult res = ed.GetSelection();
                    if (res.Status != PromptStatus.OK)
                    {
                        // 如果选择结果状态不是 OK，则返回 null 并输出提示信息
                        ed.WriteMessage("\n选择已取消或出错\n");
                        return null;
                    }
                    // 获取选中的对象 ID 数组
                    ObjectId[] ids = res.Value.GetObjectIds();
                    if (ids.Length == 0)
                    {
                        // 如果没有选中任何实体，输出提示信息并返回 null
                        ed.WriteMessage("\n没有选中实体\n");
                        return null;
                    }
                    if (ids.Length > 1)
                    {
                        // 如果选中了多个实体，输出提示信息并返回 null
                        ed.WriteMessage("\n选中多个实体\n");
                        return null;
                    }
                    // 开启事务处理
                    using (Transaction trans = db.TransactionManager.StartTransaction())
                    {
                        // 尝试获取选中的实体对象
                        Entity ent = trans.GetObject(ids[0], OpenMode.ForWrite) as Entity;
                        if (ent != null)
                        {
                            // 如果实体对象不为空，输出提示信息并提交事务
                            ed.WriteMessage($"\n选中单个实体成功，选中 {ent.GetType().Name}\n");
                            trans.Commit();
                            return ent;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    // 如果捕获到异常，输出错误信息
                    ed.WriteMessage($"\n系统错误：{ex.Message}\n");
                }
            }
            // 如果选择过程中出现问题，返回 null
            return null;
        }
        /// <summary>
        /// 选择并返回指定类型的实体。
        /// </summary>
        /// <typeparam name="T">要选择的实体类型。</typeparam>
        /// <param name="db">数据库对象。</param>
        /// <returns>选定的实体。如果选择无效或被取消，返回 null。</returns>
        public static T SelectAEntity<T>(this Database db) where T : Entity, new()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            var type = typeof(T);
            try
            {
                // 获取文档锁以确保线程安全
                using (DocumentLock docLock = doc.LockDocument())
                {
                    // 开始事务
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        // 获取当前空间块表记录并以写模式打开
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                        // 设置实体选择选项
                        PromptEntityOptions peo = new PromptEntityOptions($"\n请选择一个 {type.Name}: ");
                        peo.SetRejectMessage($"\n选中的对象不是 {type.Name}。");
                        peo.AddAllowedClass(typeof(T), false);
                        // 提示用户选择实体
                        PromptEntityResult per = ed.GetEntity(peo);
                        if (per.Status == PromptStatus.OK)
                        {
                            // 获取并返回选定的实体
                            T entity = (T)per.ObjectId.GetObject(OpenMode.ForRead);
                            tr.Commit();
                            return entity;
                        }
                        else
                        {
                            ed.WriteMessage("\n选择被取消或无效。");
                            return null;
                        }
                    }
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
                return null;
            }
        }
        #endregion
        #region 多个选择
        public static ObjectId[] GetImpliedSelection(this Database db
            )
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            PromptSelectionResult psr = ed.SelectImplied();
            //1 得到预选择集
            if (psr.Status == PromptStatus.OK)
            {
                var ids = psr.Value.GetObjectIds();
                ed.WriteMessage($"\n获取到预选择集{ids.Count()}个实体\n");
                return ids;
            }
            return null;
        }
        public static ObjectId[] SelectIds(this Database db, IEnumerable<TypedValue> tvs = null)
        {
            tvs = tvs ?? AcTv.All;
            SelectionFilter filter = new SelectionFilter(new TypedValue[0]);
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                filter = AcTv.Getfilter(tvs);
                ed.WriteMessage("\n请选择多个实体\n");
                var res = ed.GetSelection(filter);
                if (res.Status == PromptStatus.OK)
                {
                    var ids = res.Value.GetObjectIds();
                    if (ids.Count() >= 0)
                    {
                        return ids;
                    }
                    else
                    {
                        ed.WriteMessage($"\n选择失败：返回值为空，请重新选择\n");
                        return null;
                    }
                }
                else
                {
                    ed.WriteMessage($"\n选择状态错误：返回值为空，请重新选择\n");
                    return null;
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n系统错误\n" + $"\n{ex}\n");
                return null;
            }
        }
        public static ObjectId[] SelectIdsWithoutUserAction(this Database db, IEnumerable<ObjectId> sourceIds = null, IEnumerable<TypedValue> tvs = null)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            tvs = tvs ?? AcTv.All;
            sourceIds = sourceIds ?? ed.SelectAll().Value.GetObjectIds();
            SelectionFilter filter = new SelectionFilter(new TypedValue[0]);
            try
            {
                filter = AcTv.Getfilter(AcTv.And(AcTv.All, tvs));
                var res = ed.SelectAll(filter);
                if (res.Status == PromptStatus.OK)
                {
                    var ids = res.Value.GetObjectIds();
                    ids = ids.Intersect(sourceIds).ToArray();
                    if (ids.Count() >= 0)
                    {
                        return ids;
                    }
                    else
                    {
                        ed.WriteMessage($"\n选择失败：返回值为空，请重新选择\n");
                        return null;
                    }
                }
                else
                {
                    ed.WriteMessage($"\n选择状态错误：返回值为空，请重新选择\n");
                    return null;
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n系统错误\n" + $"\n{ex}\n");
                return null;
            }
        }
        public static ObjectId[] SelectIdsWithoutUserAction(this Database db)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            SelectionFilter filter = new SelectionFilter(new TypedValue[0]);
            try
            {
                filter = AcTv.Getfilter(AcTv.All);
                var res = ed.SelectAll(filter);
                if (res.Status == PromptStatus.OK)
                {
                    var ids = res.Value.GetObjectIds();
                    if (ids.Count() >= 0)
                    {
                        return ids;
                    }
                    else
                    {
                        ed.WriteMessage($"\n选择失败：返回值为空，请重新选择\n");
                        return null;
                    }
                }
                else
                {
                    ed.WriteMessage($"\n选择状态错误：返回值为空，请重新选择\n");
                    return null;
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n系统错误\n" + $"\n{ex}\n");
                return null;
            }
        }
        public static ObjectId[] SelectIdsWithoutUserAction(this Database db, IEnumerable<TypedValue> tvs = null, IEnumerable<ObjectId> sourceIds = null)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            tvs = tvs ?? AcTv.All;
            sourceIds = sourceIds ?? ed.SelectAll().Value.GetObjectIds();
            SelectionFilter filter = new SelectionFilter(new TypedValue[0]);
            try
            {
                filter = AcTv.Getfilter(AcTv.And(AcTv.All, tvs));
                //选择完成后 重置前台显示                
                var res = ed.SelectAll(filter);
                using (SelectionSet ss = res.Value)
                {
                    if (res.Status == PromptStatus.OK)
                    {
                        var ids = res.Value.GetObjectIds();
                        ids = ids.Intersect(sourceIds).ToArray();
                        if (ids.Count() >= 0)
                        {
                            ed.WriteMessage($"\n选中多个实体成功，选中{ids.Count()}个{tvs.ToArray()[0].Value}实体\n");
                            return ids;
                        }
                        else
                        {
                            ed.WriteMessage($"\n选择失败：返回值为空，请重新选择\n");
                            return null;
                        }
                    }
                    else
                    {
                        ed.WriteMessage($"\n选择状态错误：返回值为空，请重新选择\n");
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n系统错误\n" + $"\n{ex}\n");
                return null;
            }
        }
        public static void HighLightSelection(this Database db, IEnumerable<ObjectId> ids)
        {
            var ents = ids.IdsToEntitys();
            foreach (var ent in ents)
            {
                ent.Highlight();
            }
        }
        #endregion
        #region 非空判断
        public static bool IsExist<T>(this IEnumerable<T> ts)
        {
            if (ts.Count() == 0 || ts == null)
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage("\n检查值是否为空值：检查出错误，实体为空值\n");
                return false;
            }
            return true;
        }
        public static bool IsExist<T>(this T t)
        {
            if (t == null)
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage("\n检查值是否为空值：检查出错误，实体为空值\n");
                return false;
            }
            return true;
        }
        #endregion
        /// <summary>
        /// 从选择集中选择不同类型Entities
        /// </summary>
        /// <param name="sendIds"></param>
        /// <param name="type"></param>
        /// <param name="db"></param>
        /// <param name="space"></param>
        /// <returns></returns>
        public static List<ObjectId> SelectIdFormGetSelection(ObjectId[] sendIds, CadType type, Database db = null, string space = null)
        {
            //利用C#进行CAD二次开发时，遇到 eLockViolation 的问题，这个网上说是因为“非模态窗口，要锁定文档”
            DocumentLock docLock = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.LockDocument();
            var ids = new List<ObjectId>();
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            using (var trans = db.TransactionManager.StartTransaction())
            {
                foreach (var id in sendIds)
                {
                    //从数据库中得到实体    注意ModelSpace中有锁定图层Entities的时候，
                    //*****forceOpenOnLockedLayer一定为true*********不然会给读写错误。
                    var ent = trans.GetObject(id, OpenMode.ForWrite, true, false);
                    //判断实体类型，并把实体id添加到集合
                    switch (type)
                    {
                        case CadType.Line:
                            if (ent is Line)
                            {
                                var line = ent as Line;
                                var layer = line.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.Polyline:
                            if (ent is Polyline)
                            {
                                var polyline = ent as Polyline;
                                var layer = polyline.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.BlockReference:
                            if (ent is BlockReference)
                            {
                                var blockReference = ent as BlockReference;
                                var layer = blockReference.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.DBText:
                            if (ent is DBText)
                            {
                                var dbText = ent as DBText;
                                var layer = dbText.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.MText:
                            if (ent is MText)
                            {
                                var mText = ent as MText;
                                var layer = mText.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.Hatch:
                            if (ent is Hatch)
                            {
                                var hatch = ent as Hatch;
                                var layer = hatch.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.Dimension:
                            if (ent is Dimension)
                            {
                                var dimension = ent as Dimension;
                                var layer = dimension.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.MLeader:
                            if (ent is MLeader)
                            {
                                var mLeader = ent as MLeader;
                                var layer = mLeader.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.Region:
                            if (ent is Region)
                            {
                                var region = ent as Region;
                                var layer = region.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.Curve:
                            if (ent is Curve)
                            {
                                var curve = ent as Curve;
                                var layer = curve.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.Circle:
                            if (ent is Circle)
                            {
                                var circle = ent as Circle;
                                var layer = circle.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.Ellipse:
                            if (ent is Ellipse)
                            {
                                var ellipse = ent as Ellipse;
                                var layer = ellipse.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.Arc:
                            if (ent is Arc)
                            {
                                var arc = ent as Arc;
                                var layer = arc.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.Spline:
                            if (ent is Spline)
                            {
                                var spline = ent as Spline;
                                var layer = spline.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.Xline:
                            if (ent is Xline)
                            {
                                var xline = ent as Xline;
                                var layer = xline.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.DBPoint:
                            if (ent is DBPoint)
                            {
                                var dBPoint = ent as DBPoint;
                                var layer = dBPoint.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                        case CadType.Mline:
                            if (ent is Mline)
                            {
                                var mline = ent as Mline;
                                var layer = mline.LayerId.GetObject(OpenMode.ForRead) as LayerTableRecord;
                                //对图层进行判断，只添加未加锁图层
                                if (layer.IsLocked != true)
                                {
                                    ids.Add(id);
                                }
                            }
                            break;
                    }
                }
                trans.Commit();
            }
            if (ids.Count == 0)
            {
                ed.WriteMessage("没有选中任何图形，请检查图形是否锁定");
            }
            docLock.Dispose();
            return ids;
        }
    }
}
