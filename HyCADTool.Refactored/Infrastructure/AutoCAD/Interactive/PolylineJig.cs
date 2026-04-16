using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcDbPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
using AcGiGeometry = Autodesk.AutoCAD.GraphicsInterface.Geometry;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interactive
{
    /// <summary>
    /// 多段线交互式绘制 Jig（Polyline Interactive Drawing Jig）
    /// 功能：实时预览偏移后的多段线，支持撤销（Z 关键字）
    /// </summary>
    public class PolylineJig : DrawJig
    {
        private readonly AcDbPolyline _polyline;
        private readonly Point3dCollection _points;
        private Point3d _currentPoint;
        private readonly double _offsetDistance;

        /// <summary>
        /// 获取绘制的点集合
        /// </summary>
        public Point3dCollection Points => _points;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="offsetDistance">偏移距离（Offset Distance）</param>
        public PolylineJig(double offsetDistance)
        {
            _polyline = new AcDbPolyline();
            _points = new Point3dCollection();
            _offsetDistance = offsetDistance;
        }

        /// <summary>
        /// 绘制预览（Draw Preview）
        /// </summary>
        protected override bool WorldDraw(WorldDraw draw)
        {
            AcGiGeometry geometry = draw.Geometry;
            if (geometry != null)
            {
                // 清空多段线的顶点
                _polyline.SetDatabaseDefaults();
                _polyline.Reset(true, 0);

                // 添加已捕获的点
                for (int i = 0; i < _points.Count; i++)
                {
                    _polyline.AddVertexAt(i, new Point2d(_points[i].X, _points[i].Y), 0, 0, 0);
                }

                // 添加当前点
                if (_currentPoint != null)
                {
                    _polyline.AddVertexAt(_points.Count, new Point2d(_currentPoint.X, _currentPoint.Y), 0, 0, 0);
                }

                // 生成偏移曲线并绘制
                try
                {
                    DBObjectCollection offsetCurves = _polyline.GetOffsetCurves(_offsetDistance);
                    foreach (Entity ent in offsetCurves)
                    {
                        geometry.Draw(ent);
                        ent.Dispose(); // 释放资源
                    }
                }
                catch (System.Exception)
                {
                    // 忽略偏移失败（例如点太少时）
                    geometry.Draw(_polyline);
                }
            }
            return true;
        }

        /// <summary>
        /// 采样器（Sampler）- 处理用户输入
        /// </summary>
        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions options = new JigPromptPointOptions("\n指定下一点或:");
            options.Keywords.Add("撤销");
            options.Keywords.Add("Z");
            options.AppendKeywordsToMessage = true;
            options.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted;

            PromptPointResult result = prompts.AcquirePoint(options);

            if (result.Status == PromptStatus.OK)
            {
                if (result.Value == _currentPoint)
                {
                    return SamplerStatus.NoChange;
                }
                else
                {
                    _currentPoint = result.Value;
                    return SamplerStatus.OK;
                }
            }
            else if (result.Status == PromptStatus.Keyword)
            {
                string keyword = result.StringResult;
                if (keyword.Equals("撤销", StringComparison.OrdinalIgnoreCase) || keyword.Equals("Z", StringComparison.OrdinalIgnoreCase))
                {
                    if (_points.Count > 0)
                    {
                        _points.RemoveAt(_points.Count - 1); // 撤销最后一个点
                        return SamplerStatus.OK; // 更新绘制
                    }
                    else
                    {
                        AcApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n没有点可撤销。");
                        return SamplerStatus.NoChange;
                    }
                }
            }
            else if (result.Status == PromptStatus.None)
            {
                // 用户按下 Enter，结束输入
                return SamplerStatus.Cancel;
            }

            return SamplerStatus.Cancel;
        }

        /// <summary>
        /// 启动 Jig 交互（Start Jig Interaction）
        /// </summary>
        /// <returns>用户操作状态</returns>
        public PromptStatus StartJig()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument.Editor;

            while (true)
            {
                PromptResult res = ed.Drag(this);

                if (res.Status == PromptStatus.OK)
                {
                    _points.Add(_currentPoint);
                }
                else if (res.Status == PromptStatus.Keyword)
                {
                    // 处理关键字后继续循环
                    continue;
                }
                else if (res.Status == PromptStatus.None)
                {
                    // 用户按下 Enter，结束输入
                    break;
                }
                else if (res.Status == PromptStatus.Cancel)
                {
                    return PromptStatus.Cancel;
                }
                else
                {
                    break;
                }
            }

            return PromptStatus.OK;
        }
    }
}

