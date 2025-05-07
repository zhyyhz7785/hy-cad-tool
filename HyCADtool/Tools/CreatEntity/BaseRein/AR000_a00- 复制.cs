using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
namespace HyCADTool
{
    public static partial class BaseRein
    {
        public static void SelectUsefullFiniteElements()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            try
            {
                BaseRein.SourceTextAndFinitePoly = BaseRein.SelectFiniteElementGrid();
                BaseRein.SourceTextAndEnvelopePoly = BaseRein.DrawBoundingPolyline(BaseRein.SourceTextAndFinitePoly);//4a;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }
    }
}
