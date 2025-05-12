using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("hyz_PlacePileAndVoronoiWithLloydOptimization")]
        ///选择yjk墙体水平配筋 《输入值的处理
        public static void PlacePileAndVoronoiWithLloydOptimization()
        {
            EtGpt.PlacePileAndVoronoiWithLloydOptimization();
        }
    }
}