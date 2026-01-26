using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool
{
    public static partial class Reinforcement
    {
        public static void GenerateReinforcementOutside(Polyline boundary)
        {
            double d = Reinforcement.DotReinOffset - 1;
            try
            {
                // 1. 确保多边形为顺时针
                boundary = boundary.EnsureClockwise();

                // 1. 外偏移边界
                var boundaryOffset = boundary.GetOffsetCurves(-ProtectionThickness)[0] as Polyline;
                Boundary = boundaryOffset;

                // 2. 生成线钢筋（外部 + 弯钩）
                var lines = boundaryOffset.PolyToLines();
                var lineReins = new List<Polyline>();
                foreach (var line in lines)
                {
                    var dir = line.Delta.GetNormal();

                    // 固定延伸长度
                    var start = line.StartPoint - dir * AnchorageLength;
                    var end = line.EndPoint + dir * AnchorageLength;

                    var poly = new Polyline();
                    poly.AddVertexAt(0, start.Convert2d(new Plane(Point3d.Origin, Vector3d.ZAxis)), 0, 0, 0);
                    poly.AddVertexAt(1, end.Convert2d(new Plane(Point3d.Origin, Vector3d.ZAxis)), 0, 0, 0);

                    // === 添加弯钩 ===
                    var startSeg = new LineSegment3d(poly.GetPoint3dAt(0), poly.GetPoint3dAt(1));
                    startSeg = new LineSegment3d(startSeg.EndPoint, startSeg.StartPoint); // 起点方向反向
                    var endSeg = new LineSegment3d(poly.GetPoint3dAt(poly.NumberOfVertices - 2), poly.GetPoint3dAt(poly.NumberOfVertices - 1));

                    // 起点弯钩
                    var hookStart = startSeg.AddAnchorToReinforcement(true);
                    poly.AddVertexAt(0, hookStart.Point3dTo2d(), 0, 0, 0);

                    // 终点弯钩
                    var hookEnd = endSeg.AddAnchorToReinforcement(false);
                    poly.AddVertexAt(poly.NumberOfVertices, hookEnd.Point3dTo2d(), 0, 0, 0);

                    lineReins.Add(poly);
                }
                SubReinforcements = lineReins.ToArray();

                //// 3. 生成点钢筋（外部）
                //DotReinCenterPoly = boundaryOffset;
                //DotReinPoints = boundaryOffset.AddDotRein(DotSeparation, DotStartDistance, d);
                //DotRein = DotReinPoints.PointsToDotRein();

                //// 3.1 删除无用点钢筋（减少数量）
                //ReduceDotReinPoints = boundaryOffset.AddReduceDotRein(DotSeparation, d);
                //ReduceDotRein = ReduceDotReinPoints.PointsToDotRein();

                //// 3.2 添加钢筋标注
                //var annotation = $"\\U+E532{RebarDiameter}@{RebarSpacing}";
                //Mleaders = AddMleaders(DotSeparation, annotation);

                // 4. 绘制图层
                ZTools.CreateMultipleLayers(
                    ("01_hy_1钢筋_线钢筋_外部", 1),
                   // ("01_hy_1钢筋_点钢筋_外部", 5),
                    ("00_hy_3公共_标注1_外", 3),
                    ("00_hy_3公共_标注3_引线", 92)
                );

                ZTools.SetCurrentLayer("01_hy_1钢筋_线钢筋_外部");
                SubReinforcements.ToSpace();

                //ZTools.SetCurrentLayer("01_hy_1钢筋_点钢筋_外部");
                //ReduceDotRein.ToSpace();

                //ZTools.SetCurrentLayer("00_hy_3公共_标注3_引线");
                //Mleaders.ToSpace();

                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage("\n外部钢筋与点钢筋（含弯钩与标注）生成完成。");
            }
            catch (System.Exception ex)
            {
                var ed = Application.DocumentManager.MdiActiveDocument.Editor;
                ed.WriteMessage($"\n生成外部钢筋时出错: {ex.Message}");
            }
        }
    }
}
