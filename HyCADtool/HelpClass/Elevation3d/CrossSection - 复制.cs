//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//using HyCADTool.Tools;
//using System;
//using System.Collections.Generic;
//using Exception = Autodesk.AutoCAD.Runtime.Exception;

//namespace HyCADTool.HelpClass.CreatBase
//{
//    public static class CreateSection
//    {
//        // 创建图层
//        private static void CreateLayers(Database db, Transaction tr)
//        {
//            Dictionary<string, short> layerSettings = new Dictionary<string, short>
//            {
//                { "00_hy_sectionGeometry", 1 },    // 红色
//                { "00_hy_backgroundGeometry", 2 }, // 绿色
//                { "00_hy_foregroundGeometry", 3 }, // 黄色
//                { "00_hy_curveGeometry", 4 },      // 青色
//                { "00_hy_fillGeometry", 5 },       // 蓝色
//                { "00_hy_section", 6 }             // 洋红
//            };

//            foreach (var layer in layerSettings)
//            {
//                EtGpt.CreateLayer(layer.Key, layer.Value); // 假设 EtGpt 是工具类
//            }
//        }

//        // 获取用户输入的位移距离
//        private static double GetDisplacement(Editor ed)
//        {
//            PromptDoubleOptions pdo = new PromptDoubleOptions("\n请输入剖面位移距离 (默认 6000): ");
//            pdo.DefaultValue = 6000;
//            pdo.AllowNegative = false;
//            PromptDoubleResult pdr = ed.GetDouble(pdo);
//            return pdr.Status == PromptStatus.OK ? pdr.Value : 6000;
//        }

//        // 创建变换矩阵
//        private static Matrix3d GetTransformation(bool isXDirection, double displacement, int index)
//        {
//            if (isXDirection)
//            {
//                Vector3d moveVector = new Vector3d(0, -displacement * (index + 1), 0);
//                return Matrix3d.Displacement(moveVector) * Matrix3d.Rotation(-Math.PI / 2, Vector3d.XAxis, Point3d.Origin);
//            }
//            else
//            {
//                Vector3d moveVector = new Vector3d(-displacement * (index + 1), 0, 0);
//                return Matrix3d.Displacement(moveVector) * Matrix3d.Rotation(-Math.PI / 2, Vector3d.YAxis, Point3d.Origin);
//            }
//        }

//        // 创建剖面并移动
//        private static void GenerateSection(Database db, Transaction tr, Editor ed, List<SectionLine> sectionLines, double displacement)
//        {
//            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
//            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

//            foreach (var sl in sectionLines)
//            {
//                Line line = sl.LineX ?? sl.LineY;
//                if (line == null) continue;

//                // 创建剖面点集合
//                Point3dCollection sectionPoints = new Point3dCollection { line.StartPoint, line.EndPoint };
//                Section section = new Section(sectionPoints, Vector3d.ZAxis);
//                section.Layer = "00_hy_section";
//                btr.AppendEntity(section);
//                tr.AddNewlyCreatedDBObject(section, true);

//                // 添加符号
//                sl.AddSymbols(db, tr, btr);

//                // 选择 3D 对象
//                PromptSelectionOptions pso = new PromptSelectionOptions();
//                pso.MessageForAdding = $"\n请选择第 {sl.Index + 1} 条剖切线对应的 3D 对象: ";
//                TypedValue[] filterList = new TypedValue[]
//                {
//                    new TypedValue((int)DxfCode.Operator, "<OR"),
//                    new TypedValue((int)DxfCode.Start, "3DSOLID"),
//                    new TypedValue((int)DxfCode.Start, "SURFACE"),
//                    new TypedValue((int)DxfCode.Start, "MESH"),
//                    new TypedValue((int)DxfCode.Operator, "OR>")
//                };
//                SelectionFilter filter = new SelectionFilter(filterList);
//                PromptSelectionResult psr = ed.GetSelection(pso, filter);
//                if (psr.Status != PromptStatus.OK) continue;

//                // 生成剖面几何
//                ObjectIdCollection entityIds = new ObjectIdCollection(psr.Value.GetObjectIds());
//                Matrix3d transformation = GetTransformation(sl.LineX != null, displacement, sl.Index);
//                foreach (ObjectId objId in entityIds)
//                {
//                    Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
//                    if (entity != null)
//                    {
//                        Array sectionGeometry, backgroundGeometry, foregroundGeometry, curveGeometry, fillGeometry;
//                        section.GenerateSectionGeometry(entity, out sectionGeometry, out backgroundGeometry,
//                            out foregroundGeometry, out curveGeometry, out fillGeometry);

//                        AssignGeometryToLayer(sectionGeometry, "00_hy_sectionGeometry", transformation, btr, tr);
//                        AssignGeometryToLayer(backgroundGeometry, "00_hy_backgroundGeometry", transformation, btr, tr);
//                        AssignGeometryToLayer(foregroundGeometry, "00_hy_foregroundGeometry", transformation, btr, tr);
//                        AssignGeometryToLayer(curveGeometry, "00_hy_curveGeometry", transformation, btr, tr);
//                        AssignGeometryToLayer(fillGeometry, "00_hy_fillGeometry", transformation, btr, tr);
//                    }
//                }
//            }
//        }

//        // 分配几何到图层
//        private static void AssignGeometryToLayer(Array geometry, string layerName, Matrix3d transformation,
//            BlockTableRecord btr, Transaction tr)
//        {
//            if (geometry != null && geometry.Length > 0)
//            {
//                foreach (object geom in geometry)
//                {
//                    if (geom is Entity ent && ent != null)
//                    {
//                        ent.Layer = layerName;
//                        ent.TransformBy(transformation);
//                        btr.AppendEntity(ent);
//                        tr.AddNewlyCreatedDBObject(ent, true);
//                    }
//                }
//            }
//        }

//        // 命令整合
//        [CommandMethod("CreateSectionCommand")]
//        public static void CreateSectionCommand()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;

//            try
//            {
//                using (Transaction tr = db.TransactionManager.StartTransaction())
//                {
//                    CreateLayers(db, tr);
//                    double displacement = GetDisplacement(ed);
//                    List<SectionLine> sectionLines = SectionLine.SelectLines(ed);
//                    if (sectionLines.Count == 0) return;
//                    SectionLine.SortAndIndexLines(sectionLines);
//                    GenerateSection(db, tr, ed, sectionLines, displacement);
//                    tr.Commit();
//                }
//                ed.Regen();
//            }
//            catch (Exception ex)
//            {
//                ed.WriteMessage($"\n错误: {ex.Message}");
//            }
//        }
//    }
//}