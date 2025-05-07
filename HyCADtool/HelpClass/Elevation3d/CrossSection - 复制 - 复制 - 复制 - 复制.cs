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
//        // -------------------- 新增：获取选定 3D 对象的 zMin, zMax --------------------
//        private static void GetZMinZMax(ObjectIdCollection entityIds, Transaction tr, out double zMin, out double zMax)
//        {
//            zMin = double.MaxValue;
//            zMax = double.MinValue;

//            foreach (ObjectId objId in entityIds)
//            {
//                if (objId.IsNull) continue;
//                Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
//                if (ent != null)
//                {
//                    try
//                    {
//                        Extents3d ext = ent.GeometricExtents;
//                        if (ext.MinPoint.Z < zMin) zMin = ext.MinPoint.Z;
//                        if (ext.MaxPoint.Z > zMax) zMax = ext.MaxPoint.Z;
//                    }
//                    catch
//                    {
//                        // 某些实体可能不支持 GeometricExtents
//                    }
//                }
//            }

//            // 若始终没赋值成功，则设为 0
//            if (zMin == double.MaxValue) zMin = 0;
//            if (zMax == double.MinValue) zMax = 0;
//        }

//        // -------------------- 原有方法：创建图层 --------------------
//        private static void CreateLayers(Database db, Transaction tr)
//        {
//            Dictionary<string, short> layerSettings = new Dictionary<string, short>
//            {
//                { "00_hy_Section_SectionGeometry", 1 },
//                { "00_hy_Section_BackgroundGeometry", 2 },
//                { "00_hy_Section_ForegroundGeometry", 3 },
//                { "00_hy_Section_CurveGeometry", 4 },
//                { "00_hy_Section_FillGeometry", 5 },
//                { "00_hy_Section_Section", 6 },
//                { "00_hy_Section_Symbol", 7 },
//                { "00_hy_Section_标高线", 2 }
//            };

//            foreach (var layer in layerSettings)
//            {
//                EtGpt.CreateLayer(layer.Key, layer.Value);
//            }
//        }

//        // -------------------- 原有方法：提示用户输入剖面位移距离 --------------------
//        private static double GetDisplacement(Editor ed)
//        {
//            PromptDoubleOptions pdo = new PromptDoubleOptions("\n请输入剖面位移距离 (默认 6000): ")
//            {
//                DefaultValue = 6000,
//                AllowNegative = false
//            };
//            PromptDoubleResult pdr = ed.GetDouble(pdo);
//            return pdr.Status == PromptStatus.OK ? pdr.Value : 6000;
//        }

//        // -------------------- 原有方法：选择 3D 实体 --------------------
//        private static ObjectIdCollection Select3DObjects(Editor ed)
//        {
//            PromptSelectionOptions pso = new PromptSelectionOptions
//            {
//                MessageForAdding = "\n请选择要生成剖面的 3D 对象: "
//            };
//            TypedValue[] filterList = new TypedValue[]
//            {
//                new TypedValue((int)DxfCode.Operator, "<OR"),
//                new TypedValue((int)DxfCode.Start, "3DSOLID"),
//                new TypedValue((int)DxfCode.Start, "SURFACE"),
//                new TypedValue((int)DxfCode.Start, "MESH"),
//                new TypedValue((int)DxfCode.Operator, "OR>")
//            };
//            SelectionFilter filter = new SelectionFilter(filterList);
//            PromptSelectionResult psr = ed.GetSelection(pso, filter);

//            return (psr.Status == PromptStatus.OK)
//                ? new ObjectIdCollection(psr.Value.GetObjectIds())
//                : new ObjectIdCollection();
//        }

//        // -------------------- 原有方法：获取旋转+位移矩阵(不变) --------------------
//        private static Matrix3d GetTransformation(bool isXDirection, double displacement, int index)
//        {
//            return isXDirection
//                ? Matrix3d.Displacement(new Vector3d(0, -displacement * (index + 1), 0))
//                  * Matrix3d.Rotation(-Math.PI / 2, Vector3d.XAxis, Point3d.Origin)
//                : Matrix3d.Displacement(new Vector3d(-displacement * (index + 1), 0, 0))
//                  * Matrix3d.Rotation(-Math.PI / 2, Vector3d.YAxis, Point3d.Origin);
//        }

//        // -------------------- 原有方法：将几何分层并做变换 --------------------
//        private static void AssignGeometryToLayerAndCollect(
//            Array geometry,
//            string layerName,
//            Matrix3d transformation,
//            BlockTableRecord btr,
//            Transaction tr,
//            List<Entity> collector)
//        {
//            if (geometry == null || geometry.Length == 0) return;
//            foreach (object geom in geometry)
//            {
//                if (geom is Entity ent && ent != null)
//                {
//                    ent.Layer = layerName;
//                    ent.TransformBy(transformation);
//                    btr.AppendEntity(ent);
//                    tr.AddNewlyCreatedDBObject(ent, true);
//                    collector.Add(ent);
//                }
//            }
//        }

//        // -------------------- 生成剖面 + 三条线 + 变换 --------------------
//        private static void GenerateSection(
//            Database db,
//            Transaction tr,
//            Editor ed,
//            List<SectionLine> sectionLines,
//            double displacement,
//            ObjectIdCollection entityIds,
//            BlockTableRecord btr,
//            double zMin,      // 新增: 用于画三条线
//            double zMax       // 新增: 同上
//        )
//        {
//            foreach (var sl in sectionLines)
//            {
//                Line line = sl.LineX ?? sl.LineY;
//                if (line == null)
//                {
//                    ed.WriteMessage("\n警告：剖面线的 LineX 和 LineY 均为 null，已跳过。");
//                    continue;
//                }

//                // 1) 创建 Section
//                Point3dCollection sectionPoints = new Point3dCollection { line.StartPoint, line.EndPoint };
//                Section section = new Section(sectionPoints, Vector3d.ZAxis)
//                {
//                    Layer = "00_hy_Section_Section"
//                };

//                Vector3d lineVector = (line.EndPoint - line.StartPoint).GetNormal();
//                Vector3d viewingDir = lineVector.CrossProduct(Vector3d.ZAxis).GetNormal();
//                section.ViewingDirection = viewingDir;

//                double largeHeight = 1000000;
//                section.SetHeight(SectionHeight.HeightAboveSectionLine, largeHeight);
//                section.SetHeight(SectionHeight.HeightBelowSectionLine, largeHeight);

//                btr.AppendEntity(section);
//                tr.AddNewlyCreatedDBObject(section, true);

//                if (section.Settings.IsNull)
//                {
//                    ed.WriteMessage("\n错误：section.Settings 无效，无法配置 SectionSettings。");
//                    continue;
//                }

//                try
//                {
//                    using (SectionSettings settings = tr.GetObject(section.Settings, OpenMode.ForWrite) as SectionSettings)
//                    {
//                        if (settings == null)
//                        {
//                            ed.WriteMessage("\n错误：无法打开 SectionSettings 对象。");
//                            continue;
//                        }
//                        settings.CurrentSectionType = SectionType.Section2d;
//                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.BackgroundGeometry, true);
//                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.ForegroundGeometry, false);
//                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.CurveTangencyLines, true);
//                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.IntersectionFill, true);
//                    }
//                }
//                catch (Autodesk.AutoCAD.Runtime.Exception ex)
//                {
//                    ed.WriteMessage($"\n错误：配置 SectionSettings 时发生异常: {ex.Message}");
//                    continue;
//                }

//                // 2) 剖面线端点符号
//                sl.AddSymbols(db, tr, btr);

//                // 3) 计算“旋转+位移”矩阵(原有逻辑)
//                bool isXDirection = (sl.LineX != null);
//                Matrix3d transformation = GetTransformation(isXDirection, displacement, sl.Index);

//                // 4) 在 xz 或 yz 平面 添加三条线 (zMax,0,zMin)
//                double[] zValues = { zMax, 0.0, zMin };
//                double length = 10 * sl.Scale; // 短线长度

//                List<Entity> customLines = new List<Entity>();
//                if (isXDirection)
//                {
//                    // X 剖面 => xz 平面：y=固定, 线平行X
//                    double yFixed = line.StartPoint.Y;
//                    double xMinVal = Math.Min(line.StartPoint.X, line.EndPoint.X);

//                    foreach (double zv in zValues)
//                    {
//                        Point3d st = new Point3d(xMinVal, yFixed, zv);
//                        Point3d en = new Point3d(xMinVal + length, yFixed, zv);

//                        Line shortLine = new Line(st, en)
//                        {
//                            Layer = "00_hy_Section_标高线",
//                            ColorIndex = 1
//                        };
//                        customLines.Add(shortLine);
//                    }
//                }
//                else
//                {
//                    // Y 剖面 => yz 平面：x=固定, 线平行Y
//                    double xFixed = line.StartPoint.X;
//                    double yMinVal = Math.Min(line.StartPoint.Y, line.EndPoint.Y);

//                    foreach (double zv in zValues)
//                    {
//                        Point3d st = new Point3d(xFixed, yMinVal, zv);
//                        Point3d en = new Point3d(xFixed, yMinVal + length, zv);

//                        Line shortLine = new Line(st, en)
//                        {
//                            Layer = "00_hy_Section_标高线",
//                            ColorIndex = 1
//                        };
//                        customLines.Add(shortLine);
//                    }
//                }

//                // 把这三条线也统一执行transform并入库
//                List<Entity> generatedEntities = new List<Entity>();
//                AssignGeometryToLayerAndCollect(
//                    customLines.ToArray(),
//                    "00_hy_Section_标高线",
//                    transformation,
//                    btr,
//                    tr,
//                    generatedEntities
//                );

//                // 5) 针对每个 3D 实体生成截面几何，并执行同样的 transform
//                foreach (ObjectId objId in entityIds)
//                {
//                    if (objId.IsNull) continue;
//                    try
//                    {
//                        Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
//                        if (ent != null)
//                        {
//                            Array sectionGeometry, backgroundGeometry, foregroundGeometry, curveGeometry, fillGeometry;
//                            section.GenerateSectionGeometry(
//                                ent,
//                                out sectionGeometry,
//                                out backgroundGeometry,
//                                out foregroundGeometry,
//                                out curveGeometry,
//                                out fillGeometry
//                            );

//                            AssignGeometryToLayerAndCollect(sectionGeometry,
//                                "00_hy_Section_SectionGeometry",
//                                transformation, btr, tr, generatedEntities);
//                            AssignGeometryToLayerAndCollect(backgroundGeometry,
//                                "00_hy_Section_BackgroundGeometry",
//                                transformation, btr, tr, generatedEntities);

//                            // 如果需要其他几何，可再打开
//                            //AssignGeometryToLayerAndCollect(foregroundGeometry,"00_hy_Section_ForegroundGeometry", transformation,btr,tr,generatedEntities);
//                            //AssignGeometryToLayerAndCollect(curveGeometry,    "00_hy_Section_CurveGeometry",      transformation,btr,tr,generatedEntities);
//                            //AssignGeometryToLayerAndCollect(fillGeometry,     "00_hy_Section_FillGeometry",       transformation,btr,tr,generatedEntities);
//                        }
//                    }
//                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
//                    {
//                        ed.WriteMessage($"\n错误：处理 ObjectId {objId} 时异常: {ex.Message}");
//                    }
//                }

//                // 6) 添加剖面文字 + 下划线
//                AddSectionLabel(db, tr, btr, sl, generatedEntities);
//            }
//        }

//        // -------------------- 计算实体外包矩形，用于文字等定位 --------------------
//        private static Extents3d GetEntitiesExtents(List<Entity> entities)
//        {
//            bool first = true;
//            Extents3d totalExtents = new Extents3d();
//            foreach (Entity ent in entities)
//            {
//                try
//                {
//                    Extents3d ext = ent.GeometricExtents;
//                    if (first)
//                    {
//                        totalExtents = ext;
//                        first = false;
//                    }
//                    else
//                    {
//                        totalExtents.AddExtents(ext);
//                    }
//                }
//                catch
//                {
//                    // 某些实体可能无法获取外包框
//                }
//            }
//            return totalExtents;
//        }

//        // -------------------- 添加剖面文字 + 下划线 --------------------
//        private static void AddSectionLabel(
//            Database db,
//            Transaction tr,
//            BlockTableRecord btr,
//            SectionLine sl,
//            List<Entity> sectionEntities)
//        {
//            if (sectionEntities == null || sectionEntities.Count == 0) return;

//            Extents3d ext = GetEntitiesExtents(sectionEntities);
//            if (ext.MinPoint == null || ext.MaxPoint == null) return;

//            Point3d center = new Point3d(
//                (ext.MinPoint.X + ext.MaxPoint.X) / 2.0,
//                (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0,
//                0
//            );

//            double offsetY = 10 * sl.Scale;
//            center = new Point3d(center.X, center.Y + offsetY, 0);

//            bool isXDirection = sl.LineX != null;
//            string label = isXDirection
//                ? (sl.Index + 1).ToString()  // 1,2,3 ...
//                : GetAlphaIndex(sl.Index);   // A,B,...Z, A1...

//            string textString = $"{label}-{label} 剖面图";

//            double textHeight = 5 * sl.Scale;
//            double charWidth = 0.6 * textHeight * 0.7;
//            double textWidth = textString.Length * charWidth;

//            double extraTextOffset = 0.6 * textHeight;
//            center = new Point3d(center.X, center.Y + extraTextOffset, 0);

//            DBText dbText = new DBText
//            {
//                TextString = textString,
//                Height = textHeight,
//                Layer = "00_hy_Section_Symbol",
//                ColorIndex = 7,
//                WidthFactor = 0.7,
//                HorizontalMode = TextHorizontalMode.TextCenter,
//                VerticalMode = TextVerticalMode.TextVerticalMid,
//                AlignmentPoint = center
//            };
//            btr.AppendEntity(dbText);
//            tr.AddNewlyCreatedDBObject(dbText, true);

//            double underlineOffset = 0.6 * textHeight;
//            double underlineLength = textWidth + 4 * charWidth;
//            double halfUL = underlineLength / 2.0;
//            Point3d underlineStart = new Point3d(center.X - halfUL, center.Y - underlineOffset, 0);
//            Point3d underlineEnd = new Point3d(center.X + halfUL, center.Y - underlineOffset, 0);

//            Polyline pline = CreateWidePolyline(underlineStart, underlineEnd, 0.7 * sl.Scale);
//            pline.Layer = "00_hy_Section_Symbol";
//            pline.ColorIndex = 7;
//            btr.AppendEntity(pline);
//            tr.AddNewlyCreatedDBObject(pline, true);
//        }

//        private static string GetAlphaIndex(int index)
//        {
//            int letterIndex = index % 26;
//            int repeatCount = index / 26;
//            char letter = (char)('A' + letterIndex);
//            string suffix = repeatCount == 0 ? "" : repeatCount.ToString();
//            return letter + suffix;
//        }

//        private static Polyline CreateWidePolyline(Point3d start, Point3d end, double globalWidth)
//        {
//            Polyline pl = new Polyline();
//            pl.AddVertexAt(0, new Point2d(start.X, start.Y), 0, globalWidth, globalWidth);
//            pl.AddVertexAt(1, new Point2d(end.X, end.Y), 0, globalWidth, globalWidth);
//            return pl;
//        }

//        // -------------------- 命令入口 --------------------
//        [CommandMethod("hy3_Elevation_CreateSection")]
//        public static void CreateSectionCommand()
//        {
//            Document doc = Application.DocumentManager.MdiActiveDocument;
//            Database db = doc.Database;
//            Editor ed = doc.Editor;

//            try
//            {
//                using (Transaction tr = db.TransactionManager.StartTransaction())
//                {
//                    // 1) 创建图层
//                    CreateLayers(db, tr);

//                    // 2) 获取剖面位移距离
//                    double displacement = GetDisplacement(ed);

//                    // 3) 选择剖面线
//                    var sectionLines = SectionLine.SelectLines(ed);
//                    if (sectionLines.Count == 0) return;
//                    SectionLine.SortAndIndexLines(sectionLines);

//                    // 4) 选择 3D 对象
//                    ObjectIdCollection entityIds = Select3DObjects(ed);
//                    if (entityIds.Count == 0) return;

//                    // 5) 计算 zMin, zMax
//                    GetZMinZMax(entityIds, tr, out double zMin, out double zMax);

//                    // 6) 获取图形空间
//                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

//                    // 7) 生成剖面(含三条线), 并一起变换
//                    GenerateSection(db, tr, ed, sectionLines, displacement, entityIds, btr, zMin, zMax);

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
