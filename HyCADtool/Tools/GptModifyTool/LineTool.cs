using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using System;
namespace HyCADTool.Tools
{
    public static partial class HyTool
    {
        #region 直线相关操作
        public static bool IsParallel(this LineSegment3d seg1, LineSegment3d seg2)
        {
            bool b = false;
            if (seg1.Direction.IsEqualTo(seg2.Direction, BaseConfig.ToleranceVec) ||
                seg1.Direction.IsEqualTo(-seg2.Direction, BaseConfig.ToleranceVec))
            {
                b = true;
            }
            return b;
        }
        public static bool IsParallel(this Line line1, Line line2)
        {
            bool b = false;
            if (line1.Delta.GetNormal().IsEqualTo(line2.Delta.GetNormal(), BaseConfig.ToleranceVec) ||
                line1.Delta.GetNormal().IsEqualTo(-line2.Delta.GetNormal(), BaseConfig.ToleranceVec))
            {
                b = true;
            }
            return b;
        }
        /// <summary>
        /// 平行直线的垂直距离
        /// </summary>
        /// <param name="line1"></param>
        /// <param name="line2"></param>
        /// <returns></returns>
        public static double ParallelLineDistance(this Line line1, Line line2)
        {
            var pointI1 = line1.GetClosestPointTo(
                line2.StartPoint, line1.Delta.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
            var pointI2 = line2.GetClosestPointTo(
               pointI1, line2.Delta.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
            var d = pointI1.DistanceTo(pointI2);
            return d;
        }
        public static double ParallelLineDistance(this LineSegment3d seg1, LineSegment3d seg2)
        {
            var line1 = new Line(seg1.StartPoint, seg1.EndPoint);
            var line2 = new Line(seg2.StartPoint, seg2.EndPoint);
            var pointI1 = line1.GetClosestPointTo(
                line2.StartPoint, line1.Delta.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
            var pointI2 = line2.GetClosestPointTo(
               pointI1, line2.Delta.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
            var d = pointI1.DistanceTo(pointI2);
            return d;
        }
        public static double ParallelLineDistance(this LineSegment2d seg1, LineSegment2d seg2)
        {
            var line1 = new Line(seg1.StartPoint.Point2dTo3d(), seg1.EndPoint.Point2dTo3d());
            var line2 = new Line(seg2.StartPoint.Point2dTo3d(), seg2.EndPoint.Point2dTo3d());
            var pointI1 = line1.GetClosestPointTo(
                line2.StartPoint, line1.Delta.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
            var pointI2 = line2.GetClosestPointTo(
               pointI1, line2.Delta.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
            var d = pointI1.DistanceTo(pointI2);
            return d;
        }
        //public static Point2d GetIntersect(this LineSegment2d seg1, LineSegment2d seg2)
        //{
        //    Point2dCollection intersectionPoints = new Point2dCollection();
        //    var a= seg1.IntersectWith(seg2);
        //    var line1 = new Line(seg1.StartPoint.Point2dTo3d(), seg1.EndPoint.Point2dTo3d());
        //    var line2 = new Line(seg2.StartPoint.Point2dTo3d(), seg2.EndPoint.Point2dTo3d());
        //    var pointI1 = line1.GetClosestPointTo(
        //        line2.StartPoint, line1.Delta.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
        //    var pointI2 = line2.GetClosestPointTo(
        //       pointI1, line2.Delta.GetNormal().RotateBy(Math.PI / 2, Vector3d.ZAxis), true);
        //    var d = pointI1.DistanceTo(pointI2);
        //    return d;
        //}
        #endregion
    }
}
