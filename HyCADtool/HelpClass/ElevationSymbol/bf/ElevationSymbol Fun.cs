//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using System;
//using System.Collections.Generic;
//using HyCADTool.Tools;
//using static HyCADTool.Config.BaseConfig;
//using HyCADTool.Config;
//using Autodesk.AutoCAD.ApplicationServices;
//namespace HyCADTool.HelpClass.ElevationSymbol
//{
//    public partial class ElevationSymbol : DrawJig, IElevationSymbol
//    {
//        // 移除 AdjustPointsByState 和 AdjustTextByState，已迁移到 ElevationGeometry 和 ElevationTextManager
//        public static void RotateAllByAngle(double angleDegrees, Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> dictionary)
//        {
//            using (Transaction tr = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.StartTransaction())
//            {
//                foreach (var entry in dictionary)
//                {
//                    ObjectId shapeId = entry.Key.Item1;
//                    ObjectId shape1Id = entry.Key.Item2;
//                    ObjectId textId = entry.Value;
//                    Polyline shape = tr.GetObject(shapeId, OpenMode.ForWrite) as Polyline;
//                    if (shape != null && shape.NumberOfVertices >= 3)
//                    {
//                        Point3d center = shape.GetPoint3dAt(2);
//                        double angleRadians = angleDegrees * Math.PI / 180.0;
//                        shape.TransformBy(Matrix3d.Rotation(angleRadians, Vector3d.ZAxis, center));
//                        Polyline shape1 = tr.GetObject(shape1Id, OpenMode.ForWrite) as Polyline;
//                        if (shape1 != null)
//                        {
//                            shape1.TransformBy(Matrix3d.Rotation(angleRadians, Vector3d.ZAxis, center));
//                        }
//                        DBText text = tr.GetObject(textId, OpenMode.ForWrite) as DBText;
//                        if (text != null)
//                        {
//                            text.TransformBy(Matrix3d.Rotation(angleRadians, Vector3d.ZAxis, center));
//                            text.AlignmentPoint = text.Position;
//                            double originalRotation = text.Rotation;
//                            text.Rotation = originalRotation + angleRadians;
//                        }
//                    }
//                }
//                tr.Commit();
//            }
//        }
//        public static void UpdateElevationsByPoint(Point3d newBasePoint, Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> dictionary)
//        {
//            using (Transaction tr = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.StartTransaction())
//            {
//                foreach (var entry in dictionary)
//                {
//                    ObjectId shapeId = entry.Key.Item1;
//                    ObjectId textId = entry.Value;
//                    Polyline shape = tr.GetObject(shapeId, OpenMode.ForRead) as Polyline;
//                    DBText text = tr.GetObject(textId, OpenMode.ForWrite) as DBText;
//                    if (shape != null && text != null && shape.NumberOfVertices >= 3)
//                    {
//                        Point3d referencePoint = shape.GetPoint3dAt(2);
//                        double elevation = (referencePoint.Y - newBasePoint.Y);
//                        double tolerance = 0.001;
//                        text.TextString = Math.Abs(referencePoint.Y - newBasePoint.Y) < tolerance ? $"±{elevation:F3}" : $"{elevation:F3}";
//                    }
//                    else
//                    {
//                        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n跳过无效的对象（Shape: {shapeId}），顶点数量不足或对象无效。");
//                    }
//                }
//                tr.Commit();
//            }
//        }
//        public static Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> BuildSymbolDictionaryFromSelection()
//        {
//            Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> tempDictionary = new Dictionary<Tuple<ObjectId, ObjectId>, ObjectId>();
//            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
//            Database db = Application.DocumentManager.MdiActiveDocument.Database;
//            TypedValue[] filter = new TypedValue[]
//            {
//                new TypedValue((int)DxfCode.LayerName, "00_hy_3公共_标注4_标高")
//            };
//            SelectionFilter selFilter = new SelectionFilter(filter);
//            PromptSelectionResult psr = ed.GetSelection(selFilter);
//            if (psr.Status != PromptStatus.OK)
//            {
//                ed.WriteMessage("\n未选择任何对象。");
//                return tempDictionary;
//            }
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                SelectionSet ss = psr.Value;
//                List<ObjectId> shapes = new List<ObjectId>();
//                List<ObjectId> shape1s = new List<ObjectId>();
//                List<ObjectId> texts = new List<ObjectId>();
//                foreach (ObjectId id in ss.GetObjectIds())
//                {
//                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
//                    if (ent is Polyline polyline)
//                    {
//                        if (polyline.NumberOfVertices == 4)
//                        {
//                            shapes.Add(id);
//                        }
//                        else if (polyline.NumberOfVertices == 2)
//                        {
//                            shape1s.Add(id);
//                        }
//                    }
//                    else if (ent is DBText)
//                    {
//                        texts.Add(id);
//                    }
//                }
//                foreach (ObjectId shapeId in shapes)
//                {
//                    Polyline shape = tr.GetObject(shapeId, OpenMode.ForRead) as Polyline;
//                    if (shape == null || shape.NumberOfVertices != 4) continue;
//                    Point3d shapeThirdPoint = shape.GetPoint3dAt(2);
//                    foreach (ObjectId shape1Id in shape1s)
//                    {
//                        Polyline shape1 = tr.GetObject(shape1Id, OpenMode.ForRead) as Polyline;
//                        if (shape1 == null || shape1.NumberOfVertices != 2) continue;
//                        Point2d startPoint = shape1.GetPoint2dAt(0);
//                        Point2d endPoint = shape1.GetPoint2dAt(1);
//                        Point3d shape1MidPoint = new Point3d(
//                            (startPoint.X + endPoint.X) / 2,
//                            (startPoint.Y + endPoint.Y) / 2,
//                            (shapeThirdPoint.Z + shapeThirdPoint.Z) / 2
//                        );
//                        double tolerance = 0.001;
//                        if (shape1MidPoint.DistanceTo(shapeThirdPoint) < tolerance)
//                        {
//                            DBText nearestText = null;
//                            double minDistance = double.MaxValue;
//                            foreach (ObjectId textId in texts)
//                            {
//                                DBText text = tr.GetObject(textId, OpenMode.ForRead) as DBText;
//                                if (text != null)
//                                {
//                                    double distance = shapeThirdPoint.DistanceTo(text.Position);
//                                    if (distance < minDistance)
//                                    {
//                                        minDistance = distance;
//                                        nearestText = text;
//                                    }
//                                }
//                            }
//                            if (nearestText != null)
//                            {
//                                var key = new Tuple<ObjectId, ObjectId>(shape.ObjectId, shape1.ObjectId);
//                                tempDictionary[key] = nearestText.ObjectId;
//                                texts.Remove(nearestText.ObjectId);
//                            }
//                            break;
//                        }
//                    }
//                }
//                tr.Commit();
//            }
//            return tempDictionary;
//        }
//    }
//}