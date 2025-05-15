using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Clipper2Lib;
using HyCADTool.Drawing;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using static HyCADTool.Tools.EtGpt;

namespace HyCADTool.Models.Cluster
{
    public static class DimPointsAndAxisConfig
    {
        public static bool IncludeBPs { get; set; } = false;
        public static bool IncludeAAPs { get; set; } = false;
        public static bool IncludeBAPs { get; set; } = false;
        public static bool IncludeABs { get; set; } = false;
        public static bool IncludeSteelPlatePs { get; set; } = false;


        public static List<Point3d> SelectPoints(DimPointsAndAxis dpa)
        {
            return dpa.GetFilteredPoints();
        }
    }

    public class DimPointsAndAxis
    {
        public List<Point3d> BPs { get; private set; } = new List<Point3d>();
        public List<Point3d> A_APs { get; private set; } = new List<Point3d>();
        public List<Point3d> B_APs { get; private set; } = new List<Point3d>();
        public List<Point3d> ABs { get; private set; } = new List<Point3d>();
        public List<Point3d> SteelPlatePs { get; private set; } = new List<Point3d>();
        public List<Line> AxisLines { get; private set; } = new List<Line>();
        private readonly double _tolerance;
        public List<Point3d> AllPoints =>
        (DimPointsAndAxisConfig.IncludeBPs ? BPs : Enumerable.Empty<Point3d>())
        .Concat(DimPointsAndAxisConfig.IncludeAAPs ? A_APs : Enumerable.Empty<Point3d>())
        .Concat(DimPointsAndAxisConfig.IncludeBAPs ? B_APs : Enumerable.Empty<Point3d>())
        .Concat(DimPointsAndAxisConfig.IncludeABs ? ABs : Enumerable.Empty<Point3d>())
        .Concat(DimPointsAndAxisConfig.IncludeSteelPlatePs ? SteelPlatePs : Enumerable.Empty<Point3d>())
        .Distinct(new Point3dComparer(0.001))
        .ToList();
        private DimPointsAndAxis(double tolerance = 0.001)
        {
            _tolerance = tolerance;
        }

        public static DimPointsAndAxis GetInput(double tolerance = 0.001)
        {
            var instance = new DimPointsAndAxis(tolerance);
            instance.InitializeFromSelection();
            return instance;
        }

        private void InitializeFromSelection()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            var filterList = new TypedValue[]
            {
                new TypedValue((int)DxfCode.Operator, "<or"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Start, "POINT"),
                new TypedValue((int)DxfCode.Start, "CIRCLE"),
                new TypedValue((int)DxfCode.Start, "INSERT"),
                new TypedValue((int)DxfCode.Operator, "or>")
            };

            SelectionFilter filter = new SelectionFilter(filterList);
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择轮廓、轴线、点、圆或块: "
            };

            PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK) return;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var basePolylines = new List<Polyline>();

                foreach (ObjectId objId in selRes.Value.GetObjectIds())
                {
                    if (!(tr.GetObject(objId, OpenMode.ForRead) is Entity ent)) continue;
                    string layerName = ent.Layer ?? string.Empty;

                    switch (ent)
                    {
                        case Polyline pl:
                            if (layerName == "dcelInter")
                            {
                                basePolylines.Add(pl);
                            }
                            else if (layerName.StartsWith("00_Hy_螺栓_预埋板"))
                            {
                                var ext = pl.GeometricExtents;
                                var center = new Point3d((ext.MinPoint.X + ext.MaxPoint.X) / 2,
                                                         (ext.MinPoint.Y + ext.MaxPoint.Y) / 2, 0);
                                SteelPlatePs.Add(center);
                            }
                            break;

                        case Line line when layerName == "00_hy_3公共_轴线_总":
                            AxisLines.Add(line);
                            break;

                        case DBPoint pt when layerName.StartsWith("00_Hy_螺栓"):
                            ABs.Add(pt.Position);
                            break;

                        case Circle c when layerName.StartsWith("00_Hy_螺栓"):
                            ABs.Add(c.Center);
                            break;

                        case BlockReference br when layerName.StartsWith("00_Hy_螺栓"):
                            ABs.Add(br.Position);
                            break;
                    }
                }

                BPs = GetVertices(basePolylines);
                A_APs = GetAxisSelfIntersections();
                B_APs = GetPolylineAxisIntersections(basePolylines);

                tr.Commit();
            }
        }

        private List<Point3d> GetVertices(List<Polyline> polylines)
        {
            var set = new HashSet<Point3d>(new Point2dEqualityComparer(_tolerance));
            foreach (var pl in polylines)
            {
                for (int i = 0; i < pl.NumberOfVertices; i++)
                    set.Add(pl.GetPoint3dAt(i));
            }
            return set.ToList();
        }

        private List<Point3d> GetAxisSelfIntersections()
        {
            var points = new HashSet<Point3d>(new Point2dEqualityComparer(_tolerance));
            for (int i = 0; i < AxisLines.Count; i++)
            {
                for (int j = i + 1; j < AxisLines.Count; j++)
                    points.UnionWith(GetIntersectionPoints(AxisLines[i], AxisLines[j]));
            }
            return points.ToList();
        }

        private List<Point3d> GetPolylineAxisIntersections(List<Polyline> polylines)
        {
            var points = new HashSet<Point3d>(new Point2dEqualityComparer(_tolerance));
            var lines = ExplodePolylinesToLines(polylines);
            foreach (var baseLine in lines)
                foreach (var axis in AxisLines)
                    points.UnionWith(GetIntersectionPoints(baseLine, axis));

            return points.ToList();
        }

        private List<Line> ExplodePolylinesToLines(List<Polyline> polylines)
        {
            var lines = new List<Line>();
            foreach (var pl in polylines)
            {
                var coll = new DBObjectCollection();
                pl.Explode(coll);
                foreach (DBObject obj in coll)
                    if (obj is Line line)
                        lines.Add(line);
            }
            return lines;
        }

        private List<Point3d> GetIntersectionPoints(Line l1, Line l2)
        {
            var pts = new Point3dCollection();
            try { l1.IntersectWith(l2, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero); }
            catch { }
            return pts.Cast<Point3d>().ToList();
        }

        public List<Point3d> GetFilteredPoints()
        {
            var pts = new List<Point3d>();
            if (DimPointsAndAxisConfig.IncludeBPs) pts.AddRange(BPs);
            if (DimPointsAndAxisConfig.IncludeAAPs) pts.AddRange(A_APs);
            if (DimPointsAndAxisConfig.IncludeBAPs) pts.AddRange(B_APs);
            if (DimPointsAndAxisConfig.IncludeABs) pts.AddRange(ABs);
            if (DimPointsAndAxisConfig.IncludeSteelPlatePs) pts.AddRange(SteelPlatePs);

            return pts
                .Distinct(new Point2dEqualityComparer(_tolerance))
                .ToList();
        }

        public List<Point3d> SelectPoints => GetFilteredPoints();

        private class Point2dEqualityComparer : IEqualityComparer<Point3d>
        {
            private readonly double _tol;
            public Point2dEqualityComparer(double tol) => _tol = tol;
            public bool Equals(Point3d p1, Point3d p2) =>
                Math.Abs(p1.X - p2.X) <= _tol && Math.Abs(p1.Y - p2.Y) <= _tol;
            public int GetHashCode(Point3d p) => HashCode.Combine((int)(p.X / _tol), (int)(p.Y / _tol));
        }
    }
}