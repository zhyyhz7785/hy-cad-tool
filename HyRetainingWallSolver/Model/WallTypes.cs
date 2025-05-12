//using System.Collections.Generic;
//using System.Reflection;
//using System;
//using Autodesk.AutoCAD.Geometry;
//using Autodesk.AutoCAD.DatabaseServices;
//namespace HyRetainingWallSolver.Model
//{
//    public enum BoundaryFixity
//    {
//        Fixed,     // 固定端
//        Hinged,    // 简支
//        Free       // 自由边
//    }
//    public class WallBoundary
//    {
//        public LineSegment3d Edge { get; set; }
//        public BoundaryFixity Fixity { get; set; }
//        public WallBoundary(LineSegment3d edge, BoundaryFixity fixity)
//        {
//            Edge = edge;
//            Fixity = fixity;
//        }
//    }
//    public class WallGeometryInput
//    {
//        public Polyline Outline { get; set; }
//        public List<WallBoundary> Boundaries { get; set; } = new List<WallBoundary>();
//    }
//}