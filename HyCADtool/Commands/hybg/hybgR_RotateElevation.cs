using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass.ElevationSymbol;
using System;
using System.Collections.Generic;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("bgR_RotateElevation")]
        public static void RotateElevation()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            try
            {
                // 提示用户输入旋转角度
                PromptDoubleResult pdr = ed.GetDouble("\n输入旋转角度 (度): ");
                if (pdr.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n未成功输入角度，命令中止。");
                    return;
                }
                double angleDegrees = pdr.Value;
                double angleRadians = angleDegrees * Math.PI / 180.0; // 转换为弧度
                // 使用 ElevationSymbol.BuildSymbolDictionaryFromSelection 选择并构建 SymbolDictionary
                Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> symbolDictionary = ElevationSymbol.BuildSymbolDictionaryFromSelection();
                if (symbolDictionary.Count == 0)
                {
                    ed.WriteMessage("\n未找到有效的标高符号，命令中止。");
                    return;
                }
                // 使用 Transaction 旋转所有对象
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var entry in symbolDictionary)
                    {
                        ObjectId shapeId = entry.Key.Item1;
                        ObjectId shape1Id = entry.Key.Item2;
                        ObjectId textId = entry.Value;
                        // 获取 Shape 并确定旋转中心为第三个点
                        Polyline shape = tr.GetObject(shapeId, OpenMode.ForWrite) as Polyline;
                        if (shape != null && shape.NumberOfVertices >= 3) // 确保有第三个点
                        {
                            Point3d center = shape.GetPoint3dAt(2); // 第三个端点作为旋转中心
                            // 旋转 Shape
                            shape.TransformBy(Matrix3d.Rotation(angleRadians, Vector3d.ZAxis, center));
                            // 获取并旋转 Shape1
                            Polyline shape1 = tr.GetObject(shape1Id, OpenMode.ForWrite) as Polyline;
                            if (shape1 != null)
                            {
                                shape1.TransformBy(Matrix3d.Rotation(angleRadians, Vector3d.ZAxis, center));
                            }
                            // 获取并旋转 DBText
                            DBText text = tr.GetObject(textId, OpenMode.ForWrite) as DBText;
                            if (text != null)
                            {
                                text.TransformBy(Matrix3d.Rotation(angleRadians, Vector3d.ZAxis, center)); // 自动调整 Position 和 Rotation
                            }
                        }
                        else
                        {
                            ed.WriteMessage("\n跳过无效的 Shape，顶点数量不足。");
                        }
                    }
                    tr.Commit();
                }
                ed.WriteMessage("\n标高符号已旋转。");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}\n");
            }
        }
    }
}