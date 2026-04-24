using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interactive;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.User;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Configuration;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 绘制钢筋命令（对应旧命令 gg → MyPolyline）
    /// 流程：PolylineJig 沿混凝土边界绘制 → 偏移（保护层厚度）→ 自动添加两端弯钩 → 写入图纸
    /// 用途：快速绘制带弯钩的钢筋线，偏移距离 = 保护层厚度
    /// </summary>
    public class DrawOffsetPolylineCommand
    {
        private readonly ILayerService _layerService;

        private static string LayerLineRein => UserLayerNameResolver.Get(LayerSemanticIds.ReinLine, LayerBuiltinDefaults.ReinLine);

        public DrawOffsetPolylineCommand()
        {
            _layerService = ServiceLocator.Resolve<ILayerService>();
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            _layerService.SetCurrentLayer(LayerLineRein);

            var vm = SettingsPanelViewModel.Current;
            double scale = vm?.Scale ?? 40.0;
            double offsetDistance = (vm?.ProtectionThickness ?? 1.0) * scale; // 绿色参数 × Scale
            double hookLength = (vm?.HookLength ?? 1.0) * scale;             // 绿色参数 × Scale
            double reinWidth = (vm?.PolylineWidth ?? 0.4) * scale;

            #region agent log
            AgentDebugLogger.Log("initial", "H1", "DrawOffsetPolylineCommand.Execute", "gg width parameters",
                new
                {
                    hasViewModel = vm != null,
                    scale,
                    polylineWidth = vm?.PolylineWidth,
                    offsetDistance,
                    hookLength,
                    reinWidth
                });
            #endregion

            // 确保样式已同步
            vm?.EnsureStylesApplied();

            // 1. 交互式沿边界绘制（Jig 实时预览偏移效果）
            var jig = new PolylineJig(-offsetDistance);
            if (jig.StartJig() != PromptStatus.OK || jig.Points.Count <= 1)
                return;

            using (var trans = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // 2. 构建辅助多段线用于偏移计算（不写入图纸）
                var tempPoly = new Polyline();
                for (int i = 0; i < jig.Points.Count; i++)
                {
                    tempPoly.AddVertexAt(i, new Point2d(jig.Points[i].X, jig.Points[i].Y), 0, 0, 0);
                }

                // 3. 生成偏移曲线（= 钢筋线）
                var offsetCurves = tempPoly.GetOffsetCurves(-offsetDistance);

                foreach (Entity ent in offsetCurves)
                {
                    // 4. 对偏移后的多段线自动添加两端弯钩
                    if (ent is Polyline offsetPoly)
                    {
                        AddHooksAtBothEnds(offsetPoly, hookLength);
                        offsetPoly.ApplyReinforcementWidth(reinWidth);
                    }

                    // 5. 写入模型空间
                    btr.AppendEntity(ent);
                    trans.AddNewlyCreatedDBObject(ent, true);
                }

                trans.Commit();
            }
        }

        /// <summary>
        /// 在多段线两端自动添加 45° 弯钩（对应旧 AddAnchor(polyline, tr, hookLength)）
        /// 起点：沿起始方向旋转 45° 插入弯钩端点
        /// 终点：沿终止方向旋转 135° 插入弯钩端点 + 返回原终点（标准弯钩表示）
        /// </summary>
        private static void AddHooksAtBothEnds(Polyline polyline, double hookLength)
        {
            if (polyline.NumberOfVertices < 2)
                return;

            // 捕获原始起终点和方向（修改前）
            Point3d startPoint = polyline.GetPoint3dAt(0);
            Point3d endPoint = polyline.GetPoint3dAt(polyline.NumberOfVertices - 1);
            Vector3d startDir = polyline.GetFirstDerivative(0).GetNormal();
            Vector3d endDir = polyline.GetFirstDerivative(polyline.NumberOfVertices - 1).GetNormal();

            // 起点弯钩：方向旋转 45°，在 index 0 前插入
            Vector3d startHookDir = startDir.RotateBy(Math.PI / 4.0, Vector3d.ZAxis);
            Point3d startHookEnd = startPoint + startHookDir * hookLength;
            polyline.AddVertexAt(0, new Point2d(startHookEnd.X, startHookEnd.Y), 0, 0, 0);

            // 终点弯钩：方向旋转 135°，在末尾追加弯钩端点 + 返回原终点
            Vector3d endHookDir = endDir.RotateBy(3 * Math.PI / 4.0, Vector3d.ZAxis);
            Point3d endHookEnd = endPoint + endHookDir * hookLength;
            int endIndex = polyline.NumberOfVertices;
            polyline.AddVertexAt(endIndex, new Point2d(endHookEnd.X, endHookEnd.Y), 0, 0, 0);
            polyline.AddVertexAt(endIndex + 1, new Point2d(endPoint.X, endPoint.Y), 0, 0, 0);
        }
    }
}
