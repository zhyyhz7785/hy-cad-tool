using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Drawing;
using HyCADTool.Models;
using HyCADTool.Models.Cluster;
using System.Collections.Generic;

namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        /// <summary>
        /// 快速测试 DrawInCad：选择图形 → 自动生成尺寸 / 聚类 → 写入或仅分析
        /// </summary>
        [CommandMethod("HY_TestDrawInCad")]
        public static void TestDrawInCad()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            DrawInCad.Draw_BPs = false;
            DrawInCad.Draw_AAPs = false;
            DrawInCad.Draw_BAPs = false;
            DrawInCad.Draw_ABs = false;
            DrawInCad.Draw_SteelPlPs = false;

            DrawInCad.Draw_AxisCircle = false;
            DrawInCad.Draw_AxisText = false;

            DrawInCad.Draw_RegionFrame = false;
            DrawInCad.Draw_RegionText = false;

            DrawInCad.Draw_ClusterEnvelopePolyline = true;
            DrawInCad.Draw_ClusterEnvelopeExpandedPolyline = false;
            DrawInCad.Draw_ClusterHull = false;
            DrawInCad.Draw_ClusterPts = false;

            DrawInCad.Draw_DimX = false;
            DrawInCad.Draw_DimY = false;

            // 2) 将选择集暂存至 DimPointsAndAxis
            var dpa = DimPointsAndAxis.GetInput();  // 内部再次读取选择集
            var ps=dpa.GetFilteredPoints();
            // 3) 构建 AxisDatas（含Region）
            AxisDatas axes = AxisDatas.FromLines(dpa.AxisLines, dpa.SelectPoints);

            // 4) 计算尺寸标注
            DimHelper dimHelper = DimHelper.Build(dpa.SelectPoints);

            // 5) 生成聚类（此处仅用 X 向配置做演示，可自行调整）
            List<ClusterResult> clusters =
                ClusterFactory.Create(dpa.SelectPoints, DimHelper.ClusterConfigX);

            // ───── 可选：修改输出开关进行测试 ─────
            //DrawInCad.Draw_DimY       = false; // 例如关闭 Y 向尺寸
            //DrawInCad.Draw_ClusterHull= false; // 关闭凸包
            //DrawInCad.EnableCadOutput = false; // 单元测试模式：不落图
            
            // 6) 调用 DrawInCad 写入 / 或者仅分析
            DrawInCad.Draw(
                    dpa,
                    axes,
                    clusters,
                    dimHelper.AllDimensions);

            ed.WriteMessage("\nHY_TestDrawInCad 运行完毕。");
        }
    }
}
