using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Models;
using HyCADTool.Utilities;
using System.Collections.Generic;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        /// <summary>
        /// 获取多段线及其筏板厚度（基于多段线内螺栓的最大 h1，厚度 = max(h1) + 150）
        /// </summary>
        /// <returns>包含多段线和对应厚度的字典列表</returns>
        public static List<Dictionary<Polyline, double>> RaftThicknessBybolted()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            List<Dictionary<Polyline, double>> result = new List<Dictionary<Polyline, double>>();
            try
            {
                // 定义过滤器，同时选择多段线和螺栓
                TypedValue[] filterList = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Start, "CIRCLE"),
                    new TypedValue((int)DxfCode.Operator, "OR>")
                };
                SelectionFilter filter = new SelectionFilter(filterList);
                PromptSelectionOptions selOpts = new PromptSelectionOptions
                {
                    MessageForAdding = "\n请选择多段线和地脚螺栓 (圆): "
                };
                ed.WriteMessage("\n开始选择对象...");
                PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
                if (selRes.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n选择取消或未选中任何对象。");
                    return result;
                }
                ed.WriteMessage($"\n选中了 {selRes.Value.Count} 个对象。");
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 分离多段线和螺栓
                    var polylineList = new List<Polyline>();
                    var circleList = new List<Circle>();
                    foreach (SelectedObject selObj in selRes.Value)
                    {
                        Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent is Polyline pline)
                        {
                            polylineList.Add(pline);
                            ed.WriteMessage($"\n找到多段线，图层: {pline.Layer}");
                        }
                        else if (ent is Circle circle && circle.Layer.StartsWith("00_Hy_螺栓"))
                        {
                            circleList.Add(circle);
                            ed.WriteMessage($"\n找到螺栓圆，图层: {circle.Layer}");
                        }
                    }
                    ed.WriteMessage($"\n找到 {polylineList.Count} 个多段线和 {circleList.Count} 个螺栓。");
                    // 处理每个多段线
                    foreach (Polyline polyline in polylineList)
                    {
                        // 找到多段线内包含的所有螺栓
                        List<Circle> containedCircles = new List<Circle>();
                        foreach (Circle circle in circleList)
                        {
                            if (GeometryUtils.IsPointInside(polyline, circle.Center))
                            {
                                containedCircles.Add(circle);
                            }
                        }
                        // 计算最大 h1
                        double maxH1 = 0.0;
                        foreach (Circle circle in containedCircles)
                        {
                            AnchorBolt anchorBolt = ExtensionDictionaryUtils.ReadAnchorBoltFromExtensionDictionary(tr, circle, ed);
                            if (anchorBolt != null && anchorBolt.H1 > maxH1)
                            {
                                maxH1 = anchorBolt.H1;
                            }
                        }
                        // 计算厚度
                        double thickness = maxH1 > 0 ? maxH1 + 100: 0.0; // 如果没有有效螺栓，厚度为 0
                        // 将多段线和厚度存入字典
                        Dictionary<Polyline, double> polyThickness = new Dictionary<Polyline, double>
                        {
                            { polyline, thickness }
                        };
                        result.Add(polyThickness);
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
            return result;
        }
        /// <summary>
        /// AutoCAD 命令：在多段线形心处绘制筏板厚度文本
        /// </summary>
        [CommandMethod("HyRT_aftThicknessText")]
        public static void DrawRaftThicknessText()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                // 获取比例因子
                PromptDoubleOptions scaleOpts = new PromptDoubleOptions("\n请输入比例因子 [默认50]: ")
                {
                    DefaultValue = 50
                };
                PromptDoubleResult scaleRes = ed.GetDouble(scaleOpts);
                if (scaleRes.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n比例因子输入取消。");
                    return;
                }
                double scale = scaleRes.Value;
                // 调用方法获取多段线和厚度
                List<Dictionary<Polyline, double>> raftData = RaftThicknessBybolted();
                if (raftData.Count == 0)
                {
                    ed.WriteMessage("\n未选择有效的多段线或螺栓，无法生成厚度文本。");
                    return;
                }
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 获取或创建图层
                    LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;
                    string layerName = "00_Hy_Raft_ThicknessText";
                    if (!lt.Has(layerName))
                    {
                        LayerTableRecord ltr = new LayerTableRecord
                        {
                            Name = layerName,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, 7) // 白色
                        };
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }
                    // 获取模型空间
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    // 为每个多段线创建文本
                    foreach (var dict in raftData)
                    {
                        foreach (var pair in dict)
                        {
                            Polyline polyline = pair.Key;
                            double thickness = pair.Value;
                            // 计算形心
                            Point3d centroid = polyline.GetCentroid();
                            // 创建 DBText
                            using (DBText text = new DBText())
                            {
                                text.Position = centroid; // 文本位置为形心
                                text.Height = 7 * scale; // 高度 7 * Scale
                                text.WidthFactor = 0.7;  // 宽度比例 0.7
                                text.TextString = $"T={thickness:F0}"; // 厚度取整
                                text.Layer = layerName;  // 设置图层
                                // 使用默认文字样式（Standard）
                                btr.AppendEntity(text);
                                tr.AddNewlyCreatedDBObject(text, true);
                            }
                        }
                    }
                    tr.Commit();
                }
                ed.WriteMessage($"\n成功为 {raftData.Count} 个多段线添加厚度文本！");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
    // 扩展方法：计算多段线的形心
    public static class PolylineExtensions
    {
        public static Point3d GetCentroid(this Polyline polyline)
        {
            double area = 0.0;
            double xSum = 0.0;
            double ySum = 0.0;
            int n = polyline.NumberOfVertices;
            for (int i = 0; i < n; i++)
            {
                Point2d p1 = polyline.GetPoint2dAt(i);
                Point2d p2 = polyline.GetPoint2dAt((i + 1) % n);
                double cross = p1.X * p2.Y - p2.X * p1.Y;
                area += cross;
                xSum += (p1.X + p2.X) * cross;
                ySum += (p1.Y + p2.Y) * cross;
            }
            area /= 2.0;
            if (area == 0) return polyline.GetPoint3dAt(0); // 防止除以零
            double x = xSum / (6.0 * area);
            double y = ySum / (6.0 * area);
            return new Point3d(x, y, polyline.Elevation);
        }
    }
}