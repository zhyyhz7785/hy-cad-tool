using System;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// <c>hyRoadLaneMarking</c> v1.1 —— 车道分界标线（单命令）。GB 5768-2009 §5.1。
    ///
    /// <para><b>交互</b></para>
    /// <list type="number">
    /// <item>选类型 [白实线 W / 白虚线 D / 黄实线 Y / 双黄 YY]，默认 W；</item>
    /// <item>拾取起点；</item>
    /// <item>拾取终点；</item>
    /// <item>可选覆盖线宽（默认 <see cref="LaneMarking.DefaultStripeWidth"/> = 0.15 m）。</item>
    /// </list>
    ///
    /// <para><b>实现</b></para>
    /// <list type="bullet">
    /// <item>Domain：<see cref="LaneMarkingDesigner.Create"/> + <see cref="LaneMarkingDesigner.ExpandSegments"/>；</item>
    /// <item>Infrastructure：<see cref="RoadLaneMarkingService.DrawLaneMarking"/>——虚线多段、双黄两段、实线一段，
    /// 全部共享同一 <see cref="LaneMarking.Id"/>，KIND=<see cref="RoadLaneMarkingService.LaneMarkingKind_"/>；</item>
    /// <item>图层 <see cref="HyRoadLayers.MarkingLayer"/>，ColorIndex 白=7 / 黄=2 覆盖图层色。</item>
    /// </list>
    /// </summary>
    public sealed class RoadLaneMarkingCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var kindOpt = new PromptKeywordOptions(
                "\n[道路] 车道分界类型 [白实(W)/白虚(D)/黄实(Y)/双黄(YY)]：")
            {
                AllowNone = true,
            };
            kindOpt.Keywords.Add("W");
            kindOpt.Keywords.Add("D");
            kindOpt.Keywords.Add("Y");
            kindOpt.Keywords.Add("YY");
            kindOpt.Keywords.Default = "W";
            var kw = ed.GetKeywords(kindOpt);
            LaneMarkingKind kind = LaneMarkingKind.SolidWhite;
            if (kw.Status == PromptStatus.OK)
            {
                switch (kw.StringResult)
                {
                    case "D":  kind = LaneMarkingKind.DashedWhite; break;
                    case "Y":  kind = LaneMarkingKind.SolidYellow; break;
                    case "YY": kind = LaneMarkingKind.DoubleYellow; break;
                    default:   kind = LaneMarkingKind.SolidWhite; break;
                }
            }

            var pOpt1 = new PromptPointOptions("\n[道路] 起点：") { AllowNone = false };
            var p1 = ed.GetPoint(pOpt1);
            if (p1.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            var pOpt2 = new PromptPointOptions("\n[道路] 终点：")
            {
                AllowNone = false,
                UseBasePoint = true,
                BasePoint = p1.Value,
                UseDashedLine = true,
            };
            var p2 = ed.GetPoint(pOpt2);
            if (p2.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            double width = AskDouble(ed,
                $"[道路] 标线线宽 (默认 {LaneMarking.DefaultStripeWidth:F2} m)",
                LaneMarking.DefaultStripeWidth);

            LaneMarking marking;
            try
            {
                marking = LaneMarkingDesigner.Create(
                    new Point2D(p1.Value.X, p1.Value.Y),
                    new Point2D(p2.Value.X, p2.Value.Y),
                    kind,
                    width);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 标线参数无效：{ex.Message}");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadLaneMarkingService>();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ids = svc.DrawLaneMarking(tr, db, marking, HyRoadLayers.MarkingLayer);
                tr.Commit();
                ed.WriteMessage(
                    $"\n[道路] 已绘制 {KindDisplayName(kind)}：长度 {marking.Length:F2} m，" +
                    $"共 {ids.Count} 段，Id={marking.Id:N}");
            }
        }

        private static string KindDisplayName(LaneMarkingKind kind)
        {
            switch (kind)
            {
                case LaneMarkingKind.DashedWhite:  return "白色虚线";
                case LaneMarkingKind.SolidYellow:  return "黄色单实线";
                case LaneMarkingKind.DoubleYellow: return "黄色双实线";
                default:                           return "白色单实线";
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
