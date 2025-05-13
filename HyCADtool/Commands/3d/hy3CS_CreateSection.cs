using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass.CreatBase;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("hy3C_CreateSection")]
        public static void CreateSectionCommand()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    CreateSection.CreateLayers(db, tr);
                    double displacement = CreateSection.GetDisplacement(ed);
                    var sectionLines = SectionLine.SelectLines(ed);
                    if (sectionLines.Count == 0) return;
                    SectionLine.SortAndIndexLines(sectionLines);
                    ObjectIdCollection entityIds = CreateSection.Select3DObjects(ed);
                    if (entityIds.Count == 0) return;
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    CreateSection.GenerateSection(db, tr, ed, sectionLines, displacement, entityIds, btr);
                    tr.Commit();
                }
                ed.Regen();
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }
    }
}
