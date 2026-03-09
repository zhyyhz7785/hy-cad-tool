using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using AcDbPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interactive
{
    /// <summary>
    /// 弯钩交互式绘制 Jig（Hook Interactive Drawing Jig）
    /// 功能：为钢筋多段线添加弯钩，根据光标方向自动确定弯钩角度
    /// </summary>
    public class HookJig : EntityJig
    {
        private readonly AcDbPolyline _polyline;
        private readonly Point3d _lastVertex;
        private Point3d _jigPoint;
        private readonly int _numVertices;
        private readonly double _hookLength;
        private readonly bool _isVertical;
        private double _angle;
        private bool _isCompleted;
        private readonly Vector3d _segDirection;
        private readonly double _hookSegmentWidth;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="polyline">要添加弯钩的多段线</param>
        /// <param name="hookLength">弯钩长度（Hook Length）</param>
        /// <param name="isVertical">是否为竖向弯钩（Is Vertical Hook）</param>
        public HookJig(AcDbPolyline polyline, double hookLength, bool isVertical) : base(polyline)
        {
            _hookLength = hookLength;
            _polyline = polyline;
            _isVertical = isVertical;
            _numVertices = polyline.NumberOfVertices;

            if (_numVertices < 2)
                throw new InvalidOperationException("多段线的顶点数量不足，无法计算方向！");

            _lastVertex = polyline.GetPoint3dAt(_numVertices - 1);
            _jigPoint = _lastVertex;

            // 计算最后一段的方向向量
            _segDirection = polyline.GetPoint3dAt(_numVertices - 2).GetVectorTo(_lastVertex).GetNormal();
            _hookSegmentWidth = ResolveHookSegmentWidth(polyline, _numVertices - 2);
        }

        /// <summary>
        /// 采样器（Sampler）- 处理用户输入
        /// </summary>
        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            // 如果操作已完成，返回 Cancel
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
                // 用户点击确认，结束操作
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

            // 根据交叉方向选择弯钩角度
            if (cross < 0)
            {
                _angle = _isVertical ? Math.PI * 6 / 4 : Math.PI * 5 / 4; // 270° 或 225°
            }
            else
            {
                _angle = _isVertical ? Math.PI * 2 / 4 : Math.PI * 3 / 4; // 90° 或 135°
            }

            // 计算弯钩方向并添加顶点
            Vector3d hookDirection = _segDirection.RotateBy(_angle, Vector3d.ZAxis);
            Point3d hookPoint = _lastVertex + hookDirection * _hookLength;
            _polyline.AddVertexAt(_numVertices, new Point2d(hookPoint.X, hookPoint.Y), 0, 0, 0);
            _polyline.SetStartWidthAt(_numVertices - 1, _hookSegmentWidth);
            _polyline.SetEndWidthAt(_numVertices - 1, _hookSegmentWidth);

            return true; // 实体已更新，需要重绘
        }

        private static double ResolveHookSegmentWidth(AcDbPolyline polyline, int sourceSegmentIndex)
        {
            if (polyline == null || sourceSegmentIndex < 0)
                return 0;

            double constantWidth = polyline.ConstantWidth;
            if (constantWidth > 0)
                return constantWidth;

            double endWidth = polyline.GetEndWidthAt(sourceSegmentIndex);
            if (endWidth > 0)
                return endWidth;

            double startWidth = polyline.GetStartWidthAt(sourceSegmentIndex);
            if (startWidth > 0)
                return startWidth;

            return 0;
        }
    }
}

