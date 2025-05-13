using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Models;
using HyCADTool.Models.Cluster;
using HyCADTool.Tools;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("HY_TestAxisRegions")]
        public static void TestAxisRegions()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // 选择轴线
            PromptSelectionOptions opts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择轴线（必须为 LINE 类型，已分类为横纵轴）:"
            };
            TypedValue[] filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Start, "LINE")
            };
            SelectionFilter filter = new SelectionFilter(filterList);
            PromptSelectionResult selResult = ed.GetSelection(opts, filter);
            if (selResult.Status != PromptStatus.OK) return;

            List<Line> lines = new List<Line>();
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in selResult.Value.GetObjectIds())
                {
                    var line = tr.GetObject(id, OpenMode.ForRead) as Line;
                    if (line != null) lines.Add(line);
                }
                tr.Commit();
            }

            if (lines.Count < 2)
            {
                ed.WriteMessage("\n必须选择至少两条轴线用于测试。");
                return;
            }

            // 构造 Axis
            var axes = Axis.FromLines(lines);
            var xAxes = axes.Where(a => !a.IsVertical).ToList();
            var yAxes = axes.Where(a => a.IsVertical).ToList();

            // 设置区域范围（可自动计算）
            double xMin = lines.Min(l => l.GeometricExtents.MinPoint.X);
            double xMax = lines.Max(l => l.GeometricExtents.MaxPoint.X);
            double yMin = lines.Min(l => l.GeometricExtents.MinPoint.Y);
            double yMax = lines.Max(l => l.GeometricExtents.MaxPoint.Y);

            // 区域生成
            List<Extents3d> xRegions = Axis.GenerateCenteredRegions(xAxes, xMin, xMax);
            List<Extents3d> yRegions = Axis.GenerateCenteredRegions(yAxes, yMin, yMax);

            // 绘图
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                ObjectId layerId = EtGpt.CreateLayer("00_hy_测试_轴线区域", 93, db);

                void DrawRegion(Extents3d ext)
                {
                    var pl = new Polyline();
                    pl.AddVertexAt(0, new Point2d(ext.MinPoint.X, ext.MinPoint.Y), 0, 0, 0);
                    pl.AddVertexAt(1, new Point2d(ext.MaxPoint.X, ext.MinPoint.Y), 0, 0, 0);
                    pl.AddVertexAt(2, new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y), 0, 0, 0);
                    pl.AddVertexAt(3, new Point2d(ext.MinPoint.X, ext.MaxPoint.Y), 0, 0, 0);
                    pl.Closed = true;
                    pl.LayerId = layerId;
                    btr.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);
                }

                foreach (var region in xRegions) DrawRegion(region);
                foreach (var region in yRegions) DrawRegion(region);

                tr.Commit();
            }

            ed.WriteMessage($"\n共绘制 {xRegions.Count} 个 X 向区域，{yRegions.Count} 个 Y 向区域。");
        }
    }
}
