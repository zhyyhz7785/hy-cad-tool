using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Models;
using Newtonsoft.Json;
using System;

namespace HyCADTool.Utilities
{
    public static class ExtensionDictionaryUtils
    {
        public static Guid ReadGuidFromExtensionDictionary(Transaction tr, Entity entity, Editor ed)
        {
            if (entity.ExtensionDictionary.IsValid &&
                tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead) is DBDictionary extDict &&
                extDict.Contains("EntityGuid"))
            {
                Xrecord xrec = tr.GetObject(extDict.GetAt("EntityGuid"), OpenMode.ForRead) as Xrecord;
                if (xrec?.Data?.AsArray().Length > 0)
                {
                    TypedValue tv = xrec.Data.AsArray()[0];
                    if (tv.TypeCode == (int)DxfCode.Text && Guid.TryParse(tv.Value.ToString(), out Guid guid))
                    {
                        return guid;
                    }
                }
            }
            return Guid.Empty;
        }

        public static void WriteGuidToExtensionDictionary(Transaction tr, Entity entity, Guid guid, Editor ed)
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

            Xrecord xrec = new Xrecord { Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, guid.ToString())) };
            if (extDict.Contains("EntityGuid")) extDict.Remove("EntityGuid");
            extDict.SetAt("EntityGuid", xrec);
            tr.AddNewlyCreatedDBObject(xrec, true);
        }

        public static T ReadFromExtensionDictionary<T>(Transaction tr, Entity entity, string key, Editor ed) where T : class
        {
            if (entity.ExtensionDictionary.IsValid &&
                tr.GetObject(entity.ExtensionDictionary, OpenMode.ForRead) is DBDictionary extDict &&
                extDict.Contains(key))
            {
                Xrecord xrec = tr.GetObject(extDict.GetAt(key), OpenMode.ForRead) as Xrecord;
                if (xrec?.Data?.AsArray().Length > 0)
                {
                    TypedValue tv = xrec.Data.AsArray()[0];
                    if (tv.TypeCode == (int)DxfCode.Text)
                    {
                        string jsonData = tv.Value.ToString();
                        return JsonConvert.DeserializeObject<T>(jsonData);
                    }
                }
            }
            return null;
        }

        public static void WriteToExtensionDictionary<T>(Transaction tr, Entity entity, T data, string key, Editor ed) where T : class
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
            Xrecord xrec = new Xrecord { Data = new ResultBuffer(new TypedValue((int)DxfCode.Text, jsonData)) };
            if (extDict.Contains(key)) extDict.Remove(key);
            extDict.SetAt(key, xrec);
            tr.AddNewlyCreatedDBObject(xrec, true);
        }

        public static BaseData ReadBaseDataFromExtensionDictionary(Transaction tr, Polyline pline, Editor ed)
        {
            return ReadFromExtensionDictionary<BaseData>(tr, pline, "BaseData", ed);
        }

        public static void WriteBaseDataToExtensionDictionary(Transaction tr, Polyline pline, BaseData data, Editor ed)
        {
            WriteToExtensionDictionary(tr, pline, data, "BaseData", ed);
        }

        public static BoltData ReadBoltDataFromExtensionDictionary(Transaction tr, Circle circle, Editor ed)
        {
            return ReadFromExtensionDictionary<BoltData>(tr, circle, "BoltData", ed);
        }

        public static void WriteBoltDataToExtensionDictionary(Transaction tr, Circle circle, BoltData data, Editor ed)
        {
            WriteToExtensionDictionary(tr, circle, data, "BoltData", ed);
        }

        public static AnchorBolt ReadAnchorBoltFromExtensionDictionary(Transaction tr, Circle circle, Editor ed)
        {
            return ReadFromExtensionDictionary<AnchorBolt>(tr, circle, "AnchorBolt", ed);
        }

        public static void WriteAnchorBoltToExtensionDictionary(Transaction tr, Circle circle, AnchorBolt data, Editor ed)
        {
            WriteToExtensionDictionary(tr, circle, data, "AnchorBolt", ed);
        }

        public static AxisData ReadAxisDataFromExtensionDictionary(Transaction tr, Line line, Editor ed)
        {
            return ReadFromExtensionDictionary<AxisData>(tr, line, "AxisData", ed);
        }

        public static void WriteAxisDataToExtensionDictionary(Transaction tr, Line line, AxisData data, Editor ed)
        {
            WriteToExtensionDictionary(tr, line, data, "AxisData", ed);
        }
    }
}