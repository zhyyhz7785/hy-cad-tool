using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
namespace HyCADTool.HelpClass.CreatBase
{
    public static class CreateSection
    {
        // 创建所需图层
        public static void CreateLayers(Database db, Transaction tr)
        {
            Dictionary<string, short> layerSettings = new Dictionary<string, short>
    {
        { "00_hy_Section_SectionGeometry", 1 },
        { "00_hy_Section_BackgroundGeometry", 2 },
        { "00_hy_Section_ForegroundGeometry", 3 },
        { "00_hy_Section_CurveGeometry", 4 },
        { "00_hy_Section_FillGeometry", 5 },
        { "00_hy_Section_Section", 6 },
        { "00_hy_Section_Symbol", 7 },
        // 新增标高线图层，颜色 2
        { "00_hy_Section_标高线", 2 }
    };
            foreach (var layer in layerSettings)
            {
                EtGpt.CreateLayer(layer.Key, layer.Value);
            }
        }
        // 获取剖面位移距离
        public static double GetDisplacement(Editor ed)
        {
            PromptDoubleOptions pdo = new PromptDoubleOptions("\n请输入剖面位移距离 (默认 12000): ")
            {
                DefaultValue = 12000,
                AllowNegative = false
            };
            PromptDoubleResult pdr = ed.GetDouble(pdo);
            return pdr.Status == PromptStatus.OK ? pdr.Value : 12000;
        }
        // 选择 3D 实体
        public static ObjectIdCollection Select3DObjects(Editor ed)
        {
            PromptSelectionOptions pso = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择要生成剖面的 3D 对象: "
            };
            TypedValue[] filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "3DSOLID"),
                new TypedValue((int)DxfCode.Start, "SURFACE"),
                new TypedValue((int)DxfCode.Start, "MESH"),
                new TypedValue((int)DxfCode.Operator, "OR>")
            };
            SelectionFilter filter = new SelectionFilter(filterList);
            PromptSelectionResult psr = ed.GetSelection(pso, filter);
            return psr.Status == PromptStatus.OK ? new ObjectIdCollection(psr.Value.GetObjectIds()) : new ObjectIdCollection();
        }
        // 根据是 X 还是 Y 方向，获取对应的旋转+位移矩阵
        public static Matrix3d GetTransformation(bool isXDirection, double displacement, int index)
        {
            return isXDirection
                ? Matrix3d.Displacement(new Vector3d(0, -displacement * (index + 1), 0))
                  * Matrix3d.Rotation(-Math.PI / 2, Vector3d.XAxis, Point3d.Origin)
                : Matrix3d.Displacement(new Vector3d(-displacement * (index + 1), 0, 0))
                  * Matrix3d.Rotation(-Math.PI / 2, Vector3d.YAxis, Point3d.Origin);
        }
        /// <summary>
        /// 将生成的几何放到指定图层并应用变换，同时收集到列表
        /// </summary>
        public static void AssignGeometryToLayerAndCollect(
            Array geometry,
            string layerName,
            Matrix3d transformation,
            BlockTableRecord btr,
            Transaction tr,
            List<Entity> collector)
        {
            if (geometry == null || geometry.Length == 0) return;
            foreach (object geom in geometry)
            {
                if (geom is Entity ent && ent != null)
                {
                    ent.Layer = layerName;
                    ent.TransformBy(transformation);
                    btr.AppendEntity(ent);
                    tr.AddNewlyCreatedDBObject(ent, true);
                    collector.Add(ent);
                }
            }
        }
        /// <summary>
        /// 根据当前剖面线，生成 2D 剖面几何并收集外包框，以便放置文字等。
        /// </summary>
        public static void GenerateSection(
            Database db,
            Transaction tr,
            Editor ed,
            List<SectionLine> sectionLines,
            double displacement,
            ObjectIdCollection entityIds,
            BlockTableRecord btr)
        {
            foreach (var sl in sectionLines)
            {
                Line line = sl.LineX ?? sl.LineY;
                if (line == null)
                {
                    ed.WriteMessage("\n警告：剖面线的 LineX 和 LineY 均为 null，已跳过。");
                    continue;
                }
                Point3dCollection sectionPoints = new Point3dCollection { line.StartPoint, line.EndPoint };
                Section section = new Section(sectionPoints, Vector3d.ZAxis)
                {
                    Layer = "00_hy_Section_Section"
                };
                Vector3d lineVector = (line.EndPoint - line.StartPoint).GetNormal();
                Vector3d viewingDir = lineVector.CrossProduct(Vector3d.ZAxis).GetNormal();
                section.ViewingDirection = viewingDir;
                double largeHeight = 1000000;
                section.SetHeight(SectionHeight.HeightAboveSectionLine, largeHeight);
                section.SetHeight(SectionHeight.HeightBelowSectionLine, largeHeight);
                btr.AppendEntity(section);
                tr.AddNewlyCreatedDBObject(section, true);
                if (section.Settings.IsNull)
                {
                    ed.WriteMessage("\n错误：section.Settings 无效，无法配置 SectionSettings。");
                    continue;
                }
                // 配置剖面设置
                try
                {
                    using (SectionSettings settings = tr.GetObject(section.Settings, OpenMode.ForWrite) as SectionSettings)
                    {
                        if (settings == null)
                        {
                            ed.WriteMessage("\n错误：无法打开 SectionSettings 对象。");
                            continue;
                        }
                        settings.CurrentSectionType = SectionType.Section2d;
                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.BackgroundGeometry, true);
                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.ForegroundGeometry, false);
                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.CurveTangencyLines, true);
                        settings.SetVisibility(SectionType.Section2d, SectionGeometry.IntersectionFill, true);
                    }
                }
                catch (Autodesk.AutoCAD.Runtime.Exception ex)
                {
                    ed.WriteMessage($"\n错误：配置 SectionSettings 时发生异常: {ex.Message}");
                    continue;
                }
                // 添加端点符号（已在 SectionLine.AddSymbols 中改为使用 Polyline）
                sl.AddSymbols(db, tr, btr);
                Matrix3d transformation = GetTransformation(sl.LineX != null, displacement, sl.Index);
                // 收集当前剖面所有实体
                List<Entity> generatedEntities = new List<Entity>();
                // 针对每个3D实体生成2D剖面几何
                foreach (ObjectId objId in entityIds)
                {
                    if (objId.IsNull) continue;
                    try
                    {
                        Entity entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (entity != null)
                        {
                            Array sectionGeometry, backgroundGeometry, foregroundGeometry, curveGeometry, fillGeometry;
                            section.GenerateSectionGeometry(entity,
                                out sectionGeometry,
                                out backgroundGeometry,
                                out foregroundGeometry,
                                out curveGeometry,
                                out fillGeometry);
                            AssignGeometryToLayerAndCollect(sectionGeometry, "00_hy_Section_SectionGeometry", transformation, btr, tr, generatedEntities);
                            AssignGeometryToLayerAndCollect(backgroundGeometry, "00_hy_Section_BackgroundGeometry", transformation, btr, tr, generatedEntities);
                            // 若需要可打开以下
                            //AssignGeometryToLayerAndCollect(foregroundGeometry, "00_hy_Section_ForegroundGeometry", transformation, btr, tr, generatedEntities);
                            //AssignGeometryToLayerAndCollect(curveGeometry, "00_hy_Section_CurveGeometry", transformation, btr, tr, generatedEntities);
                            //AssignGeometryToLayerAndCollect(fillGeometry,  "00_hy_Section_FillGeometry", transformation, btr, tr, generatedEntities);
                        }
                    }
                    catch (Autodesk.AutoCAD.Runtime.Exception ex)
                    {
                        ed.WriteMessage($"\n错误：处理 ObjectId {objId} 时异常: {ex.Message}");
                    }
                }
                // 最后，添加剖面文字+下划线
                AddSectionLabel(db, tr, btr, sl, generatedEntities);
            }
        }
        // 计算实体集合的外包矩形
        private static Extents3d GetEntitiesExtents(List<Entity> entities)
        {
            bool first = true;
            Extents3d totalExtents = new Extents3d();
            foreach (Entity ent in entities)
            {
                try
                {
                    Extents3d ext = ent.GeometricExtents;
                    if (first)
                    {
                        totalExtents = ext;
                        first = false;
                    }
                    else
                    {
                        totalExtents.AddExtents(ext);
                    }
                }
                catch
                {
                    // 某些实体可能无法获取外包框
                }
            }
            return totalExtents;
        }
        public static void AddSectionLabel(
     Database db,
     Transaction tr,
     BlockTableRecord btr,
     SectionLine sl,
     List<Entity> sectionEntities)
        {
            if (sectionEntities == null || sectionEntities.Count == 0) return;
            Extents3d ext = GetEntitiesExtents(sectionEntities);
            if (ext.MinPoint == null || ext.MaxPoint == null) return;
            // 1. 外包框中心
            Point3d center = new Point3d(
                (ext.MinPoint.X + ext.MaxPoint.X) / 2.0,
                (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0,
                0
            );
            // 2. 向上偏移
            double offsetY = 10 * sl.Scale;
            center = new Point3d(center.X, center.Y + offsetY, 0);
            // 3. 根据剖面方向生成标签：X -> 1-1；Y -> A-A 等
            bool isXDirection = sl.LineX != null;
            string label = isXDirection
                ? (sl.Index + 1).ToString() // 1,2,3 ...
                : GetAlphaIndex(sl.Index);  // A,B,...Z, A1...
            string textString = $"{label}-{label} 剖面图";
            // 4. 文字大小与字符宽度
            double textHeight = 5 * sl.Scale;
            double charWidth = 0.6 * textHeight * 0.7; // (文字高度×0.6×宽度因子0.7)
            double textWidth = textString.Length * charWidth;
            // ---- 在此处先让文字的“中心”再向上抬高，避免与下划线重叠 ----
            // 您可根据需要调整此系数(0.6)，若还觉得重叠，可调大
            double extraTextOffset = 0.6 * textHeight;
            center = new Point3d(center.X, center.Y + extraTextOffset, 0);
            // ---- 文字居中对齐设置 ----
            DBText dbText = new DBText
            {
                TextString = textString,
                Height = textHeight,
                Layer = "00_hy_Section_Symbol",
                ColorIndex = 7,
                WidthFactor = 0.7
            };
            // 对齐模式：水平居中 + 垂直居中
            dbText.HorizontalMode = TextHorizontalMode.TextCenter;
            dbText.VerticalMode = TextVerticalMode.TextVerticalMid;
            // AlignmentPoint 即为我们修正后的 center
            dbText.AlignmentPoint = center;
            // 将文字加入图形
            btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);
            // 5. 下划线: 长度 = [文字总宽度] + [4 个字符宽度]，并让其中点与新的 center 对齐
            double underlineOffset = 0.6 * textHeight;   // 下划线比文字中心往下 0.2倍文字高
            double underlineLength = textWidth + 4 * charWidth;
            double halfUL = underlineLength / 2.0;
            Point3d underlineStart = new Point3d(center.X - halfUL, center.Y - underlineOffset, 0);
            Point3d underlineEnd = new Point3d(center.X + halfUL, center.Y - underlineOffset, 0);
            // 生成带宽度的多段线 (Polyline)
            Polyline pline = CreateWidePolyline(underlineStart, underlineEnd, 0.7 * sl.Scale);
            pline.Layer = "00_hy_Section_Symbol";
            pline.ColorIndex = 7;
            btr.AppendEntity(pline);
            tr.AddNewlyCreatedDBObject(pline, true);
        }
        /// <summary>
        /// 将 index 转成 A, B, ..., Z, A1, B1, ..., 的形式
        /// </summary>
        public static string GetAlphaIndex(int index)
        {
            int letterIndex = index % 26;
            int repeatCount = index / 26;
            char letter = (char)('A' + letterIndex);
            string suffix = repeatCount == 0 ? "" : repeatCount.ToString();
            return letter + suffix;
        }
        /// <summary>
        /// 辅助方法：创建一个 2 点的 2D Polyline，并设置全宽度
        /// </summary>
        public static Polyline CreateWidePolyline(Point3d start, Point3d end, double globalWidth)
        {
            // 注意：Polyline是2D的，需要传入Point2d
            Polyline pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(start.X, start.Y), 0, globalWidth, globalWidth);
            pl.AddVertexAt(1, new Point2d(end.X, end.Y), 0, globalWidth, globalWidth);
            return pl;
        }
    }
}
