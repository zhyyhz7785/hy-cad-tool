using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 钢筋标注命令（对应旧命令 gb / gb1 / gb2）
    ///   gb  - 多引线标注 + 点钢筋
    ///   gb1 - 单引线标注（无点钢筋）
    ///   gb2 - 六点引线标注 + 点钢筋
    /// </summary>
    public class MleaderReinCommand
    {
        /// <summary>标注模式</summary>
        public enum Mode
        {
            /// <summary>gb: 多引线 + 点钢筋</summary>
            Standard,
            /// <summary>gb1: 单引线</summary>
            Single,
            /// <summary>gb2: 六点引线 + 点钢筋</summary>
            Six
        }

        private readonly ILayerService _layerService;
        private readonly Mode _mode;

        private const string LayerMLeader = "00_hy_3公共_标注3_引线";
        private const string LayerDotRein = "01_hy_1钢筋_点钢筋";
        private const double PointOffset = 100.0; // 沿多段线偏移距离（mm）
        private const double SixPointDimDistance = 465.0; // gb2 水平偏移距离

        public MleaderReinCommand(Mode mode = Mode.Standard)
        {
            _mode = mode;
            _layerService = ServiceLocator.Resolve<ILayerService>();
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 从面板读取最新参数（样式已在 CommandRegistry.Run 中同步，无需重复调用）
            var vm = SettingsPanelViewModel.Current;

            double scale = vm?.Scale ?? 40.0;
            double mleaderDistance = (vm?.MleaderDistance ?? 6.0) * scale;
            double rebarDiameter = vm?.RebarDiameter ?? 14.0;
            double rebarSpacing = vm?.RebarSpacing ?? 200.0;
            double reinDiameter = (vm?.ReinforcementDiameter ?? 0.35) * scale;
            // DotReinOffsetOut = (DotReinOffset - 1) * Scale，与旧 Reinforcement.DotReinOffsetOut 一致
            double dotReinOffsetRaw = vm?.DotReinOffset ?? 1.35;
            double dotReinOffsetOut = (dotReinOffsetRaw - 1) * scale;

            // 确保样式已同步
            vm?.EnsureStylesApplied();

            string content = $"\\U+E532{rebarDiameter}@{rebarSpacing}";

            // 图层已在 PluginInitializer 中创建，直接设置当前图层
            _layerService.SetCurrentLayer(LayerMLeader);

            try
            {
                switch (_mode)
                {
                    case Mode.Standard:
                        ExecuteStandard(db, ed, mleaderDistance, content, reinDiameter, dotReinOffsetOut);
                        break;
                    case Mode.Single:
                        ExecuteSingle(db, ed, mleaderDistance, content, dotReinOffsetOut);
                        break;
                    case Mode.Six:
                        ExecuteSix(db, ed, content, reinDiameter, dotReinOffsetOut);
                        break;
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }

        // ================================================================
        //  三种模式的执行方法
        // ================================================================

        /// <summary>gb: 多引线标注 + 点钢筋</summary>
        private void ExecuteStandard(Database db, Editor ed, double mleaderDistance, string content,
            double reinDiameter, double dotReinOffset)
        {
            var points = GetReinPoints(db, ed, dotReinOffset);
            if (points == null || points.Count < 3) return;

            var ps = points.ToArray();
            var ml = ps.AddMleader(mleaderDistance, content);

            // 点钢筋：排除 index=1（中心点），仅保留两侧偏移点
            var dotPoints = ps.Where((p, i) => i != 1);
            var psDraw = dotPoints.PointsToDotRein(reinDiameter);

            AddEntitiesToSpace(db, ml, psDraw);
        }

        /// <summary>gb1: 单引线标注（无点钢筋）</summary>
        private void ExecuteSingle(Database db, Editor ed, double mleaderDistance, string content,
            double dotReinOffset)
        {
            var points = GetReinPoints(db, ed, dotReinOffset);
            if (points == null || points.Count < 3) return;

            var ps = points.ToArray();
            var ml = ps.AddMleaderOne(mleaderDistance, content);

            ml.ToSpace();
        }

        /// <summary>gb2: 六点引线标注 + 点钢筋</summary>
        private void ExecuteSix(Database db, Editor ed, string content,
            double reinDiameter, double dotReinOffset)
        {
            var points = GetReinPointsSix(db, ed, dotReinOffset);
            if (points == null || points.Count < 6) return;

            var ps = points.ToArray();
            var ml = ps.AddMleaderSix(SixPointDimDistance, content);

            // 点钢筋：排除 index=1 和 index=4（两组的中心点）
            var dotPoints = ps.Where((p, i) => i != 1 && i != 4);
            var psDraw = dotPoints.PointsToDotRein(reinDiameter);

            AddEntitiesToSpace(db, ml, psDraw);
        }

        // ================================================================
        //  写入模型空间（统一处理图层）
        // ================================================================

        /// <summary>
        /// 将 MLeader 和点钢筋实体写入模型空间，点钢筋设置为指定图层
        /// </summary>
        private void AddEntitiesToSpace(Database db, MLeader ml, Polyline[] dotReins)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // 添加点钢筋（指定图层）
                foreach (var pl in dotReins)
                {
                    btr.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);
                    pl.Layer = LayerDotRein;
                }

                // 添加引线（当前图层）
                btr.AppendEntity(ml);
                tr.AddNewlyCreatedDBObject(ml, true);

                tr.Commit();
            }
        }

        // ================================================================
        //  交互式获取钢筋标注点
        // ================================================================

        /// <summary>
        /// 获取3个标注点：沿多段线偏移的两个点钢筋位置 + 中心点
        /// 返回 [偏移点1, 中心点, 偏移点2]
        /// </summary>
        private List<Point3d> GetReinPoints(Database db, Editor ed, double dotReinOffset)
        {
            var peo = new PromptEntityOptions("\n请选择需要标注的多段线：");
            peo.SetRejectMessage("\n请选择一个多段线对象。");
            peo.AddAllowedClass(typeof(Polyline), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var pl = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                if (pl == null) { tr.Commit(); return null; }

                var points = GetPointsAlongPolyline(pl, per.PickedPoint, PointOffset, dotReinOffset);
                tr.Commit();
                return points;
            }
        }

        /// <summary>
        /// 获取6个标注点（两组各3个），用于双排标注
        /// 第一组选第一条多段线，第二组选第二条（以第一组中心点投影到线段）
        /// </summary>
        private List<Point3d> GetReinPointsSix(Database db, Editor ed, double dotReinOffset)
        {
            var a = GetReinPoints(db, ed, dotReinOffset);
            if (a == null || a.Count < 3) return null;

            var centerPt = a[1]; // 第一组的中心点

            // 选择第二条多段线
            var peo = new PromptEntityOptions("\n请选择第二条需要标注的多段线：");
            peo.SetRejectMessage("\n请选择一个多段线对象。");
            peo.AddAllowedClass(typeof(Polyline), true);
            var per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var pl = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                if (pl == null) { tr.Commit(); return null; }

                // 找到选中位置对应的线段
                Point3d closestPt = pl.GetClosestPointTo(per.PickedPoint, false);
                double param = pl.GetParameterAtPoint(closestPt);
                int segIndex = Math.Min((int)Math.Floor(param), Math.Max(pl.NumberOfVertices - 2, 0));

                // 将第一组中心点投影到该线段上
                Point3d segStart = pl.GetPoint3dAt(segIndex);
                Point3d segEnd = pl.GetPoint3dAt(segIndex + 1);
                var segment = new LineSegment3d(segStart, segEnd);
                Point3d projectedPt = GetPerpendicularPoint(segment, centerPt);

                // 以投影点为基准获取第二组3个点（dv=17.5 与旧代码一致）
                var points = GetPointsAlongPolyline(pl, projectedPt, PointOffset, 17.5);
                tr.Commit();

                if (points == null || points.Count < 3) return null;

                var result = new List<Point3d>(a);
                result.AddRange(points);
                return result;
            }
        }

        // ================================================================
        //  几何计算辅助方法
        // ================================================================

        /// <summary>
        /// 沿多段线获取3个点：在选中位置前后各偏移 offset 距离，并沿法线方向偏移 dv
        /// 返回 [法线偏移点1, 最近点, 法线偏移点2]
        /// </summary>
        private static List<Point3d> GetPointsAlongPolyline(Polyline pl, Point3d selPt, double offset, double dv)
        {
            var points = new List<Point3d>();

            // 找到多段线上距离选择点最近的点
            Point3d a = pl.GetClosestPointTo(selPt, false);
            double distA = pl.GetDistAtPoint(a);

            // 前后各偏移 offset 距离，限制在多段线长度范围内
            double distMinus = Math.Max(distA - offset, 0);
            double distPlus = Math.Min(distA + offset, pl.Length);

            Point3d ptMinus = pl.GetPointAtDist(distMinus);
            Point3d ptPlus = pl.GetPointAtDist(distPlus);

            // 沿法线方向偏移
            Vector3d tangentMinus = pl.GetFirstDerivative(ptMinus).GetNormal();
            Vector3d normalMinus = tangentMinus.RotateBy(Math.PI / 2, Vector3d.ZAxis).GetNormal();
            Vector3d tangentPlus = pl.GetFirstDerivative(ptPlus).GetNormal();
            Vector3d normalPlus = tangentPlus.RotateBy(Math.PI / 2, Vector3d.ZAxis).GetNormal();

            ptMinus = ptMinus + normalMinus * dv;
            ptPlus = ptPlus + normalPlus * dv;

            points.Add(ptMinus);
            points.Add(a);
            points.Add(ptPlus);

            return points;
        }

        /// <summary>
        /// 计算点到线段的垂足（投影点）
        /// </summary>
        private static Point3d GetPerpendicularPoint(LineSegment3d segment, Point3d point)
        {
            Point3d start = segment.StartPoint;
            Point3d end = segment.EndPoint;
            Vector3d segVec = end - start;
            Vector3d ptVec = point - start;

            double segLenSq = segVec.DotProduct(segVec);
            if (segLenSq < 1e-10) return start;

            double t = ptVec.DotProduct(segVec) / segLenSq;
            t = Math.Max(0, Math.Min(1, t)); // 限制在线段范围内

            return start + segVec * t;
        }

    }
}
