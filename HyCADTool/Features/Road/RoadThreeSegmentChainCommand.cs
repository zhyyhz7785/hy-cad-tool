using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Road
{
    /// <summary>
    /// 在 <b>XY 平面</b>（立面图语境）沿 +X 方向画首尾相接的三段 2D 直线 a / b / c。
    /// <b>X 代表里程，Y 代表高程</b>；所有端点 Z = 0（留拾取点本身的 Z 不动）。
    /// 与其他业务代码零耦合，入口通过 ReCall 命令表反射调用（N4 占位符 或 将来正式命名）。
    /// 本类**不带 <c>[CommandMethod]</c>**（Refactored 项目禁用，C2 热重载会 eDuplicateKey）。
    /// 几何规则：
    /// <list type="bullet">
    ///   <item>每段 X 方向长度固定 <b>3.5 m</b>（依次 +3.5）；</item>
    ///   <item>每段坡度 <b>1.5%</b>（X 每走 1，Y 升 0.015；参数可改，负 = 下坡）；</item>
    ///   <item>b 段两端存在「Y 跳变」—— XY 平面上与相邻段端点同 X 不同 Y：
    ///     <list type="bullet">
    ///       <item>左端跳变：<c>b起点.Y − a终点.Y = +0.2</c>（b 起点比 a 终点高 0.2）；</item>
    ///       <item>右端跳变：<c>c起点.Y − b终点.Y = −0.2</c>（c 起点比 b 终点低 0.2）。</item>
    ///     </list>
    ///   </item>
    ///   <item>输出 5 条 <see cref="Line"/>：a/b/c 为红/黄/绿；两处 Y 跳变处再画 a 终点→b 起点、b 终点→c 起点（蓝）。</item>
    /// </list>
    /// </summary>
    public sealed class RoadThreeSegmentChainCommand
    {
        private const double DefaultSegLengthM = 3.5;
        private const double DefaultSlopePct = 1.5;
        private const double DefaultLeftJumpM = 0.2;
        private const double DefaultRightJumpM = -0.2;

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var optPt = new PromptPointOptions("\n[ABC3] 拾取 a 段起点（X=里程, Y=高程）：") { AllowNone = false };
            var resPt = ed.GetPoint(optPt);
            if (resPt.Status != PromptStatus.OK) return;
            double x0 = resPt.Value.X;
            double y0 = resPt.Value.Y;
            double z0 = resPt.Value.Z; // 保留拾取点自身 Z（不参与计算）

            double len = AskDouble(ed, "段长 m", DefaultSegLengthM, min: 0.001);
            double slopePct = AskDouble(ed, "坡度 %（沿 +X，正 = Y 升）", DefaultSlopePct, allowNegative: true);
            double leftJump = AskDouble(ed, "左端 Y 跳变 m（b起点 − a终点）", DefaultLeftJumpM, allowNegative: true);
            double rightJump = AskDouble(ed, "右端 Y 跳变 m（c起点 − b终点）", DefaultRightJumpM, allowNegative: true);

            double s = slopePct / 100.0;
            double dy = len * s;

            var aStart = new Point3d(x0,             y0,                          z0);
            var aEnd   = new Point3d(x0 + len,       y0 + dy,                     z0);
            var bStart = new Point3d(aEnd.X,         aEnd.Y + leftJump,           z0);
            var bEnd   = new Point3d(x0 + 2 * len,   bStart.Y + dy,               z0);
            var cStart = new Point3d(bEnd.X,         bEnd.Y + rightJump,          z0);
            var cEnd   = new Point3d(x0 + 3 * len,   cStart.Y + dy,               z0);

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var ms = (BlockTableRecord)tr.GetObject(
                    SymbolUtilityServices.GetBlockModelSpaceId(db),
                    OpenMode.ForWrite);

                AppendColoredLine(tr, ms, aStart, aEnd, colorIndex: 1); // red   : a
                AppendColoredLine(tr, ms, bStart, bEnd, colorIndex: 2); // yellow: b
                AppendColoredLine(tr, ms, cStart, cEnd, colorIndex: 3); // green : c
                // 立面里同 X、不同 Y 的跳变：显式画出竖向连线
                const double eps = 1e-9;
                if (aEnd.DistanceTo(bStart) > eps)
                    AppendColoredLine(tr, ms, aEnd, bStart, colorIndex: 5); // blue: a终→b起
                if (bEnd.DistanceTo(cStart) > eps)
                    AppendColoredLine(tr, ms, bEnd, cStart, colorIndex: 5); // blue: b终→c起

                tr.Commit();
            }

            ed.WriteMessage(
                $"\n[ABC3] 完成：L={len:F3} 坡={slopePct:F3}% 左跳={leftJump:+0.000;-0.000} 右跳={rightJump:+0.000;-0.000}（+2 条蓝线：跳变竖连）" +
                $"\n  a: ({aStart.X:F3},{aStart.Y:F3}) → ({aEnd.X:F3},{aEnd.Y:F3})" +
                $"\n  b: ({bStart.X:F3},{bStart.Y:F3}) → ({bEnd.X:F3},{bEnd.Y:F3})" +
                $"\n  c: ({cStart.X:F3},{cStart.Y:F3}) → ({cEnd.X:F3},{cEnd.Y:F3})");
        }

        private static void AppendColoredLine(Transaction tr, BlockTableRecord ms, Point3d p1, Point3d p2, short colorIndex)
        {
            var ln = new Line(p1, p2)
            {
                ColorIndex = colorIndex,
            };
            ms.AppendEntity(ln);
            tr.AddNewlyCreatedDBObject(ln, true);
        }

        private static double AskDouble(Editor ed, string prompt, double defaultValue, double min = double.NegativeInfinity, bool allowNegative = false)
        {
            var opt = new PromptDoubleOptions($"\n[ABC3] {prompt} <{defaultValue:F3}>：")
            {
                DefaultValue = defaultValue,
                UseDefaultValue = true,
                AllowNone = true,
                AllowNegative = allowNegative,
            };
            var r = ed.GetDouble(opt);
            if (r.Status != PromptStatus.OK) return defaultValue;
            if (r.Value < min) return defaultValue;
            return r.Value;
        }
    }
}
