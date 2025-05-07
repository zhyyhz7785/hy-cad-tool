//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.Colors;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using System.Collections.Generic;
//using System.Threading.Tasks;
//namespace HyCADTool.Tools
//{
//    public static partial class EtGpt
//    {
//        /// <summary>
//        /// 将实体添加到指定的空间（如模型空间或图纸空间）。
//        /// 如果实体已经存在于数据库中，则只返回其 ObjectId。
//        /// </summary>
//        /// <param name="ent">要添加的实体。</param>
//        /// <param name="db">AutoCAD 数据库对象。如果为 null，则使用当前活动文档的数据库。</param>
//        /// <param name="space">空间名称。如果为 null，则默认使用模型空间。</param>
//        /// <returns>实体的 ObjectId。</returns>
//        /// <exception cref="Autodesk.AutoCAD.Runtime.Exception">如果发生 AutoCAD 异常，则捕获并记录错误信息。</exception>
//        public static ObjectId ToSpace(this Entity ent, Database db = null, string space = null)
//        {
//            // 获取当前活动文档的数据库，如果未提供
//            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            ObjectId id = ObjectId.Null;
//            // 锁定文档以确保线程安全
//            using (DocumentLock docLock = doc.LockDocument())
//            {
//                // 开始事务
//                using (Transaction trans = db.TransactionManager.StartTransaction())
//                {
//                    try
//                    {
//                        // 获取块表并以只读模式打开
//                        var blkTbl = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                        // 获取目标空间（模型空间或图纸空间）并以写模式打开
//                        var spaceRecord = trans.GetObject(blkTbl[space ?? BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                        // 如果实体尚未添加到数据库
//                        if (ent.Id.IsNull)
//                        {
//                            // 将实体添加到目标空间
//                            id = spaceRecord.AppendEntity(ent);
//                            // 标记实体为新创建
//                            trans.AddNewlyCreatedDBObject(ent, true);
//                            // 提交事务
//                            trans.Commit();
//                        }
//                        else
//                        {
//                            // 如果实体已经存在，直接返回其 ObjectId
//                            id = ent.Id;
//                        }
//                    }
//                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
//                    {
//                        // 捕获并记录 AutoCAD 异常
//                        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n错误: {ex.Message}");
//                    }
//                }
//            }
//            return id;
//        }
//        /// <summary>
//        /// 将实体集合添加到指定的空间（如模型空间或图纸空间）。
//        /// 如果实体已经存在于数据库中，则只返回其 ObjectId。
//        /// </summary>
//        /// <param name="ents">要添加的实体集合。</param>
//        /// <param name="db">AutoCAD 数据库对象。如果为 null，则使用当前活动文档的数据库。</param>
//        /// <param name="space">空间名称。如果为 null，则默认使用模型空间。</param>
//        /// <returns>实体的 ObjectId 集合。</returns>
//        /// <exception cref="Autodesk.AutoCAD.Runtime.Exception">如果发生 AutoCAD 异常，则捕获并记录错误信息。</exception>
//        public static ObjectIdCollection ToSpace(this IEnumerable<Entity> ents, Database db = null, string space = null)
//        {
//            // 获取当前活动文档的数据库，如果未提供
//            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var ids = new ObjectIdCollection();
//            // 锁定文档以确保线程安全
//            using (DocumentLock docLock = doc.LockDocument())
//            {
//                // 开始事务
//                using (Transaction trans = db.TransactionManager.StartTransaction())
//                {
//                    try
//                    {
//                        // 获取块表并以只读模式打开
//                        var blkTbl = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                        // 获取目标空间（模型空间或图纸空间）并以写模式打开
//                        var spaceRecord = trans.GetObject(blkTbl[space ?? BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                        // 遍历实体集合
//                        foreach (var ent in ents)
//                        {
//                            // 如果实体尚未添加到数据库
//                            if (ent.Id.IsNull)
//                            {
//                                // 将实体添加到目标空间
//                                ids.Add(spaceRecord.AppendEntity(ent));
//                                // 标记实体为新创建
//                                trans.AddNewlyCreatedDBObject(ent, true);
//                            }
//                            else
//                            {
//                                // 如果实体已经存在，直接添加其 ObjectId 到集合
//                                ids.Add(ent.Id);
//                            }
//                        }
//                        // 提交事务
//                        trans.Commit();
//                    }
//                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
//                    {
//                        // 捕获并记录 AutoCAD 异常
//                        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n错误: {ex.Message}");
//                    }
//                }
//            }
//            return ids;
//        }
//        /// <summary>
//        /// 将实体集合添加到特定空间。
//        /// </summary>
//        /// <param name="ents">实体集合</param>
//        /// <param name="db">数据库对象</param>
//        /// <param name="space">空间名称</param>
//        /// <returns>实体的 ObjectId 集合</returns>
//        /// <exception cref="Autodesk.AutoCAD.Runtime.Exception">如果发生 AutoCAD 异常，则捕获并记录错误信息。</exception>
//        public static ObjectIdCollection ToSpaceParallel(this IEnumerable<Entity> ents, Database db = null, string space = null)
//        {
//            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var ids = new ObjectIdCollection();
//            using (DocumentLock docLock = doc.LockDocument())
//            {
//                using (Transaction trans = db.TransactionManager.StartTransaction())
//                {
//                    try
//                    {
//                        var blkTbl = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                        var spaceRecord = trans.GetObject(blkTbl[space ?? BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                        // 并行处理每个实体
//                        Parallel.ForEach(ents, ent =>
//                        {
//                            lock (trans)
//                            {
//                                if (ent.Id.IsNull)
//                                {
//                                    ids.Add(spaceRecord.AppendEntity(ent));
//                                    trans.AddNewlyCreatedDBObject(ent, true);
//                                }
//                                else
//                                {
//                                    ids.Add(ent.Id);
//                                }
//                            }
//                        });
//                        trans.Commit();
//                    }
//                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
//                    {
//                        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n错误: {ex.Message}");
//                    }
//                }
//            }
//            return ids;
//        }
//        public static ObjectIdCollection ToSpace(this IEnumerable<Point3d> points, int pdMode = 3, double pdSize = -5, Database db = null)
//        {
//            // 获取当前活动文档的数据库，如果未提供
//            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var ids = new ObjectIdCollection();
//            // 设置点的显示样式和大小
//            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
//            Application.SetSystemVariable("PDMODE", pdMode);
//            Application.SetSystemVariable("PDSIZE", pdSize);
//            // 锁定文档以确保线程安全
//            using (DocumentLock docLock = doc.LockDocument())
//            {
//                // 开始事务
//                using (Transaction trans = db.TransactionManager.StartTransaction())
//                {
//                    try
//                    {
//                        // 获取块表并以只读模式打开
//                        var blkTbl = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                        // 获取模型空间块表记录
//                        var btr = trans.GetObject(blkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                        // 将每个点添加到图形中
//                        foreach (var point in points)
//                        {
//                            DBPoint dbPoint = new DBPoint(point)
//                            {

//                            };
//                            dbPoint.SetDatabaseDefaults();
//                            ids.Add(btr.AppendEntity(dbPoint));
//                            trans.AddNewlyCreatedDBObject(dbPoint, true);
//                        }
//                        // 提交事务
//                        trans.Commit();
//                    }
//                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
//                    {
//                        // 捕获并记录 AutoCAD 异常
//                        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n错误: {ex.Message}");
//                        // 如果事务失败，则回滚
//                        trans.Abort();
//                    }
//                }
//            }
//            return ids;
//        }
//        public static ObjectIdCollection ToSpace(this IEnumerable<Point3d> points, string layerName, short colorIndex = 4, int pdMode = 3, double pdSize = -5,
//       Database db = null)
//        {
//            // 获取当前活动文档的数据库，如果未提供
//            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            var ids = new ObjectIdCollection();

//            // 设置点的显示样式和大小
//            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
//            Application.SetSystemVariable("PDMODE", pdMode);
//            Application.SetSystemVariable("PDSIZE", pdSize);

//            // 锁定文档以确保线程安全
//            using (DocumentLock docLock = doc.LockDocument())
//            {
//                // 开始事务
//                using (Transaction trans = db.TransactionManager.StartTransaction())
//                {
//                    try
//                    {
//                        // 获取块表并以只读模式打开
//                        var blkTbl = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                        // 获取模型空间块表记录
//                        var btr = trans.GetObject(blkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

//                        // 检查并处理图层
//                        LayerTable lt = trans.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
//                        if (!lt.Has(layerName))
//                        {
//                            // 如果图层不存在，创建新图层
//                            lt.UpgradeOpen();
//                            LayerTableRecord ltr = new LayerTableRecord
//                            {
//                                Name = layerName,
//                                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
//                            };
//                            lt.Add(ltr);
//                            trans.AddNewlyCreatedDBObject(ltr, true);
//                        }
//                        else
//                        {
//                            // 如果图层存在，更新图层颜色为输入的colorIndex
//                            lt.UpgradeOpen();
//                            LayerTableRecord ltr = trans.GetObject(lt[layerName], OpenMode.ForWrite) as LayerTableRecord;
//                            ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
//                        }

//                        // 将每个点添加到图形中
//                        foreach (var point in points)
//                        {
//                            DBPoint dbPoint = new DBPoint(point)
//                            {
//                                Layer = layerName,
//                                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex) // 直接使用输入的colorIndex
//                            };

//                            dbPoint.SetDatabaseDefaults();
//                            ids.Add(btr.AppendEntity(dbPoint));
//                            trans.AddNewlyCreatedDBObject(dbPoint, true);
//                        }

//                        // 提交事务
//                        trans.Commit();
//                    }
//                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
//                    {
//                        // 捕获并记录 AutoCAD 异常
//                        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n错误: {ex.Message}");
//                        // 如果事务失败，则回滚
//                        trans.Abort();
//                    }
//                }
//            }
//            return ids;
//        }

//        public static ObjectId ToSpace(this Point3d point, string layerName = "Hy_Points", short colorIndex = 4, int pdMode = 3, double pdSize = -5, Database db = null)
//        {
//            // 获取当前活动文档的数据库，如果未提供
//            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
//            var doc = Application.DocumentManager.MdiActiveDocument;
//            ObjectId pointId = ObjectId.Null;
//            // 设置点的显示样式和大小
//            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
//            Application.SetSystemVariable("PDMODE", pdMode);
//            Application.SetSystemVariable("PDSIZE", pdSize);
//            // 锁定文档以确保线程安全
//            using (DocumentLock docLock = doc.LockDocument())
//            {
//                // 开始事务
//                using (Transaction trans = db.TransactionManager.StartTransaction())
//                {
//                    try
//                    {
//                        // 获取块表并以只读模式打开
//                        var blkTbl = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                        // 获取模型空间块表记录
//                        var btr = trans.GetObject(blkTbl[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                        // 确保图层存在，如果不存在则创建
//                        LayerTable lt = trans.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;
//                        if (!lt.Has(layerName))
//                        {
//                            lt.UpgradeOpen();
//                            LayerTableRecord ltr = new LayerTableRecord
//                            {
//                                Name = layerName,
//                                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
//                            };
//                            lt.Add(ltr);
//                            trans.AddNewlyCreatedDBObject(ltr, true);
//                        }
//                        // 创建并添加点到图形中
//                        DBPoint dbPoint = new DBPoint(point)
//                        {
//                            Layer = layerName,
//                            Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
//                        };
//                        dbPoint.SetDatabaseDefaults();
//                        pointId = btr.AppendEntity(dbPoint);
//                        trans.AddNewlyCreatedDBObject(dbPoint, true);
//                        // 提交事务
//                        trans.Commit();
//                    }
//                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
//                    {
//                        // 捕获并记录 AutoCAD 异常
//                        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n错误: {ex.Message}");
//                        // 如果事务失败，则回滚
//                        trans.Abort();
//                    }
//                }
//            }
//            return pointId;
//        }
//    }
//}
