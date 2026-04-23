using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Drawing;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Workflows.Road
{
    /// <summary>
        /// 「向 CAD 绘制横断面」的 AutoCAD 交互器：
        /// 提供 <see cref="PromptInsertionPoint"/>（Editor.GetPoint）与 <see cref="DrawAt"/>（LockDocument + Transaction + Clear+Draw）两步工具。
    ///
    /// <para>本类有意不持有任何"上次 origin"状态，由 View 层根据会话语义缓存（首次 Prompt，之后复用）。
    /// 这样解耦避免了跨文档切换时残留插入点的问题。</para>
    ///
    /// <para>与 <see cref="RoadCsPickGeometryInteractor"/> 一样，本类是 View → AutoCAD 的窄桥，
    /// 绝不反向回调 ViewModel；ViewModel 只通过 <see cref="CrossSectionDrawViewModel.DrawStructureLinesRequested"/>
    /// 把业务数据（<see cref="CrossSectionDesignerResult"/>）交给 View，View 再调用本类完成 AutoCAD 侧副作用。</para>
    /// </summary>
    public static class RoadCsDrawSingleLineInteractor
    {
        /// <summary>
        /// 在命令行让用户拾取一个插入点（WCS 二维）。
        /// 用户按 Esc / 回车未输入时返回 null（并在命令行打印"已取消"）。
        /// </summary>
        /// <remarks>
        /// 调用者应在调用前 <c>window.Hide()</c>，否则模态 WPF 窗口会挡住 AutoCAD 视口，
        /// 用户看不到命令行提示也无法捕捉鼠标事件。
        /// </remarks>
        public static Point2d? PromptInsertionPoint()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;

            var opt = new PromptPointOptions("\n[道路] 指定横断面插入点：")
            {
                AllowNone = false,
            };
            var res = doc.Editor.GetPoint(opt);
            if (res.Status != PromptStatus.OK)
            {
                doc.Editor.WriteMessage("\n[道路] 已取消。");
                return null;
            }
            return new Point2d(res.Value.X, res.Value.Y);
        }

        /// <summary>
        /// 在 <paramref name="origin"/> 位置，以 <see cref="CrossSectionDrawMode.SingleLine"/> 模式绘制
        /// <paramref name="result"/> 的横断面结构线；绘制前先按 Template.Id 清除上一次画过的同一模板实体。
        /// </summary>
        /// <remarks>
        /// 此方法包装 <c>doc.LockDocument() + Transaction</c>，在调用时会占用 AutoCAD 文档锁，
        /// 期间 WPF 窗口仍可保持可见（不会因为锁冲突而崩溃 / 阻塞），符合"反复调整即时重绘"的交互预期。
        /// </remarks>
        public static DrawResult DrawAt(
            CrossSectionDesignerResult result,
            Point2d origin,
            CrossSectionAnnotationStyle annotationStyle = null,
            DrawingSheetTitleSpec sheetTitleSpec = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return DrawResult.Failed("无活动 AutoCAD 文档。");

            var drawService = ServiceLocator.Resolve<RoadStandardSectionDrawService>();

            int erased;
            int created;
            try
            {
                using (doc.LockDocument())
                using (var tr = doc.Database.TransactionManager.StartTransaction())
                {
                    erased = drawService.Clear(tr, doc.Database, result.Template.Id);
                    created = drawService.Draw(
                        tr,
                        doc.Database,
                        result.Figure,
                        result.Template,
                        origin,
                        modelUnitPerMeter: 1.0,
                        mode: CrossSectionDrawMode.TopSurfaceWithAnnotation,
                        layout: result.Layout,
                        annotationStyle: annotationStyle,
                        sheetTitleSpec: sheetTitleSpec);
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage($"\n[道路] 横断面绘制失败：{ex.Message}");
                return DrawResult.Failed(ex.Message);
            }

            doc.Editor.WriteMessage(
                $"\n[道路] 横断面已更新（擦除 {erased} / 生成 {created}）@ ({origin.X:F2}, {origin.Y:F2})。");
            return DrawResult.Success(origin, erased, created);
        }

        /// <summary>View 层按需判断是否成功并向用户反馈的简化结果。</summary>
        public sealed class DrawResult
        {
            public bool Ok { get; }
            public Point2d Origin { get; }
            public int Erased { get; }
            public int Created { get; }
            public string Error { get; }

            private DrawResult(bool ok, Point2d origin, int erased, int created, string error)
            {
                Ok = ok;
                Origin = origin;
                Erased = erased;
                Created = created;
                Error = error ?? string.Empty;
            }

            public static DrawResult Success(Point2d origin, int erased, int created)
                => new DrawResult(true, origin, erased, created, null);

            public static DrawResult Failed(string error)
                => new DrawResult(false, Point2d.Origin, 0, 0, error ?? "未知错误");
        }
    }
}
