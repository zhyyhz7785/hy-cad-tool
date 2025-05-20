using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
namespace HyCADTool.Tools
{
    public static partial class HyTool
    {
        /// <summary>
        /// 设置当前文字样式。
        /// </summary>
        /// <param name="textStyleName">文字样式的名称。</param>
        [CommandMethod("SetCurrentTextStyle")]
        public static void SetCurrentTextStyle(string textStyleName)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    TextStyleTable textStyleTable = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
                    if (textStyleTable.Has(textStyleName))
                    {
                        ObjectId textStyleId = textStyleTable[textStyleName];
                        db.Textstyle = textStyleId;
                    }
                    else
                    {
                        doc.Editor.WriteMessage($"\n文字样式 '{textStyleName}' 不存在.");
                    }
                    tr.Commit();
                }
            }
        }
        /// <summary>
        /// 设置当前标注样式。
        /// </summary>
        /// <param name="dimStyleName">标注样式的名称。</param>
        [CommandMethod("SetCurrentDimStyle")]
        public static void SetCurrentDimStyle(string dimStyleName)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DimStyleTable dimStyleTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
                    if (dimStyleTable.Has(dimStyleName))
                    {
                        ObjectId dimStyleId = dimStyleTable[dimStyleName];
                        db.Dimstyle = dimStyleId;
                    }
                    else
                    {
                        doc.Editor.WriteMessage($"\n标注样式 '{dimStyleName}' 不存在.");
                    }
                    tr.Commit();
                }
            }
        }
        /// <summary>
        /// 设置当前引线样式。
        /// </summary>
        /// <param name="leaderStyleName">引线样式的名称。</param>
        [CommandMethod("SetCurrentLeaderStyle")]
        public static void SetCurrentLeaderStyle(string leaderStyleName)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBDictionary mLeaderStyleDict = (DBDictionary)tr.GetObject(db.MLeaderStyleDictionaryId, OpenMode.ForRead);
                    if (mLeaderStyleDict.Contains(leaderStyleName))
                    {
                        ObjectId leaderStyleId = mLeaderStyleDict.GetAt(leaderStyleName);
                        db.MLeaderstyle = leaderStyleId;
                    }
                    else
                    {
                        doc.Editor.WriteMessage($"\n引线样式 '{leaderStyleName}' 不存在.");
                    }
                    tr.Commit();
                }
            }
        }
        /// <summary>
        /// 设置当前图层。
        /// </summary>
        /// <param name="layerName">图层的名称。</param>
        [CommandMethod("SetCurrentLayer")]
        public static void SetCurrentLayer(string layerName)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (layerTable.Has(layerName))
                    {
                        ObjectId layerId = layerTable[layerName];
                        db.Clayer = layerId;
                    }
                    else
                    {
                        doc.Editor.WriteMessage($"\n图层 '{layerName}' 不存在.");
                    }
                    tr.Commit();
                }
            }
        }
        public static void MakeMark(this Point3d pointBase, string str = "P",
            double diameter = 50, double TextHeight = 125, double x = 0, double y = 0)
        {
            Vector3d vec = new Vector3d(x, y, 0);
            Matrix3d matrix3dd = Matrix3d.Displacement(vec);
            pointBase = pointBase.TransformBy(matrix3dd);
            Circle circle = new Circle();
            DBText text = new DBText();
            text.Position = pointBase;
            text.TextString = str;
            text.Height = TextHeight;
            text.ColorIndex = 7;
            circle.Center = pointBase;
            circle.Diameter = diameter;
            circle.ColorIndex = 1;
            text.ToSpace();
            circle.ToSpace();
        }
        public static void ChangeEntitiesPropertyInDb<T>(this IEnumerable<T> ents, Action<T> act, Database db = null, string space = null) where T : Entity
        {
            db = db ?? Application.DocumentManager.MdiActiveDocument.Database;
            using (var trans = db.TransactionManager.StartTransaction())
            {
                foreach (var ent in ents)
                {
                    ent.Id.GetObject(OpenMode.ForWrite);
                    act.Invoke(ent);
                }
                trans.Commit();
            }
        }
    }
}
