using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions
{
    /// <summary>
    /// 过滤扩展方法集
    /// 从旧项目 HyCADtool/Tools/SelectTool/AcTv.cs 迁移
    /// 提供实体筛选、属性过滤等功能
    /// </summary>
    public static class FilterExtensions
    {
        #region 类型名称转换

        /// <summary>
        /// 将 C# 类型名转换为 AutoCAD DXF 类型名
        /// </summary>
        public static string ChangeCadTypeName(this string str)
        {
            switch (str)
            {
                case "Arc": return "ARC";
                case "Circle": return "CIRCLE";
                case "Ellipse": return "ELLIPSE";
                case "Leader": return "LEADER";
                case "Line": return "LINE";
                case "Polyline": return "LWPOLYLINE";
                case "Spline": return "SPLINE";
                case "Xline": return "XLINE";
                case "BlockReference": return "INSERT";
                case "Hatch": return "HATCH";
                case "DBPoint": return "POINT";
                case "DBText": return "TEXT";
                case "Dimension": return "DIMENSION";
                case "MLeader": return "MULTILEADER";
                case "MText": return "MTEXT";
                case "Region": return "REGION";
                default: return str;
            }
        }

        #endregion

        #region 过滤器创建

        /// <summary>
        /// 根据类型名称字符串创建过滤器 TypedValue
        /// </summary>
        public static IEnumerable<TypedValue> GetfilterWithString(this string str) =>
            new[] { new TypedValue((int)DxfCode.Start, ChangeCadTypeName(str)) };

        /// <summary>
        /// 根据图层名称创建过滤器
        /// </summary>
        public static IEnumerable<TypedValue> GetLayerFilter(this string layerName) =>
            new[] { new TypedValue((int)DxfCode.LayerName, layerName) };

        /// <summary>
        /// 根据线宽创建过滤器
        /// </summary>
        public static IEnumerable<TypedValue> GetLineWeightFilter(this int lineWeight) =>
            new[] { new TypedValue((int)DxfCode.LineWeight, lineWeight) };

        /// <summary>
        /// 将 TypedValue 集合转换为 SelectionFilter
        /// </summary>
        public static SelectionFilter Getfilter(this IEnumerable<TypedValue> tvs) =>
            new SelectionFilter(tvs.ToArray());

        /// <summary>
        /// 使用过滤器选择所有实体
        /// </summary>
        public static ObjectId[] SelectWithFilterAll(this SelectionFilter filter)
        {
            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return new ObjectId[0];

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
                    ed.WriteMessage($"选中了 {res.Value.Count} 个满足过滤条件的图形对象。\n");
                    var ids = res.Value.GetObjectIds();
                    return ids;
                }
                else
                {
                    ed.WriteMessage("未能选中任何对象。\n");
                    return new ObjectId[0];
                }
            }
        }

        #endregion

        #region 获取实际属性（处理 ByLayer/ByBlock）

        /// <summary>
        /// 获取实体的实际颜色（处理 ByLayer 和 ByBlock）
        /// </summary>
        public static Color GetTrueColor(this Entity ent)
        {
            if (ent.Color.IsByLayer)
            {
                using (var tr = ent.Database.TransactionManager.StartTransaction())
                {
                    var layer = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                    tr.Commit();
                    return layer.Color;
                }
            }
            else if (ent.Color.IsByBlock && ent is BlockReference br)
            {
                return br.Color;
            }
            return ent.Color;
        }

        /// <summary>
        /// 获取实体的实际线宽（处理 ByLayer 和 ByBlock）
        /// </summary>
        public static int GetTrueLineWeight(this Entity ent)
        {
            if (ent.LineWeight == LineWeight.ByLayer)
            {
                using (var tr = ent.Database.TransactionManager.StartTransaction())
                {
                    var layer = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                    tr.Commit();
                    return (int)layer.LineWeight;
                }
            }
            else if (ent.LineWeight == LineWeight.ByBlock && ent is BlockReference br)
            {
                return (int)br.LineWeight;
            }
            return (int)ent.LineWeight;
        }

        /// <summary>
        /// 获取实体的实际线型（处理 ByLayer 和 ByBlock）
        /// </summary>
        public static ObjectId GetTrueLinetype(this Entity ent)
        {
            if (ent.Linetype == "ByLayer")
            {
                using (var tr = ent.Database.TransactionManager.StartTransaction())
                {
                    var layer = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                    tr.Commit();
                    return layer.LinetypeObjectId;
                }
            }
            else if (ent.Linetype == "ByBlock" && ent is BlockReference br)
            {
                return br.LinetypeId;
            }
            return ent.LinetypeId;
        }

        /// <summary>
        /// 获取实体的实际透明度（处理 ByLayer 和 ByBlock）
        /// </summary>
        public static int GetTrueTransparency(this Entity ent)
        {
            try
            {
                if (ent.Transparency.IsByAlpha)
                    return ent.Transparency.Alpha;

                if (ent.Transparency.IsByLayer)
                {
                    using (var tr = ent.Database.TransactionManager.StartTransaction())
                    {
                        var layer = (LayerTableRecord)tr.GetObject(ent.LayerId, OpenMode.ForRead);
                        tr.Commit();
                        return layer.Transparency.IsByAlpha ? layer.Transparency.Alpha : 255;
                    }
                }
                if (ent.Transparency.IsByBlock && ent is BlockReference br)
                {
                    return br.Transparency.Alpha;
                }
            }
            catch
            {
                return 255;
            }
            return 255;
        }

        #endregion

        #region 实体筛选方法

        /// <summary>
        /// 根据提取器和谓词函数筛选实体（泛型版本）
        /// </summary>
        public static ObjectId[] FilterEntitiesBy<T>(this Document doc, ObjectId[] ids, Func<Entity, T> extractor, Func<T, bool> predicate)
        {
            List<ObjectId> result = new List<ObjectId>();
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                if (ids == null || ids.Length == 0)
                {
                    var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    ids = ms.Cast<ObjectId>().ToArray();
                }
                foreach (var id in ids)
                {
                    if (tr.GetObject(id, OpenMode.ForRead) is Entity ent)
                    {
                        T val = extractor(ent);
                        if (predicate(val))
                            result.Add(id);
                    }
                }
                tr.Commit();
            }
            return result.ToArray();
        }

        /// <summary>
        /// 根据提取器和期望值筛选实体（简化版本）
        /// </summary>
        public static ObjectId[] FilterEntitiesBy<T>(this Document doc, ObjectId[] ids, Func<Entity, T> extractor, T expected) =>
            doc.FilterEntitiesBy(ids, extractor, val => EqualityComparer<T>.Default.Equals(val, expected));

        /// <summary>
        /// 根据谓词函数筛选实体（直接版本）
        /// </summary>
        public static ObjectId[] FilterEntitiesBy(this Document doc, Func<Entity, bool> predicate, ObjectId[] inputIds)
        {
            var result = new List<ObjectId>();
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in inputIds)
                {
                    if (!(tr.GetObject(id, OpenMode.ForRead) is Entity ent))
                        continue;

                    try
                    {
                        if (predicate(ent))
                            result.Add(id);
                    }
                    catch
                    {
                        // 忽略无效的属性或转换错误
                    }
                }

                tr.Commit();
            }

            return result.ToArray();
        }

        #endregion

        #region 按属性筛选

        /// <summary>
        /// 根据颜色筛选实体
        /// </summary>
        public static ObjectId[] GetEntitiesWithMatchingColorInputIds(this Color targetColor, Document doc, ObjectId[] ids = null) =>
            doc.FilterByColor(targetColor, ids);

        /// <summary>
        /// 根据线型筛选实体
        /// </summary>
        public static ObjectId[] GetEntitiesWithMatchingLinetype(this ObjectId targetLinetype, Document doc, ObjectId[] ids = null) =>
            doc.FilterByLinetype(targetLinetype, ids);

        /// <summary>
        /// 根据透明度筛选实体
        /// </summary>
        public static ObjectId[] GetEntitiesWithMatchingTransparency(this int alpha, Document doc, ObjectId[] ids = null) =>
            doc.FilterByTransparency(alpha, ids);

        /// <summary>
        /// 按颜色筛选（内部方法）
        /// </summary>
        public static ObjectId[] FilterByColor(this Document doc, Color color, ObjectId[] ids = null) =>
            doc.FilterEntitiesBy(ids, e => e.GetTrueColor().ColorValue, color.ColorValue);

        /// <summary>
        /// 按线型筛选（内部方法）
        /// </summary>
        public static ObjectId[] FilterByLinetype(this Document doc, ObjectId linetype, ObjectId[] ids = null) =>
            doc.FilterEntitiesBy(ids, e => e.GetTrueLinetype(), linetype);

        /// <summary>
        /// 按透明度筛选（内部方法）
        /// </summary>
        public static ObjectId[] FilterByTransparency(this Document doc, int alpha, ObjectId[] ids = null) =>
            doc.FilterEntitiesBy(ids, e => e.GetTrueTransparency(), alpha);

        #endregion

        #region 可筛选属性

        /// <summary>
        /// 获取实体的可筛选属性字典（仅 int 和 double 类型）
        /// </summary>
        public static Dictionary<string, (object Value, string Type)> GetFilterableProperties(this Entity entity)
        {
            var props = new Dictionary<string, (object, string)>();
            var type = entity.GetType();
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var t = prop.PropertyType;
                if (t == typeof(int) || t == typeof(double))
                {
                    try
                    {
                        var val = prop.GetValue(entity);
                        if (val != null)
                            props[prop.Name] = (val, t.Name);
                    }
                    catch { }
                }
            }
            return props;
        }

        #endregion

        #region 比较方法

        /// <summary>
        /// 泛型比较方法（用于表达式筛选）
        /// </summary>
        public static bool Compare<T>(T actual, T target, string op) where T : IComparable
        {
            switch (op)
            {
                case "==":
                case "=": return actual.CompareTo(target) == 0;
                case "!=": return actual.CompareTo(target) != 0;
                case ">": return actual.CompareTo(target) > 0;
                case "<": return actual.CompareTo(target) < 0;
                case ">=": return actual.CompareTo(target) >= 0;
                case "<=": return actual.CompareTo(target) <= 0;
                case "contains": return actual.ToString().Contains(target.ToString());
                default: return false;
            }
        }

        #endregion
    }
}
