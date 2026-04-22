using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Enums;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interactive
{
    /// <summary>
    /// 标高符号交互式绘制 Jig
    /// 迁移自旧代码 ElevationSymbol : DrawJig
    /// 
    /// 符号结构：Shape(4顶点多段线) + Shape1(2顶点水平线) + Label(DBText)
    /// 标高计算：按当前绘图单位把模型空间高差换算为米。
    /// </summary>
    public class ElevationSymbolJig : DrawJig
    {
        public Polyline Shape { get; private set; }
        public Polyline Shape1 { get; private set; }
        public DBText Label { get; private set; }
        public Point3d BasePoint { get; private set; }
        public double Scale { get; private set; }
        public double D { get; private set; }
        public ElevationSymbolState State { get; set; }

        private Point3d _currentPoint;
        private readonly Editor _editor;
        private readonly ObjectId _textStyleId;
        private readonly ObjectId _layerId;
        private readonly double _angleRadians;

        public Point3d CurrentPoint => _currentPoint;

        /// <summary>
        /// 标高图层名称
        /// </summary>
        public const string ElevationLayerName = "00_hy_3公共_标注4_标高";
        public const short ElevationLayerColor = 140;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="basePoint">基准点</param>
        /// <param name="scale">比例</param>
        /// <param name="d">构造参数 d（默认2）</param>
        /// <param name="editor">编辑器</param>
        /// <param name="textStyleId">文字样式 ObjectId</param>
        /// <param name="layerId">图层 ObjectId</param>
        /// <param name="state">翻转状态</param>
        /// <param name="angleDegrees">旋转角度（度）</param>
        public ElevationSymbolJig(
            Point3d basePoint, double scale, double d,
            Editor editor, ObjectId textStyleId, ObjectId layerId,
            ElevationSymbolState state = ElevationSymbolState.Normal,
            double angleDegrees = 0.0)
        {
            BasePoint = basePoint;
            Scale = scale;
            D = d;
            State = state;
            _currentPoint = basePoint;
            _editor = editor;
            _textStyleId = textStyleId;
            _layerId = layerId;
            _angleRadians = angleDegrees * Math.PI / 180.0;

            UpdateSymbol(0.0, true);
        }

        /// <summary>
        /// 更新符号几何（根据标高值和状态重建 Shape/Shape1/Label）
        /// </summary>
        public void UpdateSymbol(double elevation, bool isBasePoint = false)
        {
            double paperToModelScale = ElevationService.ToModelLength(1.0, Scale);
            double sqrt2 = Math.Sqrt(2) / 2 * D;

            // Shape: 4 顶点多段线（标高三角形符号）
            var points = new Point3d[4];
            points[0] = RotatePoint(new Point3d(_currentPoint.X + 3.5 * D * paperToModelScale, _currentPoint.Y + sqrt2 * paperToModelScale, 0), _currentPoint, _angleRadians);
            points[1] = RotatePoint(new Point3d(_currentPoint.X - sqrt2 * paperToModelScale, _currentPoint.Y + sqrt2 * paperToModelScale, 0), _currentPoint, _angleRadians);
            points[2] = _currentPoint;
            points[3] = RotatePoint(new Point3d(sqrt2 * paperToModelScale + _currentPoint.X, _currentPoint.Y + sqrt2 * paperToModelScale, 0), _currentPoint, _angleRadians);

            // Shape1: 2 顶点水平线
            var points1 = new Point3d[2];
            points1[0] = RotatePoint(new Point3d(_currentPoint.X + sqrt2 * paperToModelScale, _currentPoint.Y, 0), _currentPoint, _angleRadians);
            points1[1] = RotatePoint(new Point3d(_currentPoint.X - sqrt2 * paperToModelScale, _currentPoint.Y, 0), _currentPoint, _angleRadians);

            AdjustPointsByState(ref points);

            Shape = new Polyline();
            Shape1 = new Polyline();

            if (!_layerId.IsNull)
            {
                Shape.LayerId = _layerId;
                Shape1.LayerId = _layerId;
            }

            for (int i = 0; i < 4; i++)
                Shape.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);

            for (int i = 0; i < 2; i++)
                Shape1.AddVertexAt(i, new Point2d(points1[i].X, points1[i].Y), 0, 0, 0);

            // Label: 标高文字
            var textPos = RotatePoint(
                new Point3d(_currentPoint.X + (4 - sqrt2 / 2) * paperToModelScale, _currentPoint.Y + sqrt2 * paperToModelScale, 0),
                _currentPoint, _angleRadians);

            var vm = Presentation.ViewModels.SettingsPanelViewModel.Current;
            double textSize = (vm != null ? vm.TextSize : 2.5) * paperToModelScale;
            double widthFactor = vm != null ? vm.TextXScale : 0.7;

            Label = new DBText
            {
                TextStyleId = _textStyleId,
                Position = textPos,
                Height = textSize,
                WidthFactor = widthFactor,
                TextString = isBasePoint ? $"±{elevation:F3}" : $"{elevation:F3}",
                HorizontalMode = TextHorizontalMode.TextCenter,
                AlignmentPoint = textPos,
                Rotation = _angleRadians
            };

            if (!_layerId.IsNull)
                Label.LayerId = _layerId;

            AdjustTextByState(sqrt2, paperToModelScale);
        }

        /// <summary>
        /// 将符号添加到数据库
        /// </summary>
        public void AddToDatabase(Transaction tr, BlockTableRecord btr)
        {
            btr.AppendEntity(Shape);
            tr.AddNewlyCreatedDBObject(Shape, true);
            btr.AppendEntity(Shape1);
            tr.AddNewlyCreatedDBObject(Shape1, true);
            btr.AppendEntity(Label);
            tr.AddNewlyCreatedDBObject(Label, true);
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var opts = new JigPromptPointOptions("\n选择标高点 [D-左右翻转/S-上下翻转/O-重选基点]: ");
            opts.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NoZeroResponseAccepted;
            opts.Keywords.Add("D");
            opts.Keywords.Add("S");
            opts.Keywords.Add("O");

            PromptPointResult res = prompts.AcquirePoint(opts);
            if (res.Status == PromptStatus.OK)
            {
                if (_currentPoint == res.Value) return SamplerStatus.NoChange;
                _currentPoint = res.Value;
                double elevation = ElevationService.ToElevationMeters(_currentPoint.Y - BasePoint.Y);
                bool isBase = Math.Abs(elevation) <= 0.001;
                UpdateSymbol(elevation, isBase);
                return SamplerStatus.OK;
            }
            else if (res.Status == PromptStatus.Keyword)
            {
                HandleKeyword(res.StringResult);
                double elevation = ElevationService.ToElevationMeters(_currentPoint.Y - BasePoint.Y);
                bool isBase = Math.Abs(elevation) <= 0.001;
                UpdateSymbol(elevation, isBase);
                return SamplerStatus.NoChange;
            }
            return SamplerStatus.Cancel;
        }

        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
        {
            draw.Geometry.Draw(Shape);
            draw.Geometry.Draw(Shape1);
            draw.Geometry.Draw(Label);
            return true;
        }

        private void HandleKeyword(string keyword)
        {
            switch (keyword.ToLower())
            {
                case "d": // 左右翻转
                    switch (State)
                    {
                        case ElevationSymbolState.Normal: State = ElevationSymbolState.FlipHorizontal; break;
                        case ElevationSymbolState.FlipVertical: State = ElevationSymbolState.FlipBoth; break;
                        case ElevationSymbolState.FlipHorizontal: State = ElevationSymbolState.Normal; break;
                        case ElevationSymbolState.FlipBoth: State = ElevationSymbolState.FlipVertical; break;
                    }
                    break;
                case "s": // 上下翻转
                    switch (State)
                    {
                        case ElevationSymbolState.Normal: State = ElevationSymbolState.FlipVertical; break;
                        case ElevationSymbolState.FlipHorizontal: State = ElevationSymbolState.FlipBoth; break;
                        case ElevationSymbolState.FlipVertical: State = ElevationSymbolState.Normal; break;
                        case ElevationSymbolState.FlipBoth: State = ElevationSymbolState.FlipHorizontal; break;
                    }
                    break;
                case "o": // 重选基点
                    PromptPointResult res = _editor.GetPoint("\n选择新的基点: ");
                    if (res.Status == PromptStatus.OK)
                    {
                        BasePoint = res.Value;
                        _currentPoint = BasePoint;
                        UpdateSymbol(0.0, true);
                    }
                    break;
            }
        }

        #region 几何辅助

        private static Point3d RotatePoint(Point3d point, Point3d center, double angle)
        {
            double cosTheta = Math.Cos(angle);
            double sinTheta = Math.Sin(angle);
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            return new Point3d(
                center.X + (dx * cosTheta - dy * sinTheta),
                center.Y + (dx * sinTheta + dy * cosTheta), 0);
        }

        /// <summary>
        /// 静态旋转点（供外部命令使用）
        /// </summary>
        public static Point3d RotatePointStatic(Point3d point, Point3d center, double angle)
        {
            return RotatePoint(point, center, angle);
        }

        private void AdjustPointsByState(ref Point3d[] points)
        {
            switch (State)
            {
                case ElevationSymbolState.FlipVertical:
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new Point3d(points[i].X, 2 * _currentPoint.Y - points[i].Y, 0);
                    break;
                case ElevationSymbolState.FlipHorizontal:
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new Point3d(2 * _currentPoint.X - points[i].X, points[i].Y, 0);
                    break;
                case ElevationSymbolState.FlipBoth:
                    for (int i = 0; i < points.Length; i++)
                        points[i] = new Point3d(2 * _currentPoint.X - points[i].X, 2 * _currentPoint.Y - points[i].Y, 0);
                    break;
            }
        }

        private void AdjustTextByState(double sqrt2, double paperToModelScale)
        {
            var vm = Presentation.ViewModels.SettingsPanelViewModel.Current;
            double textSize = (vm != null ? vm.TextSize : 2.5);
            Point3d textPos = Label.Position;

            switch (State)
            {
                case ElevationSymbolState.FlipVertical:
                    textPos = new Point3d(textPos.X, _currentPoint.Y - (sqrt2 + textSize) * paperToModelScale, 0);
                    break;
                case ElevationSymbolState.FlipHorizontal:
                    textPos = new Point3d(_currentPoint.X - (4 - sqrt2 / 2) * paperToModelScale, textPos.Y, 0);
                    break;
                case ElevationSymbolState.FlipBoth:
                    textPos = new Point3d(_currentPoint.X - (4 - sqrt2 / 2) * paperToModelScale, _currentPoint.Y - (sqrt2 + textSize) * paperToModelScale, 0);
                    break;
            }

            Label.Position = textPos;
            Label.AlignmentPoint = textPos;
        }

        #endregion
    }
}
