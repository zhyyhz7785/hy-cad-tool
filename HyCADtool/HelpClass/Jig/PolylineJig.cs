using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using System;
namespace HyCADTool.HelpClass.Jig
{
    public class PolylineJig : DrawJig
    {
        private Autodesk.AutoCAD.DatabaseServices.Polyline _polyline = new Autodesk.AutoCAD.DatabaseServices.Polyline();
        public Point3dCollection _points = new Point3dCollection();
        private Point3d _currentPoint;
        public double _offsetDistance; // 偏移距离，可根据需要调整
        protected override bool WorldDraw(WorldDraw draw)
        {
            Geometry geometry = draw.Geometry;
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
                // 生成偏移曲线
                DBObjectCollection offsetCurves = _polyline.GetOffsetCurves(_offsetDistance);
                // 绘制偏移后的多段线
                foreach (Entity ent in offsetCurves)
                {
                    geometry.Draw(ent);
                    ent.Dispose(); // 注意释放资源
                }
            }
            return true;
        }
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
                        return SamplerStatus.OK;        // 更新绘制
                    }
                    else
                    {
                        // 无点可撤销，保持当前状态
                        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n没有点可撤销。");
                        return SamplerStatus.NoChange;
                    }
                }
            }
            else if (result.Status == PromptStatus.None)
            {
                // 用户按下 Enter，结束输入
                return SamplerStatus.Cancel;
            }
            else
            {
                return SamplerStatus.Cancel;
            }
            return SamplerStatus.Cancel;
        }
        public PromptStatus StartJig()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
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
