//using Autodesk.AutoCAD.DatabaseServices;
//using Autodesk.AutoCAD.Geometry;
//using System;
//using static HyCADTool.Config.BaseConfig;
//namespace HyCADTool.HelpClass.ElevationSymbol
//{
//    public class ElevationTextManager
//    {
//        public DBText Label { get; private set; }
//        private double _scale;
//        private ObjectId _layerId;
//        private ObjectId _textStyleId;
//        private double _angleRadians;
//        public ElevationTextManager(Point3d position, double scale, ObjectId layerId, ObjectId textStyleId, double angleRadians)
//        {
//            _scale = scale;
//            _layerId = layerId;
//            _textStyleId = textStyleId;
//            _angleRadians = angleRadians;
//            UpdateText(position, 0.0); // 初始化文字
//        }
//        public void UpdateText(Point3d position, double elevation)
//        {
//            string elevationText = Math.Abs(elevation) < 0.001 ? $"±{elevation:F3}" : $"{elevation:F3}";
//            Label = new DBText
//            {
//                LayerId = _layerId,
//                TextStyleId = _textStyleId,
//                Position = position, // 确保使用传入的 position
//                Height = TextStyleConfig.TextSize * _scale,
//                WidthFactor = 0.7,
//                TextString = elevationText,
//                HorizontalMode = TextHorizontalMode.TextCenter,
//                VerticalMode = TextVerticalMode.TextVerticalMid,
//                AlignmentPoint = position,
//                Rotation = _angleRadians
//            };
//        }
//        public void SetProperties(DBText sourceText)
//        {
//            if (Label != null && sourceText != null)
//            {
//                Label.Height = sourceText.Height;
//                Label.WidthFactor = sourceText.WidthFactor;
//                Label.TextStyleId = sourceText.TextStyleId;
//                Label.HorizontalMode = sourceText.HorizontalMode;
//                Label.VerticalMode = sourceText.VerticalMode;
//            }
//        }
//    }
//}