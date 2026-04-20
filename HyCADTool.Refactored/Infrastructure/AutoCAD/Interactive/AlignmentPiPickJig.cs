using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcDbPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interactive
{
    /// <summary>
    /// 导线法（hyRoadAlnByPi）连续点取 PI：已定点之间画实线，末点至光标为橡皮筋（虚线由 JigPromptPointOptions 提供）。
    /// 支持 <c>撤销/Z</c> 回退上一确认点（至少保留起点）。
    /// </summary>
    public sealed class AlignmentPiPickJig : DrawJig
    {
        private readonly List<Point3d> _points;
        private Point3d _cursor;
        private bool _hasCursorSample;

        /// <param name="firstPoint">已通过 <see cref="Editor.GetPoint"/> 取得的 PI 起点。</param>
        public AlignmentPiPickJig(Point3d firstPoint)
        {
            _points = new List<Point3d> { firstPoint };
            _cursor = firstPoint;
            _hasCursorSample = false;
        }

        /// <summary>含起点在内的全部 PI 点（世界坐标）。</summary>
        public IReadOnlyList<Point3d> Points => _points;

        /// <summary>循环 <see cref="Editor.Drag"/> 直至用户回车结束或取消。</summary>
        public PromptStatus Run(Editor ed)
        {
            while (true)
            {
                PromptResult res = ed.Drag(this);
                if (res.Status == PromptStatus.OK)
                {
                    _points.Add(_cursor);
                }
                else if (res.Status == PromptStatus.Keyword)
                {
                    continue;
                }
                else if (res.Status == PromptStatus.None)
                {
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

        protected override bool WorldDraw(WorldDraw draw)
        {
            var geometry = draw.Geometry;
            if (geometry == null) return true;

            using (var pl = new AcDbPolyline())
            {
                pl.SetDatabaseDefaults();
                pl.Reset(true, 0);
                int n = _points.Count;
                for (int i = 0; i < n; i++)
                    pl.AddVertexAt(i, new Point2d(_points[i].X, _points[i].Y), 0, 0, 0);

                if (_hasCursorSample && n > 0)
                    pl.AddVertexAt(n, new Point2d(_cursor.X, _cursor.Y), 0, 0, 0);

                geometry.Draw(pl);
            }

            return true;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var opts = new JigPromptPointOptions(
                $"\n[道路] 指定下一 PI 点（已 {_points.Count} 个，回车结束）[撤销(Z)]：")
            {
                UseBasePoint = true,
                BasePoint = _points[_points.Count - 1],
            };
            opts.Keywords.Add("撤销");
            opts.Keywords.Add("Z");
            opts.AppendKeywordsToMessage = true;
            opts.UserInputControls =
                UserInputControls.Accept3dCoordinates | UserInputControls.NullResponseAccepted;

            PromptPointResult result = prompts.AcquirePoint(opts);

            if (result.Status == PromptStatus.OK)
            {
                if (_hasCursorSample &&
                    result.Value.IsEqualTo(_cursor, new Tolerance(1e-10, 1e-10)))
                    return SamplerStatus.NoChange;

                _cursor = result.Value;
                _hasCursorSample = true;
                return SamplerStatus.OK;
            }

            if (result.Status == PromptStatus.Keyword)
            {
                string kw = result.StringResult;
                if (string.Equals(kw, "撤销", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kw, "Z", StringComparison.OrdinalIgnoreCase))
                {
                    if (_points.Count > 1)
                    {
                        _points.RemoveAt(_points.Count - 1);
                        _hasCursorSample = false;
                        return SamplerStatus.OK;
                    }

                    AcApp.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n[道路] 仅剩起点，无法撤销。");
                    return SamplerStatus.NoChange;
                }
            }
            else if (result.Status == PromptStatus.None)
            {
                return SamplerStatus.Cancel;
            }

            return SamplerStatus.Cancel;
        }
    }
}
