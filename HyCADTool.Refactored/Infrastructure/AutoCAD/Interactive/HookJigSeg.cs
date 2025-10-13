using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interactive
{
    /// <summary>
    /// 分段弯钩交互式绘制 Jig（Hook Segment Interactive Drawing Jig）
    /// 功能：根据边界线段方向添加弯钩
    /// </summary>
    public class HookJigSeg : EntityJig
    {
        private readonly Polyline _polyline;
        private readonly Point3d _lastVertex;
        private Point3d _jigPoint;
        private readonly int _numVertices;
        private readonly double _hookLength;
        private double _angle;
        private bool _isCompleted;
        private readonly Vector3d _segDirection;
        private readonly double _boundarySegAngle;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="polyline">要添加弯钩的多段线</param>
        /// <param name="hookLength">弯钩长度（Hook Length）</param>
        /// <param name="seg">边界线段（Boundary Segment）</param>
        public HookJigSeg(Polyline polyline, double hookLength, LineSegment3d seg) : base(polyline)
        {
            _hookLength = hookLength;
            _polyline = polyline;
            _numVertices = polyline.NumberOfVertices;

            if (_numVertices < 2)
                throw new InvalidOperationException("多段线的顶点数量不足，无法计算方向！");

            _lastVertex = polyline.GetPoint3dAt(_numVertices - 1);
            _jigPoint = _lastVertex;

            // 计算最后一段的方向向量
            _segDirection = polyline.GetPoint3dAt(_numVertices - 2).GetVectorTo(_lastVertex).GetNormal();

            // 计算边界线段的角度
            var boundarySegDirection = seg.EndPoint.GetVectorTo(seg.StartPoint);
            Vector3d xAxis = new Vector3d(1, 0, 0);
            _boundarySegAngle = boundarySegDirection.GetAngleTo(xAxis);
        }

        /// <summary>
        /// 采样器（Sampler）- 处理用户输入
        /// </summary>
        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            if (_isCompleted)
            {
                return SamplerStatus.Cancel;
            }

            JigPromptPointOptions jigOpts = new JigPromptPointOptions("\n移动光标以选择弯钩方向，点击确认: ");
            jigOpts.BasePoint = _lastVertex;
            jigOpts.UseBasePoint = true;
            jigOpts.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NoNegativeResponseAccepted;

            PromptPointResult ppr = prompts.AcquirePoint(jigOpts);

            if (ppr.Status == PromptStatus.OK)
            {
                if (_jigPoint.DistanceTo(ppr.Value) < Tolerance.Global.EqualPoint)
                {
                    return SamplerStatus.NoChange;
                }
                else
                {
                    _jigPoint = ppr.Value;
                    return SamplerStatus.OK;
                }
            }
            else if (ppr.Status == PromptStatus.None)
            {
                _isCompleted = true;
                return SamplerStatus.Cancel;
            }
            else
            {
                return SamplerStatus.Cancel;
            }
        }

        /// <summary>
        /// 更新实体（Update Entity）
        /// </summary>
        protected override bool Update()
        {
            // 清除之前添加的弯钩顶点
            while (_polyline.NumberOfVertices > _numVertices)
            {
                _polyline.RemoveVertexAt(_polyline.NumberOfVertices - 1);
            }

            // 计算方向
            Vector3d dir = _lastVertex.GetVectorTo(_jigPoint);
            if (dir.Length == 0)
                return false;

            // 计算叉积以确定方向
            double cross = _segDirection.CrossProduct(dir).Z;

            // 根据交叉方向和边界线段角度选择弯钩角度
            if (cross < 0)
            {
                _angle = _boundarySegAngle + Math.PI / 2; // +90°
            }
            else
            {
                _angle = _boundarySegAngle - Math.PI / 2; // -90°
            }

            // 计算弯钩方向并添加顶点
            Vector3d hookDirection = _segDirection.RotateBy(_angle, Vector3d.ZAxis);
            Point3d hookPoint = _lastVertex + hookDirection * _hookLength;
            _polyline.AddVertexAt(_numVertices, new Point2d(hookPoint.X, hookPoint.Y), 0, 0, 0);

            return true;
        }
    }
}

