using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using static HyCADTool.Config.BaseConfig;
namespace HyCADTool.HelpClass.ElevationSymbol
{
    public class ElevationSymbol : DrawJig
    {
        public Polyline Shape { get; private set; }
        public Polyline Shape1 { get; private set; }
        public DBText Label { get; private set; }
        public Point3d BasePoint { get; private set; }
        public double Scale { get; private set; }
        public double D { get; private set; }
        public ElevationSymbolState State { get; set; }
        private Point3d _currentPoint;
        private Editor _editor;
        private ObjectId _textStyleId;
        private ObjectId _layerId;
        private double _angleDegrees;
        private double _angleRadians;
        public Point3d CurrentPoint => _currentPoint;
        // 静态字典不再由 ElevationSymbol 自动维护
        public static Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> SymbolDictionary = new Dictionary<Tuple<ObjectId, ObjectId>, ObjectId>();
        // 静态缓存图层和文本样式的 ObjectId，避免重复创建
        private static ObjectId _cachedTextStyleId = ObjectId.Null;
        private static ObjectId _cachedLayerId = ObjectId.Null;
        public ElevationSymbol(Point3d basePoint, double scale, double d, Editor editor, ElevationSymbolState state = ElevationSymbolState.Normal, double angleDegrees = 0.0)
        {
            BasePoint = basePoint;
            Scale = BaseConfig.Scale; // 使用全局配置的 Scale
            D = d;
            State = state;
            _currentPoint = basePoint;
            _editor = editor;
            _angleDegrees = angleDegrees;
            // 初始化或重用文本样式和图层
            if (_cachedTextStyleId.IsNull)
            {
                _cachedTextStyleId = Tools.Tools.CreateTextStyle(Tools.Tools.TextStyleConfig.Name);
            }
            _textStyleId = _cachedTextStyleId;
            if (_cachedLayerId.IsNull)
            {
                _cachedLayerId = Tools.Tools.CreateLayer("00_hy_3公共_标注4_标高", 140);
            }
            _layerId = _cachedLayerId;
            UpdateRotation();
            UpdateSymbol(0.0, true);
        }
        private void UpdateRotation()
        {
            _angleRadians = _angleDegrees * Math.PI / 180.0;
        }
        public void UpdateSymbol(double elevation, bool isBasePoint = false)
        {
            double sqrt2 = Math.Sqrt(2) / 2 * D;
            Point3d[] points = new Point3d[4];
            points[0] = RotatePoint(new Point3d(_currentPoint.X + 3.5 * D * Scale, _currentPoint.Y + sqrt2 * Scale, 0), _currentPoint, _angleRadians);
            points[1] = RotatePoint(new Point3d(_currentPoint.X - sqrt2 * Scale, _currentPoint.Y + sqrt2 * Scale, 0), _currentPoint, _angleRadians);
            points[2] = _currentPoint;
            points[3] = RotatePoint(new Point3d(sqrt2 * Scale + _currentPoint.X, _currentPoint.Y + sqrt2 * Scale, 0), _currentPoint, _angleRadians);
            Point3d[] points1 = new Point3d[2];
            points1[0] = RotatePoint(new Point3d(_currentPoint.X + sqrt2 * Scale, _currentPoint.Y, 0), _currentPoint, _angleRadians);
            points1[1] = RotatePoint(new Point3d(_currentPoint.X - sqrt2 * Scale, _currentPoint.Y, 0), _currentPoint, _angleRadians);
            AdjustPointsByState(ref points);
            Shape = new Polyline();
            Shape1 = new Polyline();
            Shape.LayerId = _layerId;
            Shape1.LayerId = _layerId;
            for (int i = 0; i < 4; i++)
            {
                Shape.AddVertexAt(i, new Point2d(points[i].X, points[i].Y), 0, 0, 0);
            }
            for (int i = 0; i < 2; i++)
            {
                Shape1.AddVertexAt(i, new Point2d(points1[i].X, points1[i].Y), 0, 0, 0);
            }
            Label = new DBText
            {
                LayerId = _layerId,
                TextStyleId = _textStyleId,
                Position = RotatePoint(new Point3d(_currentPoint.X + (4 - sqrt2 / 2) * Scale, _currentPoint.Y + sqrt2 * Scale, 0), _currentPoint, _angleRadians),
                Height = Tools.Tools.TextStyleConfig.TextSize * Scale,
                WidthFactor = 0.7,
                TextString = isBasePoint ? $"±{elevation:F3}" : $"{elevation:F3}",
                HorizontalMode = TextHorizontalMode.TextCenter,
                AlignmentPoint = RotatePoint(new Point3d(_currentPoint.X + (4 - sqrt2 / 2) * Scale, _currentPoint.Y + sqrt2 * Scale, 0), _currentPoint, _angleRadians),
                Rotation = _angleRadians
            };
            AdjustTextByState(sqrt2);
        }
        private Point3d RotatePoint(Point3d point, Point3d center, double angle)
        {
            double cosTheta = Math.Cos(angle);
            double sinTheta = Math.Sin(angle);
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            double newX = center.X + (dx * cosTheta - dy * sinTheta);
            double newY = center.Y + (dx * sinTheta + dy * cosTheta);
            return new Point3d(newX, newY, 0);
        }
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
            JigPromptPointOptions opts = new JigPromptPointOptions("\n选择标高点 [D-左右翻转/S-上下翻转/O-重选基点]: ");
            opts.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NoZeroResponseAccepted;
            opts.Keywords.Add("D");
            opts.Keywords.Add("S");
            opts.Keywords.Add("O");
            PromptPointResult res = prompts.AcquirePoint(opts);
            if (res.Status == PromptStatus.OK)
            {
                if (_currentPoint == res.Value) return SamplerStatus.NoChange;
                _currentPoint = res.Value;
                double elevation = (_currentPoint.Y - BasePoint.Y) / 1000.0;
                const double tolerance = 0.001;
                bool isBasePointDynamic = Math.Abs(elevation) <= tolerance;
                UpdateSymbol(elevation, isBasePointDynamic);
                return SamplerStatus.OK;
            }
            else if (res.Status == PromptStatus.Keyword)
            {
                HandleKeyword(res.StringResult);
                double elevation = (_currentPoint.Y - BasePoint.Y) / 1000.0;
                const double tolerance = 0.001;
                bool isBasePointDynamic = Math.Abs(elevation) <= tolerance;
                UpdateSymbol(elevation, isBasePointDynamic);
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
                case "d":
                    switch (State)
                    {
                        case ElevationSymbolState.Normal:
                            State = ElevationSymbolState.FlipHorizontal;
                            break;
                        case ElevationSymbolState.FlipVertical:
                            State = ElevationSymbolState.FlipBoth;
                            break;
                        case ElevationSymbolState.FlipHorizontal:
                            State = ElevationSymbolState.Normal;
                            break;
                        case ElevationSymbolState.FlipBoth:
                            State = ElevationSymbolState.FlipVertical;
                            break;
                    }
                    break;
                case "s":
                    switch (State)
                    {
                        case ElevationSymbolState.Normal:
                            State = ElevationSymbolState.FlipVertical;
                            break;
                        case ElevationSymbolState.FlipHorizontal:
                            State = ElevationSymbolState.FlipBoth;
                            break;
                        case ElevationSymbolState.FlipVertical:
                            State = ElevationSymbolState.Normal;
                            break;
                        case ElevationSymbolState.FlipBoth:
                            State = ElevationSymbolState.FlipHorizontal;
                            break;
                    }
                    break;
                case "o":
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
        private void AdjustTextByState(double sqrt2)
        {
            Point3d textPos = Label.Position;
            switch (State)
            {
                case ElevationSymbolState.FlipVertical:
                    textPos = new Point3d(textPos.X, _currentPoint.Y - (sqrt2 + Tools.Tools.TextStyleConfig.TextSize) * Scale, 0);
                    break;
                case ElevationSymbolState.FlipHorizontal:
                    textPos = new Point3d(_currentPoint.X - (4 - sqrt2 / 2) * Scale, textPos.Y, 0);
                    break;
                case ElevationSymbolState.FlipBoth:
                    textPos = new Point3d(_currentPoint.X - (4 - sqrt2 / 2) * Scale, _currentPoint.Y - (sqrt2 + Tools.Tools.TextStyleConfig.TextSize) * Scale, 0);
                    break;
            }
            Label.Position = textPos;
            Label.AlignmentPoint = textPos;
        }
        /// <summary>
        /// 以Shape的第三个点为中心旋转所有符号和文字到指定角度
        /// </summary>
        /// <param name="angleDegrees">旋转角度（度）</param>
        public static void RotateAllByAngle(double angleDegrees, Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> dictionary)
        {
            using (Transaction tr = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.StartTransaction())
            {
                foreach (var entry in dictionary)
                {
                    ObjectId shapeId = entry.Key.Item1;
                    ObjectId shape1Id = entry.Key.Item2;
                    ObjectId textId = entry.Value;
                    Polyline shape = tr.GetObject(shapeId, OpenMode.ForWrite) as Polyline;
                    Polyline shape1 = tr.GetObject(shape1Id, OpenMode.ForWrite) as Polyline;
                    DBText text = tr.GetObject(textId, OpenMode.ForWrite) as DBText;
                    if (shape != null && shape1 != null && text != null)
                    {
                        Point3d center = shape.GetPoint3dAt(2);
                        // 旋转 Shape
                        for (int i = 0; i < shape.NumberOfVertices; i++)
                        {
                            Point3d vertex = shape.GetPoint3dAt(i);
                            Point3d rotatedVertex = RotatePointStatic(vertex, center, angleDegrees * Math.PI / 180.0);
                            shape.SetPointAt(i, new Point2d(rotatedVertex.X, rotatedVertex.Y));
                        }
                        // 旋转 Shape1
                        for (int i = 0; i < shape1.NumberOfVertices; i++)
                        {
                            Point3d vertex = shape1.GetPoint3dAt(i);
                            Point3d rotatedVertex = RotatePointStatic(vertex, center, angleDegrees * Math.PI / 180.0);
                            shape1.SetPointAt(i, new Point2d(rotatedVertex.X, rotatedVertex.Y));
                        }
                        // 旋转文字
                        Point3d textPos = text.Position;
                        Point3d rotatedTextPos = RotatePointStatic(textPos, center, angleDegrees * Math.PI / 180.0);
                        text.Position = rotatedTextPos;
                        text.AlignmentPoint = rotatedTextPos;
                        // 保持文字的相对方向：基于原始 Rotation 增加旋转角度
                        double originalRotation = text.Rotation; // 获取文字的原始旋转角度（弧度）
                        double newRotation = originalRotation + (angleDegrees * Math.PI / 180.0); // 增加旋转角度
                        text.Rotation = newRotation; // 设置新的相对旋转角度
                    }
                }
                tr.Commit();
            }
        }
        public static Point3d RotatePointStatic(Point3d point, Point3d center, double angle)
        {
            double cosTheta = Math.Cos(angle);
            double sinTheta = Math.Sin(angle);
            double dx = point.X - center.X;
            double dy = point.Y - center.Y;
            double newX = center.X + (dx * cosTheta - dy * sinTheta);
            double newY = center.Y + (dx * sinTheta + dy * cosTheta);
            return new Point3d(newX, newY, 0);
        }
        /// <summary>
        /// 更新所有标高文字基于新输入点
        /// </summary>
        /// <param name="newBasePoint">新基准点</param>
        public static void UpdateElevationsByPoint(Point3d newBasePoint, Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> dictionary)
        {
            using (Transaction tr = Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.StartTransaction())
            {
                foreach (var entry in dictionary)
                {
                    ObjectId shapeId = entry.Key.Item1;
                    ObjectId textId = entry.Value;
                    Polyline shape = tr.GetObject(shapeId, OpenMode.ForRead) as Polyline;
                    DBText text = tr.GetObject(textId, OpenMode.ForWrite) as DBText;
                    if (shape != null && text != null && shape.NumberOfVertices >= 3) // 确保有足够的顶点
                    {
                        Point3d referencePoint = shape.GetPoint3dAt(2);
                        double elevation = (referencePoint.Y - newBasePoint.Y) / 1000.0;
                        double tolerance = 0.001;
                        text.TextString = Math.Abs(referencePoint.Y - newBasePoint.Y) < tolerance ? $"±{elevation:F3}" : $"{elevation:F3}";
                    }
                    else
                    {
                        Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n跳过无效的对象（Shape: {shapeId}），顶点数量不足或对象无效。");
                    }
                }
                tr.Commit();
            }
        }
        public static Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> BuildSymbolDictionaryFromSelection()
        {
            // 创建一个临时字典，用于存储标高符号的映射关系
            // 键是 Tuple<ObjectId, ObjectId>（表示 Shape 和 Shape1 的 ObjectId）
            // 值是 ObjectId（对应匹配的 DBText 的 ObjectId）
            Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> tempDictionary = new Dictionary<Tuple<ObjectId, ObjectId>, ObjectId>();
            // 获取当前活动文档的 Editor，用于与用户交互
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            // 获取当前活动文档的 Database，用于操作图纸数据
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            // 定义一个 TypedValue 数组作为选择过滤器
            // 过滤器指定只选择 "00_hy_3公共_标注4_标高" 图层中的对象
            TypedValue[] filter = new TypedValue[]
            {
        new TypedValue((int)DxfCode.LayerName, "00_hy_3公共_标注4_标高")
            };
            // 创建 SelectionFilter 对象，将过滤器应用到选择操作
            SelectionFilter selFilter = new SelectionFilter(filter);
            // 提示用户选择对象，并应用过滤器
            PromptSelectionResult psr = ed.GetSelection(selFilter);
            // 检查选择是否成功
            // 如果状态不是 OK（例如用户取消或未选择对象），输出提示并返回空字典
            if (psr.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择任何对象。");
                return tempDictionary;
            }
            // 使用 using 语句确保 Transaction 在使用完毕后正确释放资源
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 获取选择集，包含用户选择的所有对象
                SelectionSet ss = psr.Value;
                // 初始化列表，分别存储符合条件的 Polyline 和所有 DBText 的 ObjectId
                List<ObjectId> shapes = new List<ObjectId>();    // 存储 4 个端点的 Polyline (Shape)
                List<ObjectId> shape1s = new List<ObjectId>();   // 存储 2 个端点的 Polyline (Shape1)
                List<ObjectId> texts = new List<ObjectId>();     // 存储所有 DBText
                // 遍历选择集中的所有 ObjectId
                foreach (ObjectId id in ss.GetObjectIds())
                {
                    // 通过 Transaction 获取对象，OpenMode.ForRead 表示只读模式
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    // 如果对象是 Polyline，检查顶点数并分类
                    if (ent is Polyline polyline)
                    {
                        if (polyline.NumberOfVertices == 4)
                        {
                            shapes.Add(id); // 添加 4 个端点的 Polyline 作为 Shape
                        }
                        else if (polyline.NumberOfVertices == 2)
                        {
                            shape1s.Add(id); // 添加 2 个端点的 Polyline 作为 Shape1
                        }
                    }
                    // 如果对象是 DBText，添加到 texts 列表
                    else if (ent is DBText)
                    {
                        texts.Add(id);
                    }
                }
                // 遍历所有 Shape，寻找匹配的 Shape1
                foreach (ObjectId shapeId in shapes)
                {
                    Polyline shape = tr.GetObject(shapeId, OpenMode.ForRead) as Polyline;
                    if (shape == null || shape.NumberOfVertices != 4) continue; // 确保 Shape 有效且有 4 个顶点
                    // 获取 Shape 的第三个端点作为参考点
                    Point3d shapeThirdPoint = shape.GetPoint3dAt(2);
                    // 寻找匹配的 Shape1（2 个端点的 Polyline）
                    foreach (ObjectId shape1Id in shape1s)
                    {
                        Polyline shape1 = tr.GetObject(shape1Id, OpenMode.ForRead) as Polyline;
                        if (shape1 == null || shape1.NumberOfVertices != 2) continue; // 确保 Shape1 有效且有 2 个顶点
                        // 计算 Shape1 的中点
                        Point2d startPoint = shape1.GetPoint2dAt(0);
                        Point2d endPoint = shape1.GetPoint2dAt(1);
                        Point3d shape1MidPoint = new Point3d(
                            (startPoint.X + endPoint.X) / 2,
                            (startPoint.Y + endPoint.Y) / 2,
                            (shapeThirdPoint.Z + shapeThirdPoint.Z) / 2 // 假设 Z 坐标一致
                        );
                        // 定义一个容差，用于判断中点是否与第三个端点相同
                        double tolerance = 0.001; // 可调整的容差值，单位为绘图单位
                        // 检查中点与第三个端点是否在容差范围内
                        if (shape1MidPoint.DistanceTo(shapeThirdPoint) < tolerance)
                        {
                            // 找到匹配的 Shape 和 Shape1，接下来寻找最近的 DBText
                            DBText nearestText = null;
                            double minDistance = double.MaxValue;
                            foreach (ObjectId textId in texts)
                            {
                                DBText text = tr.GetObject(textId, OpenMode.ForRead) as DBText;
                                if (text != null)
                                {
                                    double distance = shapeThirdPoint.DistanceTo(text.Position);
                                    if (distance < minDistance)
                                    {
                                        minDistance = distance;
                                        nearestText = text;
                                    }
                                }
                            }
                            // 如果找到最近的文字，添加到字典
                            if (nearestText != null)
                            {
                                var key = new Tuple<ObjectId, ObjectId>(shape.ObjectId, shape1.ObjectId);
                                tempDictionary[key] = nearestText.ObjectId;
                                texts.Remove(nearestText.ObjectId); // 移除已匹配的文字，避免重复
                            }
                            // 匹配成功后跳出 Shape1 循环，处理下一个 Shape
                            break;
                        }
                    }
                }
                // 提交事务，确保所有操作完成
                tr.Commit();
            }
            // 返回生成的字典
            return tempDictionary;
        }
    }
}