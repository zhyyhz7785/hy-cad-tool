using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored;

namespace HyCADTool.Refactored.Presentation.Commands
{
    public class PaperV
    {
        public static void Test()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ppr = ed.GetPoint("\n请选择基点: ");
            Point2d point2d = new Point2d(0, 0);
            if (ppr.Status == PromptStatus.OK)
            {
                var point1 = ppr.Value;
                point2d = new Point2d(point1.X, point1.Y);
            }
            try
            {
                using (Transaction acTrans = db.TransactionManager.StartTransaction())
                {
                    // Open the Block table for read
                    BlockTable acBlkTbl;
                    acBlkTbl = acTrans.GetObject(db.BlockTableId,
                                                 OpenMode.ForRead) as BlockTable;
                    // Open the Block table record Paper space for write
                    BlockTableRecord acBlkTblRec;
                    acBlkTblRec = acTrans.GetObject(acBlkTbl[BlockTableRecord.PaperSpace],
                                                    OpenMode.ForWrite) as BlockTableRecord;
                    // Switch to the previous Paper space layout
                    Application.SetSystemVariable("TILEMODE", 0);
                    ed.SwitchToPaperSpace();
                    // Create a Viewport
                    for (int i = 0; i < 30; i++)
                    {
                        Viewport acVport = new Viewport();
                            acVport.CenterPoint = new Point3d(0, 0, 0) + new Vector3d(0, 430 * i, 0);
                            acVport.Width = 240;
                            acVport.Height = 390;
                            acVport.CustomScale = 2;
                            acVport.SetUcs(Point3d.Origin, new Vector3d(0, 1, 0), new Vector3d(-1, 0, 0));
                            acVport.ViewCenter = point2d + new Vector2d(0, (210 * i - 20 * i));
                            //acVport.TwistAngle = Math.PI / 2;
                            // Add the new object to the block table record and the transaction
                            acBlkTblRec.AppendEntity(acVport);
                            acTrans.AddNewlyCreatedDBObject(acVport, true);
                            acVport.On = true;
                            // Change the view direction
                            // acVport.ViewDirection = new Vector3d(1, 1, 1);
                            // Enable the viewport
                            // Activate model space in the viewport
                            // ed.SwitchToModelSpace();
                            // Set the new viewport current via an imported ObjectARX function
                            // acedSetCurrentVPort(acVport.UnmanagedObject);
                    }
                    // Save the new objects to the database
                    acTrans.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n{ex}\n");
            }
        }
    }
}
