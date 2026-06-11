using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using AcDbPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace HyCADTool.Shared.AutoCAD.Interactive
{
    /// <summary>
    /// 沿给定方向（通常为相交钢筋线段方向）绘制 15d 弯钩；光标在 ±方向 一侧决定左右。
    /// </summary>
    public class DirectionalHookJig : EntityJig
    {
        private readonly AcDbPolyline _polyline;
        private readonly Point3d _basePoint;
        private readonly Vector3d _hookDir;
        private readonly double _hookLength;
        private readonly int _numVertices;
        private readonly double _hookSegmentWidth;
        private Point3d _jigPoint;
        private bool _isCompleted;

        public DirectionalHookJig(
            AcDbPolyline polyline, Point3d basePoint, Vector3d hookBaseDir, double hookLength)
            : base(polyline)
        {
            _polyline = polyline;
            _basePoint = basePoint;
            _hookLength = hookLength;
            _numVertices = polyline.NumberOfVertices;

            if (_numVertices < 1)
                throw new InvalidOperationException("多段线顶点不足，无法添加弯钩。");

            if (hookBaseDir.Length < 1e-10)
                throw new InvalidOperationException("弯钩方向无效。");

            _hookDir = hookBaseDir.GetNormal();
            _jigPoint = _basePoint;
            int segIdx = Math.Max(0, _numVertices - 2);
            _hookSegmentWidth = HookJig.ResolveHookSegmentWidth(polyline, segIdx);
        }

        public double SourceWidth => _hookSegmentWidth;

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            if (_isCompleted)
                return SamplerStatus.Cancel;

            var jigOpts = new JigPromptPointOptions("\n移动光标选择弯钩方向（沿相交钢筋，点击确认）：");
            jigOpts.BasePoint = _basePoint;
            jigOpts.UseBasePoint = true;
            jigOpts.UserInputControls =
                UserInputControls.Accept3dCoordinates | UserInputControls.NoNegativeResponseAccepted;

            PromptPointResult ppr = prompts.AcquirePoint(jigOpts);
            if (ppr.Status == PromptStatus.OK)
            {
                if (_jigPoint.DistanceTo(ppr.Value) < Tolerance.Global.EqualPoint)
                    return SamplerStatus.NoChange;
                _jigPoint = ppr.Value;
                return SamplerStatus.OK;
            }

            if (ppr.Status == PromptStatus.None)
            {
                _isCompleted = true;
                return SamplerStatus.OK;
            }

            return SamplerStatus.Cancel;
        }

        protected override bool Update()
        {
            while (_polyline.NumberOfVertices > _numVertices)
                _polyline.RemoveVertexAt(_polyline.NumberOfVertices - 1);

            Vector3d toCursor = _basePoint.GetVectorTo(_jigPoint);
            if (toCursor.Length < 1e-10)
                return false;

            double sign = Math.Sign(_hookDir.DotProduct(toCursor));
            if (Math.Abs(sign) < 1e-10)
                sign = 1;

            Vector3d dir = _hookDir * sign;
            Point3d hookPoint = _basePoint + dir * _hookLength;
            _polyline.AddVertexAt(_numVertices, new Point2d(hookPoint.X, hookPoint.Y), 0, 0, 0);

            int segIdx = _numVertices - 1;
            if (segIdx >= 0)
            {
                _polyline.SetStartWidthAt(segIdx, _hookSegmentWidth);
                _polyline.SetEndWidthAt(segIdx, _hookSegmentWidth);
            }

            return true;
        }
    }
}
