using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
namespace CadUtils
{
    public static partial class EtGpt
    {
        /// <summary>
        /// 创建一个新图层。
        /// </summary>
        /// <param name="layerName">图层的名称。</param>
        /// <param name="colorIndex">图层的颜色索引（可选）。</param>
        [CommandMethod("CreateLayer")]
        public static ObjectId CreateLayer(
         string layerName,              // 图层名称
          short colorIndex = 7,          // 颜色索引，默认值为7（白色）
         Database db = null,           // 数据库对象，可选参数
          Editor ed = null)             // 编辑器对象，可选参数
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            // 如果未提供db，则从活动文档获取
            db = db ?? doc.Database;
            // 如果未提供ed，则从活动文档获取
            ed = ed ?? doc.Editor;
            var id = new ObjectId();       // 初始化图层ID
                                           // 获取文档锁
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 获取图层表
                    LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (layerTable.Has(layerName))
                    {
                        // 如果图层已存在，返回现有图层ID
                        id = layerTable[layerName];
                        //ed.WriteMessage($"\n图层 '{layerName}' 已经存在。");
                    }
                    else
                    {
                        // 升级图层表权限为可写
                        layerTable.UpgradeOpen();
                        // 创建新图层记录
                        LayerTableRecord newLayer = new LayerTableRecord
                        {
                            Name = layerName,
                            Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex)
                        };
                        // 添加新图层并获取ID
                        id = layerTable.Add(newLayer);
                        // 将新创建的对象添加到数据库
                        tr.AddNewlyCreatedDBObject(newLayer, true);
                        ed.WriteMessage($"\n图层 '{layerName}' 已创建。");
                    }
                    // 提交事务
                    tr.Commit();
                }
            }
            return id;    // 返回图层ID
        }
        /// <summary>
        /// 创建多个图层。
        /// </summary>
        /// <param name="layerInfos">图层信息列表，包含图层名称和颜色索引。</param>
        [CommandMethod("CreateMultipleLayers")]
        public static void CreateMultipleLayers(params (string layerName, short colorIndex)[] layerInfos)
        {
            // 获取当前文档和数据库
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            // 获取文档锁以确保线程安全
            using (DocumentLock docLock = doc.LockDocument())
            {
                // 开启事务处理
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 获取图层表对象
                    LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    // 遍历传入的图层信息列表
                    foreach (var (layerName, colorIndex) in layerInfos)
                    {
                        // 检查图层是否已经存在
                        if (layerTable.Has(layerName))
                        {
                            // 如果图层已存在，输出提示信息
                            doc.Editor.WriteMessage($"\n图层 '{layerName}' 已经存在.");
                        }
                        else
                        {
                            // 如果图层不存在，准备创建新图层
                            layerTable.UpgradeOpen(); // 将图层表升级为写模式
                            LayerTableRecord newLayer = new LayerTableRecord
                            {
                                Name = layerName, // 设置图层名称
                                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex) // 设置图层颜色
                            };
                            layerTable.Add(newLayer); // 将新图层添加到图层表
                            tr.AddNewlyCreatedDBObject(newLayer, true); // 通知事务处理新创建的对象
                            doc.Editor.WriteMessage($"\n图层 '{layerName}' 已创建.");
                        }
                    }
                    // 提交事务
                    tr.Commit();
                }
            }
        }
        public static void SetLayer(this Entity entity, string newLayerName)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "实体对象不能为空。");
            }
            if (string.IsNullOrEmpty(newLayerName))
            {
                throw new ArgumentNullException(nameof(newLayerName), "图层名称不能为空或为空字符串。");
            }
            Database db = entity.Database ?? Application.DocumentManager.MdiActiveDocument.Database;
            if (db == null)
            {
                throw new InvalidOperationException("无法获取实体关联的数据库或当前活动文档的数据库。");
            }
            ObjectId layerId;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (lt.Has(newLayerName))
                {
                    layerId = lt[newLayerName];
                }
                else
                {
                    LayerTableRecord ltr = new LayerTableRecord
                    {
                        Name = newLayerName
                    };
                    lt.UpgradeOpen();
                    layerId = lt.Add(ltr);
                    tr.AddNewlyCreatedDBObject(ltr, true);
                }
                // 检查实体是否已经以写模式打开
                if (!entity.IsWriteEnabled)
                {
                    entity.UpgradeOpen(); // 仅在需要时调用 UpgradeOpen()
                }
                entity.LayerId = layerId;
                tr.Commit();
            }
        }
        /// <summary>
        /// 根据图层名称获取图层的 ObjectId。
        /// </summary>
        /// <param name="db">AutoCAD 数据库对象。</param>
        /// <param name="layerName">图层名称。</param>
        /// <returns>指定图层的 ObjectId，如果图层不存在则抛出异常。</returns>
        public static ObjectId GetLayerId(this string layerName)
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            if (string.IsNullOrWhiteSpace(layerName))
            {
                throw new ArgumentException("图层名称不能为空或空白。", nameof(layerName));
            }
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 获取图层表
                var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (layerTable.Has(layerName))
                {
                    // 返回图层的 ObjectId
                    return layerTable[layerName];
                }
                else
                {
                    throw new System.Exception($"图层 '{layerName}' 不存在。");
                }
            }
        }
    }
}
