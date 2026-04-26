using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.EquipmentFoundation.Domain.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace HyCADTool.Shared.AutoCAD.Xdata
{
    /// <summary>
    /// AutoCAD 扩展字典通用读写服务
    /// 迁移自旧代码 ExtensionDictionaryUtils
    /// </summary>
    public static class ExtensionDictionaryService
    {
        #region 通用泛型读写

        /// <summary>
        /// 从实体的扩展字典读取 JSON 数据并反序列化
        /// </summary>
        public static T Read<T>(Transaction tr, Entity entity, string key) where T : class
        {
            if (!entity.ExtensionDictionary.IsValid) return null;

            var extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
            if (extDict == null || !extDict.Contains(key)) return null;

            var xrec = tr.GetObject(extDict.GetAt(key), OpenMode.ForRead) as Xrecord;
            if (xrec?.Data?.AsArray().Length > 0)
            {
                TypedValue tv = xrec.Data.AsArray()[0];
                if (tv.TypeCode == (int)DxfCode.Text)
                {
                    return JsonConvert.DeserializeObject<T>(tv.Value.ToString());
                }
            }
            return null;
        }

        /// <summary>
        /// 将数据序列化为 JSON 并写入实体的扩展字典
        /// </summary>
        public static void Write<T>(Transaction tr, Entity entity, T data, string key) where T : class
        {
            entity.UpgradeOpen();

            DBDictionary extDict;
            if (entity.ExtensionDictionary.IsValid)
            {
                extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
            }
            else
            {
                entity.CreateExtensionDictionary();
                extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
            }

            string jsonData = JsonConvert.SerializeObject(data);
            var xrec = new Xrecord { Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, jsonData)) };

            if (extDict.Contains(key)) extDict.Remove(key);
            extDict.SetAt(key, xrec);
            tr.AddNewlyCreatedDBObject(xrec, true);
        }

        #endregion

        #region Guid 读写

        /// <summary>
        /// 从实体扩展字典读取 Guid
        /// </summary>
        public static Guid ReadGuid(Transaction tr, Entity entity)
        {
            if (!entity.ExtensionDictionary.IsValid) return Guid.Empty;

            var extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
            if (extDict == null || !extDict.Contains("EntityGuid")) return Guid.Empty;

            var xrec = tr.GetObject(extDict.GetAt("EntityGuid"), OpenMode.ForRead) as Xrecord;
            if (xrec?.Data?.AsArray().Length > 0)
            {
                TypedValue tv = xrec.Data.AsArray()[0];
                if (tv.TypeCode == (int)DxfCode.Text && Guid.TryParse(tv.Value.ToString(), out Guid guid))
                {
                    return guid;
                }
            }
            return Guid.Empty;
        }

        /// <summary>
        /// 将 Guid 写入实体扩展字典
        /// </summary>
        public static void WriteGuid(Transaction tr, Entity entity, Guid guid)
        {
            entity.UpgradeOpen();

            DBDictionary extDict;
            if (entity.ExtensionDictionary.IsValid)
            {
                extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
            }
            else
            {
                entity.CreateExtensionDictionary();
                extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
            }

            var xrec = new Xrecord { Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, guid.ToString())) };
            if (extDict.Contains("EntityGuid")) extDict.Remove("EntityGuid");
            extDict.SetAt("EntityGuid", xrec);
            tr.AddNewlyCreatedDBObject(xrec, true);
        }

        /// <summary>
        /// 确保实体拥有 Guid，如没有则创建并写入
        /// </summary>
        public static Guid EnsureGuid(Transaction tr, Entity entity)
        {
            var guid = ReadGuid(tr, entity);
            if (guid == Guid.Empty)
            {
                guid = Guid.NewGuid();
                WriteGuid(tr, entity, guid);
            }
            return guid;
        }

        #endregion

        #region 长字符串读写（分片存储，突破 XRecord 单条 2000 字符限制）

        private const int CHUNK_SIZE = 2000;

        /// <summary>
        /// 写入长字符串：分片为每段 CHUNK_SIZE 字符的 TypedValue[]
        /// 用于存储 Markdown 源码等大文本
        /// </summary>
        public static void WriteLongString(Transaction tr, Entity entity, string text, string key)
        {
            entity.UpgradeOpen();

            DBDictionary extDict;
            if (entity.ExtensionDictionary.IsValid)
            {
                extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
            }
            else
            {
                entity.CreateExtensionDictionary();
                extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForWrite) as DBDictionary;
            }

            // 分片
            var chunks = new List<TypedValue>();
            if (!string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < text.Length; i += CHUNK_SIZE)
                {
                    int len = Math.Min(CHUNK_SIZE, text.Length - i);
                    chunks.Add(new TypedValue((int)DxfCode.Text, text.Substring(i, len)));
                }
            }

            var xrec = new Xrecord { Data = new ResultBuffer(chunks.ToArray()) };

            if (extDict.Contains(key)) extDict.Remove(key);
            extDict.SetAt(key, xrec);
            tr.AddNewlyCreatedDBObject(xrec, true);
        }

        /// <summary>
        /// 读取长字符串：拼接所有 TypedValue 还原完整文本
        /// </summary>
        public static string ReadLongString(Transaction tr, Entity entity, string key)
        {
            if (!entity.ExtensionDictionary.IsValid) return null;

            var extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
            if (extDict == null || !extDict.Contains(key)) return null;

            var xrec = tr.GetObject(extDict.GetAt(key), OpenMode.ForRead) as Xrecord;
            if (xrec?.Data == null) return null;

            var sb = new StringBuilder();
            foreach (TypedValue tv in xrec.Data)
            {
                if (tv.TypeCode == (int)DxfCode.Text)
                    sb.Append(tv.Value.ToString());
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        /// <summary>
        /// 检查实体是否包含指定 key 的扩展字典数据
        /// </summary>
        public static bool HasKey(Transaction tr, Entity entity, string key)
        {
            if (!entity.ExtensionDictionary.IsValid) return false;
            var extDict = tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
            return extDict != null && extDict.Contains(key);
        }

        #endregion

        #region AnchorBolt 快捷方法

        public static AnchorBolt ReadAnchorBolt(Transaction tr, Entity entity)
        {
            return Read<AnchorBolt>(tr, entity, "AnchorBolt");
        }

        public static void WriteAnchorBolt(Transaction tr, Entity entity, AnchorBolt bolt)
        {
            Write(tr, entity, bolt, "AnchorBolt");
        }

        #endregion

        #region BaseData 快捷方法

        public static BaseData ReadBaseData(Transaction tr, Entity entity)
        {
            return Read<BaseData>(tr, entity, "BaseData");
        }

        public static void WriteBaseData(Transaction tr, Entity entity, BaseData data)
        {
            Write(tr, entity, data, "BaseData");
        }

        #endregion

        #region BoltData 快捷方法

        public static BoltData ReadBoltData(Transaction tr, Entity entity)
        {
            return Read<BoltData>(tr, entity, "BoltData");
        }

        public static void WriteBoltData(Transaction tr, Entity entity, BoltData data)
        {
            Write(tr, entity, data, "BoltData");
        }

        #endregion

        #region AxisData 快捷方法

        public static AxisData ReadAxisData(Transaction tr, Entity entity)
        {
            return Read<AxisData>(tr, entity, "AxisData");
        }

        public static void WriteAxisData(Transaction tr, Entity entity, AxisData data)
        {
            Write(tr, entity, data, "AxisData");
        }

        #endregion
    }
}
