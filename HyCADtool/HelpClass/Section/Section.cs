//using Autodesk.AutoCAD.BoundaryRepresentation;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using HyCADTool.Config;
//using HyCADTool.HelpClass.ElevationSymbol;
//using HyCADTool.Tools;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//namespace HyCADTool.HelpClass.Section
//{
//    public class Section
//    {
//        public Polyline Shape { get; private set; }
//        public DBText Label { get; private set; }
//        public Point3d BasePoint { get; private set; }
//        public double Scale { get; private set; }
//        public double D { get; private set; }
//        public ElevationSymbolState State { get; set; }
//        private Point3d _currentPoint;
//        private Editor _editor;
//        private ObjectId _textStyleId;
//        private ObjectId _layerId;
//        private double _angleDegrees;
//        private double _angleRadians;
//        public Point3d CurrentPoint => _currentPoint;
//        // 静态字典不再由 ElevationSymbol 自动维护
//        public static Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> SymbolDictionary = new Dictionary<Tuple<ObjectId, ObjectId>, ObjectId>();
//        // 静态缓存图层和文本样式的 ObjectId，避免重复创建
//        private static ObjectId _cachedTextStyleId = ObjectId.Null;
//        private static ObjectId _cachedLayerId = ObjectId.Null;
//        public Section(Point3d basePoint, double scale, double d, Editor editor, 
//            ElevationSymbolState state = ElevationSymbolState.Normal, double angleDegrees = 0.0)
//        {
//        }
//        public static Dictionary<Polyline, string> GetPolylineTextDictionary()
//        {
//            Dictionary<Polyline, string> resultDict = new Dictionary<Polyline, string>();
//            try
//            {
//                Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
//                Database db = HostApplicationServices.WorkingDatabase;
//                // 提示用户选择对象
//                PromptSelectionOptions selOpts = new PromptSelectionOptions();
//                selOpts.MessageForAdding = "\n请选择封闭的Polyline和DBText: ";
//                // 创建过滤器，只选择Polyline和DBText
//                TypedValue[] filterlist = new TypedValue[]
//                {
//            new TypedValue((int)DxfCode.Operator, "<or"),
//            new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
//            new TypedValue((int)DxfCode.Start, "TEXT"),
//            new TypedValue((int)DxfCode.Operator, "or>")
//                };
//                SelectionFilter filter = new SelectionFilter(filterlist);
//                PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
//                if (selRes.Status != PromptStatus.OK)
//                    return resultDict;
//                using (Transaction tr = db.TransactionManager.StartTransaction())
//                {
//                    // 存储所有选择的Polyline和Text
//                    List<Polyline> polylines = new List<Polyline>();
//                    List<DBText> texts = new List<DBText>();
//                    // 分离选择的Polyline和Text
//                    foreach (ObjectId id in selRes.Value.GetObjectIds())
//                    {
//                        if (id.ObjectClass.DxfName == "LWPOLYLINE")
//                        {
//                            Polyline pl = tr.GetObject(id, OpenMode.ForRead) as Polyline;
//                            if (pl != null && pl.Closed) // 确保是封闭的
//                            {
//                                polylines.Add(pl);
//                            }
//                        }
//                        else if (id.ObjectClass.DxfName == "TEXT")
//                        {
//                            DBText txt = tr.GetObject(id, OpenMode.ForRead) as DBText;
//                            if (txt != null)
//                            {
//                                texts.Add(txt);
//                            }
//                        }
//                    }
//                    // 检查每个Text是否在某个Polyline内部
//                    foreach (DBText text in texts)
//                    {
//                        Point3d checkPoint = text.Position; // 使用文本的插入点
//                        foreach (Polyline pl in polylines)
//                        {
//                            // 使用PointContainment判断点是否在多段线内部
//                            using (Point3dCollection points = new Point3dCollection())
//                            {
//                                PointContainment containment;
//                                pl.GetPointContainment(checkPoint, out containment);
//                                if (containment == PointContainment.Inside)
//                                {
//                                    string textValue = text.TextString.Trim();
//                                    // 检查值是否唯一
//                                    if (!resultDict.ContainsValue(textValue))
//                                    {
//                                        resultDict[pl] = textValue;
//                                    }
//                                    break; // 找到包含的Polyline后跳出内层循环
//                                }
//                            }
//                        }
//                    }
//                    tr.Commit();
//                }
//            }
//            catch (System.Exception ex)
//            {
//                Autodesk.AutoCAD.ApplicationServices.Application.ShowAlertDialog(
//                    "错误: " + ex.Message);
//            }
//            return resultDict;
//        }
//        // 在你的Section类中使用
//        public void ProcessSection()
//        {
//            Dictionary<Polyline, string> plTextDict = GetPolylineTextDictionary();
//            // 处理结果
//            foreach (KeyValuePair<Polyline, string> kvp in plTextDict)
//            {
//                Editor ed = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor;
//                ed.WriteMessage($"\n找到多段线，包含文字: {kvp.Value}");
//            }
//        }
//    }
//}
