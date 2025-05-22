using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
namespace HyCADTool.HelpClass.TitleBlock
{
    // 接口定义
    public interface ITitleBlock
    {
        string Name { get; }
        double Width { get; }
        double Height { get; }
        double LeftMargin { get; }
        double RightMargin { get; }
        double TopMargin { get; }
        double BottomMargin { get; }
        double SignWidth { get; }
        double SignHeight { get; }
        Polyline GetInnerFrame(Point3d basePoint);
    }
    // 标准图框实现类
    public class StandardTitleBlock : ITitleBlock
    {
        public string Name { get; }
        public double Width { get; }
        public double Height { get; }
        public double LeftMargin { get; }
        public double RightMargin { get; }
        public double TopMargin { get; }
        public double BottomMargin { get; }
        public double SignWidth { get; }
        public double SignHeight { get; }
        public StandardTitleBlock(
            string name, double width, double height,
            double left, double right, double top, double bottom,
            double signW, double signH)
        {
            Name = name;
            Width = width;
            Height = height;
            LeftMargin = left;
            RightMargin = right;
            TopMargin = top;
            BottomMargin = bottom;
            SignWidth = signW;
            SignHeight = signH;
        }
        public Polyline GetInnerFrame(Point3d basePoint)
        {
            var pt1 = new Point2d(basePoint.X + LeftMargin, basePoint.Y + BottomMargin);
            var pt2 = new Point2d(basePoint.X + Width - RightMargin, basePoint.Y + Height - TopMargin);
            var poly = new Polyline(4);
            poly.AddVertexAt(0, new Point2d(pt1.X, pt1.Y), 0, 0, 0);
            poly.AddVertexAt(1, new Point2d(pt2.X, pt1.Y), 0, 0, 0);
            poly.AddVertexAt(2, new Point2d(pt2.X, pt2.Y), 0, 0, 0);
            poly.AddVertexAt(3, new Point2d(pt1.X, pt2.Y), 0, 0, 0);
            poly.Closed = true;
            poly.ConstantWidth = 0.5; // 设置多段线全局宽度
            return poly;
        }
    }
    // 工厂类
    public static class TitleBlockFactory
    {
        public static ITitleBlock Create(string size, double lengthScale = 1.0)
        {
            var configs = new Dictionary<string, (double W, double H, double L, double T, double R, double B, double SW, double SH)>
        {
            { "A4", (297, 210, 25, 5, 5, 5, 180, 40) },
            { "A3", (420, 297, 25, 5, 5, 5, 180, 40) },
            { "A2", (594, 420, 25, 10, 10, 10, 180, 50) },
            { "A1", (841, 594, 25, 10, 10, 10, 180, 50) },
            { "A0", (1189, 841, 25, 10, 10, 10, 180, 60) }
        };
            if (!configs.ContainsKey(size))
                throw new ArgumentException($"未定义图纸类型: {size}");
            var (w, h, l, t, r, b, sw, sh) = configs[size];
            if (w >= h)
                w *= lengthScale;
            else
                h *= lengthScale;
            return new StandardTitleBlock(size, w, h, l, r, t, b, sw, sh);
        }
    }
    // 图框绘图器
    public static class TitleBlockDrawer
    {
        public static void DrawTitleBlock(Transaction tr, BlockTableRecord btr, ITitleBlock tb, Database db, Point3d basePoint)
        {
            var layerId = Tools.Et.CreateLayer("00_hy_图框", 7, db);
            // 外框
            var outer = CreateRect(
                new Point2d(basePoint.X, basePoint.Y),
                new Point2d(basePoint.X + tb.Width, basePoint.Y + tb.Height),
                LineWeight.LineWeight018, layerId);
            btr.AppendEntity(outer); tr.AddNewlyCreatedDBObject(outer, true);
            // 内框
            var inner = CreateRect(
                new Point2d(basePoint.X + tb.LeftMargin, basePoint.Y + tb.BottomMargin),
                new Point2d(basePoint.X + tb.Width - tb.RightMargin, basePoint.Y + tb.Height - tb.TopMargin),
                LineWeight.LineWeight050, layerId);
            btr.AppendEntity(inner); tr.AddNewlyCreatedDBObject(inner, true);
            // 图签位置
            double sx = basePoint.X + tb.Width - tb.RightMargin - tb.SignWidth;
            double sy = basePoint.Y + tb.BottomMargin;
            var sign = CreateRect(
                new Point2d(sx, sy), new Point2d(sx + tb.SignWidth, sy + tb.SignHeight),
                LineWeight.ByLineWeightDefault, layerId);
            btr.AppendEntity(sign); tr.AddNewlyCreatedDBObject(sign, true);
            var label = new DBText
            {
                Position = new Point3d(sx + tb.SignWidth / 2, sy + tb.SignHeight / 2, 0),
                Height = 10,
                TextString = "图签位置",
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = new Point3d(sx + tb.SignWidth / 2, sy + tb.SignHeight / 2, 0),
                Layer = "00_hy_图框"
            };
            label.AdjustAlignment(db);
            btr.AppendEntity(label); tr.AddNewlyCreatedDBObject(label, true);
        }
        private static Polyline CreateRect(Point2d pt1, Point2d pt2, LineWeight lw, ObjectId layerId)
        {
            var pl = new Polyline(4);
            pl.AddVertexAt(0, new Point2d(pt1.X, pt1.Y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(pt2.X, pt1.Y), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(pt2.X, pt2.Y), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(pt1.X, pt2.Y), 0, 0, 0);
            pl.Closed = true;
            pl.LayerId = layerId;
            pl.LineWeight = lw;
            if (lw == LineWeight.LineWeight050)
            {
                pl.ConstantWidth = 0.5;
            }
            return pl;
        }
    }
}