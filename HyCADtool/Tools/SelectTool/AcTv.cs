using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HyCADTool.HelpClass
{
    public static class AcTv
    {
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
        public static IEnumerable<TypedValue> GetLayerFilter(this string layerName) => (new[] { new TypedValue((int)DxfCode.LayerName, layerName) });
        public static IEnumerable<TypedValue> GetLinetypeFilter(this string targetLinetype) => (new[] { new TypedValue((int)DxfCode.LinetypeName, targetLinetype) });
        public static IEnumerable<TypedValue> GetLineWeightFilter(this int lineWeight) => new[] { new TypedValue((int)DxfCode.LineWeight, (int)lineWeight) };
        public static IEnumerable<TypedValue> GetLineTypeScale(this double d) => new[] { new TypedValue((int)DxfCode.LinetypeScale, d) };
        #endregion

        public static IEnumerable<TypedValue> And(params IEnumerable<TypedValue>[] tvs)
        {
            yield return new TypedValue((int)DxfCode.Operator, "<AND");
            foreach (var item in tvs.SelectMany(t => t))
                yield return item;
            yield return new TypedValue((int)DxfCode.Operator, "AND>");
        }

        public static IEnumerable<TypedValue> Or(params IEnumerable<TypedValue>[] tvs)
        {
            yield return new TypedValue((int)DxfCode.Operator, "<OR");
            foreach (var item in tvs.SelectMany(t => t))
                yield return item;
            yield return new TypedValue((int)DxfCode.Operator, "OR>");
        }

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

        public static IEnumerable<TypedValue> GetfilterWithString(this string str) =>
            new[] { new TypedValue((int)DxfCode.Start, ChangeCadTypeName(str)) };

        public static SelectionFilter Getfilter(this IEnumerable<TypedValue> tvs) =>
            new SelectionFilter(tvs.ToArray());

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

        public static ObjectId[] FilterEntitiesBy<T>(this Document doc, ObjectId[] ids, Func<Entity, T> extractor, T expected) =>
            doc.FilterEntitiesBy(ids, extractor, val => EqualityComparer<T>.Default.Equals(val, expected));

        public static ObjectId[] GetEntitiesWithMatchingColorInputIds(this Color targetColor, Document doc, ObjectId[] ids = null) =>
            doc.FilterByColor(targetColor, ids);

        public static ObjectId[] GetEntitiesWithMatchingLinetype(this ObjectId targetLinetype, Document doc, ObjectId[] ids = null) =>
            doc.FilterByLinetype(targetLinetype, ids);

        public static ObjectId[] GetEntitiesWithMatchingTransparency(this int alpha, Document doc, ObjectId[] ids = null) =>
            doc.FilterByTransparency(alpha, ids);

        public static ObjectId[] FilterByColor(this Document doc, Color color, ObjectId[] ids = null) =>
            doc.FilterEntitiesBy(ids, e => e.GetTrueColor().ColorValue, color.ColorValue);

        public static ObjectId[] FilterByLinetype(this Document doc, ObjectId linetype, ObjectId[] ids = null) =>
            doc.FilterEntitiesBy(ids, e => e.GetTrueLinetype(), linetype);

        public static ObjectId[] FilterByTransparency(this Document doc, int alpha, ObjectId[] ids = null) =>
            doc.FilterEntitiesBy(ids, e => e.GetTrueTransparency(), alpha);

        public static Dictionary<string, (object Value, string Type)> GetFilterableProperties(this Entity entity)
        {
            var props = new Dictionary<string, (object, string)>();
            var type = entity.GetType();
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var t = prop.PropertyType;
                if (t == typeof(int) || t == typeof(double)  )
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
        public static Func<Entity, bool> BuildEntityPredicate(string propertyName, string op, string value)
        {
            return (Entity ent) =>
            {
                try
                {
                    var prop = ent.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                    if (prop == null) return false;

                    object actualValue = prop.GetValue(ent);
                    if (actualValue == null) return false;

                    Type type = prop.PropertyType;

                    if (type == typeof(double))
                    {
                        if (!double.TryParse(value, out double target)) return false;
                        double actual = (double)actualValue;
                        return Compare(actual, target, op);
                    }
                    else if (type == typeof(int))
                    {
                        if (!int.TryParse(value, out int target)) return false;
                        int actual = (int)actualValue;
                        return Compare(actual, target, op);
                    }
                    else if (type == typeof(string))
                    {
                        string actual = actualValue.ToString();
                        return Compare(actual, value, op);
                    }
                    else if (type == typeof(bool))
                    {
                        if (!bool.TryParse(value, out bool target)) return false;
                        bool actual = (bool)actualValue;
                        return Compare(actual, target, op);
                    }

                    return false;
                }
                catch
                {
                    return false;
                }
            };
        }

        private static bool Compare<T>(T actual, T target, string op) where T : IComparable
        {
            switch (op)
            {
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

    }
}
