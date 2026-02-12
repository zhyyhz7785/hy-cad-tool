using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Entities;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 根据多段线内螺栓计算筏板厚度并在形心处标注文字（HyRT_aftThicknessText）
    /// 厚度 = max(H1) + 100
    /// </summary>
    public class RaftThicknessTextCommand
    {
        private const string LayerName = "00_Hy_Raft_ThicknessText";

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                // 获取比例
                var scaleOpts = new PromptDoubleOptions("\n请输入比例因子 [默认50]: ") { DefaultValue = 50 };
                var scaleRes = ed.GetDouble(scaleOpts);
                if (scaleRes.Status != PromptStatus.OK) return;
                double scale = scaleRes.Value;

                // 选择多段线和圆
                var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择多段线和地脚螺栓(圆): " };
                var filter = new SelectionFilter(new[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Start, "CIRCLE"),
                    new TypedValue((int)DxfCode.Operator, "OR>")
                });
                var selRes = ed.GetSelection(selOpts, filter);
                if (selRes.Status != PromptStatus.OK) return;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var polylines = new List<Polyline>();
                    var circles = new List<Circle>();

                    foreach (SelectedObject so in selRes.Value)
                    {
                        var ent = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Entity;
                        if (ent is Polyline pl)
                            polylines.Add(pl);
                        else if (ent is Circle c && c.Layer.StartsWith("00_Hy_螺栓"))
                            circles.Add(c);
                    }

                    // 确保图层
                    var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    if (!lt.Has(LayerName))
                    {
                        lt.UpgradeOpen();
                        var ltr = new LayerTableRecord
                        {
                            Name = LayerName,
                            Color = Color.FromColorIndex(ColorMethod.ByAci, 7)
                        };
                        lt.Add(ltr);
                        tr.AddNewlyCreatedDBObject(ltr, true);
                    }

                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (var pline in polylines)
                    {
                        double maxH1 = 0;
                        foreach (var circle in circles)
                        {
                            if (!IsPointInside(pline, circle.Center)) continue;
                            var bolt = ExtensionDictionaryService.ReadAnchorBolt(tr, circle);
                            if (bolt != null && bolt.H1 > maxH1)
                                maxH1 = bolt.H1;
                        }

                        double thickness = maxH1 > 0 ? maxH1 + 100 : 0;
                        var centroid = GetCentroid(pline);

                        var text = new DBText
                        {
                            Position = centroid,
                            Height = 7 * scale,
                            WidthFactor = 0.7,
                            TextString = $"T={thickness:F0}",
                            Layer = LayerName
                        };

                        ms.AppendEntity(text);
                        tr.AddNewlyCreatedDBObject(text, true);
                    }

                    tr.Commit();
                }

                ed.WriteMessage("\n筏板厚度标注完成");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n标注失败: {ex.Message}");
            }
        }

        private static bool IsPointInside(Polyline pline, Point3d pt)
        {
            int n = pline.NumberOfVertices;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = pline.GetPoint2dAt(i);
                var pj = pline.GetPoint2dAt(j);
                if (((pi.Y > pt.Y) != (pj.Y > pt.Y)) &&
                    (pt.X < (pj.X - pi.X) * (pt.Y - pi.Y) / (pj.Y - pi.Y + 1e-10) + pi.X))
                    inside = !inside;
            }
            return inside;
        }

        private static Point3d GetCentroid(Polyline pline)
        {
            double area = 0, cx = 0, cy = 0;
            int n = pline.NumberOfVertices;
            for (int i = 0; i < n; i++)
            {
                var p0 = pline.GetPoint2dAt(i);
                var p1 = pline.GetPoint2dAt((i + 1) % n);
                double cross = p0.X * p1.Y - p1.X * p0.Y;
                area += cross;
                cx += (p0.X + p1.X) * cross;
                cy += (p0.Y + p1.Y) * cross;
            }
            area *= 0.5;
            if (System.Math.Abs(area) < 1e-10) return pline.GetPoint3dAt(0);
            return new Point3d(cx / (6 * area), cy / (6 * area), pline.Elevation);
        }
    }
}
