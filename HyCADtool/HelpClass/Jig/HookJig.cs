using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
namespace HyCADTool.HelpClass.Jig
{
    public class HookJig : EntityJig
    {
        private Polyline polyline;
        private Point3d lastVertex;
        private Point3d jigPoint;
        private int numVertices;
        private double hookLength;
        private double angle;
        private bool isCompleted = false; // 用于判断操作是否完成
        private Vector3d segDirection;
        private bool isVertical;
        public HookJig(Polyline polyline, double hookLength, bool isVertical) : base(polyline)
        {
            this.hookLength = hookLength;
            this.polyline = polyline;
            this.isVertical = isVertical;
            numVertices = polyline.NumberOfVertices;
            if (numVertices < 2)
                throw new InvalidOperationException("Polyline 的顶点数量不足，无法计算方向！");
            lastVertex = polyline.GetPoint3dAt(numVertices - 1);
            jigPoint = lastVertex; // 初始化 jigPoint
                                   // 计算最后一段的方向向量
            segDirection = polyline.GetPoint3dAt(numVertices - 2).GetVectorTo(lastVertex).GetNormal();
        }
        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            // 如果操作已完成，返回 Cancel，结束 Jig
            if (isCompleted)
            {
                return SamplerStatus.Cancel;
            }
            // 设置交互提示信息
            JigPromptPointOptions jigOpts = new JigPromptPointOptions("\n移动光标以选择弯钩方向，点击确认: ");
            jigOpts.BasePoint = lastVertex;
            jigOpts.UseBasePoint = true;
            jigOpts.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NoNegativeResponseAccepted;
            // 获取用户输入
            PromptPointResult ppr = prompts.AcquirePoint(jigOpts);
            // 如果获取到有效的点
            if (ppr.Status == PromptStatus.OK)
            {
                // 如果鼠标位置没有变化，返回 NoChange
                if (jigPoint.DistanceTo(ppr.Value) < Tolerance.Global.EqualPoint)
                {
                    return SamplerStatus.NoChange;
                }
                else
                {
                    // 更新目标点（jigPoint）
                    jigPoint = ppr.Value;
                    return SamplerStatus.OK;
                }
            }
            else if (ppr.Status == PromptStatus.None)
            {
                // 用户点击确认，结束操作
                isCompleted = true;
                return SamplerStatus.Cancel;
            }
            else
            {
                return SamplerStatus.Cancel; // 用户取消操作
            }
        }
        protected override bool Update()
        {
            // 清除之前添加的弯钩顶点
            while (polyline.NumberOfVertices > numVertices)
            {
                polyline.RemoveVertexAt(polyline.NumberOfVertices - 1);
            }
            // 计算方向和弯钩点
            Vector3d dir = lastVertex.GetVectorTo(jigPoint);
            if (dir.Length == 0)
                return false;
            // 计算叉积以确定方向
            double cross = segDirection.CrossProduct(dir).Z;
            // 根据交叉方向选择弯钩角度
            if (cross < 0)
            {
                if (isVertical)
                {
                    angle = Math.PI * 6 / 4; // 45 度
                }
                else
                {
                    angle = Math.PI * 5 / 4; // 45 度
                }
            }
            else
            {
                if (isVertical)
                {
                    angle = Math.PI * 2 / 4; // -45 度}
                }
                else
                {
                    angle = Math.PI * 3 / 4; // -45 度}
                }
            }
            // 计算弯钩方向并添加顶点
            Vector3d hookDirection = segDirection.RotateBy(angle, Vector3d.ZAxis);
            Point3d hookPoint = lastVertex + hookDirection * hookLength;
            polyline.AddVertexAt(numVertices, new Point2d(hookPoint.X, hookPoint.Y), 0, 0, 0);
            return true; // 实体已更新，需要重绘
        }
    }
}
