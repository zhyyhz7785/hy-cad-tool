using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Models;
using HyCADTool.Utilities;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("hyabR_RectSection")] // AutoCAD 命令名 "hyabRectSection"
        public static void AnchorBoltCreateSection()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                // 提示用户选择圆或块
                PromptSelectionOptions selOpts = new PromptSelectionOptions();
                selOpts.MessageForAdding = "\n请选择一个或多个螺栓（圆或块）以绘制剖面图: ";
                // 设置筛选器：仅选择圆和块，且图层名匹配 "00_Hy_螺栓_[1-9]"
                TypedValue[] filter = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "CIRCLE,INSERT"),
                    new TypedValue((int)DxfCode.LayerName, "00_Hy_螺栓_[1-9]")
                };
                SelectionFilter sf = new SelectionFilter(filter);
                PromptSelectionResult selRes = ed.GetSelection(selOpts, sf);
                if (selRes.Status != PromptStatus.OK) return;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    // 获取所有选择的对象
                    ObjectId[] objIds = selRes.Value.GetObjectIds();
                    int successCount = 0;
                    foreach (ObjectId objId in objIds)
                    {
                        Entity ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity; // OpenMode.ForWrite for deletion
                        if (ent == null) continue;
                        const string dataKey = "AnchorBolt";
                        AnchorBolt bolt = null;
                        Point3d center = Point3d.Origin;
                        // 从圆或块中提取螺栓数据和中心点
                        if (ent is Circle circle)
                        {
                            bolt = ExtensionDictionaryUtils.ReadFromExtensionDictionary<AnchorBolt>(tr, circle, dataKey, ed);
                            center = circle.Center;
                        }
                        else if (ent is BlockReference br)
                        {
                            bolt = ExtensionDictionaryUtils.ReadFromExtensionDictionary<AnchorBolt>(tr, br, dataKey, ed);
                            center = br.Position;
                        }
                        if (bolt == null)
                        {
                            ed.WriteMessage($"\n警告: 对象缺少螺栓数据，跳过此对象。");
                            continue;
                        }
                        // 计算剖面图参数
                        double halfD = bolt.D / 2.0; // 半径
                        double height = bolt.H1 + bolt.H2; // 总高度
                        // 定义矩形剖面的四个顶点
                        Point3d q1 = new Point3d(center.X - halfD, center.Y, center.Z);     // 左上点
                        Point3d q2 = new Point3d(center.X + halfD, center.Y, center.Z);     // 右上点
                        Point3d q3 = new Point3d(center.X + halfD, center.Y - height, center.Z); // 右下点
                        Point3d q4 = new Point3d(center.X - halfD, center.Y - height, center.Z); // 左下点
                        // 创建闭合的矩形多段线
                        Polyline rect = new Polyline();
                        rect.AddVertexAt(0, new Point2d(q1.X, q1.Y), 0, 0, 0);
                        rect.AddVertexAt(1, new Point2d(q2.X, q2.Y), 0, 0, 0);
                        rect.AddVertexAt(2, new Point2d(q3.X, q3.Y), 0, 0, 0);
                        rect.AddVertexAt(3, new Point2d(q4.X, q4.Y), 0, 0, 0);
                        rect.Closed = true; // 闭合多段线
                        rect.Layer = ent.Layer; // 使用与原对象相同的图层
                        // 将矩形添加到模型空间
                        btr.AppendEntity(rect);
                        tr.AddNewlyCreatedDBObject(rect, true);
                        // 删除原始螺栓对象
                        ent.Erase();
                        successCount++;
                        ed.WriteMessage($"\n成功绘制螺栓 '{bolt.Model}' 的矩形剖面，高度: {height} mm，宽度: {bolt.D} mm，并删除原对象。");
                    }
                    tr.Commit();
                    ed.WriteMessage($"\n操作完成: 成功处理 {successCount} 个螺栓。");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }
}