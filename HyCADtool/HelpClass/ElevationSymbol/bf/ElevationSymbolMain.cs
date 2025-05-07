//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.EditorInput;
//using Autodesk.AutoCAD.Geometry;
//using System;
//using System.Collections.Generic;
//using HyCADTool.Tools;
//using static HyCADTool.Config.BaseConfig;
//using HyCADTool.Config;
//using Autodesk.AutoCAD.ApplicationServices;
//namespace HyCADTool.HelpClass.ElevationSymbol
//{
//    public partial class ElevationSymbol : DrawJig, IElevationSymbol
//    {
//        private ElevationGeometry _geometry;
//        private ElevationTextManager _textManager;
//        public Point3d BasePoint { get; private set; }
//        public double Scale { get; private set; }
//        public double D { get; private set; }
//        public string LayerName { get; private set; } = "00_hy_3公共_标注4_标高";
//        public ElevationSymbolState State { get; set; }
//        private Point3d _currentPoint;
//        private Editor _editor;
//        private ObjectId _textStyleId;
//        private ObjectId _layerId;
//        private double _angleDegrees;
//        private double _angleRadians;
//        // 静态缓存文本样式和图层 ID
//        private static ObjectId _cachedTextStyleId = ObjectId.Null;
//        private static ObjectId _cachedLayerId = ObjectId.Null;
//        public Point3d CurrentPoint => _currentPoint;
//        public static Dictionary<Tuple<ObjectId, ObjectId>, ObjectId> SymbolDictionary = new Dictionary<Tuple<ObjectId, ObjectId>, ObjectId>();
//        public ElevationSymbol(Point3d position, double scale, double d, Editor editor, double angleDegrees = 0.0, 
//            ElevationSymbolState state = ElevationSymbolState.Normal, bool isTextPosition = false)
//        {
//            Matrix3d ucsToWcs = editor.CurrentUserCoordinateSystem.Inverse();
//            Point3d wcsPosition = position.TransformBy(ucsToWcs);
//            Scale = scale;
//            D = d;
//            State = state;
//            _editor = editor;
//            _angleDegrees = angleDegrees;
//            UpdateRotation();
//            _currentPoint = isTextPosition ? wcsPosition : wcsPosition;
//            BasePoint = _currentPoint;
//            // 初始化或重用文本样式和图层
//            //_textStyleId = GetOrCreateTextStyle();
//            _textStyleId = EtGpt.CreateTextStyle(BaseConfig.TextStyleConfig.Name);
//            // _layerId = GetOrCreateLayer();
//            _layerId = EtGpt.CreateLayer(LayerName, 140);
//            // 初始化几何和文字管理器
//            _geometry = new ElevationGeometry(_currentPoint, Scale, D, _angleRadians, _layerId, State);
//            _textManager = new ElevationTextManager(_currentPoint, Scale, _layerId, _textStyleId, _angleRadians);
//            UpdateSymbol(0.0, true); // 初始化符号
//        }
//        private void UpdateRotation()
//        {
//            _angleRadians = _angleDegrees * Math.PI / 180.0;
//        }
//        public void UpdateSymbol(double elevation, bool isBasePoint = false)
//        {
//            // 显式传递 _currentPoint 确保同步
//            _geometry.UpdateGeometry(State, _currentPoint);
//            _textManager.UpdateText(_currentPoint, elevation);
//        }
//        public void SetLabelProperties(DBText sourceText)
//        {
//            _textManager.SetProperties(sourceText);
//        }
//        public void AddToDatabase(Transaction tr, BlockTableRecord btr)
//        {
//            Polyline shapeToAdd = _geometry.Shape.Clone() as Polyline;
//            Polyline shape1ToAdd = _geometry.Shape1.Clone() as Polyline;
//            DBText labelToAdd = _textManager.Label.Clone() as DBText;
//            btr.AppendEntity(shapeToAdd);
//            tr.AddNewlyCreatedDBObject(shapeToAdd, true);
//            btr.AppendEntity(shape1ToAdd);
//            tr.AddNewlyCreatedDBObject(shape1ToAdd, true);
//            btr.AppendEntity(labelToAdd);
//            tr.AddNewlyCreatedDBObject(labelToAdd, true);
//            _editor.WriteMessage($"\nShape Position: {shapeToAdd.GetPoint3dAt(2)}\nLabel Position: {labelToAdd.Position}\n");
//        }
//        public PromptResult StartJig(Editor editor)
//        {
//            return editor.Drag(this);
//        }
//        protected override SamplerStatus Sampler(JigPrompts prompts)
//        {
//            JigPromptPointOptions opts = new JigPromptPointOptions("\n选择标高点 [D-左右翻转/S-上下翻转/O-重选基点/SC-修改比例/DL-修改构造参数]: ");
//            opts.UserInputControls = UserInputControls.Accept3dCoordinates | UserInputControls.NoZeroResponseAccepted;
//            opts.Keywords.Add("D");
//            opts.Keywords.Add("S");
//            opts.Keywords.Add("O");
//            opts.Keywords.Add("SC");
//            opts.Keywords.Add("DL");
//            PromptPointResult res = prompts.AcquirePoint(opts);
//            if (res.Status == PromptStatus.OK)
//            {
//                Matrix3d ucsToWcs = _editor.CurrentUserCoordinateSystem.Inverse();
//                Point3d wcsPoint = res.Value.TransformBy(ucsToWcs);
//                if (_currentPoint == wcsPoint) return SamplerStatus.NoChange;
//                _currentPoint = wcsPoint;
//                double elevation = (_currentPoint.Y - BasePoint.Y);
//                UpdateSymbol(elevation, _currentPoint == BasePoint); // 同步更新
//                return SamplerStatus.OK;
//            }
//            else if (res.Status == PromptStatus.Keyword)
//            {
//                HandleKeyword(res.StringResult);
//                double elevation = (_currentPoint.Y - BasePoint.Y);
//                UpdateSymbol(elevation, _currentPoint == BasePoint); // 同步更新
//                return SamplerStatus.NoChange;
//            }
//            return SamplerStatus.Cancel;
//        }
//        protected override bool WorldDraw(Autodesk.AutoCAD.GraphicsInterface.WorldDraw draw)
//        {
//            // 确保只绘制当前对象
//            if (_geometry != null && _geometry.Shape != null)
//                draw.Geometry.Draw(_geometry.Shape);
//            if (_geometry != null && _geometry.Shape1 != null)
//                draw.Geometry.Draw(_geometry.Shape1);
//            if (_textManager != null && _textManager.Label != null)
//                draw.Geometry.Draw(_textManager.Label);
//            return true;
//        }
//        private void HandleKeyword(string keyword)
//        {
//            switch (keyword.ToLower())
//            {
//                case "d":
//                    switch (State)
//                    {
//                        case ElevationSymbolState.Normal:
//                            State = ElevationSymbolState.FlipHorizontal;
//                            break;
//                        case ElevationSymbolState.FlipVertical:
//                            State = ElevationSymbolState.FlipBoth;
//                            break;
//                        case ElevationSymbolState.FlipHorizontal:
//                            State = ElevationSymbolState.Normal;
//                            break;
//                        case ElevationSymbolState.FlipBoth:
//                            State = ElevationSymbolState.FlipVertical;
//                            break;
//                    }
//                    break;
//                case "s":
//                    switch (State)
//                    {
//                        case ElevationSymbolState.Normal:
//                            State = ElevationSymbolState.FlipVertical;
//                            break;
//                        case ElevationSymbolState.FlipHorizontal:
//                            State = ElevationSymbolState.FlipBoth;
//                            break;
//                        case ElevationSymbolState.FlipVertical:
//                            State = ElevationSymbolState.Normal;
//                            break;
//                        case ElevationSymbolState.FlipBoth:
//                            State = ElevationSymbolState.FlipHorizontal;
//                            break;
//                    }
//                    break;
//                case "o":
//                    PromptPointResult res = _editor.GetPoint("\n选择新的基点: ");
//                    if (res.Status == PromptStatus.OK)
//                    {
//                        Matrix3d ucsToWcs = _editor.CurrentUserCoordinateSystem.Inverse();
//                        BasePoint = res.Value.TransformBy(ucsToWcs);
//                        _currentPoint = BasePoint;
//                        UpdateSymbol(0.0, true);
//                    }
//                    break;
//                case "sc":
//                    PromptDoubleOptions pdo = new PromptDoubleOptions("\n输入比例 [默认50]: ");
//                    pdo.DefaultValue = BaseConfig.Scale;
//                    PromptDoubleResult pdr = _editor.GetDouble(pdo);
//                    if (pdr.Status == PromptStatus.OK && pdr.Value > 0)
//                    {
//                        Scale = pdr.Value;
//                        BaseConfig.Scale = Scale;
//                        UpdateSymbol((_currentPoint.Y - BasePoint.Y), _currentPoint == BasePoint);
//                    }
//                    break;
//                case "dl":
//                    PromptDoubleOptions dOpt = new PromptDoubleOptions("\n输入构造参数 d [默认10.0]: ");
//                    dOpt.DefaultValue = BaseConfig.ElevationLength;
//                    PromptDoubleResult dRes = _editor.GetDouble(dOpt);
//                    if (dRes.Status == PromptStatus.OK && dRes.Value > 0)
//                    {
//                        D = dRes.Value;
//                        BaseConfig.ElevationLength = D;
//                        UpdateSymbol((_currentPoint.Y - BasePoint.Y), _currentPoint == BasePoint);
//                    }
//                    break;
//            }
//        }
//    }
//}