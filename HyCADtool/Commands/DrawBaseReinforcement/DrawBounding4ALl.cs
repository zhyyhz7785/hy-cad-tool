using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Log;
[assembly: CommandClass(typeof(HyCADTool.Command.DrawBaseReinforcement))]
namespace HyCADTool.Command
{
    public static partial class DrawBaseReinforcement
    {
        // CommandMethod 特性表明这是一个 AutoCAD 命令
        [CommandMethod("hyb4")]
        public static void DrawBounding()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {              
                    var a = TestFunction.SelectFiniteElementGrid();//4;
                    var b = TestFunction.DrawBoundingPolyline(a);//4a;
                    var c = TestFunction.GroupBySpatialProximity(b, 1000);//4b;
                    TestFunction.CreateOptimizedBoundingPolygonFromPolygons(c, db, "00_hy_调整配筋轮廓", 1);//4c;                                                                                        
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n{ex}\n");
            }
        }
    }
}
