using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcDb = Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Shared.AutoCAD.Services.Road;

namespace HyCADTool.Features.Road.PlanAlignment.Services
{
    /// <summary>
    /// 路线工作台三态瞬态图形服务（AutoCAD Transient Graphics）：
    /// <list type="bullet">
    ///   <item><b>Outline</b>（黄）— 平面线位列表当前行，整条中心线；<b>只要有选中线位就恒显示</b>；</item>
    ///   <item><b>PI Point</b>（绿）— PI 列表当前内部行，交点处小圆；直径 = 当前视图高度的 <see cref="PiScreenRatio"/>（默认 5%），
    ///         随 AutoCAD ZOOM/PAN/VIEW/REGEN 命令结束自动重绘；</item>
    ///   <item><b>Segment Line</b>（蓝）— 分段表当前行，段起终点弦线。</item>
    /// </list>
    ///
    /// <para>三态独立三批 Drawable，互不干扰；可同时存在（黄线 + 绿点 + 蓝线）。</para>
    ///
    /// 设计约束（参见 skill: hycad-autocad-singleton-database-context）：
    /// - <b>不缓存</b> Document / Database：每次 Render 时重新解析 <c>MdiActiveDocument</c>；
    /// - 预览只使用 Transient Graphics，不入 ModelSpace，也不开启事务；
    /// - 单实例生命周期由调用方约束，一次编辑会话对应一个实例。
    /// </summary>
    public sealed class RoadAlignmentPreviewService : IDisposable
    {
        /// <summary>路线轮廓（列表选中线位）— ACI 黄。</summary>
        public const short ColorAlignmentOutline = 2;

        /// <summary>PI 交点（PI 列表选中）— ACI 绿。</summary>
        public const short ColorPiPoint = 3;

        /// <summary>分段（分段表选中）— ACI 蓝。</summary>
        public const short ColorSegmentLine = 5;

        /// <summary>
        /// Outline 颜色索引（ACI 0-256）。默认 <see cref="ColorAlignmentOutline"/>=黄；
        /// 老调用方（<c>RoadExtractCommand</c> / <c>RoadAlignmentTableCommand</c>）可以设置其他颜色做一次性高亮，此时一般只用 <see cref="Update(Polyline3D)"/>。
        /// </summary>
        public short ColorIndex { get; set; } = ColorAlignmentOutline;

        /// <summary>PI 小圆直径占当前视图高度的比例（0.05 = 5%）。</summary>
        public double PiScreenRatio { get; set; } = 0.05;

        /// <summary>PI 小圆在无法读取视图高度时的兜底半径（图纸单位）。</summary>
        public double PiFallbackRadius { get; set; } = 2.5;

        /// <summary>Transient 绘制模式。DirectTopmost 置顶；不被 Zoom 擦除。</summary>
        public TransientDrawingMode DrawingMode { get; set; } = TransientDrawingMode.DirectTopmost;

        private readonly List<Drawable> _outlineDrawables = new List<Drawable>();
        private readonly List<Drawable> _piDrawables = new List<Drawable>();
        private readonly List<Drawable> _segmentDrawables = new List<Drawable>();

        private Polyline3D _currentOutline;
        private bool _hasPi;
        private double _piX, _piY;
        private bool _hasSegment;
        private Point2D _segStart, _segEnd;

        private Document _subscribedDoc;
        private bool _disposed;

        public RoadAlignmentPreviewService()
        {
            TrySubscribeViewEvents(AcApp.DocumentManager.MdiActiveDocument);
        }

        // =============================== 外部 API ===============================

        /// <summary>兼容旧调用：等价于仅设置 Outline（清除 PI / Segment）。</summary>
        public void Update(Polyline3D polyline) => Render(polyline, false, 0, 0, false, default, default);

        /// <summary>仅替换 Outline，保留当前 PI / Segment。</summary>
        public void ShowAlignmentOutline(Polyline3D polyline)
            => Render(polyline, _hasPi, _piX, _piY, _hasSegment, _segStart, _segEnd);

        /// <summary>仅替换 PI 小圆（绿），保留当前 Outline / Segment。</summary>
        public void ShowPiPoint(double x, double y)
            => Render(_currentOutline, true, x, y, _hasSegment, _segStart, _segEnd);

        /// <summary>仅替换 Segment 蓝线，保留当前 Outline / PI。</summary>
        public void ShowSegmentLine(Point2D start, Point2D end)
            => Render(_currentOutline, _hasPi, _piX, _piY, true, start, end);

        /// <summary>
        /// 一次性设定三态（任何一个传 null / false 表示清除该态）。
        /// 用于 VM 统一入口 <c>RefreshWorkbenchTransientVisuals</c>。
        /// </summary>
        public void Render(
            Polyline3D outline,
            (double X, double Y)? pi,
            (Point2D Start, Point2D End)? segment)
        {
            bool hasPi = pi.HasValue;
            double px = hasPi ? pi.Value.X : 0;
            double py = hasPi ? pi.Value.Y : 0;

            bool hasSeg = segment.HasValue;
            Point2D s = hasSeg ? segment.Value.Start : default;
            Point2D e = hasSeg ? segment.Value.End : default;

            Render(outline, hasPi, px, py, hasSeg, s, e);
        }

        private void Render(
            Polyline3D outline,
            bool hasPi, double piX, double piY,
            bool hasSeg, Point2D segStart, Point2D segEnd)
        {
            if (_disposed) return;

            _currentOutline = outline;
            _hasPi = hasPi;
            _piX = piX;
            _piY = piY;
            _hasSegment = hasSeg;
            _segStart = segStart;
            _segEnd = segEnd;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                ClearBucket(_outlineDrawables);
                ClearBucket(_piDrawables);
                ClearBucket(_segmentDrawables);
                return;
            }
            TrySubscribeViewEvents(doc);

            RenderOutline(doc);
            RenderPi(doc);
            RenderSegment(doc);
            UpdateScreenSafe(doc);
        }

        /// <summary>清除全部三态。</summary>
        public void Clear()
        {
            if (_disposed) return;
            _currentOutline = null;
            _hasPi = false;
            _hasSegment = false;

            ClearBucket(_outlineDrawables);
            ClearBucket(_piDrawables);
            ClearBucket(_segmentDrawables);

            try { AcApp.DocumentManager.MdiActiveDocument?.Editor.UpdateScreen(); }
            catch { /* ignore */ }
        }

        // =============================== 内部：三态各自 Render ===============================

        private void RenderOutline(Document doc)
        {
            ClearBucket(_outlineDrawables);
            if (_currentOutline == null || _currentOutline.VertexCount < 2) return;

            AcDb.Polyline acPoly = null;
            try
            {
                acPoly = RoadGeometryBridge.ToAutoCadPolyline(_currentOutline);
                acPoly.ColorIndex = ColorIndex;
                AddTransient(_outlineDrawables, acPoly);
                acPoly = null;
            }
            finally
            {
                acPoly?.Dispose();
            }
        }

        private void RenderPi(Document doc)
        {
            ClearBucket(_piDrawables);
            if (!_hasPi) return;

            double r = ComputePiRadius(doc);
            AcDb.Circle c = null;
            try
            {
                c = new AcDb.Circle(new Point3d(_piX, _piY, 0), Vector3d.ZAxis, r);
                c.ColorIndex = ColorPiPoint;
                AddTransient(_piDrawables, c);
                c = null;
            }
            finally
            {
                c?.Dispose();
            }
        }

        private void RenderSegment(Document doc)
        {
            ClearBucket(_segmentDrawables);
            if (!_hasSegment) return;

            AcDb.Polyline pl = null;
            try
            {
                pl = new AcDb.Polyline(2);
                pl.AddVertexAt(0, new Point2d(_segStart.X, _segStart.Y), 0, 0, 0);
                pl.AddVertexAt(1, new Point2d(_segEnd.X, _segEnd.Y), 0, 0, 0);
                pl.ColorIndex = ColorSegmentLine;
                AddTransient(_segmentDrawables, pl);
                pl = null;
            }
            finally
            {
                pl?.Dispose();
            }
        }

        /// <summary>
        /// 读当前 ViewPort 的高度（图纸单位）→ 直径 = h × <see cref="PiScreenRatio"/> → 半径 = 直径 / 2。
        /// 读不到时退回 <see cref="PiFallbackRadius"/>。
        /// </summary>
        private double ComputePiRadius(Document doc)
        {
            try
            {
                using (var view = doc.Editor.GetCurrentView())
                {
                    double h = view?.Height ?? 0;
                    if (h > 1e-9)
                    {
                        double r = h * PiScreenRatio * 0.5;
                        if (r > 1e-9) return r;
                    }
                }
            }
            catch { /* GetCurrentView 在某些状态下抛异常 */ }
            return PiFallbackRadius;
        }

        // =============================== 内部：Transient 绘制 / 清除 ===============================

        private void AddTransient(List<Drawable> bucket, AcDb.Entity ent)
        {
            TransientManager.CurrentTransientManager.AddTransient(
                ent,
                DrawingMode,
                128,
                new IntegerCollection());
            bucket.Add(ent);
        }

        private static void ClearBucket(List<Drawable> bucket)
        {
            if (bucket.Count == 0) return;
            var tm = TransientManager.CurrentTransientManager;
            foreach (var d in bucket)
            {
                try { tm.EraseTransient(d, new IntegerCollection()); }
                catch { /* 已被回收 */ }
                try { d.Dispose(); }
                catch { /* 同上 */ }
            }
            bucket.Clear();
        }

        private static void UpdateScreenSafe(Document doc)
        {
            try { doc.Editor.UpdateScreen(); }
            catch { /* ignore */ }
        }

        // =============================== 视图变化订阅（PI 缩放跟随） ===============================

        private void TrySubscribeViewEvents(Document doc)
        {
            if (doc == null || _disposed) return;
            if (ReferenceEquals(_subscribedDoc, doc)) return;

            UnsubscribeViewEvents();

            try
            {
                doc.CommandEnded += OnDocumentCommandEnded;
                _subscribedDoc = doc;
            }
            catch { /* ignore */ }
        }

        private void UnsubscribeViewEvents()
        {
            if (_subscribedDoc == null) return;
            try { _subscribedDoc.CommandEnded -= OnDocumentCommandEnded; }
            catch { /* ignore */ }
            _subscribedDoc = null;
        }

        /// <summary>
        /// AutoCAD 命令结束事件：ZOOM / PAN / VIEW / REGEN / 3DORBIT 等会改变视图比例，
        /// 收到则重绘 PI 小圆以保持「直径 = 视图高度 × PiScreenRatio」。
        /// Outline / Segment 用图纸坐标，不受缩放影响，但一并 Render 开销极低。
        /// </summary>
        private void OnDocumentCommandEnded(object sender, CommandEventArgs e)
        {
            if (_disposed) return;
            if (!IsViewChangeCommand(e?.GlobalCommandName)) return;
            if (!_hasPi && _currentOutline == null && !_hasSegment) return;

            var doc = sender as Document ?? AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            try
            {
                RenderOutline(doc);
                RenderPi(doc);
                RenderSegment(doc);
                UpdateScreenSafe(doc);
            }
            catch { /* ignore — 防止事件链向上抛 */ }
        }

        private static bool IsViewChangeCommand(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim().TrimStart('_', '.', '-').ToUpperInvariant();
            switch (n)
            {
                case "ZOOM":
                case "RTZOOM":
                case "PAN":
                case "RTPAN":
                case "VIEW":
                case "-VIEW":
                case "REGEN":
                case "REGENALL":
                case "3DORBIT":
                case "3DFORBIT":
                case "3DCORBIT":
                case "NAVSWHEEL":
                case "STEERINGWHEELS":
                case "DSVIEWER":
                    return true;
                default:
                    return false;
            }
        }

        // =============================== IDisposable ===============================

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UnsubscribeViewEvents();
            Clear();
            GC.SuppressFinalize(this);
        }

        ~RoadAlignmentPreviewService() { Dispose(); }
    }
}
