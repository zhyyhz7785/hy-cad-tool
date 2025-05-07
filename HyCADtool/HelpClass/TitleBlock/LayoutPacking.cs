//// RectanglePackerWrapper.cs - 替换为 MaxRects 算法实现（内嵌 RectanglePacker 结构）
//using System;
//using System.Collections.Generic;
//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;

//namespace HyCADTool.LayoutPacking
//{
//    public class PackedRectangle
//    {
//        public int Index;
//        public double Width;
//        public double Height;
//        public Point2d Position;
//        public object Tag;
//    }

//    public class PackingFrame
//    {
//        public List<PackedRectangle> Rectangles = new List<PackedRectangle>();
//        public Point2d Origin;
//    }

//    public class RectanglePackerWrapper
//    {
//        private readonly double containerWidth;
//        private readonly double containerHeight;
//        private readonly double spacing;

//        public RectanglePackerWrapper(double width, double height, double spacing = 10, double margin = 0)
//        {
//            containerWidth = width;
//            containerHeight = height;
//            this.spacing = spacing;
//        }

//        public List<PackingFrame> PackRectangles(List<LayoutViewport> viewports)
//        {
//            var frames = new List<PackingFrame>();
//            var queue = new Queue<LayoutViewport>(viewports);

//            while (queue.Count > 0)
//            {
//                var packer = new RectanglePacker(containerWidth, containerHeight);
//                var frame = new PackingFrame();

//                var retry = new Queue<LayoutViewport>();
//                while (queue.Count > 0)
//                {
//                    var vp = queue.Dequeue();
//                    var packed = packer.TryPack(vp.Width + spacing, vp.Height + spacing);
//                    if (packed != null)
//                    {
//                        frame.Rectangles.Add(new PackedRectangle
//                        {
//                            Index = vp.Index,
//                            Width = vp.Width,
//                            Height = vp.Height,
//                            Position = packed.Value,
//                            Tag = vp
//                        });
//                    }
//                    else
//                    {
//                        retry.Enqueue(vp);
//                    }
//                }
//                frames.Add(frame);
//                queue = retry;
//            }

//            return frames;
//        }
//    }

//    // 简化版 MaxRects 算法（仅 Best Short Side Fit）
//    public class RectanglePacker
//    {
//        private readonly double binWidth;
//        private readonly double binHeight;
//        private List<Rect> freeRects;

//        public RectanglePacker(double width, double height)
//        {
//            binWidth = width;
//            binHeight = height;
//            freeRects = new List<Rect> { new Rect(0, 0, width, height) };
//        }

//        public Point2d? TryPack(double width, double height)
//        {
//            int bestIndex = -1;
//            double bestShortSide = double.MaxValue;
//            Rect bestRect = default;

//            for (int i = 0; i < freeRects.Count; i++)
//            {
//                var free = freeRects[i];
//                if (width <= free.W && height <= free.H)
//                {
//                    double leftoverH = Math.Abs(free.H - height);
//                    double leftoverW = Math.Abs(free.W - width);
//                    double shortSide = Math.Min(leftoverH, leftoverW);

//                    if (shortSide < bestShortSide)
//                    {
//                        bestIndex = i;
//                        bestShortSide = shortSide;
//                        bestRect = new Rect(free.X, free.Y, width, height);
//                    }
//                }
//            }

//            if (bestIndex == -1) return null;

//            var target = freeRects[bestIndex];
//            SplitFreeRect(target, bestRect);
//            freeRects.RemoveAt(bestIndex);
//            return new Point2d(bestRect.X, bestRect.Y);
//        }

//        private void SplitFreeRect(Rect free, Rect placed)
//        {
//            if (placed.X < free.X + free.W && placed.X + placed.W > free.X)
//            {
//                if (placed.Y > free.Y && placed.Y < free.Y + free.H)
//                {
//                    freeRects.Add(new Rect(free.X, free.Y, free.W, placed.Y - free.Y));
//                }
//                if (placed.Y + placed.H < free.Y + free.H)
//                {
//                    freeRects.Add(new Rect(free.X, placed.Y + placed.H, free.W, (free.Y + free.H) - (placed.Y + placed.H)));
//                }
//            }

//            if (placed.Y < free.Y + free.H && placed.Y + placed.H > free.Y)
//            {
//                if (placed.X > free.X && placed.X < free.X + free.W)
//                {
//                    freeRects.Add(new Rect(free.X, free.Y, placed.X - free.X, free.H));
//                }
//                if (placed.X + placed.W < free.X + free.W)
//                {
//                    freeRects.Add(new Rect(placed.X + placed.W, free.Y, (free.X + free.W) - (placed.X + placed.W), free.H));
//                }
//            }
//        }
//    }

//    public class Rect
//    {
//        public double X, Y, W, H;
//        public Rect(double x, double y, double w, double h)
//        {
//            X = x; Y = y; W = w; H = h;
//        }
//    }

//    public class LayoutViewport
//    {
//        public Polyline Polyline { get; }
//        public int Index { get; }
//        public Extents3d Bounds { get; }
//        public double Width => Bounds.MaxPoint.X - Bounds.MinPoint.X;
//        public double Height => Bounds.MaxPoint.Y - Bounds.MinPoint.Y;
//        public Point3d Center => new Point3d((Bounds.MinPoint.X + Bounds.MaxPoint.X) / 2, (Bounds.MinPoint.Y + Bounds.MaxPoint.Y) / 2, 0);

//        public LayoutViewport(Polyline pl, int index)
//        {
//            Polyline = pl;
//            Index = index;
//            Bounds = pl.GeometricExtents;
//        }
//    }
//}
