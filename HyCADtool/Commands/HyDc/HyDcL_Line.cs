using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("HyDcL_Line")] // AutoCAD 命令名 "hycpad" (Create Pad)
        public static void CreatePadFromLine()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            double d = 100.0; // 默认厚度 100
            var id= HyTool.CreateLayer("00_hy_垫层",7);
            try
            {
                // 提示用户选择一条直线
                PromptSelectionOptions selOpts = new PromptSelectionOptions();
                selOpts.MessageForAdding = "\n请选择一条直线以创建垫层: ";
                selOpts.SingleOnly = true; // 限制只选择一个对象
                // 设置筛选器：仅选择直线
                TypedValue[] filter = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "LINE")
                };
                SelectionFilter sf = new SelectionFilter(filter);
                PromptSelectionResult selRes = ed.GetSelection(selOpts, sf);
                if (selRes.Status != PromptStatus.OK) return;
                // 获取默认厚度 d
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    // 获取选择的直线
                    ObjectId objId = selRes.Value.GetObjectIds()[0];
                    Line line = tr.GetObject(objId, OpenMode.ForRead) as Line;
                    if (line == null) return;
                    // 计算直线的方向向量
                    Vector3d lineVector = line.EndPoint - line.StartPoint;
                    double angle = lineVector.GetAngleTo(Vector3d.XAxis); // 获取与X轴的角度
                    if (lineVector.Y < 0) angle = -angle; // 调整角度方向
                    // 计算-90度方向的向量（顺时针旋转90度）
                    Vector3d padVector = lineVector.RotateBy(-Math.PI / 2, Vector3d.ZAxis).GetNormal() * d;
                    // 计算延长后的起点和终点
                    Vector3d extensionVector = lineVector.GetNormal() * d;
                    Point3d extendedStart = line.StartPoint - extensionVector;
                    Point3d extendedEnd = line.EndPoint + extensionVector;
                    // 定义垫层的四个顶点
                    Point3d p1 = extendedStart;                          // 延长后的起点
                    Point3d p2 = extendedEnd;                            // 延长后的终点
                    Point3d p3 = extendedEnd + padVector;                // 终点向下d
                    Point3d p4 = extendedStart + padVector;              // 起点向下d
                    // 创建闭合的矩形多段线作为垫层
                    Polyline pad = new Polyline();
                    pad.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
                    pad.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
                    pad.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
                    pad.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
                    pad.Closed = true; // 闭合多段线
                    pad.LayerId =id; // 使用与原直线相同的图层
                    // 将垫层添加到模型空间
                    btr.AppendEntity(pad);
                    tr.AddNewlyCreatedDBObject(pad, true);
                    tr.Commit();
                    ed.WriteMessage($"\n成功创建垫层，厚度: {d}，长度: {(extendedEnd - extendedStart).Length}");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
}