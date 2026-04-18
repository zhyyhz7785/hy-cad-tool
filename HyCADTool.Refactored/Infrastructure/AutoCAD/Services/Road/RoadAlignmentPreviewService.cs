using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcDb = Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 把 Domain <see cref="Polyline3D"/> 投影到 AutoCAD Transient 图层的预览服务。
    ///
    /// 设计约束（参见 skill: hycad-autocad-singleton-database-context）：
    /// - <b>不缓存</b> Document / Database：每次 <see cref="Update"/> / <see cref="Clear"/>
    ///   时重新解析 <c>MdiActiveDocument</c>；
    /// - 对外可见的只有 <see cref="Update"/> / <see cref="Clear"/> / <see cref="Dispose"/>；
    /// - 预览只使用 Transient Graphics，不入 ModelSpace，也不开启事务；
    /// - 单实例生命周期被调用方（命令层）约束，一次编辑会话对应一个实例。
    ///
    /// 用法示例（命令层）：
    /// <code>
    /// using (var preview = new RoadAlignmentPreviewService())
    /// {
    ///     vm.PreviewRequested += (_, r) => preview.Update(r.Polyline);
    ///     AcApp.ShowModalWindow(win);
    /// } // 这里会自动 Clear
    /// </code>
    /// </summary>
    public sealed class RoadAlignmentPreviewService : IDisposable
    {
        /// <summary>预览线的 AutoCAD 颜色索引（255 色）。默认偏亮的黄色，在暗色背景上显眼。</summary>
        public short ColorIndex { get; set; } = 2; // AutoCAD Yellow

        /// <summary>Transient 绘制模式。DirectTopmost 置顶；不被 Zoom 擦除。</summary>
        public TransientDrawingMode DrawingMode { get; set; } = TransientDrawingMode.DirectTopmost;

        private readonly List<Drawable> _drawables = new List<Drawable>();
        private bool _disposed;

        // 记录发起本次预览会话的 Document 标识：
        // 若更新时发现活动文档换了，则先 Clear 再按新文档画；避免把旧 Drawable 误 Erase 到新文档。
        // 注意：TransientManager 是进程级的，Drawable 本身不绑文档；但为了语义清晰仍做此守护。
        private object _originDocId;

        /// <summary>
        /// 用新的 <paramref name="polyline"/> 替换当前预览。传入 null 或空折线时等价于 <see cref="Clear"/>。
        /// </summary>
        public void Update(Polyline3D polyline)
        {
            if (_disposed) return;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                Clear();
                return;
            }

            // 文档切换防御：如果当前活动文档与首次绘制时不一致，直接清掉老 Drawable。
            if (_originDocId != null && !ReferenceEquals(_originDocId, doc))
            {
                Clear();
            }
            _originDocId = doc;

            Clear();

            if (polyline == null || polyline.VertexCount < 2) return;

            // 复用 Bridge：Domain Polyline3D → AutoCAD Polyline（未入库，直接作为 Drawable 用）。
            // ToAutoCadPolyline 会保留 bulges，Transient 自然按弧段渲染。
            // AcDb.Polyline 既是数据库实体也是 Drawable，TransientManager 可直接接受。
            AcDb.Polyline acPoly = null;
            try
            {
                acPoly = RoadGeometryBridge.ToAutoCadPolyline(polyline);
                acPoly.ColorIndex = ColorIndex;

                TransientManager.CurrentTransientManager.AddTransient(
                    acPoly,
                    DrawingMode,
                    128,
                    new IntegerCollection());

                _drawables.Add(acPoly);
                acPoly = null; // 所有权已转移到 _drawables，避免 finally 重复 Dispose
            }
            finally
            {
                acPoly?.Dispose();
            }

            // 刷新屏幕，让瞬态图形立即可见
            try { doc.Editor.UpdateScreen(); }
            catch { /* 某些状态下 Editor 不可用，忽略 */ }
        }

        /// <summary>清除当前所有预览 Drawable（幂等、异常安全）。</summary>
        public void Clear()
        {
            if (_drawables.Count == 0) return;

            var tm = TransientManager.CurrentTransientManager;
            foreach (var d in _drawables)
            {
                try { tm.EraseTransient(d, new IntegerCollection()); }
                catch { /* Drawable 已被 AutoCAD 回收/文档关闭 */ }
                try { d.Dispose(); }
                catch { /* 同上 */ }
            }
            _drawables.Clear();

            try { AcApp.DocumentManager.MdiActiveDocument?.Editor.UpdateScreen(); }
            catch { /* UpdateScreen 失败不影响正确性 */ }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Clear();
            GC.SuppressFinalize(this);
        }

        ~RoadAlignmentPreviewService() { Dispose(); }
    }
}
