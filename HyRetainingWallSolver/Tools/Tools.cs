using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyRetainingWallSolver.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace HyRetainingWallSolver.Tools
{
    public static partial class HyUtils
    {
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
        public static int FindSegmentIndex(List<WallBoundary> boundaries, LineSegment3d seg)
        {
            for (int i = 0; i < boundaries.Count; i++)
            {
                if ((boundaries[i].Start.IsEqualTo(seg.StartPoint, new Tolerance(1e-6, 1e-6)) &&
                     boundaries[i].End.IsEqualTo(seg.EndPoint, new Tolerance(1e-6, 1e-6))) ||
                    (boundaries[i].Start.IsEqualTo(seg.EndPoint, new Tolerance(1e-6, 1e-6)) &&
                     boundaries[i].End.IsEqualTo(seg.StartPoint, new Tolerance(1e-6, 1e-6))))
                {
                    return i;
                }
            }
            return -1;
        }
        public static (Polyline Polyline, Point3d ClosestPoint, double Parameter, LineSegment3d SelectedSegment)? GetPolylineInfo(string peoString)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                PromptEntityOptions peo = new PromptEntityOptions($"\n{peoString}");
                peo.SetRejectMessage("\n请选择一个多段线对象。");
                peo.AddAllowedClass(typeof(Polyline), true);
                peo.AllowNone = true;
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                    return null;
                ObjectId plId = per.ObjectId;
                Point3d selPt = per.PickedPoint;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline pl = tr.GetObject(plId, OpenMode.ForRead) as Polyline;
                    if (pl == null)
                    {
                        ed.WriteMessage("\n选择的对象不是有效的多段线。\n");
                        return null;
                    }
                    Point3d closestPoint = pl.GetClosestPointTo(selPt, false);
                    double parameter = pl.GetParameterAtPoint(closestPoint);
                    int segmentIndex = GetSegmentIndexAt(pl, parameter);
                    var selectedSegment = pl.GetLineSegmentAt(segmentIndex);
                    tr.Commit();
                    return (pl, closestPoint, parameter, selectedSegment);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n出错：{ex.Message}\n");
                return null;
            }
        }
        public static int GetSegmentIndexAt(Polyline pline, double param)
        {
            int count = pline.NumberOfVertices;
            for (int i = 0; i < count; i++)
            {
                double startParam = i;
                double endParam = i + 1;
                if (param >= startParam && param < endParam)
                {
                    return i;
                }
            }
            return count - 1;
        }
        // ... 前略
        // ... 前略
        public static void DrawFixedSymbol(Line baseLine, BlockTableRecord btr, Transaction tr, double scale)
        {
            Vector3d direction = (baseLine.EndPoint - baseLine.StartPoint).GetNormal();
            Vector3d normal = direction.GetPerpendicularVector().GetNormal();
            double spacing = scale;
            double totalLength = baseLine.Length;
            int count = (int)(totalLength / spacing);
            for (int i = 0; i <= count; i++)
            {
                Point3d origin = baseLine.StartPoint + direction.MultiplyBy(i * spacing);
                Line symbol = new Line(origin, origin + normal.MultiplyBy(scale));
                symbol.Layer = "HY_Fixed";
                symbol.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);
                btr.AppendEntity(symbol);
                tr.AddNewlyCreatedDBObject(symbol, true);
            }
        }
        public static void DrawHingedSymbol(Line baseLine, BlockTableRecord btr, Transaction tr, double scale)
        {
            Vector3d direction = (baseLine.EndPoint - baseLine.StartPoint).GetNormal();
            Vector3d normal = direction.GetPerpendicularVector().GetNormal();
            double spacing = 2 * scale;
            double totalLength = baseLine.Length;
            int count = (int)(totalLength / spacing);
            for (int i = 0; i <= count; i++)
            {
                Point3d center = baseLine.StartPoint + direction.MultiplyBy(i * spacing);
                center += normal.MultiplyBy(scale * 0.5);
                Circle circ = new Circle(center, Vector3d.ZAxis, scale * 0.5);
                circ.Layer = "HY_Hinged";
                circ.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
                btr.AppendEntity(circ);
                tr.AddNewlyCreatedDBObject(circ, true);
            }
        }
    }
}
