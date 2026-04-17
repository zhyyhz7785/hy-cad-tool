using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// P1：导线法（PI 点法）创建平面线位。对应设计院最常用工作流：
    /// 读取坐标表 → 逐点输入 PI → 指定统一圆曲线半径 → 一键出 Alignment。
    ///
    /// 交互：
    /// 1. 循环点取 PI 点（≥ 2 个），回车结束；
    /// 2. 输入统一圆曲线半径 R（0 表示全折线），默认 30 m；
    /// 3. 命令自动构造含 bulge 的 AutoCAD Polyline，挂 <c>HyRoadLayers.AlignmentLayer</c>，入 ModelSpace；
    /// 4. 复用 <see cref="RoadAlignmentService.ImportFromPolyline"/> 写 Xdata + 发事件 + 登记 Domain；
    /// 5. 命令收尾同步落盘 <c>.roaddesign.json</c>（与 hyRoadA 一致）。
    ///
    /// 与 <c>hyRoadA</c> 的分工：
    /// - <c>hyRoadA</c>：拾取用户已画好的 Polyline；适合"图形驱动"派。
    /// - <c>hyRoadAlnByPi</c>：按坐标表直接生成；适合"数据驱动"派（主流市政 + 鸿业 / 纬地）。
    /// 两者最终落入同一个 Domain Alignment，JSON 表示完全一致。
    /// </summary>
    public sealed class RoadAlignmentByPiCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;
            var db = doc.Database;

            var pis = CollectPiPoints(ed);
            if (pis == null) return; // 用户取消 / 点数不足

            var radius = PromptRadius(ed);
            if (double.IsNaN(radius)) return; // 用户取消

            PiDesignResult result;
            try
            {
                result = AlignmentPiDesigner.Build(pis, radius);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[道路] 几何生成失败：{ex.Message}");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadAlignmentService>();
            var exporter = ServiceLocator.Resolve<RoadJsonExportService>();
            var registry = ServiceLocator.Resolve<RoadDesignRegistry>();

            Domain.Models.Road.Alignment alignment;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 与 hyRoadA 一致：SAVEAS 后先 rebind 防 key 漂移
                svc.RebindForDocument(doc.Name, tr, db);

                // Domain Polyline3D → AutoCAD Polyline；再挂图层 + 入 ModelSpace
                var acadPoly = RoadGeometryBridge.ToAutoCadPolyline(result.Polyline);
                AssignLayerIfExists(tr, db, acadPoly, HyRoadLayers.AlignmentLayer);

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(
                    bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                ms.AppendEntity(acadPoly);
                tr.AddNewlyCreatedDBObject(acadPoly, true);

                // 走既有 Import 链路：写 Xdata + 发事件 + 登记 Domain（Created 分支）
                alignment = svc.ImportFromPolyline(doc.Name, tr, db, acadPoly);
                tr.Commit();
            }

            // 事务外同步落盘（与 hyRoadA 一致）
            string savedTo = null;
            if (registry.TryGet(doc.Name, out var design))
            {
                savedTo = exporter.SaveForDocument(design, doc.Name);
            }

            ed.WriteMessage(
                $"\n[道路] 导线法生成 {alignment.Name}："
                + $"PI {pis.Count} 个，圆角 {result.CurvedPiCount} 段，直角 {result.StraightPiCount} 段，"
                + $"跳过 {result.SkippedCount} 段，"
                + $"顶点 {alignment.Centerline.VertexCount} 个，"
                + $"半径 R={radius:F3} m，"
                + $"平面长度 {alignment.Centerline.GetPlanarLength():F3} m。");

            foreach (var w in result.Warnings)
            {
                ed.WriteMessage("\n[道路] ⚠ " + w);
            }

            if (savedTo != null)
                ed.WriteMessage($"\n[道路] JSON 已同步落盘：{savedTo}");
            else
                ed.WriteMessage(
                    "\n[道路] 未落盘（DWG 尚未保存）。先 QSAVE / SAVEAS，再跑 hyRoadSave 即可。");
        }

        /// <summary>
        /// 循环拾取 PI 点。首点必填、后续点回车结束。ESC / 右键视为取消。
        /// 返回 null 表示取消或点数不足；调用方据此静默退出。
        /// </summary>
        private static List<Point2D> CollectPiPoints(Editor ed)
        {
            var pts = new List<Point2D>();

            var firstOpts = new PromptPointOptions("\n[道路] 指定 PI 起点：")
            {
                AllowNone = false,
            };
            var firstRes = ed.GetPoint(firstOpts);
            if (firstRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return null;
            }
            pts.Add(new Point2D(firstRes.Value.X, firstRes.Value.Y));

            while (true)
            {
                var nextOpts = new PromptPointOptions(
                    $"\n[道路] 指定下一 PI 点（已输 {pts.Count} 个，回车结束）：")
                {
                    AllowNone = true,
                    UseBasePoint = true,
                    BasePoint = new Point3d(pts[pts.Count - 1].X, pts[pts.Count - 1].Y, 0),
                    UseDashedLine = true,
                };
                var r = ed.GetPoint(nextOpts);
                if (r.Status == PromptStatus.None) break; // 回车结束
                if (r.Status != PromptStatus.OK)
                {
                    ed.WriteMessage("\n[道路] 已取消。");
                    return null;
                }
                pts.Add(new Point2D(r.Value.X, r.Value.Y));
            }

            if (pts.Count < 2)
            {
                ed.WriteMessage($"\n[道路] 需要至少 2 个 PI 点（当前 {pts.Count}），已取消。");
                return null;
            }

            return pts;
        }

        /// <summary>
        /// 提示输入统一半径，默认 30 m，接受 0（全折线），不接受负值。
        /// 返回 NaN 代表用户取消。
        /// </summary>
        private static double PromptRadius(Editor ed)
        {
            var opt = new PromptDistanceOptions(
                "\n[道路] 圆曲线统一半径 R（输入 0 = 全折线，回车 = 30）：")
            {
                DefaultValue = 30.0,
                UseDefaultValue = true,
                AllowNegative = false,
                AllowZero = true,
                AllowNone = true,
            };
            var res = ed.GetDistance(opt);
            if (res.Status == PromptStatus.None) return opt.DefaultValue;
            if (res.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消。");
                return double.NaN;
            }
            return res.Value;
        }

        /// <summary>
        /// 图层存在才挂；不存在时保持 "0" 图层（PluginInitializer 已在启动时建表，
        /// 这里兜底处理"用户手动删了图层"的罕见场景）。
        /// </summary>
        private static void AssignLayerIfExists(Transaction tr, Database db, Polyline poly, string layerName)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(layerName))
            {
                poly.Layer = layerName;
            }
        }
    }
}
