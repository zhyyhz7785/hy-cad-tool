using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Linq;
[assembly: CommandClass(typeof(HyCADTool.Command.DrawBaseReinforcement))]
namespace HyCADTool.Command
{
    public static partial class DrawBaseReinforcement
    {
        private static readonly double[] AllowedDiameters = { 6, 8, 10, 12, 14, 16, 18, 20, 22, 25 };
        // 定义属性来存储用户输入
        public static double MinAdditionalDiameter { get; set; } = 6;
        public static double AdditionalSpacing { get; set; } = 200;
        public static string Direction { get; set; } = "x";
        public static double Scale { get; set; } = 100;
        public static double AnchorFactor { get; set; } = 35;
        public static double RebarDiameter { get; set; } = 12;
        public static double RebarSpacing { get; set; } = 200;
        public static double TextToLineDistance { get; set; } = 1;
        public static double HookLength { get; set; } = 1;
        public static double PolylineWidth { get; set; } = 0.4;       
        public static void SetUp()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            var userInput = GetUserInput(ed);
            if (userInput == null) return;
            // 更新属性
            MinAdditionalDiameter = userInput.Value.MinAdditionalDiameter;
            AdditionalSpacing = userInput.Value.AdditionalSpacing;
            Direction = userInput.Value.Direction;
            Scale = userInput.Value.Scale;
            RebarDiameter = userInput.Value.RebarDiameter;
            RebarSpacing = userInput.Value.RebarSpacing;
            
          
        }
        private static (double MinAdditionalDiameter, double AdditionalSpacing, string Direction, double Scale, double RebarDiameter, double RebarSpacing)? GetUserInput(Editor ed)
        {
            var diameterResult = ed.GetDouble("\n请输入附加钢筋最小直径 (6, 8, 10, 12, 14, 16, 18, 20, 22, 25) [默认值: 6]: ");
            if (diameterResult.Status == PromptStatus.OK && AllowedDiameters.Contains(diameterResult.Value))
            {
                MinAdditionalDiameter = diameterResult.Value;
            }
            var spacingResult = ed.GetDouble($"\n请输入附加钢筋间距 (单位: mm) [默认值: {AdditionalSpacing}]: ");
            if (spacingResult.Status == PromptStatus.OK)
            {
                AdditionalSpacing = spacingResult.Value;
            }
            var directionResult = ed.GetString($"\n请输入钢筋方向 (x 或 y) [默认值: {Direction}]: ");
            if (directionResult.Status == PromptStatus.OK && (directionResult.StringResult == "x" || directionResult.StringResult == "y"))
            {
                Direction = directionResult.StringResult;
            }
            var scaleResult = ed.GetDouble($"\n请输入比例 [默认值: {Scale}]: ");
            if (scaleResult.Status == PromptStatus.OK)
            {
                Scale = scaleResult.Value;
            }
            var rebarDiameterResult = ed.GetDouble($"\n请输入通长钢筋直径 (6, 8, 10, 12, 14, 16, 18, 20, 22, 25) [默认值: {RebarDiameter}]: ");
            if (rebarDiameterResult.Status == PromptStatus.OK && AllowedDiameters.Contains(rebarDiameterResult.Value))
            {
                RebarDiameter = rebarDiameterResult.Value;
            }
            var rebarSpacingResult = ed.GetDouble($"\n请输入通长钢筋间距 (单位: mm) [默认值: {RebarSpacing}]: ");
            if (rebarSpacingResult.Status == PromptStatus.OK)
            {
                RebarSpacing = rebarSpacingResult.Value;
            }
            return (MinAdditionalDiameter, AdditionalSpacing, Direction, Scale, RebarDiameter, RebarSpacing);
        }        
        
    }
}
