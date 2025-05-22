using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
namespace HyCADTool.Tools
{
    public static partial class Et
    {
        /// <summary>
        /// 获取用户输入的点
        /// </summary>
        /// <param name="promptMessage">提示用户输入点的消息</param>
        /// <returns>用户输入的点，如果用户取消则返回null</returns>
        public static Point3d? GetUserPoint(string promptMessage = "\n请输入一个点: ")
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            PromptPointResult ppr = ed.GetPoint(promptMessage);
            if (ppr.Status == PromptStatus.OK)
            {
                return ppr.Value;
            }
            else
            {
                ed.WriteMessage("\n点输入失败或取消。");
                return null;
            }
        }
    }
}
