using HyCAD.Geometry.Interfaces;
using System;

namespace HyCAD.Geometry.Algorithms
{
    /// <summary>
    /// 纯算法射线-多段线交点服务（平台无关，无 AutoCAD 事务开销）。
    /// </summary>
    public class RayPolylineIntersectionService : ILineIntersectionService
    {
        private const double MinRayParameter = 1e-6;
        private const double ParallelTolerance = 1e-10;
        private const double DirectionDotTolerance = 0.999;

        public Point2D GetNearestForwardIntersection(Point2D segmentEndPoint, Vector2D direction, Polyline2D boundary)
        {
            if (TryGetNearestForwardIntersection(segmentEndPoint, direction, boundary, out Point2D hit))
                return hit;

            return segmentEndPoint;
        }

        public bool TryGetNearestForwardIntersection(
            Point2D segmentEndPoint,
            Vector2D direction,
            Polyline2D boundary,
            out Point2D intersection)
        {
            intersection = segmentEndPoint;

            if (boundary == null || !direction.TryNormalize(out Vector2D dir))
                return false;

            Point2D nearest = segmentEndPoint;
            double nearestDist = double.MaxValue;
            bool found = false;

            int segCount = boundary.SegmentCount;
            for (int i = 0; i < segCount; i++)
            {
                var seg = boundary.GetSegmentAt(i);
                if (TryRayHitSegment(segmentEndPoint, dir, seg.StartPoint, seg.EndPoint, out Point2D hit, out double t)
                    && t < nearestDist)
                {
                    nearestDist = t;
                    nearest = hit;
                    found = true;
                }
            }

            if (found)
            {
                intersection = nearest;
                return true;
            }

            return false;
        }

        private static bool TryRayHitSegment(
            Point2D origin, Vector2D dir, Point2D segStart, Point2D segEnd,
            out Point2D hit, out double rayParameter)
        {
            hit = Point2D.Origin;
            rayParameter = 0;

            double sx = segEnd.X - segStart.X;
            double sy = segEnd.Y - segStart.Y;
            double cross = dir.X * sy - dir.Y * sx;

            if (System.Math.Abs(cross) < ParallelTolerance)
                return TryRayHitCollinearSegment(origin, dir, segStart, segEnd, out hit, out rayParameter);

            double ox = segStart.X - origin.X;
            double oy = segStart.Y - origin.Y;
            rayParameter = (ox * sy - oy * sx) / cross;
            double u = (ox * dir.Y - oy * dir.X) / cross;

            if (rayParameter < MinRayParameter || u < -1e-6 || u > 1.0 + 1e-6)
                return false;

            hit = new Point2D(origin.X + rayParameter * dir.X, origin.Y + rayParameter * dir.Y);
            return true;
        }

        /// <summary>
        /// 射线与边共线/近平行时：取边上位于射线正方向且距起点最近的点（含端点）。
        /// </summary>
        private static bool TryRayHitCollinearSegment(
            Point2D origin, Vector2D dir, Point2D segStart, Point2D segEnd,
            out Point2D hit, out double rayParameter)
        {
            hit = Point2D.Origin;
            rayParameter = 0;

            if (!IsPointOnRayForward(origin, dir, segStart, out double tStart)
                && !IsPointOnRayForward(origin, dir, segEnd, out double tEnd))
            {
                return false;
            }

            bool hasStart = IsPointOnRayForward(origin, dir, segStart, out tStart);
            bool hasEnd = IsPointOnRayForward(origin, dir, segEnd, out tEnd);

            if (hasStart && hasEnd)
            {
                if (tStart <= tEnd)
                {
                    rayParameter = tStart;
                    hit = segStart;
                }
                else
                {
                    rayParameter = tEnd;
                    hit = segEnd;
                }
            }
            else if (hasStart)
            {
                rayParameter = tStart;
                hit = segStart;
            }
            else
            {
                rayParameter = tEnd;
                hit = segEnd;
            }

            return rayParameter >= MinRayParameter;
        }

        private static bool IsPointOnRayForward(Point2D origin, Vector2D dir, Point2D point, out double rayParameter)
        {
            rayParameter = 0;
            Vector2D offset = origin.VectorTo(point);
            if (offset.IsZero())
                return false;

            if (!offset.TryNormalize(out Vector2D offsetDir))
                return false;

            if (offsetDir.Dot(dir) < DirectionDotTolerance)
                return false;

            rayParameter = origin.DistanceTo(point);
            return rayParameter >= MinRayParameter;
        }
    }
}
