using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
namespace SectionTest
{
    public class Commands
    {
        [CommandMethod("Create2DSectionXY")]
        public void Create2DSectionXY()
        {
            // 获取当前文档和编辑器
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                // 提示用户选择多个三维实体
                PromptSelectionOptions pso = new PromptSelectionOptions
                {
                    MessageForAdding = "\n请选择三维实体（可多选）: "
                };
                PromptSelectionResult psr = ed.GetSelection(pso);
                if (psr.Status != PromptStatus.OK) return;
                // 检查所选对象并收集三维实体
                ObjectIdCollection targets = new ObjectIdCollection();
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in psr.Value.GetObjectIds())
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent is Solid3d)
                        {
                            targets.Add(id);
                        }
                    }
                    if (targets.Count == 0)
                    {
                        ed.WriteMessage("\n未选择任何三维实体!");
                        return;
                    }
                    // 获取模型空间
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite) as BlockTableRecord;
                    // 定义剖面路径点（沿 X 轴在 XY 平面，法向量为 Y 轴）
                    Point3dCollection pts = new Point3dCollection
                    {
                        new Point3d(-100000, 0, 0),  // 起点
                        new Point3d(100000, 0, 0)    // 终点
                    };
                    // 创建剖面对象，法向量为 YAxis
                    using (Section sec = new Section(pts, Vector3d.YAxis))
                    {
                        sec.State = SectionState.Plane;
                        // 添加剖面到模型空间（可选，用于调试）
                        ms.AppendEntity(sec);
                        tr.AddNewlyCreatedDBObject(sec, true);
                        // 设置剖面类型为二维
                        SectionSettings ss = tr.GetObject(sec.Settings, OpenMode.ForWrite) as SectionSettings;
                        ss.CurrentSectionType = SectionType.Section2d;
                        // 设置源对象
                        ss.SetSourceObjects(SectionType.Section2d, targets);
                        // 设置生成选项（移除 DestinationBlock）
                        //ss.SetGenerationOptions(SectionType.Section2d, SectionGeneration.SourceSelectedObjects);
                        // 生成二维剖面几何并收集所有实体
                        ObjectIdCollection sectionEntities = new ObjectIdCollection();
                        foreach (ObjectId targetId in targets)
                        {
                            Solid3d solid = tr.GetObject(targetId, OpenMode.ForRead) as Solid3d;
                            Array intersectionBoundary, intersectionFillAnnotation, background, foreground, curveTangency;
                            sec.GenerateSectionGeometry(solid, out intersectionBoundary,
                                out intersectionFillAnnotation, out background, out foreground, out curveTangency);
                            // 定义变换：绕 X 轴旋转 -90°，向下平移 6000
                            Matrix3d transform = Matrix3d.Rotation(-Math.PI / 2, Vector3d.XAxis, Point3d.Origin) *
                                                Matrix3d.Displacement(new Vector3d(0, 0, -6000));
                            // 处理生成的几何
                            foreach (Entity e in intersectionBoundary)
                            {
                                e.TransformBy(transform);
                                ms.AppendEntity(e);
                                tr.AddNewlyCreatedDBObject(e, true);
                                sectionEntities.Add(e.ObjectId);
                            }
                            foreach (Entity e in intersectionFillAnnotation)
                            {
                                e.TransformBy(transform);
                                ms.AppendEntity(e);
                                tr.AddNewlyCreatedDBObject(e, true);
                                sectionEntities.Add(e.ObjectId);
                            }
                        }
                        // 设置填充样式（可选）
                        ss.SetHatchPatternName(SectionType.Section2d, SectionGeometry.IntersectionFill, "ANSI31");
                        ss.SetHatchVisibility(SectionType.Section2d, SectionGeometry.IntersectionFill, true);
                        // 可选：将生成的剖面几何转换为块
                        CreateBlockFromEntities(db, tr, sectionEntities, "SectionBlock", Point3d.Origin);
                    }
                    tr.Commit();
                }
                ed.WriteMessage("\n二维剖面生成完成，已旋转并平移到指定位置！");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }
        // 创建块的辅助方法
        private void CreateBlockFromEntities(Database db, Transaction tr, ObjectIdCollection entities, string blockName, Point3d basePoint)
        {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForWrite) as BlockTable;
            if (bt.Has(blockName)) return; // 如果块已存在，则跳过
            // 创建块定义
            using (BlockTableRecord btr = new BlockTableRecord())
            {
                btr.Name = blockName;
                btr.Origin = basePoint;
                // 将实体添加到块定义中并从模型空间移除
                foreach (ObjectId id in entities)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    btr.AppendEntity(ent);
                }
                bt.Add(btr);
                tr.AddNewlyCreatedDBObject(btr, true);
                // 在模型空间中插入块引用
                BlockTableRecord ms = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                using (BlockReference br = new BlockReference(basePoint, btr.ObjectId))
                {
                    ms.AppendEntity(br);
                    tr.AddNewlyCreatedDBObject(br, true);
                }
            }
        }
    }
}