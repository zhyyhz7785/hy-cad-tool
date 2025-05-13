using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass.DCEL;
using HyCADTool.Tools;
using System.Collections.Generic;
using System.Linq;
using Face = HyCADTool.HelpClass.DCEL.Face;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("d2p")]
        public static void CreateDCELPolylinesFromLines(List<Curve> inputLines = null)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            List<Line> lines = new List<Line>();
            Dictionary<ObjectId, string> lineLayers = new Dictionary<ObjectId, string>();
            List<ObjectId> lineIds = new List<ObjectId>();
            // 获取直线
            if (inputLines != null && inputLines.Count > 0)
            {
                // 如果提供了直线列表，使用输入的直线
                foreach (var curve in inputLines)
                {
                    if (curve is Line line)
                    {
                        lines.Add(line);
                        lineLayers.Add(curve.ObjectId, curve.Layer);
                        lineIds.Add(curve.ObjectId);
                    }
                }
            }
            else
            {
                // 未提供直线，提示用户选择直线
                PromptSelectionOptions opts = new PromptSelectionOptions
                {
                    MessageForAdding = "\n请选择用于生成 DCEL 多段线的直线："
                };
                SelectionFilter filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Start, "LINE")
                });
                PromptSelectionResult res = ed.GetSelection(opts, filter);
                if (res.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未选择任何直线。");
                    return;
                }
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selObj in res.Value)
                    {
                        if (selObj != null)
                        {
                            Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
                            if (ent is Line line)
                            {
                                lines.Add(line);
                                lineLayers.Add(selObj.ObjectId, ent.Layer);
                                lineIds.Add(selObj.ObjectId);
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            // 检查直线列表是否为空
            if (lines.Count == 0)
            {
                ed.WriteMessage("\n没有可用的直线来生成 DCEL。");
                return;
            }
            // 利用直线创建 DCEL
            DCEL dcel = DCELFactory.CreateFromCurves(lines.Cast<Curve>().ToList());
            // 绘制外轮廓面
            DrawOuterFacesInAutoCAD(dcel, lineLayers, lineIds);
            // 删除原始直线
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (var lineId in lineIds)
                {
                    DBObject obj = tr.GetObject(lineId, OpenMode.ForWrite);
                    obj.Erase();
                }
                tr.Commit();
            }
        }
        private static void DrawOuterFacesInAutoCAD(DCEL dcel, Dictionary<ObjectId, string> lineLayers, List<ObjectId> lineIds)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                // 确保所有相关图层存在
                LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                HashSet<string> uniqueLayers = new HashSet<string>(lineLayers.Values);
                foreach (var layerName in uniqueLayers)
                {
                    if (!lt.Has(layerName))
                    {
                        lt.UpgradeOpen();
                        LayerTableRecord newLayer = new LayerTableRecord
                        {
                            Name = layerName
                        };
                        lt.Add(newLayer);
                        tr.AddNewlyCreatedDBObject(newLayer, true);
                    }
                }
                // 绘制外轮廓面
                foreach (Face face in dcel.OuterFaces)
                {
                    var halfedges = face.Components;
                    var vertices = halfedges.Select(p => p.StartVertex.Position.Point3dTo2d()).ToList();
                    if (vertices.Count > 0)
                    {
                        // 找到与该面相关的原始直线的图层
                        string layerName = lineLayers[lineIds.First()]; // 默认使用第一条直线的图层
                        foreach (var he in halfedges)
                        {
                            foreach (var lineId in lineIds)
                            {
                                Line line = tr.GetObject(lineId, OpenMode.ForRead) as Line;
                                if (line != null &&
                                    (he.StartVertex.Position.IsEqualTo(line.StartPoint, new Tolerance(DCELFactory.ToleranceDouble, DCELFactory.ToleranceDouble)) ||
                                     he.StartVertex.Position.IsEqualTo(line.EndPoint, new Tolerance(DCELFactory.ToleranceDouble, DCELFactory.ToleranceDouble))))
                                {
                                    layerName = lineLayers[lineId];
                                    break;
                                }
                            }
                        }
                        // 创建封闭多段线
                        Polyline polyline = new Polyline(vertices.Count);
                        polyline.Layer = layerName; // 使用匹配的图层
                        for (int i = 0; i < vertices.Count; i++)
                        {
                            polyline.AddVertexAt(i, vertices[i], 0, 0, 0);
                        }
                        polyline.Closed = true;
                        btr.AppendEntity(polyline);
                        tr.AddNewlyCreatedDBObject(polyline, true);
                    }
                }
                tr.Commit();
            }
        }
    }
}