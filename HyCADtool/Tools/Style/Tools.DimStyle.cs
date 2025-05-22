using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Config;
using System;
namespace HyCADTool.Tools
{
    public static partial class HyTool
    {
        /// <summary>
        /// 创建或更新尺寸样式。
        /// </summary>
        /// <param name="name">尺寸样式名称。</param>
        /// <param name="textStyleName">文本样式名称。</param>
        /// <param name="scale">比例因子。</param>
        public static ObjectId CreateDimStyle(string name)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            ObjectId dimId = new ObjectId();
            using (DocumentLock docLock = doc.LockDocument())
            {
                // 获取指定名称的文本样式的 ObjectId
                var textId = GetSymbolRecordFromDbByName<TextStyleTable, TextStyleTableRecord>(db.TextStyleTableId, HyTool.TextStyleConfig.Name);
                // 创建或更新尺寸样式
                dimId = CreateSymbolRecord<DimStyleTable, DimStyleTableRecord>(db.DimStyleTableId, name, s =>
                {
                    #region 设置样式
                    s.Name = name;
                    s.Dimtdec = 0; // 公差精度
                    s.Dimexo = 1; // 尺寸界线偏移
                    s.Dimexe = 1; // 尺寸界线超出量
                    s.Dimdle = 0.5; // 尺寸线超出量
                    s.Dimtxt = 2.5; // 文字高度
                    s.Dimgap = 1; // 文字偏移
                    s.Dimasz = 1; // 箭头大小
                    s.Dimdec = 0; // 精度
                    s.Dimscale = BaseConfig.Scale; // 比例
                    s.Dimtxsty = textId.Id; // 文字样式
                    s.Dimtofl = true; // 尺寸线强制
                    s.Dimtad = 1; // 文字位置垂直
                    s.Dimtix = true; // 文字在内
                    s.Dimtih = false; // 文字在内对齐
                    s.Dimtoh = false; // 文字外部对齐
                    s.Dimclrt = Color.FromColorIndex(ColorMethod.ByColor, 7); // 文字颜色白色
                    #endregion
                    // 设置尺寸样式数据
                    db.SetDimstyleData(s);
                });
                // 设置当前尺寸
                // 设置当前尺寸样式
                if (dimId != ObjectId.Null)
                {
                    db.Dimstyle = dimId;
                }
                else
                {
                    dimId = GetSymbolRecordFromDbByName<DimStyleTable, DimStyleTableRecord>(db.Dimstyle, name).Id;
                    db.Dimstyle = dimId;
                }
                // 设置箭头样式
                SetDimStyleArrows("_ARCHTICK", "_ARCHTICK");
                doc.Editor.WriteMessage($"\n创建标注样式 '{name}' 成功。");
                return dimId;
            }
        }
        /// <summary>
        /// 设置尺寸样式的箭头样式。
        /// </summary>
        /// <param name="arrow1">第一箭头样式名称。</param>
        /// <param name="arrow2">第二箭头样式名称。</param>
        public static void SetDimStyleArrows(string arrow1, string arrow2)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            using (DocumentLock docLock = doc.LockDocument())
            {
                // 获取箭头样式的 ObjectId
                var id1 = GetArrowObjectId("DIMBLK1", arrow1);
                var id2 = GetArrowObjectId("DIMBLK2", arrow2);
                using (var trans = db.TransactionManager.StartTransaction())
                {
                    var dimStyle = trans.GetObject(db.Dimstyle, OpenMode.ForWrite) as DimStyleTableRecord;
                    dimStyle.Dimsah = true;
                    dimStyle.Dimblk1 = id1;
                    dimStyle.Dimblk2 = id2;
                    // 设置尺寸样式数据
                    db.SetDimstyleData(dimStyle);
                    trans.Commit();
                }
            }
        }
        /// <summary>
        /// 获取指定箭头样式的 ObjectId。
        /// </summary>
        /// <param name="arrow">系统变量名称。</param>
        /// <param name="newArrName">箭头样式名称。</param>
        /// <returns>箭头样式的 ObjectId。</returns>
        private static ObjectId GetArrowObjectId(string arrow, string newArrName)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            ObjectId arrObjId = ObjectId.Null;
            using (DocumentLock docLock = doc.LockDocument())
            {
                // 获取当前箭头样式的名称
                string oldArrName = Application.GetSystemVariable(arrow) as string;
                // 设置新的箭头样式名称
                Application.SetSystemVariable(arrow, newArrName);
                if (!string.IsNullOrEmpty(oldArrName))
                {
                    Application.SetSystemVariable(arrow, oldArrName);
                }
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    if (bt.Has(newArrName))
                    {
                        arrObjId = bt[newArrName];
                    }
                    tr.Commit();
                }
            }
            return arrObjId;
        }
        /// <summary>
        /// 创建或更新符号表记录。
        /// </summary>
        /// <typeparam name="TTable">符号表类型。</typeparam>
        /// <typeparam name="TRecord">符号表记录类型。</typeparam>
        /// <param name="tableId">符号表的 ObjectId。</param>
        /// <param name="name">符号表记录名称。</param>
        /// <param name="action">配置符号表记录的委托。</param>
        /// <returns>创建或更新的符号表记录的 ObjectId。</returns>
        public static ObjectId CreateSymbolRecord<TTable, TRecord>(ObjectId tableId, string name, Action<TRecord> action)
            where TTable : SymbolTable
            where TRecord : SymbolTableRecord, new()
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            using (var trans = db.TransactionManager.StartTransaction())
            {
                var table = (TTable)trans.GetObject(tableId, OpenMode.ForRead);
                if (table.Has(name))
                {
                    var record = (TRecord)trans.GetObject(table[name], OpenMode.ForWrite);
                    action(record);
                    trans.Commit();
                    return record.ObjectId;
                }
                else
                {
                    table.UpgradeOpen();
                    var record = new TRecord
                    {
                        Name = name
                    };
                    action(record);
                    var id = table.Add(record);
                    trans.AddNewlyCreatedDBObject(record, true);
                    trans.Commit();
                    return id;
                }
            }
        }
        /// <summary>
        /// 从数据库中获取指定名称的符号表记录。
        /// </summary>
        /// <typeparam name="TTable">符号表类型。</typeparam>
        /// <typeparam name="TRecord">符号表记录类型。</typeparam>
        /// <param name="tableId">符号表的 ObjectId。</param>
        /// <param name="name">符号表记录名称。</param>
        /// <param name="db">数据库对象。</param>
        /// <returns>符号表记录对象。</returns>
        public static TRecord GetSymbolRecordFromDbByName<TTable, TRecord>(this ObjectId tableId, string name, Database db = null)
            where TTable : SymbolTable
            where TRecord : SymbolTableRecord
        {
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            using (var trans = db.TransactionManager.StartTransaction())
            {
                var table = (TTable)trans.GetObject(tableId, OpenMode.ForRead);
                if (table.Has(name))
                {
                    var record = (TRecord)trans.GetObject(table[name], OpenMode.ForRead);
                    trans.Commit();
                    return record;
                }
                return null;
            }
        }
    }
}
