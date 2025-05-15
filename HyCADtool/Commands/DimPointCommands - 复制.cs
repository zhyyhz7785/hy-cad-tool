using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Models;
using HyCADTool.Tools;
using System.Collections.Generic;



namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("HY_AnnotateAxes")]
        public static void AnnotateAxes()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            var data = HyCADTool.Models.Cluster.DimPointsAndAxis.GetInput();
            if (data == null || data.AxisLines == null || data.AxisLines.Count == 0)
            {
                ed.WriteMessage("\n未获取到有效输入。");
                return;
            }

            // 构建注释对象
            var ann = AxisDatas.FromLines(data.AxisLines);

            // 合并所有图元
            var allEntities = new List<Entity>();
            allEntities.AddRange(ann.AxisCircles);
            allEntities.AddRange(ann.AxisTexts);
            allEntities.AddRange(ann.RegionFrames);
            allEntities.AddRange(ann.RegionTexts);

            // 写入模型空间
            allEntities.ToSpace();
        }

    }
}
