//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//using HyCADTool.Tools;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//namespace HyCADTool.Command
//{
//    public static partial class HyCommand
//    {
//        [CommandMethod("HYMBRC_CreateLayoutViewports")]
//        public static void CreateViewportsFromModelBounds()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            // 获取视口缩放比例
//            PromptDoubleOptions scaleOpts = new PromptDoubleOptions("\n请输入视口比例（如 1:50 输入 0.02）：")
//            {
//                AllowNegative = false,
//                AllowZero = false,
//                DefaultValue = 0.02
//            };
//            PromptDoubleResult scaleRes = ed.GetDouble(scaleOpts);
//            if (scaleRes.Status != PromptStatus.OK) return;
//            double scale = scaleRes.Value;
//            // 获取视口间距
//            PromptDoubleOptions spacingOpts = new PromptDoubleOptions("\n请输入视口间距（单位：图纸单位，默认=10）：")
//            {
//                AllowNegative = false,
//                AllowZero = true,
//                DefaultValue = 10.0
//            };
//            PromptDoubleResult spacingRes = ed.GetDouble(spacingOpts);
//            if (spacingRes.Status != PromptStatus.OK) return;
//            double spacing = spacingRes.Value;
//            using (Transaction tr = db.TransactionManager.StartTransaction())
//            {
//                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                BlockTableRecord modelSpace = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
//                BlockTableRecord paperSpace = tr.GetObject(bt[BlockTableRecord.PaperSpace], OpenMode.ForWrite) as BlockTableRecord;
//                var polylines = new List<Polyline>();
//                foreach (ObjectId id in modelSpace)
//                {
//                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
//                    if (ent is Polyline pl && pl.Layer == "00_hy_2公共_视口" && pl.Closed)
//                    {
//                        polylines.Add(pl);
//                    }
//                }
//                if (polylines.Count == 0)
//                {
//                    ed.WriteMessage("\n未找到任何符合条件的闭合 Polyline。");
//                    return;
//                }
//                // 计算所有 Polyline 的中心 X 坐标，用于保持横向相对位置
//                double baseX = polylines
//                    .Select(pl => (pl.GeometricExtents.MinPoint.X + pl.GeometricExtents.MaxPoint.X) / 2.0)
//                    .Min();
//                double currentY = 0;
//                int index = 1;
//                foreach (Polyline pl in polylines)
//                {
//                    Extents3d ext = pl.GeometricExtents;
//                    double widthModel = ext.MaxPoint.X - ext.MinPoint.X;
//                    double heightModel = ext.MaxPoint.Y - ext.MinPoint.Y;
//                    Point3d centerModel = new Point3d(
//                        (ext.MinPoint.X + ext.MaxPoint.X) / 2.0,
//                        (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0,
//                        0);
//                    double vpWidth = widthModel * scale;
//                    double vpHeight = heightModel * scale;
//                    // 横向保持相对 X，纵向采用等距堆叠
//                    double centerPaperX = (centerModel.X - baseX) * scale;
//                    double centerPaperY = currentY + vpHeight / 2.0;
//                    Point3d centerPaper = new Point3d(centerPaperX, centerPaperY, 0);
//                    // 创建视口
//                    Viewport vp = new Viewport
//                    {
//                        CenterPoint = centerPaper,
//                        Width = vpWidth,
//                        Height = vpHeight,
//                        ViewCenter = new Point2d(centerModel.X, centerModel.Y),
//                        ViewTarget = Point3d.Origin,
//                        ViewHeight = heightModel,
//                        CustomScale = scale,
//                        Layer = "00_hy_2公共_视口"
//                    };
//                    paperSpace.AppendEntity(vp);
//                    tr.AddNewlyCreatedDBObject(vp, true);
//                    vp.On = true;
//                    vp.NonRectClipEntityId = ObjectId.Null;
//                    vp.NonRectClipOn = false;
//                    vp.Locked = true;
//                    // 添加编号文字
//                    DBText text = new DBText
//                    {
//                        Position = new Point3d(centerPaper.X, centerPaper.Y - vpHeight / 2 - 500 * scale, 0),
//                        Height = 350 * scale,
//                        TextString = $"视口{index}",
//                        Layer = "00_hy_2公共_视口",
//                        HorizontalMode = TextHorizontalMode.TextCenter,
//                        VerticalMode = TextVerticalMode.TextVerticalMid,
//                        AlignmentPoint = new Point3d(centerPaper.X, centerPaper.Y - vpHeight / 2 - 500 * scale, 0)
//                    };
//                    text.AdjustAlignment(db);
//                    paperSpace.AppendEntity(text);
//                    tr.AddNewlyCreatedDBObject(text, true);
//                    currentY += vpHeight + spacing;
//                    index++;
//                }
//                tr.Commit();
//                ed.WriteMessage($"\n共生成 {polylines.Count} 个视口，已添加到当前布局。");
//            }
//        }
//    }
//}
