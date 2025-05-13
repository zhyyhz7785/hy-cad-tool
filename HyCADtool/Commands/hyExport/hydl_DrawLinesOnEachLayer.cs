using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
[assembly: CommandClass(typeof(HyCADTool.Commands.HyCommand))]
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("hydl_DrawLinesOnEachLayer")]
        public static void DrawLinesOnEachLayer()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                // 获取图层表和块表
                LayerTable layerTable = (LayerTable)trans.GetObject(db.LayerTableId, OpenMode.ForRead);
                BlockTable blockTable = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord modelSpace = (BlockTableRecord)trans.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                // 设置初始坐标和参数
                double startX = 0;
                double startY = 0;
                double lineLength = 200;
                double offsetY = 20;
                double textHeight = 5;
                double textOffset = 1; // 文本与直线的间距
                // 遍历所有图层
                foreach (ObjectId layerId in layerTable)
                {
                    LayerTableRecord layer = (LayerTableRecord)trans.GetObject(layerId, OpenMode.ForRead);
                    // 创建直线
                    Line line = new Line(new Point3d(startX, startY, 0), new Point3d(startX + lineLength, startY, 0))
                    {
                        Layer = layer.Name
                    };
                    // 创建文本
                    DBText text = new DBText
                    {
                        Position = new Point3d(startX, startY + textOffset + textHeight, 0), // 文本在直线上方，并有间距
                        Height = textHeight,
                        TextString = layer.Name,
                        Layer = layer.Name,
                    };
                    // 设置对齐方式和对齐点
                    text.HorizontalMode = TextHorizontalMode.TextLeft;
                    text.VerticalMode = TextVerticalMode.TextBottom;
                    text.AlignmentPoint = new Point3d(startX, startY + textOffset + textHeight, 0);
                    // 设置对齐
                    text.AdjustAlignment(db);
                    // 添加直线和文本到模型空间
                    modelSpace.AppendEntity(line);
                    trans.AddNewlyCreatedDBObject(line, true);
                    modelSpace.AppendEntity(text);
                    trans.AddNewlyCreatedDBObject(text, true);
                    // 更新Y坐标
                    startY += offsetY;
                }
                // 提交事务
                trans.Commit();
            }
        }
    }
}
