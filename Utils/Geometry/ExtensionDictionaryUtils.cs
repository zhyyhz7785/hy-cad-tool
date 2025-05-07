using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Newtonsoft.Json;

namespace CadUtils
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


    }
}