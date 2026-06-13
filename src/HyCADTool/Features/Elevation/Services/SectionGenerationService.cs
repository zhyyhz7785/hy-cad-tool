using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace HyCADTool.Features.Elevation.Services
{
    /// <summary>
    /// 剖面设计图生成服务（迁移自旧版 HelpClass/Elevation3d/CrossSection.cs 的 CreateSection 静态类）。
    ///
    /// 流程：对每条剖切线创建 AutoCAD <see cref="Section"/> 对象 →
    /// 对选定 3D 实体（3DSOLID/SURFACE/MESH）调用 <c>GenerateSectionGeometry</c> 生成 2D 剖面几何 →
    /// 按 X/Y 方向旋转 + 平移到图纸旁排布 → 绘制剖切符号与「N-N 剖面图」标签。
    /// </summary>
    public static class SectionGenerationService
    {
        public const string SectionGeometryLayer = "00_hy_Section_SectionGeometry";
        public const string BackgroundGeometryLayer = "00_hy_Section_BackgroundGeometry";
        public const string ForegroundGeometryLayer = "00_hy_Section_ForegroundGeometry";
        public const string CurveGeometryLayer = "00_hy_Section_CurveGeometry";
        public const string FillGeometryLayer = "00_hy_Section_FillGeometry";
        public const string SectionLayer = "00_hy_Section_Section";
        public const string SymbolLayer = "00_hy_Section_Symbol";
        public const string ElevationLineLayer = "00_hy_Section_标高线";

        /// <summary>估算单字符宽度的经验系数（≈ 0.6 字宽比 × 0.7 WidthFactor），用于按文字长度排布下划线 / 延伸线。</summary>
        public const double ApproxCharWidthFactor = 0.42;

        /// <summary>创建剖面所需图层（已存在则跳过）。</summary>
        public static void CreateLayers(Database db, Transaction tr)
        {
            var layerSettings = new Dictionary<string, short>
            {
                { SectionGeometryLayer, 1 },
                { BackgroundGeometryLayer, 2 },
                { ForegroundGeometryLayer, 3 },
                { CurveGeometryLayer, 4 },
                { FillGeometryLayer, 5 },
                { SectionLayer, 6 },
                { SymbolLayer, 7 },
                { ElevationLineLayer, 2 },
            };

            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            foreach (var kv in layerSettings)
            {
                if (layerTable.Has(kv.Key)) continue;
                if (!layerTable.IsWriteEnabled) layerTable.UpgradeOpen();
                var ltr = new LayerTableRecord
                {
                    Name = kv.Key,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, kv.Value),
                };
                layerTable.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }

        /// <summary>选择要生成剖面的 3D 对象（3DSOLID / SURFACE / MESH）。</summary>
        public static ObjectIdCollection Select3DObjects(Editor ed)
        {
            var pso = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择要生成剖面的 3D 对象: "
            };
            var filterList = new[]
            {
                new TypedValue((int)DxfCode.Operator, "<OR"),
                new TypedValue((int)DxfCode.Start, "3DSOLID"),
                new TypedValue((int)DxfCode.Start, "SURFACE"),
                new TypedValue((int)DxfCode.Start, "MESH"),
                new TypedValue((int)DxfCode.Operator, "OR>"),
            };
            var psr = ed.GetSelection(pso, new SelectionFilter(filterList));
            return psr.Status == PromptStatus.OK
                ? new ObjectIdCollection(psr.Value.GetObjectIds())
                : new ObjectIdCollection();
        }

        /// <summary>
        /// X 向剖面：剖面图依次向下排布并绕 X 轴放平；Y 向剖面：向左排布并绕 Y 轴放平。
        /// </summary>
        public static Matrix3d GetTransformation(bool isXDirection, double displacement, int index)
        {
            return isXDirection
                ? Matrix3d.Displacement(new Vector3d(0, -displacement * (index + 1), 0))
                  * Matrix3d.Rotation(-Math.PI / 2, Vector3d.XAxis, Point3d.Origin)
                : Matrix3d.Displacement(new Vector3d(-displacement * (index + 1), 0, 0))
                  * Matrix3d.Rotation(-Math.PI / 2, Vector3d.YAxis, Point3d.Origin);
        }

        /// <summary>将生成的剖面几何放到指定图层、应用变换并收集（用于事后计算外包框放标签）。</summary>
        private static void AssignGeometryToLayerAndCollect(
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
                if (geom is Entity ent)
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
        /// 主入口：对每条剖切线生成 2D 剖面几何 + 剖切符号 + 「N-N 剖面图」标签。
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
                var line = sl.Line;
                if (line == null)
                {
                    ed.WriteMessage("\n警告：剖面线的 LineX 和 LineY 均为 null，已跳过。");
                    continue;
                }

                var sectionPoints = new Point3dCollection { line.StartPoint, line.EndPoint };
                var section = new Section(sectionPoints, Vector3d.ZAxis)
                {
                    Layer = SectionLayer,
                };

                Vector3d lineVector = (line.EndPoint - line.StartPoint).GetNormal();
                section.ViewingDirection = lineVector.CrossProduct(Vector3d.ZAxis).GetNormal();

                const double largeHeight = 1000000;
                section.SetHeight(SectionHeight.HeightAboveSectionLine, largeHeight);
                section.SetHeight(SectionHeight.HeightBelowSectionLine, largeHeight);

                btr.AppendEntity(section);
                tr.AddNewlyCreatedDBObject(section, true);

                if (section.Settings.IsNull)
                {
                    ed.WriteMessage("\n错误：section.Settings 无效，无法配置 SectionSettings。");
                    continue;
                }

                try
                {
                    var settings = tr.GetObject(section.Settings, OpenMode.ForWrite) as SectionSettings;
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
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n错误：配置 SectionSettings 时发生异常: {ex.Message}");
                    continue;
                }

                // 剖切线两端符号（短粗线 + 编号）
                sl.AddSymbols(tr, btr);

                Matrix3d transformation = GetTransformation(sl.LineX != null, displacement, sl.Index);

                var generatedEntities = new List<Entity>();
                foreach (ObjectId objId in entityIds)
                {
                    if (objId.IsNull) continue;
                    try
                    {
                        var entity = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (entity == null) continue;

                        section.GenerateSectionGeometry(entity,
                            out Array sectionGeometry,
                            out Array backgroundGeometry,
                            out Array foregroundGeometry,
                            out Array curveGeometry,
                            out Array fillGeometry);

                        AssignGeometryToLayerAndCollect(sectionGeometry, SectionGeometryLayer, transformation, btr, tr, generatedEntities);
                        AssignGeometryToLayerAndCollect(backgroundGeometry, BackgroundGeometryLayer, transformation, btr, tr, generatedEntities);
                        // 前景/切线/填充几何默认关闭，与旧版一致；需要时打开：
                        //AssignGeometryToLayerAndCollect(foregroundGeometry, ForegroundGeometryLayer, transformation, btr, tr, generatedEntities);
                        //AssignGeometryToLayerAndCollect(curveGeometry, CurveGeometryLayer, transformation, btr, tr, generatedEntities);
                        //AssignGeometryToLayerAndCollect(fillGeometry, FillGeometryLayer, transformation, btr, tr, generatedEntities);
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n错误：处理 ObjectId {objId} 时异常: {ex.Message}");
                    }
                }

                AddSectionLabel(tr, btr, sl, generatedEntities);

                // 2D 几何已生成完毕，删除 Section 活体对象，避免留在图中被后续框选误选
                section.Erase();
            }
        }

        /// <summary>计算实体集合的外包矩形；所有实体均取不到外包框时返回 false。</summary>
        private static bool TryGetEntitiesExtents(List<Entity> entities, out Extents3d totalExtents)
        {
            bool first = true;
            totalExtents = new Extents3d();
            foreach (var ent in entities)
            {
                try
                {
                    var ext = ent.GeometricExtents;
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
            return !first;
        }

        /// <summary>在剖面图上方居中绘制「N-N 剖面图」文字 + 下划线。</summary>
        private static void AddSectionLabel(
            Transaction tr,
            BlockTableRecord btr,
            SectionLine sl,
            List<Entity> sectionEntities)
        {
            if (sectionEntities == null || sectionEntities.Count == 0) return;

            // 全部实体取不到外包框时跳过标签，避免标签落到原点附近
            if (!TryGetEntitiesExtents(sectionEntities, out Extents3d ext)) return;

            var center = new Point3d(
                (ext.MinPoint.X + ext.MaxPoint.X) / 2.0,
                (ext.MinPoint.Y + ext.MaxPoint.Y) / 2.0 + 10 * sl.Scale,
                0);

            string label = sl.Label;
            string textString = $"{label}-{label} 剖面图";

            double textHeight = 5 * sl.Scale;
            double charWidth = ApproxCharWidthFactor * textHeight;
            double textWidth = textString.Length * charWidth;

            // 文字中心再上抬，避免与下划线重叠
            center = new Point3d(center.X, center.Y + 0.6 * textHeight, 0);

            var dbText = new DBText
            {
                TextString = textString,
                Height = textHeight,
                Layer = SymbolLayer,
                ColorIndex = 7,
                WidthFactor = 0.7,
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
            };
            dbText.AlignmentPoint = center;
            btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);

            // 下划线：长度 = 文字宽 + 4 字符宽，中点与文字中心对齐
            double underlineOffset = 0.6 * textHeight;
            double underlineLength = textWidth + 4 * charWidth;
            double halfUL = underlineLength / 2.0;
            var underlineStart = new Point3d(center.X - halfUL, center.Y - underlineOffset, 0);
            var underlineEnd = new Point3d(center.X + halfUL, center.Y - underlineOffset, 0);

            var pline = CreateWidePolyline(underlineStart, underlineEnd, 0.7 * sl.Scale);
            pline.Layer = SymbolLayer;
            pline.ColorIndex = 7;
            btr.AppendEntity(pline);
            tr.AddNewlyCreatedDBObject(pline, true);
        }

        /// <summary>创建 2 点带全局宽度的 2D Polyline（剖切符号短粗线 / 标签下划线）。</summary>
        public static Polyline CreateWidePolyline(Point3d start, Point3d end, double globalWidth)
        {
            var pl = new Polyline();
            pl.AddVertexAt(0, new Point2d(start.X, start.Y), 0, globalWidth, globalWidth);
            pl.AddVertexAt(1, new Point2d(end.X, end.Y), 0, globalWidth, globalWidth);
            return pl;
        }
    }
}
