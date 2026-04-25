using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;

namespace HyCADTool.Presentation.Commands
{
    /// <summary>
    /// 测试Arc简化为三角形
    /// C20: 选择一个Arc，生成简化三角形（起点-中点-终点），顺时针排列
    /// </summary>
    public class TestArcSimplifyCommand
    {
        /// <summary>
        /// 测试Arc简化（通过Recall的C20调用）
        /// </summary>
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            var ed = doc.Editor;
            var db = doc.Database;

            try
            {
                ed.WriteMessage("\n━━━━ Arc简化测试 ━━━━");
                
                // 1. 选择一个Arc
                var selOptions = new PromptEntityOptions("\n选择一个圆弧（Arc）：");
                selOptions.SetRejectMessage("\n只能选择Arc类型");
                selOptions.AddAllowedClass(typeof(Arc), true);
                
                var selResult = ed.GetEntity(selOptions);
                if (selResult.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n取消选择");
                    return;
                }

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 2. 获取Arc对象
                    var arc = tr.GetObject(selResult.ObjectId, OpenMode.ForRead) as Arc;
                    if (arc == null)
                    {
                        ed.WriteMessage("\n错误：获取Arc失败");
                        return;
                    }

                    // 3. 提取Arc的关键点
                    var center = arc.Center;
                    var radius = arc.Radius;
                    var startAngle = arc.StartAngle;  // 弧度
                    var endAngle = arc.EndAngle;      // 弧度
                    
                    // 计算起点、中点、终点
                    var startPoint = arc.StartPoint;
                    var endPoint = arc.EndPoint;
                    
                    // 中点：参数中间值
                    var midParam = (arc.StartParam + arc.EndParam) / 2.0;
                    var midPoint = arc.GetPointAtParameter(midParam);

                    // 4. 输出信息
                    ed.WriteMessage("\n━━━━ Arc信息 ━━━━");
                    ed.WriteMessage($"\n圆心: ({center.X:F4}, {center.Y:F4}, {center.Z:F4})");
                    ed.WriteMessage($"\n半径: {radius:F4}");
                    ed.WriteMessage($"\n起始角度: {startAngle * 180 / Math.PI:F2}°");
                    ed.WriteMessage($"\n结束角度: {endAngle * 180 / Math.PI:F2}°");
                    ed.WriteMessage($"\n扫过角度: {(endAngle - startAngle) * 180 / Math.PI:F2}°");
                    
                    ed.WriteMessage("\n━━━━ 三角形顶点 ━━━━");
                    ed.WriteMessage($"\n起点: ({startPoint.X:F10}, {startPoint.Y:F10})");
                    ed.WriteMessage($"\n中点: ({midPoint.X:F10}, {midPoint.Y:F10})");
                    ed.WriteMessage($"\n终点: ({endPoint.X:F10}, {endPoint.Y:F10})");

                    // 5. 判断顺时针/逆时针（使用叉积）
                    var v1 = midPoint - startPoint;
                    var v2 = endPoint - midPoint;
                    var crossZ = v1.X * v2.Y - v1.Y * v2.X;
                    
                    bool isClockwise = crossZ < 0;
                    ed.WriteMessage($"\n方向: {(isClockwise ? "顺时针" : "逆时针")}");
                    
                    // 6. 创建三角形多段线（确保顺时针）
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    var polyline = new Polyline(3);
                    polyline.Layer = "0";
                    polyline.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                        Autodesk.AutoCAD.Colors.ColorMethod.ByColor, 6); // 洋红色

                    // 确保顺时针排列
                    if (isClockwise)
                    {
                        // 已经是顺时针：起点→中点→终点
                        polyline.AddVertexAt(0, new Point2d(startPoint.X, startPoint.Y), 0, 0, 0);
                        polyline.AddVertexAt(1, new Point2d(midPoint.X, midPoint.Y), 0, 0, 0);
                        polyline.AddVertexAt(2, new Point2d(endPoint.X, endPoint.Y), 0, 0, 0);
                        
                        ed.WriteMessage("\n顶点顺序: 起点→中点→终点 (顺时针)");
                    }
                    else
                    {
                        // 逆时针，需要反转：起点→终点→中点
                        polyline.AddVertexAt(0, new Point2d(startPoint.X, startPoint.Y), 0, 0, 0);
                        polyline.AddVertexAt(1, new Point2d(endPoint.X, endPoint.Y), 0, 0, 0);
                        polyline.AddVertexAt(2, new Point2d(midPoint.X, midPoint.Y), 0, 0, 0);
                        
                        ed.WriteMessage("\n顶点顺序: 起点→终点→中点 (调整为顺时针)");
                    }

                    polyline.Closed = true;

                    // 7. 添加到图形
                    btr.AppendEntity(polyline);
                    tr.AddNewlyCreatedDBObject(polyline, true);

                    // 8. 在3个顶点处绘制标记点（便于观察）
                    DrawMarker(btr, tr, startPoint, 1, "起点");  // 红色
                    DrawMarker(btr, tr, midPoint, 3, "中点");    // 绿色
                    DrawMarker(btr, tr, endPoint, 5, "终点");    // 蓝色

                    tr.Commit();

                    ed.WriteMessage("\n━━━━━━━━━━━━━━━━━━━━");
                    ed.WriteMessage("\n✅ 三角形已生成（洋红色）");
                    ed.WriteMessage("\n✅ 起点=红色, 中点=绿色, 终点=蓝色");
                    ed.WriteMessage("\n━━━━━━━━━━━━━━━━━━━━");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 绘制标记点
        /// </summary>
        private void DrawMarker(BlockTableRecord btr, Transaction tr, Point3d point, short colorIndex, string text)
        {
            // 绘制小圆圈
            var circle = new Circle
            {
                Center = point,
                Radius = 5.0,  // 标记半径
                Layer = "0",
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByColor, colorIndex)
            };
            btr.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);

            // 添加文字标注
            var textHeight = 10.0;
            var mtext = new MText
            {
                Location = new Point3d(point.X + 10, point.Y + 10, 0),
                TextHeight = textHeight,
                Contents = text,
                Layer = "0",
                Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByColor, colorIndex)
            };
            btr.AppendEntity(mtext);
            tr.AddNewlyCreatedDBObject(mtext, true);
        }
    }
}

