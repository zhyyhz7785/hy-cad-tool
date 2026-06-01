using System;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Services.Road;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

using HyCADTool.Features.Road.PlanAlignment.Services;
using HyCADTool.Features.Road.CrossSection.Domain;
using HyCADTool.Features.Road.PlanAlignment.Commands;
using HyCADTool.Features.Road.CrossSection.Commands;
namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadStopLine</c> v1.1 —— 独立停止线（单命令）。GB 5768-2009 §5.2.2。
    ///
    /// <para><b>与 <c>hyRoadIntersectionCrosswalk</c> 的停止线的区别</b></para>
    /// <list type="bullet">
    /// <item>交叉口停止线：依附 Intersection，KIND=<c>StopLine</c>，随 Crosswalk 幂等重建；</item>
    /// <item>独立停止线（本命令）：脱离 Intersection，KIND=<c>StandaloneStopLine</c>，独立 Id，
    /// 用于无转角圆弧的路段 / 支路出入口 / 小区出入口等自由场景。</item>
    /// </list>
    ///
    /// <para><b>交互</b></para>
    /// <list type="number">
    /// <item>拾取停止线左端点（车行道左缘）；</item>
    /// <item>拾取停止线右端点（车行道右缘）；</item>
    /// <item>可选覆盖线宽（默认 <see cref="StopLine.DefaultStripeWidth"/> = 0.40 m）。</item>
    /// </list>
    ///
    /// <para><b>实现</b></para>
    /// 几何由 <see cref="StopLineDesigner"/> 构造；AutoCAD 绘制由 <see cref="RoadStopLineService"/> 用
    /// <see cref="Autodesk.AutoCAD.DatabaseServices.Polyline"/> + ConstantWidth 实现；
    /// KIND=<see cref="RoadStopLineService.StandaloneStopLineKind"/> 便于二次编辑与清理。
    /// </summary>
    public sealed class RoadStopLineCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var pOpt1 = new PromptPointOptions("\n[道路] 停止线左端点（车行道左缘）：")
            {
                AllowNone = false,
            };
            var p1 = ed.GetPoint(pOpt1);
            if (p1.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            var pOpt2 = new PromptPointOptions("\n[道路] 停止线右端点（车行道右缘）：")
            {
                AllowNone = false,
                UseBasePoint = true,
                BasePoint = p1.Value,
                UseDashedLine = true,
            };
            var p2 = ed.GetPoint(pOpt2);
            if (p2.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            double width = AskDouble(
                ed,
                $"[道路] 停止线线宽 (默认 {StopLine.DefaultStripeWidth:F2} m，GB 规范 0.20~0.40)",
                StopLine.DefaultStripeWidth);

            var left = new Point2D(p1.Value.X, p1.Value.Y);
            var right = new Point2D(p2.Value.X, p2.Value.Y);

            StopLine stopLine;
            try
            {
                stopLine = StopLineDesigner.Create(left, right, width);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 停止线参数无效：{ex.Message}");
                return;
            }

            if (!StopLineDesigner.IsValidLength(stopLine))
            {
                ed.WriteMessage(
                    $"\n[道路][警告] 停止线长度 {stopLine.Length:F2} m 小于建议下限 {StopLineDesigner.MinLength:F2} m，" +
                    $"仍按用户输入绘制。");
            }
            if (!StopLineDesigner.IsValidWidth(stopLine))
            {
                ed.WriteMessage(
                    $"\n[道路][警告] 停止线线宽 {stopLine.StripeWidth:F2} m 超出 GB 5768-2009 范围 " +
                    $"[{StopLineDesigner.MinWidth:F2}, {StopLineDesigner.MaxWidth:F2}]，仍按用户输入绘制。");
            }

            var svc = ServiceLocator.Resolve<RoadStopLineService>();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var id = svc.DrawStopLine(tr, db, stopLine, HyRoadLayers.StopLineLayer);
                tr.Commit();
                ed.WriteMessage(
                    $"\n[道路] 已绘制独立停止线：长度 {stopLine.Length:F2} m，线宽 {stopLine.StripeWidth:F2} m，" +
                    $"Id={stopLine.Id:N}");
            }
        }

        private static double AskDouble(Editor ed, string prompt, double defaultValue)
        {
            var opt = new PromptDoubleOptions($"\n{prompt}：")
            {
                DefaultValue = defaultValue,
                UseDefaultValue = true,
                AllowNone = true,
                AllowNegative = false,
                AllowZero = false,
            };
            var r = ed.GetDouble(opt);
            return r.Status == PromptStatus.OK ? r.Value : defaultValue;
        }
    }
}
