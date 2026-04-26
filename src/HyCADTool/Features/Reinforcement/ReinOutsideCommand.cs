using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 外部钢筋生成命令（ggj）
    /// 选择闭合多段线 → 外偏移 → 生成线钢筋（含弯钩）
    /// </summary>
    public class ReinOutsideCommand
    {
        private static string LayerLineRein => UserLayerNameResolver.Get(LayerSemanticIds.ReinLineExternal, LayerBuiltinDefaults.ReinLineExternal);

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 读取面板参数
            var vm = SettingsPanelViewModel.Current;
            double scale = vm?.Scale ?? 40.0;
            double protectionThickness = (vm?.ProtectionThickness ?? 1.0) * scale;
            double anchorageLength = vm?.AnchorageLength ?? 500.0;
            double hookLength = (vm?.HookLength ?? 1.0) * scale;
            double reinWidth = (vm?.PolylineWidth ?? 0.4) * scale;

            // 确保样式已同步
            vm?.EnsureStylesApplied();

            // 选择多段线
            var poly = db.SelectAEntity<Polyline>();
            if (poly == null)
            {
                ed.WriteMessage("\n请选择一个闭合 Polyline");
                return;
            }

            try
            {
                // 确保顺时针
                poly = poly.EnsureClockwise();

                // 外偏移
                var offsets = poly.GetOffsetCurves(-protectionThickness);
                if (offsets.Count == 0)
                {
                    ed.WriteMessage("\n偏移失败");
                    return;
                }
                var boundary = offsets[0] as Polyline;
                if (boundary == null)
                {
                    ed.WriteMessage("\n偏移结果不是多段线");
                    return;
                }

                // 多段线拆线段
                var lines = PolyToLines(boundary);

                // 生成线钢筋（含弯钩）
                var reinPolys = new List<Polyline>();
                foreach (var line in lines)
                {
                    var dir = (line.EndPoint - line.StartPoint).GetNormal();
                    var start = line.StartPoint - dir * anchorageLength;
                    var end = line.EndPoint + dir * anchorageLength;

                    var plane = new Plane(Point3d.Origin, Vector3d.ZAxis);
                    var rein = new Polyline();
                    rein.AddVertexAt(0, start.Convert2d(plane), 0, 0, 0);
                    rein.AddVertexAt(1, end.Convert2d(plane), 0, 0, 0);

                    // 起点弯钩（方向从终点往起点，角度 5π/4）
                    var hookStart = CalculateHook(rein.GetPoint3dAt(1), rein.GetPoint3dAt(0), hookLength);
                    rein.AddVertexAt(0, hookStart.Point3dTo2d(), 0, 0, 0);

                    // 终点弯钩（方向从倒数第二到最后，角度 3π/4）
                    int last = rein.NumberOfVertices - 1;
                    var hookEnd = CalculateHook(rein.GetPoint3dAt(last - 1), rein.GetPoint3dAt(last), hookLength);
                    rein.AddVertexAt(rein.NumberOfVertices, hookEnd.Point3dTo2d(), 0, 0, 0);
                    rein.ApplyReinforcementWidth(reinWidth);

                    reinPolys.Add(rein);
                }

                // 图层已在 PluginInitializer 统一创建

                // 写入模型空间
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (var rein in reinPolys)
                    {
                        rein.Layer = LayerLineRein;
                        ms.AppendEntity(rein);
                        tr.AddNewlyCreatedDBObject(rein, true);
                    }

                    tr.Commit();
                }

                ed.WriteMessage($"\n外部钢筋生成完成，共 {reinPolys.Count} 根");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n生成失败: {ex.Message}");
            }
        }

        /// <summary>多段线拆分为线段列表</summary>
        private static List<Line> PolyToLines(Polyline poly)
        {
            var lines = new List<Line>();
            int count = poly.NumberOfVertices;
            int limit = poly.Closed ? count : count - 1;
            for (int i = 0; i < limit; i++)
            {
                var sp = poly.GetPoint3dAt(i);
                var ep = poly.GetPoint3dAt((i + 1) % count);
                if (sp.DistanceTo(ep) > Tolerance.Global.EqualPoint)
                    lines.Add(new Line(sp, ep));
            }
            return lines;
        }

        /// <summary>
        /// 计算弯钩点
        /// startPt → endPt 方向上，在 endPt 处旋转生成弯钩
        /// 起点弯钩：角度 5π/4（225°），终点弯钩：角度 3π/4（135°）
        /// </summary>
        private static Point3d CalculateHook(Point3d startPt, Point3d endPt, double hookLength)
        {
            var dir = (endPt - startPt).GetNormal();
            // 判断是起点弯钩还是终点弯钩
            // 起点弯钩传入参数是 (末端, 起始端)，方向 dir 指向起始端
            // 终点弯钩传入参数是 (倒数第二, 末端)，方向 dir 指向末端
            double angle = Math.PI * 3.0 / 4.0;
            var hookDir = dir.RotateBy(angle, Vector3d.ZAxis);
            return endPt + hookDir * hookLength;
        }
    }
}
