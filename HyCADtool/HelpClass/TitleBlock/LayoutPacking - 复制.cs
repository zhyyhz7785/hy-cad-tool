using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass.TitleBlock;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        private class ViewportInfo
        {
            public ObjectId Id { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public Point3d Center { get; set; }
            public double MinX { get; set; }
            public int SequenceNumber { get; set; }
            public Point2d NewPosition { get; set; }
        }
        private class Rectangle
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }
        private class EmptySpace
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public double Area => Width * Height;
        }
        private class RectanglePacker
        {
            private readonly double _containerWidth;
            private readonly double _containerHeight;
            private readonly List<Rectangle> _placedRectangles;
            private readonly List<EmptySpace> _emptySpaces;
            public RectanglePacker(double containerWidth, double containerHeight)
            {
                _containerWidth = containerWidth;
                _containerHeight = containerHeight;
                _placedRectangles = new List<Rectangle>();
                _emptySpaces = new List<EmptySpace>
                {
                    new EmptySpace { X = 0, Y = 0, Width = containerWidth, Height = containerHeight }
                };
            }
            public bool TryPack(double width, double height, out Point2d position)
            {
                position = new Point2d(0, 0);
                // 按照从上到下（Y坐标从小到大），从左到右（X坐标从小到大）查找合适的位置
                var orderedSpaces = _emptySpaces
                    .OrderBy(s => s.Y)
                    .ThenBy(s => s.X)
                    .ToList();
                foreach (var space in orderedSpaces)
                {
                    if (space.Width >= width && space.Height >= height)
                    {
                        // 找到合适的空间，放置矩形
                        position = new Point2d(space.X, space.Y);
                        // 添加已放置矩形
                        _placedRectangles.Add(new Rectangle
                        {
                            X = space.X,
                            Y = space.Y,
                            Width = width,
                            Height = height
                        });
                        // 更新空间
                        _emptySpaces.Remove(space);
                        // 分割空间：右侧空间
                        if (space.Width > width)
                        {
                            _emptySpaces.Add(new EmptySpace
                            {
                                X = space.X + width,
                                Y = space.Y,
                                Width = space.Width - width,
                                Height = space.Height
                            });
                        }
                        // 分割空间：底部空间
                        if (space.Height > height)
                        {
                            _emptySpaces.Add(new EmptySpace
                            {
                                X = space.X,
                                Y = space.Y + height,
                                Width = width,
                                Height = space.Height - height
                            });
                        }
                        // 优化：合并相邻空间
                        OptimizeEmptySpaces();
                        return true;
                    }
                }
                return false;
            }
            public List<EmptySpace> GetEmptySpaces()
            {
                return _emptySpaces.Where(s => s.Area > 0.01).ToList();
            }
            private void OptimizeEmptySpaces()
            {
                // 简单实现：移除面积过小的空间
                _emptySpaces.RemoveAll(s => s.Width < 0.1 || s.Height < 0.1);
            }
        }
        [CommandMethod("HY_PackViewports")]
        public static void PackViewports()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    // 询问用户选择图框类型
                    string frameType = GetFrameType(ed);
                    if (string.IsNullOrEmpty(frameType))
                    {
                        ed.WriteMessage("\n命令已取消。");
                        trans.Abort();
                        return;
                    }
                    // 获取图框信息
                    ITitleBlock titleBlock = TitleBlockFactory.Create(frameType);
                    double frameWidth = titleBlock.Width - titleBlock.LeftMargin - titleBlock.RightMargin;
                    double frameHeight = titleBlock.Height - titleBlock.TopMargin - titleBlock.BottomMargin;
                    ed.WriteMessage($"\n图框内部尺寸: {frameWidth} x {frameHeight}");
                    // 创建矩形打包器
                    RectanglePacker packer = new RectanglePacker(frameWidth, frameHeight);
                    // 获取所有视口
                    List<ViewportInfo> viewports = GetViewportsFromLayer(db, trans, "00_hy_2公共_视口");
                    if (viewports.Count == 0)
                    {
                        ed.WriteMessage("\n未找到指定图层上的视口。");
                        trans.Abort();
                        return;
                    }
                    ed.WriteMessage($"\n找到 {viewports.Count} 个视口");
                    // 按X坐标排序并分配序列号
                    viewports = viewports.OrderBy(v => v.MinX).ToList();
                    for (int i = 0; i < viewports.Count; i++)
                    {
                        viewports[i].SequenceNumber = i + 1;
                    }
                    // 第一轮：按序列顺序放置视口
                    List<ViewportInfo> unplacedViewports = new List<ViewportInfo>();
                    foreach (var viewport in viewports)
                    {
                        if (packer.TryPack(viewport.Width, viewport.Height, out Point2d position))
                        {
                            viewport.NewPosition = position;
                            ed.WriteMessage($"\n视口 {viewport.SequenceNumber} 放置在 ({position.X}, {position.Y})");
                        }
                        else
                        {
                            unplacedViewports.Add(viewport);
                            ed.WriteMessage($"\n视口 {viewport.SequenceNumber} 无法放置");
                        }
                    }
                    // 询问用户是否填充空闲空间
                    if (unplacedViewports.Count > 0)
                    {
                        PromptKeywordOptions kwordOpts = new PromptKeywordOptions("\n是否尝试填充空闲空间？");
                        kwordOpts.Keywords.Add("是");
                        kwordOpts.Keywords.Add("否");
                       // kwordOpts.DefaultValue = "是";
                        PromptResult kwordRes = ed.GetKeywords(kwordOpts);
                        if (kwordRes.Status == PromptStatus.OK && kwordRes.StringResult == "是")
                        {
                            // 第二轮：尝试填充空白区域
                            var emptySpaces = packer.GetEmptySpaces();
                            foreach (var space in emptySpaces.Where(s => s.Area > 100)) // 最小阈值，可调整
                            {
                                ViewportInfo bestFit = null;
                                double bestFitScore = 0;
                                foreach (var viewport in unplacedViewports)
                                {
                                    if (viewport.Width <= space.Width && viewport.Height <= space.Height)
                                    {
                                        // 计算适应度分数（面积比率）
                                        double fitScore = (viewport.Width * viewport.Height) / (space.Width * space.Height);
                                        if (fitScore > bestFitScore)
                                        {
                                            bestFitScore = fitScore;
                                            bestFit = viewport;
                                        }
                                    }
                                }
                                if (bestFit != null)
                                {
                                    bestFit.NewPosition = new Point2d(space.X, space.Y);
                                    unplacedViewports.Remove(bestFit);
                                    ed.WriteMessage($"\n空白区域填充：视口 {bestFit.SequenceNumber} 放置在 ({space.X}, {space.Y})");
                                }
                            }
                        }
                    }
                    // 应用视口位置
                    ApplyViewportPositions(db, trans, viewports.Where(v => v.NewPosition != null).ToList(), titleBlock);
                    ed.WriteMessage($"\n成功放置 {viewports.Count - unplacedViewports.Count} 个视口");
                    if (unplacedViewports.Count > 0)
                    {
                        ed.WriteMessage($"\n{unplacedViewports.Count} 个视口无法放置（太大或图框空间不足）");
                    }
                    trans.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }
        private static string GetFrameType(Editor ed)
        {
            PromptKeywordOptions opts = new PromptKeywordOptions("\n请选择图框类型:");
            opts.Keywords.Add("A0");
            opts.Keywords.Add("A1");
            opts.Keywords.Add("A2");
            opts.Keywords.Add("A3");
            opts.Keywords.Add("A4");
            //opts.DefaultValue = "A1";
            opts.AllowNone = false;
            PromptResult res = ed.GetKeywords(opts);
            return res.Status == PromptStatus.OK ? res.StringResult : string.Empty;
        }
        private static List<ViewportInfo> GetViewportsFromLayer(Database db, Transaction trans, string layerName)
        {
            List<ViewportInfo> viewports = new List<ViewportInfo>();
            // 获取当前空间
            BlockTableRecord paperSpace = trans.GetObject(db.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;
            if (paperSpace != null)
            {
                foreach (ObjectId objectId in paperSpace)
                {
                    if (objectId.ObjectClass.Name == "AcDbViewport")
                    {
                        Viewport viewport = trans.GetObject(objectId, OpenMode.ForRead) as Viewport;
                        if (viewport != null && !viewport.IsErased && viewport.Layer == layerName)
                        {
                            // 排除默认视口 (viewport 1)
                            if (viewport.Number != 1)
                            {
                                // 获取视口的几何信息
                                double width = viewport.Width;
                                double height = viewport.Height;
                                Point3d center = viewport.CenterPoint;
                                double minX = center.X - width / 2;
                                viewports.Add(new ViewportInfo
                                {
                                    Id = objectId,
                                    Width = width,
                                    Height = height,
                                    Center = center,
                                    MinX = minX
                                });
                            }
                        }
                    }
                }
            }
            return viewports;
        }
        private static void ApplyViewportPositions(Database db, Transaction trans, List<ViewportInfo> viewports, ITitleBlock titleBlock)
        {
            try
            {
                foreach (var viewportInfo in viewports)
                {
                    Viewport viewport = trans.GetObject(viewportInfo.Id, OpenMode.ForWrite) as Viewport;
                    if (viewport != null)
                    {
                        // 计算新中心点（相对于图框内部空间）
                        Point3d newCenter = new Point3d(
                            titleBlock.LeftMargin + viewportInfo.NewPosition.X + viewportInfo.Width / 2,
                            titleBlock.BottomMargin + viewportInfo.NewPosition.Y + viewportInfo.Height / 2,
                            0
                        );
                        viewport.CenterPoint = newCenter;
                    }
                }
            }
            catch (System.Exception ex)
            {
                throw new System.Exception($"应用视口位置时出错: {ex.Message}");
            }
        }
    }
}