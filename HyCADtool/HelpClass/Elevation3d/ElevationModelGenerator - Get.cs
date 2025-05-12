using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.HelpClass.CreatBase
{
    public partial class ElevationModelGenerator
    {
        public static Dictionary<Polyline, double> GetPolygonTextDictionary(out List<Polyline> outerContours)
        {
            var resultDict = new Dictionary<Polyline, double>();
            outerContours = new List<Polyline>();
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var db = HostApplicationServices.WorkingDatabase;
            string warningLayerName = "00ElevationWarnings";
            string textLayerName = "00_hy_3公共_标注4_标高";
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                if (!layerTable.Has("dcelOuter") || !layerTable.Has("dcelInter"))
                {
                    ed.WriteMessage("\n错误: 图层 'dcelOuter' 或 'dcelInter' 不存在，请创建这些图层后重试。");
                    tr.Commit();
                    return resultDict;
                }
                if (!layerTable.Has(warningLayerName))
                {
                    layerTable.UpgradeOpen();
                    var newLayer = new LayerTableRecord
                    {
                        Name = warningLayerName,
                        Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1)
                    };
                    layerTable.Add(newLayer);
                    tr.AddNewlyCreatedDBObject(newLayer, true);
                }
                var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择封闭的 Polyline 和标高文本 (DBText): " };
                var filter = new SelectionFilter(new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<or"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Start, "TEXT"),
                    new TypedValue((int)DxfCode.Operator, "or>")
                });
                PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
                if (selRes.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n错误: 未选择任何对象，请选择封闭的 Polyline 和标高文本后重试。");
                    tr.Commit();
                    return resultDict;
                }
                var outerPolylines = new List<Polyline>();
                var innerPolylines = new List<Polyline>();
                var texts = new List<DBText>();
                foreach (ObjectId id in selRes.Value.GetObjectIds())
                {
                    var obj = tr.GetObject(id, OpenMode.ForRead);
                    if (obj is Polyline pl && pl.Closed)
                    {
                        if (pl.Layer == "dcelOuter") outerPolylines.Add(pl);
                        else if (pl.Layer == "dcelInter") innerPolylines.Add(pl);
                    }
                    else if (obj is DBText txt)
                    {
                        if (txt.Layer == textLayerName) texts.Add(txt);
                    }
                }
                outerContours = outerPolylines; // 直接使用 AutoCAD 的 Polyline
                if (outerContours.Count == 0)
                {
                    ed.WriteMessage("\n错误: 未在 'dcelOuter' 图层中找到封闭的 Polyline，请检查图纸并添加外轮廓。");
                    tr.Commit();
                    return resultDict;
                }
                if (innerPolylines.Count == 0)
                {
                    ed.WriteMessage("\n错误: 未在 'dcelInter' 图层中找到封闭的 Polyline，请检查图纸并添加内多边形。");
                    tr.Commit();
                    return resultDict;
                }
                var polygonElevations = new Dictionary<Polyline, List<double>>();
                foreach (var text in texts)
                {
                    var checkPoint = text.Bounds.HasValue
                        ? new Point3d((text.Bounds.Value.MinPoint.X + text.Bounds.Value.MaxPoint.X) / 2,
                                      (text.Bounds.Value.MinPoint.Y + text.Bounds.Value.MaxPoint.Y) / 2, text.Position.Z)
                        : text.Position;
                    foreach (var polyline in innerPolylines)
                    {
                        if (IsPointInsidePolygon(checkPoint, polyline))
                        {
                            double elevation = ParseExtrudeDistance(text.TextString.Trim());
                            if (!polygonElevations.ContainsKey(polyline))
                                polygonElevations[polyline] = new List<double>();
                            polygonElevations[polyline].Add(elevation);
                            break;
                        }
                    }
                }
                var btr = (BlockTableRecord)tr.GetObject(SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);
                bool hasErrors = false;
                foreach (var polyline in innerPolylines)
                {
                    if (!IsValidPolyline(polyline))
                    {
                        hasErrors = true;
                        AddWarningEntity(btr, tr, polyline, warningLayerName, "错误: 自交多边形，请修正多边形几何。");
                        continue;
                    }
                    if (!polygonElevations.ContainsKey(polyline))
                    {
                        hasErrors = true;
                        AddWarningEntity(btr, tr, polyline, warningLayerName, "错误: 未找到标高，请为该多边形添加标高文本。");
                    }
                    else if (polygonElevations[polyline].Distinct().Count() > 1)
                    {
                        hasErrors = true;
                        AddWarningEntity(btr, tr, polyline, warningLayerName, "错误: 标高不一致，请确保该多边形只有一个标高值。");
                    }
                    else
                    {
                        resultDict[polyline] = polygonElevations[polyline].First();
                    }
                }
                if (hasErrors)
                {
                    ed.WriteMessage("\n命令中止: 图纸中存在错误，请查看 '00ElevationWarnings' 图层并修正问题后重试。");
                    tr.Commit();
                    return resultDict;
                }
                tr.Commit();
            }
            return resultDict;
        }
        private double GetThicknessForGeometryData(GeometryData geomData, double defaultThickness)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            // 检查 GeometryData 的 Polyline 是否包含螺栓
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var polyline = geomData.Polygon;
                    if (polyline == null || !polyline.Closed)
                    {
                        ed.WriteMessage($"\nGeometryData 的 Polyline 未定义或未闭合，使用默认厚度 {defaultThickness}\n");
                        tr.Commit();
                        return defaultThickness;
                    }
                    // 筛选图纸中的螺栓（Circle）
                    TypedValue[] filterList = new TypedValue[]
                    {
                        new TypedValue((int)DxfCode.Start, "CIRCLE"),
                        new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓*")
                    };
                    SelectionFilter filter = new SelectionFilter(filterList);
                    // 获取模型空间中的所有螺栓
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    List<double> boltHeights = new List<double>();
                    foreach (ObjectId objId in btr)
                    {
                        var circle = tr.GetObject(objId, OpenMode.ForRead) as Circle;
                        if (circle != null && circle.Layer.StartsWith("00_Hy_螺栓"))
                        {
                            // 检查螺栓中心是否在 Polyline 内
                            if (IsPointInside(polyline, circle.Center))
                            {
                                var boltData = ExtensionDictionaryUtils.ReadBoltDataFromExtensionDictionary(tr, circle, ed);
                                if (boltData != null)
                                {
                                    var anchorBolt = boltData.GetAnchorBolt();
                                    if (anchorBolt != null)
                                    {
                                        boltHeights.Add(anchorBolt.H1); // 使用 H1 作为高度
                                        ed.WriteMessage($"\n找到螺栓，型号: {anchorBolt.Model}, H1: {anchorBolt.H1} mm\n");
                                    }
                                }
                            }
                        }
                    }
                    if (boltHeights.Count > 0)
                    {
                        // 计算平均 H1 + 150mm
                        double averageH1 = boltHeights.Average();
                        double thickness = averageH1 + 150.0;
                        ed.WriteMessage($"\nGeometryData 包含 {boltHeights.Count} 个螺栓，平均 H1: {averageH1}, 厚度: {thickness} mm\n");
                        tr.Commit();
                        return thickness;
                    }
                    else
                    {
                        ed.WriteMessage($"\nGeometryData 未包含螺栓，使用默认厚度 {defaultThickness}\n");
                        tr.Commit();
                        return defaultThickness;
                    }
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n获取厚度失败: {ex.Message}，使用默认厚度 {defaultThickness}\n");
                    tr.Abort();
                    return defaultThickness;
                }
            }
        }
    }
}