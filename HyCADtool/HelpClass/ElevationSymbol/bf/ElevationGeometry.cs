using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
namespace HyCADTool.HelpClass.ElevationSymbol
{
    public class ElevationGeometry
    {
        public Polyline Shape { get; private set; }
        public Polyline Shape1 { get; private set; }
        private Point3d _center;
        private double _scale;
        private double _d;
        private double _angleRadians;
        private readonly ObjectId _layerId;
        private ElevationSymbolState _state;
        public ElevationGeometry(Point3d center, double scale, double d, double angleRadians, ObjectId layerId, ElevationSymbolState state = ElevationSymbolState.Normal)
        {
            _center = center;
            _scale = scale;
            _d = d;
            _angleRadians = angleRadians;
            _layerId = layerId;
            _state = state;
            UpdateGeometry();
        }
        public void UpdateGeometry(ElevationSymbolState? state = null, Point3d? center = null)
        {
            if (center.HasValue) _center = center.Value;
            if (state.HasValue) _state = state.Value;
            double sqrt2 = Math.Sqrt(2) / 2 * _d;
            Point3d[] points = new Point3d[4];
            points[0] = new Point3d(_center.X + 3.5 * _d * _scale, _center.Y + sqrt2 * _scale, 0);
            points[1] = new Point3d(_center.X - sqrt2 * _scale, _center.Y + sqrt2 * _scale, 0);
            points[2] = _center;
            points[3] = new Point3d(sqrt2 * _scale + _center.X, _center.Y + sqrt2 * _scale, 0);
            Point3d[] points1 = new Point3d[2];
            points1[0] = new Point3d(_center.X + sqrt2 * _scale, _center.Y, 0);
            points1[1] = new Point3d(_center.X - sqrt2 * _scale, _center.Y, 0);
            AdjustPointsByState(ref points);
            Shape = new Polyline();
            Shape1 = new Polyline();
            Shape.LayerId = _layerId;
            Shape1.LayerId = _layerId;
            for (int i = 0; i < 4; i++)
            {
                Shape.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
            }
            for (int i = 0; i < 2; i++)
            {
                Shape1.AddVertexAt(i, new Point2d(points1[i].X, points1[i].Y), 0, 0, 0);
            }
            Shape.TransformBy(Matrix3d.Rotation(_angleRadians, Vector3d.ZAxis, _center));
            Shape1.TransformBy(Matrix3d.Rotation(_angleRadians, Vector3d.ZAxis, _center));
        }
        private void AdjustPointsByState(ref Point3d[] points)
        {
            switch (_state)
            {
                case ElevationSymbolState.FlipVertical:
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new Point3d(points[i].X, 2 * _center.Y - points[i].Y, points[i].Z);
                    break;
                case ElevationSymbolState.FlipHorizontal:
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new Point3d(2 * _center.X - points[i].X, points[i].Y, points[i].Z);
                    break;
                case ElevationSymbolState.FlipBoth:
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new Point3d(2 * _center.X - points[i].X, 2 * _center.Y - points[i].Y, points[i].Z);
                    break;
            }
        }
    }
}