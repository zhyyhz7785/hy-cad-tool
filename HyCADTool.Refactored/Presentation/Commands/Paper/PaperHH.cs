using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using System;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.PaperHH))]
namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// PHH命令：创建视口，完整继承模型空间当前的UCS和PLAN视图
    /// </summary>
    public class PaperHH
    {
        [CommandMethod("phh", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public static void Test()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // 视口参数
            double viewportWidth = 315;
            double viewportHeight = 240;
            double customScale = 2;

            // 获取视图中心点
            var ptPrompt = ed.GetPoint("\n请选择视图中心点: ");
            if (ptPrompt.Status != PromptStatus.OK) return;
            Point3d centerPt = ptPrompt.Value;

            // 获取当前UCS
            Matrix3d ucsMatrix = ed.CurrentUserCoordinateSystem;
            CoordinateSystem3d currentUcs = ucsMatrix.CoordinateSystem3d;
            
            // 获取当前视图的所有参数
            ViewTableRecord currentView = ed.GetCurrentView();
            Vector3d viewDirection = currentView.ViewDirection;
            double viewTwist = currentView.ViewTwist;
            Point2d viewCenter = currentView.CenterPoint;
            Point3d viewTarget = currentView.Target;
            
            ed.WriteMessage($"\n当前UCS X轴: ({currentUcs.Xaxis.X:F2}, {currentUcs.Xaxis.Y:F2})");
            ed.WriteMessage($"\n视图方向: ({viewDirection.X:F2}, {viewDirection.Y:F2}, {viewDirection.Z:F2})");
            ed.WriteMessage($"\n视图旋转(ViewTwist): {viewTwist * 180 / Math.PI:F1}°");

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord ps = tr.GetObject(bt[BlockTableRecord.PaperSpace], OpenMode.ForWrite) as BlockTableRecord;

                    // 切换到图纸空间
                    Application.SetSystemVariable("TILEMODE", 0);
                    ed.SwitchToPaperSpace();

                    // 创建视口
                    Viewport vp = new Viewport();
                    
                    // 视口在图纸空间的位置
                    vp.CenterPoint = new Point3d(200, 150, 0);
                    vp.Width = viewportWidth;
                    vp.Height = viewportHeight;
                    vp.CustomScale = customScale;
                    
                    // 继承视图方向
                    vp.ViewDirection = viewDirection;
                    
                    // 继承视图目标点
                    vp.ViewTarget = viewTarget;
                    
                    // 继承视图旋转角度（关键：这是PLAN视图的旋转）
                    vp.TwistAngle = viewTwist;
                    
                    // 设置视图中心（用户选择的点）
                    Point3d wcsCenter = centerPt.TransformBy(ucsMatrix);
                    vp.ViewCenter = new Point2d(wcsCenter.X, wcsCenter.Y);

                    ps.AppendEntity(vp);
                    tr.AddNewlyCreatedDBObject(vp, true);
                    vp.On = true;

                    tr.Commit();
                    
                    ed.WriteMessage("\n视口创建完成。");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }
    }
}
