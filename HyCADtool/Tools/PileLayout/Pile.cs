using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Tools
{
    public static partial class Tools
    {
        public static void AnnotateCircleCenters()
        {
            // 获取当前文档和编辑器
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor editor = doc.Editor;
            // 提示用户选择多个圆形
            PromptSelectionResult selectionResult = editor.GetSelection();
            if (selectionResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n没有选择圆形。");
                return;
            }
            // 获取选择的圆形实体
            SelectionSet selectedEntities = selectionResult.Value;
            List<Circle> circles = new List<Circle>();
            foreach (SelectedObject selectedObject in selectedEntities)
            {
                if (selectedObject != null && selectedObject.ObjectId.IsValid)
                {
                    // 过滤圆形对象
                    using (Transaction tr = doc.TransactionManager.StartTransaction())
                    {
                        Entity entity = tr.GetObject(selectedObject.ObjectId, OpenMode.ForRead) as Entity;
                        if (entity is Circle circle)
                        {
                            circles.Add(circle);
                        }
                        tr.Commit();
                    }
                }
            }
            // 按照圆心的X, Y坐标从左下到右上的顺序排序
            var sortedCircles = circles.OrderBy(c => c.Center.Y).ThenBy(c => c.Center.X).ToList();
            // 提示用户输入前缀
            PromptStringOptions prefixOptions = new PromptStringOptions("\n请输入标注前缀：");
            prefixOptions.AllowSpaces = true;
            PromptResult prefixResult = editor.GetString(prefixOptions);
            if (prefixResult.Status != PromptStatus.OK)
            {
                editor.WriteMessage("\n未输入前缀，标注将被跳过。");
                return;
            }
            string prefix = prefixResult.StringResult;
            // 创建图层 00-HY-桩标号，如果该图层不存在
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                LayerTable layerTable = tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead) as LayerTable;
                if (!layerTable.Has("00-HY-桩标号"))
                {
                    // 创建新图层
                    LayerTableRecord newLayer = new LayerTableRecord
                    {
                        Name = "00-HY-桩标号",
                        Color = Color.FromRgb(255, 255, 255) // 设置颜色为白色
                    };
                    // 将新图层添加到图层表
                    layerTable.UpgradeOpen();
                    layerTable.Add(newLayer);
                    tr.AddNewlyCreatedDBObject(newLayer, true);
                }
                tr.Commit();
            }
            // 开始标注
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                int counter = 1;
                foreach (Circle circle in sortedCircles)
                {
                    Point3d center = circle.Center;
                    string label = $"{prefix}{counter}"; // 生成标注，格式为前缀+序列号
                                                         // 创建标注文本
                    DBText text = new DBText
                    {
                        Position = new Point3d(center.X + 5, center.Y + 5, 0), // 调整位置避免重叠
                        TextString = label,
                        Height = 300, // 字体高度设置为300mm
                        Color = Color.FromColorIndex(ColorMethod.ByAci, 7), // 设置标注颜色
                        Layer = "00-HY-桩标号" // 设置标注所在图层
                    };
                    // 将标注文本添加到模型空间
                    BlockTable blockTable = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord modelSpace = tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    modelSpace.AppendEntity(text);
                    tr.AddNewlyCreatedDBObject(text, true);
                    counter++; // 增加序列号
                }
                tr.Commit();
            }
            editor.WriteMessage("\n圆心标注已完成。");
        }
    }
}
