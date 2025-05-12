//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using System.Collections.Generic;
//using System;
//using HyCADTool.Tools;
//namespace HyCADTool.HelpClass.CreatBase
//{
//    public static class CreateSection
//    {
//        public static void CreateSectionAndMove()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;
//            try
//            {
//                using (Transaction tr = db.TransactionManager.StartTransaction())
//                {
//                    // 创建6个新图层并设置颜色
//                    Dictionary<string, short> layerSettings = new Dictionary<string, short>
//                    {
//                        { "00_hy_sectionGeometry", 1 },    // 红色
//                        { "00_hy_backgroundGeometry", 2 }, // 绿色
//                        { "00_hy_foregroundGeometry", 3 }, // 黄色
//                        { "00_hy_curveGeometry", 4 },      // 青色
//                        { "00_hy_fillGeometry", 5 },       // 蓝色
//                        { "00_hy_section", 6 }             // 洋红
//                    };
//                    foreach (var layer in layerSettings)
//                    {
//                        EtGpt.CreateLayer(layer.Key, layer.Value); // 假设 EtGpt 是你的工具类
//                    }
//                    // 提示用户选择剖切线
//                    PromptEntityOptions peo = new PromptEntityOptions("\n请选择剖切线 (Polyline 或 Line): ");
//                    peo.SetRejectMessage("\n请选择有效的 Polyline 或 Line 对象!");
//                    peo.AddAllowedClass(typeof(Polyline), true);
//                    peo.AddAllowedClass(typeof(Line), true);
//                    PromptEntityResult per = ed.GetEntity(peo);
//                    if (per.Status != PromptStatus.OK) return;
//                    Entity cuttingLine = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Entity;
//                    if (cuttingLine == null) return;
//                    // 创建剖面所需的点集合
//                    Point3dCollection sectionPoints = new Point3dCollection();
//                    if (cuttingLine is Polyline pl)
//                    {
//                        for (int i = 0; i < pl.NumberOfVertices; i++)
//                        {
//                            Point2d pt2d = pl.GetPoint2dAt(i);
//                            Point3d pt3d = new Point3d(pt2d.X, pt2d.Y, pl.Elevation);
//                            sectionPoints.Add(pt3d);
//                        }
//                    }
//                    else // Line
//                    {
//                        Line ln = (Line)cuttingLine;
//                        sectionPoints.Add(ln.StartPoint);
//                        sectionPoints.Add(ln.EndPoint);
//                    }
//                    if (sectionPoints.Count < 2)
//                    {
//                        ed.WriteMessage("\n错误: 剖切线点数不足，无法创建剖面");
//                        return;
//                    }
//                    // 创建Section对象
//                    Section section = new Section(sectionPoints, Vector3d.ZAxis);
//                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
//                    // 将section添加到图层
//                    section.Layer = "00_hy_section";
//                    btr.AppendEntity(section);
//                    tr.AddNewlyCreatedDBObject(section, true);
//                    // 提示用户选择3D对象，并限制为3D类型
//                    PromptSelectionOptions pso = new PromptSelectionOptions();
//                    pso.MessageForAdding = "\n请选择要生成剖面的3D对象: ";
//                    // 定义过滤器，只允许选择 3D 对象（3DSOLID, SURFACE, MESH）
//                    TypedValue[] filterList = new TypedValue[]
//                    {
//                        new TypedValue((int)DxfCode.Operator, "<OR"), // 开始 OR 条件
//                        new TypedValue((int)DxfCode.Start, "3DSOLID"), // 3D 实体
//                        new TypedValue((int)DxfCode.Start, "SURFACE"), // 曲面
//                        new TypedValue((int)DxfCode.Start, "MESH"),   // 网格
//                        new TypedValue((int)DxfCode.Operator, "OR>")  // 结束 OR 条件
//                    };
//                    SelectionFilter filter = new SelectionFilter(filterList);
//                    // 使用过滤器选择对象
//                    PromptSelectionResult psr = ed.GetSelection(pso, filter);
//                    if (psr.Status != PromptStatus.OK)
//                    {
//                        ed.WriteMessage("\n未选中任何 3D 对象，操作取消。");
//                        return;
//                    }
//                    // 处理剖面几何生成
//                    ObjectIdCollection entityIds = new ObjectIdCollection(psr.Value.GetObjectIds());
//                    Vector3d moveVector = new Vector3d(0, -6000, 0);
//                    //Vector3d moveVector = new Vector3d(0, 0, 0);
//                    Matrix3d rotation = Matrix3d.Rotation(-Math.PI / 2, Vector3d.XAxis, Point3d.Origin);
//                    //Matrix3d rotation = Matrix3d.Rotation(0, Vector3d.XAxis, Point3d.Origin);
//                    Matrix3d transformation = Matrix3d.Displacement(moveVector) * rotation;
//                    foreach (ObjectId objId in entityIds)
//                    {
//                        Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
//                        if (entity != null)
//                        {
//                            Array sectionGeometry;
//                            Array backgroundGeometry;
//                            Array foregroundGeometry;
//                            Array curveGeometry;
//                            Array fillGeometry;
//                            section.GenerateSectionGeometry(
//                                entity,
//                                out sectionGeometry,
//                                out backgroundGeometry,
//                                out foregroundGeometry,
//                                out curveGeometry,
//                                out fillGeometry
//                            );
//                            // 处理并放置到对应图层
//                            if (sectionGeometry != null && sectionGeometry.Length > 0)
//                            {
//                                foreach (object geom in sectionGeometry)
//                                {
//                                    if (geom is Entity ent && ent != null)
//                                    {
//                                        ent.Layer = "00_hy_sectionGeometry";
//                                        ent.TransformBy(transformation);
//                                        btr.AppendEntity(ent);
//                                        tr.AddNewlyCreatedDBObject(ent, true);
//                                    }
//                                }
//                            }
//                            if (backgroundGeometry != null && backgroundGeometry.Length > 0)
//                            {
//                                foreach (object geom in backgroundGeometry)
//                                {
//                                    if (geom is Entity ent && ent != null)
//                                    {
//                                        ent.Layer = "00_hy_backgroundGeometry";
//                                        ent.TransformBy(transformation);
//                                        btr.AppendEntity(ent);
//                                        tr.AddNewlyCreatedDBObject(ent, true);
//                                    }
//                                }
//                            }
//                            if (foregroundGeometry != null && foregroundGeometry.Length > 0)
//                            {
//                                foreach (object geom in foregroundGeometry)
//                                {
//                                    if (geom is Entity ent && ent != null)
//                                    {
//                                        ent.Layer = "00_hy_foregroundGeometry";
//                                        ent.TransformBy(transformation);
//                                        btr.AppendEntity(ent);
//                                        tr.AddNewlyCreatedDBObject(ent, true);
//                                    }
//                                }
//                            }
//                            if (curveGeometry != null && curveGeometry.Length > 0)
//                            {
//                                foreach (object geom in curveGeometry)
//                                {
//                                    if (geom is Entity ent && ent != null)
//                                    {
//                                        ent.Layer = "00_hy_curveGeometry";
//                                        ent.TransformBy(transformation);
//                                        btr.AppendEntity(ent);
//                                        tr.AddNewlyCreatedDBObject(ent, true);
//                                    }
//                                }
//                            }
//                            if (fillGeometry != null && fillGeometry.Length > 0)
//                            {
//                                foreach (object geom in fillGeometry)
//                                {
//                                    if (geom is Entity ent && ent != null)
//                                    {
//                                        ent.Layer = "00_hy_fillGeometry";
//                                        ent.TransformBy(transformation);
//                                        btr.AppendEntity(ent);
//                                        tr.AddNewlyCreatedDBObject(ent, true);
//                                    }
//                                }
//                            }
//                        }
//                    }
//                    tr.Commit();
//                }
//                ed.Regen();
//            }
//            catch (System.Exception ex)
//            {
//                ed.WriteMessage($"\n错误: {ex.Message}");
//            }
//        }
//    }
//}