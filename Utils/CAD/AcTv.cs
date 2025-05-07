using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
namespace CadUtils
{
    public static class AcTv
    {
        // CAD 查询数据命令
        #region ACTV.TvAll 字段
        public static IEnumerable<TypedValue> TvAll => Or(
            Arc, Circle, Ellipse, Leader, Line, Polyline, Spline, Xline,
            BlockReference, Hatch, DBPoint, DBText, Dimension, MLeader, MText, Region
        );
        #endregion
        #region 使用属性封装单个实体
        public static IEnumerable<TypedValue> All => new[] { new TypedValue(410, "Model") };
        public static IEnumerable<TypedValue> Arc => new[] { new TypedValue((int)DxfCode.Start, "ARC") };
        public static IEnumerable<TypedValue> Circle => new[] { new TypedValue((int)DxfCode.Start, "CIRCLE") };
        public static IEnumerable<TypedValue> Ellipse => new[] { new TypedValue((int)DxfCode.Start, "ELLIPSE") };
        public static IEnumerable<TypedValue> Leader => new[] { new TypedValue((int)DxfCode.Start, "LEADER") };
        public static IEnumerable<TypedValue> Line => new[] { new TypedValue((int)DxfCode.Start, "LINE") };
        public static IEnumerable<TypedValue> Polyline => new[] { new TypedValue((int)DxfCode.Start, "LWPOLYLINE") };
        public static IEnumerable<TypedValue> Spline => new[] { new TypedValue((int)DxfCode.Start, "SPLINE") };
        public static IEnumerable<TypedValue> Xline => new[] { new TypedValue((int)DxfCode.Start, "XLINE") };
        public static IEnumerable<TypedValue> BlockReference => new[] { new TypedValue((int)DxfCode.Start, "INSERT") };
        public static IEnumerable<TypedValue> Hatch => new[] { new TypedValue((int)DxfCode.Start, "HATCH") };
        public static IEnumerable<TypedValue> DBPoint => new[] { new TypedValue((int)DxfCode.Start, "POINT") };
        public static IEnumerable<TypedValue> DBText => new[] { new TypedValue((int)DxfCode.Start, "TEXT") };
        public static IEnumerable<TypedValue> Dimension => new[] { new TypedValue((int)DxfCode.Start, "DIMENSION") };
        public static IEnumerable<TypedValue> MLeader => new[] { new TypedValue((int)DxfCode.Start, "MULTILEADER") };
        public static IEnumerable<TypedValue> MText => new[] { new TypedValue((int)DxfCode.Start, "MTEXT") };
        public static IEnumerable<TypedValue> Region => new[] { new TypedValue((int)DxfCode.Start, "REGION") };
        //AutoCAD图形均有公共属性
        //图层过滤器  1
        public static IEnumerable<TypedValue> GetLayerFilter(this string layerName) => (new[] { new TypedValue((int)DxfCode.LayerName, layerName) });
        public static IEnumerable<TypedValue> GetLinetypeFilter(this string targetLinetype) => (new[] { new TypedValue((int)DxfCode.LinetypeName, targetLinetype) });
        //线宽过滤器  4
        public static IEnumerable<TypedValue> GetLineWeightFilter(this int lineWeight) => new[] { new TypedValue((int)DxfCode.LineWeight, (int)lineWeight) };
        //线型比例过滤器  5
        public static IEnumerable<TypedValue> GetLineTypeScale(this double d) => new[] { new TypedValue((int)DxfCode.LinetypeScale, d) };
        #endregion
        public static Color GetTrueColor(this Entity entity)
        {
            Color color = entity.Color;
            using (Transaction trans = entity.Database.TransactionManager.StartTransaction())
            {
                if (color.IsByLayer)
                {
                    LayerTableRecord layerRecord = trans.GetObject(entity.LayerId, OpenMode.ForRead) as LayerTableRecord;
                    color = layerRecord.Color;
                }
                else if (color.IsByBlock)
                {
                    BlockReference blockRef = entity as BlockReference;
                    if (blockRef != null)
                    {
                        color = blockRef.Color;
                    }
                }
                trans.Commit();
            }
            return color;
        }
        public static int GetTrueLineWeight(this Entity entity)
        {
            int lineWeight = (int)entity.LineWeight;

            using (Transaction trans = entity.Database.TransactionManager.StartTransaction())
            {
                if (entity.LineWeight == LineWeight.ByLayer)
                {
                    LayerTableRecord layerRecord = trans.GetObject(entity.LayerId, OpenMode.ForRead) as LayerTableRecord;
                    lineWeight = (int)layerRecord.LineWeight;
                }
                else if (entity.LineWeight == LineWeight.ByBlock)
                {
                    BlockReference blockRef = entity as BlockReference;
                    if (blockRef != null)
                    {
                        lineWeight = (int)blockRef.LineWeight;
                    }
                }
                trans.Commit();
            }
            return lineWeight;
        }
        public static ObjectId GetTrueLinetype(this Entity entity)
        {
            ObjectId linetype = entity.LinetypeId;
            using (Transaction trans = entity.Database.TransactionManager.StartTransaction())
            {
                if (entity.Linetype == "ByLayer")
                {
                    LayerTableRecord layerRecord = trans.GetObject(entity.LayerId, OpenMode.ForRead) as LayerTableRecord;
                    linetype = layerRecord.LinetypeObjectId;
                }
                else if (entity.Linetype == "ByBlock")
                {
                    BlockReference blockRef = entity as BlockReference;
                    if (blockRef != null)
                    {
                        linetype = blockRef.LinetypeId;
                    }
                }
                trans.Commit();
            }
            return linetype;
        }
        public static int GetTrueTransparency(this Entity entity)
        {
            int transparency = 255; // 默认值为完全不透明
            using (Transaction trans = entity.Database.TransactionManager.StartTransaction())
            {
                try
                {
                    if (entity.Transparency.IsByAlpha)
                    {
                        transparency = entity.Transparency.Alpha;
                    }
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.ErrorStatus == Autodesk.AutoCAD.Runtime.ErrorStatus.InvalidKey)
                {
                    // 忽略异常并使用默认值
                }
                if (entity.Transparency.IsByLayer)
                {
                    LayerTableRecord layerRecord = trans.GetObject(entity.LayerId, OpenMode.ForRead) as LayerTableRecord;
                    try
                    {
                        if (layerRecord.Transparency.IsByAlpha)
                        {
                            transparency = layerRecord.Transparency.Alpha;
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.ErrorStatus == Autodesk.AutoCAD.Runtime.ErrorStatus.InvalidKey)
                    {
                        // 忽略异常并使用默认值
                    }
                }
                else if (entity.Transparency.IsByBlock)
                {
                    BlockReference blockRef = entity as BlockReference;
                    if (blockRef != null)
                    {
                        try
                        {
                            if (blockRef.Transparency.IsByAlpha)
                            {
                                transparency = blockRef.Transparency.Alpha;
                            }
                        }
                        catch (Autodesk.AutoCAD.Runtime.Exception ex) when (ex.ErrorStatus == Autodesk.AutoCAD.Runtime.ErrorStatus.InvalidKey)
                        {
                            // 忽略异常并使用默认值
                        }
                    }
                }
                trans.Commit();
            }
            return transparency;
        }
        //匹配颜色选择  2
        public static ObjectId[] GetEntitiesWithMatchingColor(this Color targetColor, Document doc, PromptSelectionResult selectionResult = null)
        {
            List<ObjectId> matchingEntities = new List<ObjectId>();
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                IEnumerable<ObjectId> entityIds;
                if (selectionResult == null || selectionResult.Status != PromptStatus.OK)
                {
                    // 获取块表记录模型空间
                    BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    entityIds = btr.Cast<ObjectId>();
                }
                else
                {
                    entityIds = selectionResult.Value.GetObjectIds();
                }
                foreach (ObjectId id in entityIds)
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        Color color = ent.GetTrueColor();
                        if (color.ColorValue == targetColor.ColorValue)
                        {
                            matchingEntities.Add(id);
                        }
                    }
                }
                trans.Commit();
            }
            return matchingEntities.ToArray();
        }
        public static ObjectId[] GetEntitiesWithMatchingColorInputIds(this Color targetColor, Document doc, ObjectId[] entityIds = null)
        {
            List<ObjectId> matchingEntities = new List<ObjectId>();
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                if (entityIds == null || entityIds.Length == 0)
                {
                    // 获取块表记录模型空间
                    BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    entityIds = btr.Cast<ObjectId>().ToArray();
                }
                foreach (ObjectId id in entityIds)
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        Color color = ent.GetTrueColor();
                        if (color.ColorValue == targetColor.ColorValue)
                        {
                            matchingEntities.Add(id);
                        }
                    }
                }
                trans.Commit();
            }
            return matchingEntities.ToArray();
        }
        public static ObjectId[] GetEntitiesWithMatchingColorParallel(this Color targetColor, Document doc, ObjectId[] entityIds = null)
        {
            List<ObjectId> matchingEntities = new List<ObjectId>();
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                if (entityIds == null || entityIds.Length == 0)
                {
                    // 获取块表记录模型空间
                    BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    entityIds = btr.Cast<ObjectId>().ToArray();
                }
                object lockObject = new object();
                Parallel.ForEach(entityIds, id =>
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        Color color = ent.GetTrueColor();
                        if (color.ColorValue == targetColor.ColorValue)
                        {
                            lock (lockObject)
                            {
                                matchingEntities.Add(id);
                            }
                        }
                    }
                });
                trans.Commit();
            }
            return matchingEntities.ToArray();
        }
        //匹配线型选择  3
        public static ObjectId[] GetEntitiesWithMatchingLinetype(this ObjectId targetLinetype, Document doc, PromptSelectionResult selectionResult = null)
        {
            List<ObjectId> matchingEntities = new List<ObjectId>();
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                IEnumerable<ObjectId> entityIds;
                if (selectionResult == null || selectionResult.Status != PromptStatus.OK)
                {
                    BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    entityIds = btr.Cast<ObjectId>();
                }
                else
                {
                    entityIds = selectionResult.Value.GetObjectIds();
                }
                foreach (ObjectId id in entityIds)
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        ObjectId linetype = ent.GetTrueLinetype();
                        if (linetype == targetLinetype)
                        {
                            matchingEntities.Add(id);
                        }
                    }
                }
                trans.Commit();
            }
            return matchingEntities.ToArray();
        }
        public static ObjectId[] GetEntitiesWithMatchingLinetype(this ObjectId targetLinetype, Document doc, ObjectId[] objectIds = null)
        {
            List<ObjectId> matchingEntities = new List<ObjectId>();
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                IEnumerable<ObjectId> entityIds;
                if (objectIds == null || objectIds.Length == 0)
                {
                    BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    entityIds = btr.Cast<ObjectId>();
                }
                else
                {
                    entityIds = objectIds;
                }
                foreach (ObjectId id in entityIds)
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        ObjectId linetype = ent.GetTrueLinetype();
                        if (linetype == targetLinetype)
                        {
                            matchingEntities.Add(id);
                        }
                    }
                }
                trans.Commit();
            }
            return matchingEntities.ToArray();
        }
        public static ObjectId[] GetEntitiesWithMatchingTransparency(this int targetTransparency, Document doc, PromptSelectionResult selectionResult = null)
        {
            List<ObjectId> matchingEntities = new List<ObjectId>();
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                IEnumerable<ObjectId> entityIds;
                if (selectionResult == null || selectionResult.Status != PromptStatus.OK)
                {
                    BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    entityIds = btr.Cast<ObjectId>();
                }
                else
                {
                    entityIds = selectionResult.Value.GetObjectIds();
                }
                foreach (ObjectId id in entityIds)
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        int transparency = ent.GetTrueTransparency();
                        if (transparency == targetTransparency)
                        {
                            matchingEntities.Add(id);
                        }
                    }
                }
                trans.Commit();
            }
            return matchingEntities.ToArray();
        }
        public static ObjectId[] GetEntitiesWithMatchingTransparency(this int targetTransparency, Document doc, ObjectId[] objectIds = null)
        {
            List<ObjectId> matchingEntities = new List<ObjectId>();
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                IEnumerable<ObjectId> entityIds;
                if (objectIds == null || objectIds.Length == 0)
                {
                    BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
                    entityIds = btr.Cast<ObjectId>();
                }
                else
                {
                    entityIds = objectIds;
                }
                foreach (ObjectId id in entityIds)
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent != null)
                    {
                        int transparency = ent.Transparency.Alpha;
                        if (transparency == targetTransparency)
                        {
                            matchingEntities.Add(id);
                        }
                    }
                }
                trans.Commit();
            }
            return matchingEntities.ToArray();
        }
        #region ACTV 方法
        public static string ChangeCadTypeName(this string str)
        {
            switch (str)
            {
                case "Arc":
                    return "ARC";
                case "Circle":
                    return "CIRCLE";
                case "Ellipse":
                    return "ELLIPSE";
                case "Leader":
                    return "LEADER";
                case "Line":
                    return "LINE";
                case "Polyline":
                    return "LWPOLYLINE";
                case "Spline":
                    return "SPLINE";
                case "Xline":
                    return "XLINE";
                case "BlockReference":
                    return "INSERT";
                case "Hatch":
                    return "HATCH";
                case "DBPoint":
                    return "POINT";
                case "DBText":
                    return "TEXT";
                case "Dimension":
                    return "DIMENSION";
                case "MLeader":
                    return "MULTILEADER";
                case "MText":
                    return "MTEXT";
                case "Region":
                    return "REGION";
                default:
                    return str;
            }
        }
        public static string GetDxfTypeName(IEnumerable<TypedValue> tvs) => tvs.First().Value.ToString();
        public static IEnumerable<TypedValue> GetfilterWithString(this string str) =>
            new[] { new TypedValue((int)DxfCode.Start, ChangeCadTypeName(str)) };
        public static SelectionFilter Getfilter(this IEnumerable<TypedValue> tv) =>
            new SelectionFilter(tv.ToArray());
        #endregion
        #region And Or 操作符
        //public static IEnumerable<TypedValue> Or( params IEnumerable<TypedValue>[] tvss)
        //{
        //    yield return new TypedValue((int)DxfCode.Operator, "<OR");
        //    foreach (var tvs in tvss.SelectMany(tvs => tvs))
        //    {
        //        yield return tvs;
        //    }
        //    yield return new TypedValue((int)DxfCode.Operator, "OR>");
        //}
        //public static IEnumerable<TypedValue> And(params IEnumerable<TypedValue>[] tvss)
        //{
        //    yield return new TypedValue((int)DxfCode.Operator, "<And");
        //    foreach (var tvs in tvss.SelectMany(tvs => tvs))
        //    {
        //        yield return tvs;
        //    }
        //    yield return new TypedValue((int)DxfCode.Operator, "And>");
        //}
        //public static IEnumerable<TypedValue> AddPropertyDetail(this IEnumerable<TypedValue> actv, IEnumerable<TypedValue> tvss)
        //{
        //    foreach (var tv in actv.Concat(tvss))
        //    {
        //        yield return tv;
        //    }
        //}
        //public static IEnumerable<TypedValue> Greater(object d)
        //{
        //    yield return new TypedValue((int)DxfCode.Operator, ">");
        //    yield return new TypedValue((int)DxfCode.Real, $"{d}");
        //}
        #endregion
        #region And Or 操作符
        /// <summary>
        /// 创建 "OR" 逻辑运算符，用于过滤器
        /// </summary>
        /// <param name="tvss">多个 TypedValue 集合</param>
        /// <returns>包含 "OR" 运算符的 TypedValue 集合</returns>
        public static IEnumerable<TypedValue> Or(params IEnumerable<TypedValue>[] tvss)
        {
            yield return new TypedValue((int)DxfCode.Operator, "<OR");
            foreach (var tvs in tvss.SelectMany(tvs => tvs))
            {
                yield return tvs;
            }
            yield return new TypedValue((int)DxfCode.Operator, "OR>");
        }
        /// <summary>
        /// 创建 "AND" 逻辑运算符，用于过滤器
        /// </summary>
        /// <param name="tvss">多个 TypedValue 集合</param>
        /// <returns>包含 "AND" 运算符的 TypedValue 集合</returns>
        public static IEnumerable<TypedValue> And(params IEnumerable<TypedValue>[] tvss)
        {
            yield return new TypedValue((int)DxfCode.Operator, "<And");
            foreach (var tvs in tvss.SelectMany(tvs => tvs))
            {
                yield return tvs;
            }
            yield return new TypedValue((int)DxfCode.Operator, "And>");
        }
        /// <summary>
        /// 将额外的属性详细信息添加到现有的 TypedValue 集合中
        /// </summary>
        /// <param name="actv">现有的 TypedValue 集合</param>
        /// <param name="tvss">要添加的 TypedValue 集合</param>
        /// <returns>合并后的 TypedValue 集合</returns>
        public static IEnumerable<TypedValue> AddPropertyDetail(this IEnumerable<TypedValue> actv, IEnumerable<TypedValue> tvss)
        {
            return actv.Concat(tvss);
        }
        /// <summary>
        /// 创建一个 "Greater Than" 逻辑运算符，用于过滤器
        /// </summary>
        /// <param name="d">比较值</param>
        /// <returns>包含 "Greater Than" 运算符的 TypedValue 集合</returns>
        public static IEnumerable<TypedValue> Greater(object d)
        {
            yield return new TypedValue((int)DxfCode.Operator, ">");
            yield return new TypedValue((int)DxfCode.Real, d);
        }
        #endregion
        public static string GetTypeName(Entity entity) => entity.GetType().Name;
    }
}
