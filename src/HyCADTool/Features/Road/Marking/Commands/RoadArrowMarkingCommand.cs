using System;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Services.Road;
using HyCADTool.Shared.AutoCAD.Xdata;
using HyCADTool.App.Bootstrap;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// <c>hyRoadArrow</c> v1.1 —— 路面导流箭头命令。GB 5768-2009 §5.3。
    ///
    /// <para><b>交互</b></para>
    /// <list type="number">
    /// <item>选类型 [直行 S / 左转 L / 右转 R / 直左 SL / 直右 SR / 左右 LR]，默认 S；</item>
    /// <item>拾取箭尾中心点（<see cref="ArrowMarking.Anchor"/>）；</item>
    /// <item>拾取方向参考点（以 anchor → 该点的向量作为 <see cref="ArrowMarking.Direction"/>）；</item>
    /// <item>可选覆盖主长度（默认 <see cref="ArrowMarking.DefaultLength"/> = 6 m，GB 3/6/9 m 三档）。</item>
    /// </list>
    /// </summary>
    public sealed class RoadArrowMarkingCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var kindOpt = new PromptKeywordOptions(
                "\n[道路] 导流箭头类型 [直行(S)/左转(L)/右转(R)/直左(SL)/直右(SR)/左右(LR)]：")
            {
                AllowNone = true,
            };
            kindOpt.Keywords.Add("S");
            kindOpt.Keywords.Add("L");
            kindOpt.Keywords.Add("R");
            kindOpt.Keywords.Add("SL");
            kindOpt.Keywords.Add("SR");
            kindOpt.Keywords.Add("LR");
            kindOpt.Keywords.Default = "S";
            var kw = ed.GetKeywords(kindOpt);
            ArrowMarkingKind kind = ArrowMarkingKind.Straight;
            if (kw.Status == PromptStatus.OK)
            {
                switch (kw.StringResult)
                {
                    case "L":  kind = ArrowMarkingKind.Left; break;
                    case "R":  kind = ArrowMarkingKind.Right; break;
                    case "SL": kind = ArrowMarkingKind.StraightLeft; break;
                    case "SR": kind = ArrowMarkingKind.StraightRight; break;
                    case "LR": kind = ArrowMarkingKind.LeftRight; break;
                    default:   kind = ArrowMarkingKind.Straight; break;
                }
            }

            var pAnchor = new PromptPointOptions("\n[道路] 箭尾中心点：") { AllowNone = false };
            var pa = ed.GetPoint(pAnchor);
            if (pa.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            var pDir = new PromptPointOptions("\n[道路] 方向参考点（anchor → 该点为主方向）：")
            {
                AllowNone = false,
                UseBasePoint = true,
                BasePoint = pa.Value,
                UseDashedLine = true,
            };
            var pd = ed.GetPoint(pDir);
            if (pd.Status != PromptStatus.OK) { ed.WriteMessage("\n[道路] 已取消。"); return; }

            double length = AskDouble(ed,
                $"[道路] 箭身主长度 (默认 {ArrowMarking.DefaultLength:F1} m，GB 3/6/9 三档)",
                ArrowMarking.DefaultLength);

            var anchor = new Point2D(pa.Value.X, pa.Value.Y);
            var dir = new Vector2D(pd.Value.X - pa.Value.X, pd.Value.Y - pa.Value.Y);

            ArrowMarking arrow;
            try
            {
                arrow = ArrowMarkingDesigner.Create(anchor, dir, kind, length);
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n[道路] 导流箭头参数无效：{ex.Message}");
                return;
            }

            var svc = ServiceLocator.Resolve<RoadArrowMarkingService>();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    var ids = svc.DrawArrow(tr, db, arrow, HyRoadLayers.MarkingLayer);
                    tr.Commit();
                    ed.WriteMessage(
                        $"\n[道路] 已绘制 {KindDisplayName(kind)}：L={length:F2} m，共 {ids.Count} 个多边形，Id={arrow.Id:N}");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n[道路] 绘制失败：{ex.Message}");
                }
            }
        }

        private static string KindDisplayName(ArrowMarkingKind kind)
        {
            switch (kind)
            {
                case ArrowMarkingKind.Left:          return "左转箭头";
                case ArrowMarkingKind.Right:         return "右转箭头";
                case ArrowMarkingKind.StraightLeft:  return "直行+左转箭头";
                case ArrowMarkingKind.StraightRight: return "直行+右转箭头";
                case ArrowMarkingKind.LeftRight:     return "左右转箭头";
                default:                              return "直行箭头";
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
