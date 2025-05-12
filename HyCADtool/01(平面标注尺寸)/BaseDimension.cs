using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Clipper2Lib;
using HyCADTool.Config;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.HelpClass
{
    public class BaseDimension
    {
        #region 属性
        // 【新增】是否添加交点
        public bool IsPointsToSpace { get; set; } = false;
        public List<Polyline> BasePolylines { get; set; }
        public List<Polyline> AreaPolylines { get; set; }
        public List<Line> AxisLines { get; set; }
        public List<Point3d> BPs { get; set; }
        public ObjectId BPsLayerId { get; set; }
        public List<Point3d> A_APs { get; set; }
        public ObjectId A_APsLayerId { get; set; }
        public List<Point3d> B_APs { get; set; }
        public ObjectId B_APsLayerId { get; set; }
        public List<Point3d> ABs { get; set; } = new List<Point3d>();
        public ObjectId ABsLayerId { get; set; }
        public double Scale { get; set; } = BaseConfig.Scale;
        public double Tolerance { get; set; } = 0.001;
        #endregion
        #region 构造函数
        public BaseDimension(bool isPointsToSpace)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            BPsLayerId = EtGpt.CreateLayer("00_hy_基础_点_基础轮廓", 152, db, ed);
            A_APsLayerId = EtGpt.CreateLayer("00_hy_基础_点_轨迹交点", 12, db, ed);
            B_APsLayerId = EtGpt.CreateLayer("00_hy_基础_点_轨迹基础", 32, db, ed);
            ABsLayerId = EtGpt.CreateLayer("00_hy_基础_点_螺栓", 157, db, ed);
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var result = SelectPolyAndLine(db, ed, tr);
                if (result.Item1 == null || result.Item2 == null) return;
                BasePolylines = result.Item1;
                AxisLines = result.Item2;
                tr.Commit();
            }
            if (BasePolylines == null || AxisLines == null) return;
            IsPointsToSpace = isPointsToSpace;  // ✨ 重要，赋值
            InitializeLayers(db, ed);
        }
        public static (List<Polyline>, List<Line>) SelectPolyAndLine(Database db, Editor ed, Transaction tr)
        {
            var basePolylines = new List<Polyline>();
            var axisLines = new List<Line>();
            var filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<or"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Operator, "or>")
            };
            var filter = new SelectionFilter(filterList);
            var selectionResult = ed.GetSelection(filter);
            if (selectionResult.Status != PromptStatus.OK) return (null, null);
            foreach (SelectedObject selectedObj in selectionResult.Value)
            {
                if (selectedObj == null) continue;
                var entity = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Entity;
                if (entity is Polyline polyline) basePolylines.Add(polyline);
                else if (entity is Line line) axisLines.Add(line);
            }
            // 选择点/圆/块作为螺栓点
            return (basePolylines, axisLines);
        }
        public static BaseDimension Create(bool isPointsToSpace)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            var filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<or"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Operator, "or>")
            };
            var filter = new SelectionFilter(filterList);
            var selectionResult = ed.GetSelection(filter);
            if (selectionResult.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n选择已取消\n");
                return null;
            }
            var basePolylines = new List<Polyline>();
            var axisLines = new List<Line>();
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selectedObj in selectionResult.Value)
                {
                    Entity entity = tr.GetObject(selectedObj.ObjectId, OpenMode.ForRead) as Entity;
                    if (entity == null) continue;
                    if (entity is Polyline polyline) basePolylines.Add(polyline);
                    else if (entity is Line line) axisLines.Add(line);
                }
                tr.Commit();
            }
            if (basePolylines.Count == 0 && axisLines.Count == 0)
            {
                ed.WriteMessage("\n未选择有效对象\n");
                return null;
            }
            var dimension = new BaseDimension(isPointsToSpace);
            dimension.BasePolylines = basePolylines;
            dimension.AxisLines = axisLines;
            dimension.AreaPolylines = new List<Polyline>();
            dimension.InitializeLayers(db, ed);
            return dimension;
        }
        #endregion
        #region 初始化
        private void InitializeLayers(Database db, Editor ed)
        {
            if (db == null) return;
            BPs = GetBasePoints();
            A_APs = GetAxisSelfIntersectionPoints();
            B_APs = GetBaseAxisIntersectionPoints();
           // ABs = PointClusterHelper.SelectPointsOrCircles();
            if (IsPointsToSpace)
            {
                AddPointsToLayer(BPs, BPsLayerId, db);
                AddPointsToLayer(A_APs, A_APsLayerId, db);
                AddPointsToLayer(B_APs, B_APsLayerId, db);
               // AddPointsToLayer(ABs, ABsLayerId, db);
            }
        }
        private void AddPointsToLayer(List<Point3d> points, ObjectId layerId, Database db)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                foreach (Point3d pt in points)
                {
                    DBPoint dbPoint = new DBPoint(pt) { LayerId = layerId };
                    btr.AppendEntity(dbPoint);
                    tr.AddNewlyCreatedDBObject(dbPoint, true);
                }
                tr.Commit();
            }
        }
        #endregion
        #region 点集生成
        private List<Point3d> GetBasePoints()
        {
            var points = new HashSet<Point3d>(new Point2dEqualityComparer(Tolerance));
            foreach (var pline in BasePolylines)
            {
                for (int i = 0; i < pline.NumberOfVertices; i++)
                    points.Add(pline.GetPoint3dAt(i));
            }
            return points.ToList();
        }
        private List<Point3d> GetAxisSelfIntersectionPoints()
        {
            var points = new HashSet<Point3d>(new Point2dEqualityComparer(Tolerance));
            for (int i = 0; i < AxisLines.Count - 1; i++)
            {
                for (int j = i + 1; j < AxisLines.Count; j++)
                {
                    var intersect = GetIntersectionPoints(AxisLines[i], AxisLines[j]);
                    points.UnionWith(intersect);
                }
            }
            return points.ToList();
        }
        private List<Point3d> GetBaseAxisIntersectionPoints()
        {
            var points = new HashSet<Point3d>(new Point2dEqualityComparer(Tolerance));
            var baseLines = ExplodePolylinesToLines(BasePolylines);
            foreach (var baseLine in baseLines)
            {
                foreach (var axisLine in AxisLines)
                {
                    var intersect = GetIntersectionPoints(baseLine, axisLine);
                    points.UnionWith(intersect);
                }
            }
            return points.ToList();
        }
        #endregion
        #region 辅助
        private List<Line> ExplodePolylinesToLines(List<Polyline> polylines)
        {
            var lines = new List<Line>();
            foreach (var pline in polylines)
            {
                var dbObjColl = new DBObjectCollection();
                pline.Explode(dbObjColl);
                foreach (DBObject obj in dbObjColl)
                {
                    if (obj is Line line) lines.Add(line);
                }
            }
            return lines;
        }
        private List<Point3d> GetIntersectionPoints(Line line1, Line line2)
        {
            var points = new List<Point3d>();
            try
            {
                var collection = new Point3dCollection();
                line1.IntersectWith(line2, Intersect.OnBothOperands, collection, IntPtr.Zero, IntPtr.Zero);
                foreach (Point3d pt in collection)
                    points.Add(new Point3d(pt.X, pt.Y, 0));
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                HyCADTool.Log.SimpleLogger.LogWarning($"AutoCAD求交点异常：{ex.Message}");
            }
            return points;
        }
        private class Point2dEqualityComparer : IEqualityComparer<Point3d>
        {
            private readonly double _tolerance;
            public Point2dEqualityComparer(double tolerance) => _tolerance = tolerance;
            public bool Equals(Point3d p1, Point3d p2) =>
                Math.Abs(p1.X - p2.X) <= _tolerance && Math.Abs(p1.Y - p2.Y) <= _tolerance;
            public int GetHashCode(Point3d point) => HashCode.Combine((int)(point.X / _tolerance), (int)(point.Y / _tolerance));
        }
        #endregion
    }
}
