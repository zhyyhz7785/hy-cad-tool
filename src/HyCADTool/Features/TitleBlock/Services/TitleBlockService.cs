using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Services;
using System;
using System.Collections.Generic;

namespace HyCADTool.Features.TitleBlock.Services
{
    #region 接口

    /// <summary>图框尺寸与边距信息</summary>
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
    }

    #endregion

    #region 实现

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

        public StandardTitleBlock(string name, double w, double h,
            double left, double right, double top, double bottom,
            double signW, double signH)
        {
            Name = name; Width = w; Height = h;
            LeftMargin = left; RightMargin = right;
            TopMargin = top; BottomMargin = bottom;
            SignWidth = signW; SignHeight = signH;
        }
    }

    #endregion

    #region 工厂

    public static class TitleBlockFactory
    {
        private static readonly Dictionary<string, (double W, double H, double L, double T, double R, double B, double SW, double SH)> Configs
            = new Dictionary<string, (double, double, double, double, double, double, double, double)>
            {
                { "A4", (297, 210, 25, 5, 5, 5, 180, 40) },
                { "A3", (420, 297, 25, 5, 5, 5, 180, 40) },
                { "A2", (594, 420, 25, 10, 10, 10, 180, 50) },
                { "A1", (841, 594, 25, 10, 10, 10, 180, 50) },
                { "A0", (1189, 841, 25, 10, 10, 10, 180, 60) }
            };

        public static ITitleBlock Create(string size, double lengthScale = 1.0)
        {
            if (!Configs.ContainsKey(size))
                throw new ArgumentException($"未定义图纸类型: {size}");

            var (w, h, l, t, r, b, sw, sh) = Configs[size];
            if (w >= h) w *= lengthScale; else h *= lengthScale;
            return new StandardTitleBlock(size, w, h, l, r, t, b, sw, sh);
        }
    }

    #endregion

    #region 绘图器

    public static class TitleBlockDrawer
    {
        public static void Draw(Transaction tr, BlockTableRecord btr, ITitleBlock tb, Database db, Point3d basePoint)
        {
            // 图层已在 PluginInitializer 统一创建，此处只查找 ID
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            string titleLayerName = UserLayerNameResolver.Get(LayerSemanticIds.TitleBlock, LayerBuiltinDefaults.TitleBlock);
            var layerId = lt.Has(titleLayerName) ? lt[titleLayerName] : db.LayerZero;

            // 外框
            var outer = CreateRect(
                new Point2d(basePoint.X, basePoint.Y),
                new Point2d(basePoint.X + tb.Width, basePoint.Y + tb.Height),
                LineWeight.LineWeight018, layerId);
            btr.AppendEntity(outer);
            tr.AddNewlyCreatedDBObject(outer, true);

            // 内框
            var inner = CreateRect(
                new Point2d(basePoint.X + tb.LeftMargin, basePoint.Y + tb.BottomMargin),
                new Point2d(basePoint.X + tb.Width - tb.RightMargin, basePoint.Y + tb.Height - tb.TopMargin),
                LineWeight.LineWeight050, layerId);
            btr.AppendEntity(inner);
            tr.AddNewlyCreatedDBObject(inner, true);

            // 图签
            double sx = basePoint.X + tb.Width - tb.RightMargin - tb.SignWidth;
            double sy = basePoint.Y + tb.BottomMargin;
            var sign = CreateRect(
                new Point2d(sx, sy),
                new Point2d(sx + tb.SignWidth, sy + tb.SignHeight),
                LineWeight.ByLineWeightDefault, layerId);
            btr.AppendEntity(sign);
            tr.AddNewlyCreatedDBObject(sign, true);

            var label = new DBText
            {
                Position = new Point3d(sx + tb.SignWidth / 2, sy + tb.SignHeight / 2, 0),
                Height = 10,
                TextString = "图签位置",
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = new Point3d(sx + tb.SignWidth / 2, sy + tb.SignHeight / 2, 0),
                Layer = titleLayerName
            };
            label.AdjustAlignment(db);
            btr.AppendEntity(label);
            tr.AddNewlyCreatedDBObject(label, true);
        }

        private static Polyline CreateRect(Point2d p1, Point2d p2, LineWeight lw, ObjectId layerId)
        {
            var pl = new Polyline(4);
            pl.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(p2.X, p1.Y), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(p2.X, p2.Y), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(p1.X, p2.Y), 0, 0, 0);
            pl.Closed = true;
            pl.LayerId = layerId;
            pl.LineWeight = lw;
            if (lw == LineWeight.LineWeight050)
                pl.ConstantWidth = 0.5;
            return pl;
        }
    }

    #endregion
}
