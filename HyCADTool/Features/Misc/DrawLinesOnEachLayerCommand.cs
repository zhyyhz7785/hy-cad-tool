using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Misc
{
    /// <summary>
    /// 遍历所有图层，在指定位置为每个图层绘制一条示例线 + 图层名文字
    /// </summary>
    public class DrawLinesOnEachLayerCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 让用户点击插入点
            var ptResult = ed.GetPoint("\n选择插入基点: ");
            if (ptResult.Status != PromptStatus.OK) return;

            double startX = ptResult.Value.X;
            double startY = ptResult.Value.Y;
            double lineLength = 200;
            double offsetY = 20;
            double textHeight = 5;
            double textOffset = 1;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (ObjectId layerId in layerTable)
                    {
                        var layer = (LayerTableRecord)tr.GetObject(layerId, OpenMode.ForRead);

                        // 线段
                        var line = new Line(
                            new Point3d(startX, startY, 0),
                            new Point3d(startX + lineLength, startY, 0))
                        {
                            Layer = layer.Name
                        };

                        // 文字
                        var text = new DBText
                        {
                            Position = new Point3d(startX, startY + textOffset + textHeight, 0),
                            Height = textHeight,
                            TextString = layer.Name,
                            Layer = layer.Name,
                            HorizontalMode = TextHorizontalMode.TextLeft,
                            VerticalMode = TextVerticalMode.TextBottom,
                            AlignmentPoint = new Point3d(startX, startY + textOffset + textHeight, 0)
                        };
                        text.AdjustAlignment(db);

                        ms.AppendEntity(line);
                        tr.AddNewlyCreatedDBObject(line, true);
                        ms.AppendEntity(text);
                        tr.AddNewlyCreatedDBObject(text, true);

                        startY += offsetY;
                    }

                    tr.Commit();
                }

                ed.WriteMessage("\n图层示例线绘制完成");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n绘制失败: {ex.Message}");
            }
        }
    }
}
