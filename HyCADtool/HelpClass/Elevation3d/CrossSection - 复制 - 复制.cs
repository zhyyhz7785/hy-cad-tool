//using Autodesk.AutoCAD.ApplicationServices;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.Runtime;
//using HyCADTool.Tools;
//using System;
//using System.Collections.Generic;
//using System.Windows.Documents.DocumentStructures;
//using Exception = Autodesk.AutoCAD.Runtime.Exception;
//namespace HyCADTool.HelpClass.CreatBase
//{
//    public static class CreateSection
//    {
//        private static void CreateLayers(Database db, Transaction tr)
//        {
//            Dictionary<string, short> layerSettings = new Dictionary<string, short>
//            {
//                { "00_hy_sectionGeometry", 1 }, { "00_hy_backgroundGeometry", 2 }, { "00_hy_foregroundGeometry", 3 },
//                { "00_hy_curveGeometry", 4 }, { "00_hy_fillGeometry", 5 }, { "00_hy_section", 6 }
//            };
//            foreach (var layer in layerSettings) EtGpt.CreateLayer(layer.Key, layer.Value);
//        }
//        private static double GetDisplacement(Editor ed)
//        {
//            PromptDoubleOptions pdo = new PromptDoubleOptions("\n请输入剖面位移距离 (默认 6000): ") { DefaultValue = 6000, AllowNegative = false };
//            PromptDoubleResult pdr = ed.GetDouble(pdo);
//            return pdr.Status == PromptStatus.OK ? pdr.Value : 6000;
//        }
//        private static Matrix3d GetTransformation(bool isXDirection, double displacement, int index)
//        {
//            return isXDirection
//                ? Matrix3d.Displacement(new Vector3d(0, -displacement * (index + 1), 0)) * Matrix3d.Rotation(-Math.PI / 2, Vector3d.XAxis, Point3d.Origin)
//                : Matrix3d.Displacement(new Vector3d(-displacement * (index + 1), 0, 0)) * Matrix3d.Rotation(-Math.PI / 2, Vector3d.YAxis, Point3d.Origin);
//        }
//        // 选择 3D 对象（只选择一次）
//        private static ObjectIdCollection Select3DObjects(Editor ed)
//        {
//            PromptSelectionOptions pso = new PromptSelectionOptions { MessageForAdding = "\n请选择要生成剖面的 3D 对象: " };
//            TypedValue[] filterList = new TypedValue[]
//            {
//                new TypedValue((int)DxfCode.Operator, "<OR"), new TypedValue((int)DxfCode.Start, "3DSOLID"),
//                new TypedValue((int)DxfCode.Start, "SURFACE"), new TypedValue((int)DxfCode.Start, "MESH"),
//                new TypedValue((int)DxfCode.Operator, "OR>")
//            };
//            SelectionFilter filter = new SelectionFilter(filterList);
//            PromptSelectionResult psr = ed.GetSelection(pso, filter);
//            return psr.Status == PromptStatus.OK ? new ObjectIdCollection(psr.Value.GetObjectIds()) : new ObjectIdCollection();
//        }
//        private static void GenerateSection(Database db, Transaction tr, Editor ed, List<SectionLine> sectionLines,
//    double displacement, ObjectIdCollection entityIds, BlockTableRecord btr)
//        {
//            foreach (var sl in sectionLines)
//            {
//                Line line = sl.LineX ?? sl.LineY;
//                if (line == null)
//                {
//                    ed.WriteMessage("\n警告：剖面线的 LineX 和 LineY 均为 null，已跳过");
//                    continue;
//                }
//                // 使用剖断线长度定义剖面范围
//                Point3dCollection sectionPoints = new Point3dCollection { line.StartPoint, line.EndPoint };
//                // 创建 Section 对象，垂直方向为 Z 轴
//                Section section = new Section(sectionPoints, Vector3d.ZAxis)
//                {
//                    Layer = "00_hy_section"
//                };
//                // 设置观察方向，确保剖面在 XY 平面上
//                Vector3d lineVector = (line.EndPoint - line.StartPoint).GetNormal();
//                Vector3d viewingDir = lineVector.CrossProduct(Vector3d.ZAxis).GetNormal(); // 垂直于线和 Z 轴
//                section.ViewingDirection = viewingDir;
//                // 设置高度范围（无限制）
//                double largeHeight = 10000; // 足够大的值，例如 1000000
//                section.SetHeight(SectionHeight.HeightAboveSectionLine, largeHeight);  // 设置剖面线以上高度
//                section.SetHeight(SectionHeight.HeightBelowSectionLine, largeHeight);  // 设置剖面线以下高度
//                // 添加 Section 到图形
//                btr.AppendEntity(section);
//                tr.AddNewlyCreatedDBObject(section, true);
//                // 检查 section.Settings 是否有效
//                if (section.Settings.IsNull)
//                {
//                    ed.WriteMessage("\n错误：section.Settings 返回了无效的 ObjectId，无法配置 SectionSettings");
//                    continue;
//                }
//                // 获取 SectionSettings 并配置为 2D 剖面
//                try
//                {
//                    using (SectionSettings settings = tr.GetObject(section.Settings, OpenMode.ForWrite) as SectionSettings)
//                    {
//                        if (settings == null)
//                        {
//                            ed.WriteMessage("\n错误：无法打开 SectionSettings 对象");
//                            continue;
//                        }
//                        // 设置为 2D 剖面
//                        settings.CurrentSectionType = SectionType.Section2d;
//                        // 设置可见性
//                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.BackgroundGeometry, true); // 隐藏背景
//                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.ForegroundGeometry, false);  // 显示前景
//                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.CurveTangencyLines, true);  // 显示曲线
//                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.IntersectionFill, true );    // 显示填充
//                    }
//                }
//                catch (Autodesk.AutoCAD.Runtime.Exception ex)
//                {
//                    ed.WriteMessage($"\n错误：配置 SectionSettings 时发生异常: {ex.Message}");
//                    continue;
//                }
//                // 添加端点符号
//                sl.AddSymbols(db, tr, btr);
//                // 计算变换矩阵
//                Matrix3d transformation = GetTransformation(sl.LineX != null, displacement, sl.Index);
//                // 生成剖面几何
//                foreach (ObjectId objId in entityIds)
//                {
//                    if (objId.IsNull)
//                    {
//                        ed.WriteMessage("\n警告：entityIds 中包含无效的 ObjectId，已跳过");
//                        continue;
//                    }
//                    try
//                    {
//                        Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
//                        if (entity != null)
//                        {
//                            Array sectionGeometry, backgroundGeometry, foregroundGeometry, curveGeometry, fillGeometry;
//                            section.GenerateSectionGeometry(entity, out sectionGeometry, out backgroundGeometry,
//                                out foregroundGeometry, out curveGeometry, out fillGeometry);
//                            // 分配几何到图层并应用变换（调用外部方法）
//                            AssignGeometryToLayer(sectionGeometry, "00_hy_sectionGeometry", transformation, btr, tr);
//                            AssignGeometryToLayer(backgroundGeometry, "00_hy_backgroundGeometry", transformation, btr, tr);
//                            //AssignGeometryToLayer(foregroundGeometry, "00_hy_foregroundGeometry", transformation, btr, tr);
//                            //AssignGeometryToLayer(curveGeometry, "00_hy_curveGeometry", transformation, btr, tr);
//                           // AssignGeometryToLayer(fillGeometry, "00_hy_fillGeometry", transformation, btr, tr);
//                        }
//                        else
//                        {
//                            ed.WriteMessage($"\n警告：无法打开 ObjectId {objId} 对应的实体");
//                        }
//                    }
//                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
//                    {
//                        ed.WriteMessage($"\n错误：处理 ObjectId {objId} 时发生异常: {ex.Message}");
//                    }
//                }
//                // 添加剖面图标注
//                AddSectionLabel(db, tr, btr, sl, transformation);
//            }
//        }
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
//        // 新方法：添加剖面图标注
//        private static void AddSectionLabel(Database db, Transaction tr, BlockTableRecord btr, SectionLine sl, Matrix3d transformation)
//        {
//            Line line = sl.LineX ?? sl.LineY;
//            bool isXDirection = sl.LineX != null;
//            string label = isXDirection ? $"{sl.Index + 1}-{sl.Index + 1} 剖面图" : $"{(char)('A' + sl.Index)}-{(char)('A' + sl.Index)} 剖面图";
//            double textHeight = 5 * sl.Scale;
//            // 计算文字长度（近似，每个字符宽度假设为高度的 0.6 倍）
//            double textWidth = label.Length * textHeight * 0.6;
//            double underlineLength = textWidth + textHeight * 0.6; // 多一个字符长度
//            // 计算边界框的参考点（X 向下侧，Y 向左侧）
//            Point3d startPoint = line.StartPoint.TransformBy(transformation);
//            Point3d endPoint = line.EndPoint.TransformBy(transformation);
//            Point3d labelPos = isXDirection
//                ? new Point3d((startPoint.X + endPoint.X) / 2, startPoint.Y - textHeight * 2, 0) // X 向下侧
//                : new Point3d(startPoint.X - textWidth - textHeight, (startPoint.Y + endPoint.Y) / 2, 0); // Y 向左侧
//            // 创建文字（使用系统默认样式）
//            DBText text = new DBText
//            {
//                TextString = label,
//                Height = textHeight,
//                Position = labelPos,
//                Layer = "00_hy_section",
//                TextStyleId = db.Textstyle // 系统默认文字样式
//            };
//            btr.AppendEntity(text);
//            tr.AddNewlyCreatedDBObject(text, true);
//            // 创建下划线
//            Point3d underlineStart = new Point3d(labelPos.X, labelPos.Y - textHeight * 0.2, 0);
//            Point3d underlineEnd = new Point3d(labelPos.X + underlineLength, labelPos.Y - textHeight * 0.2, 0);
//            Line underline = new Line(underlineStart, underlineEnd) { Layer = "00_hy_section" };
//            btr.AppendEntity(underline);
//            tr.AddNewlyCreatedDBObject(underline, true);
//        }
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
//                    ObjectIdCollection entityIds = Select3DObjects(ed);
//                    if (entityIds.Count == 0) return;
//                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
//                    GenerateSection(db, tr, ed, sectionLines, displacement, entityIds, btr);
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