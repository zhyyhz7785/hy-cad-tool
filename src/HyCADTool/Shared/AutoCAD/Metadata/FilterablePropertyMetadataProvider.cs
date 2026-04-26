using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Metadata
{
    /// <summary>
    /// 可筛选属性元数据提供者
    /// 从旧项目 HyCADtool/Config/FilterablePropertyMetadataProvider.cs 迁移
    /// 提供各种 AutoCAD 实体类型的可筛选属性列表
    /// </summary>
    public static class FilterablePropertyMetadataProvider
    {
        /// <summary>
        /// 获取所有实体类型的可筛选属性元数据列表
        /// </summary>
        public static List<PropertyMetadata> GetMetadataList()
        {
            return new List<PropertyMetadata>
            {
                // ===== 样式类属性 =====
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "DimensionStyleName", PropertyType = "string", DisplayName = "标注样式", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "DimensionStyleName", PropertyType = "string", DisplayName = "标注样式", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "DBText", PropertyName = "TextStyleName", PropertyType = "string", DisplayName = "文字样式", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "TextStyleName", PropertyType = "string", DisplayName = "文字样式", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "MLeader", PropertyName = "BlockName", PropertyType = "string", DisplayName = "引线样式", Category = "样式类属性" },

                // ===== Arc 属性 =====
                new PropertyMetadata { EntityType = "Arc", PropertyName = "Area", PropertyType = "double", DisplayName = "面积", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Arc", PropertyName = "Center", PropertyType = "Point3d", DisplayName = "圆心", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Arc", PropertyName = "EndAngle", PropertyType = "double", DisplayName = "终止角度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Arc", PropertyName = "EndParam", PropertyType = "double", DisplayName = "终止参数", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Arc", PropertyName = "EndPoint", PropertyType = "Point3d", DisplayName = "终止点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Arc", PropertyName = "Length", PropertyType = "double", DisplayName = "长度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Arc", PropertyName = "Radius", PropertyType = "double", DisplayName = "半径", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Arc", PropertyName = "StartAngle", PropertyType = "double", DisplayName = "起始角度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Arc", PropertyName = "StartParam", PropertyType = "double", DisplayName = "起始参数", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Arc", PropertyName = "StartPoint", PropertyType = "Point3d", DisplayName = "起始点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Arc", PropertyName = "Thickness", PropertyType = "double", DisplayName = "厚度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Arc", PropertyName = "TotalAngle", PropertyType = "double", DisplayName = "总角度", Category = "几何属性" },

                // ===== BlockReference 属性 =====
                new PropertyMetadata { EntityType = "BlockReference", PropertyName = "ColorIndex", PropertyType = "Int32", DisplayName = "颜色索引", Category = "其他属性" },
                new PropertyMetadata { EntityType = "BlockReference", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "BlockReference", PropertyName = "Position", PropertyType = "Point3d", DisplayName = "位置", Category = "几何属性" },
                new PropertyMetadata { EntityType = "BlockReference", PropertyName = "Rotation", PropertyType = "double", DisplayName = "旋转角度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "BlockReference", PropertyName = "UnitFactor", PropertyType = "double", DisplayName = "单位因子", Category = "其他属性" },

                // ===== Circle 属性 =====
                new PropertyMetadata { EntityType = "Circle", PropertyName = "Area", PropertyType = "double", DisplayName = "面积", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Circle", PropertyName = "Center", PropertyType = "Point3d", DisplayName = "圆心", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Circle", PropertyName = "Circumference", PropertyType = "double", DisplayName = "周长", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Circle", PropertyName = "Diameter", PropertyType = "double", DisplayName = "直径", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Circle", PropertyName = "EndParam", PropertyType = "double", DisplayName = "终止参数", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Circle", PropertyName = "EndPoint", PropertyType = "Point3d", DisplayName = "终止点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Circle", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "Circle", PropertyName = "Radius", PropertyType = "double", DisplayName = "半径", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Circle", PropertyName = "StartParam", PropertyType = "double", DisplayName = "起始参数", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Circle", PropertyName = "StartPoint", PropertyType = "Point3d", DisplayName = "起始点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Circle", PropertyName = "Thickness", PropertyType = "double", DisplayName = "厚度", Category = "几何属性" },

                // ===== DBText 属性 =====
                new PropertyMetadata { EntityType = "DBText", PropertyName = "AlignmentPoint", PropertyType = "Point3d", DisplayName = "对齐点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "DBText", PropertyName = "Height", PropertyType = "double", DisplayName = "文字高度", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "DBText", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "DBText", PropertyName = "Oblique", PropertyType = "double", DisplayName = "倾斜角度", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "DBText", PropertyName = "Position", PropertyType = "Point3d", DisplayName = "位置", Category = "几何属性" },
                new PropertyMetadata { EntityType = "DBText", PropertyName = "Rotation", PropertyType = "double", DisplayName = "旋转角度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "DBText", PropertyName = "Thickness", PropertyType = "double", DisplayName = "厚度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "DBText", PropertyName = "WidthFactor", PropertyType = "double", DisplayName = "宽度因子", Category = "样式类属性" },

                // ===== Hatch 属性 =====
                new PropertyMetadata { EntityType = "Hatch", PropertyName = "Area", PropertyType = "double", DisplayName = "面积", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Hatch", PropertyName = "Elevation", PropertyType = "double", DisplayName = "高程", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Hatch", PropertyName = "GradientAngle", PropertyType = "double", DisplayName = "渐变角度", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "Hatch", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "Hatch", PropertyName = "NumberOfHatchLines", PropertyType = "Int32", DisplayName = "填充线数量", Category = "其他属性" },
                new PropertyMetadata { EntityType = "Hatch", PropertyName = "NumberOfLoops", PropertyType = "Int32", DisplayName = "循环数量", Category = "其他属性" },
                new PropertyMetadata { EntityType = "Hatch", PropertyName = "NumberOfPatternDefinitions", PropertyType = "Int32", DisplayName = "图案定义数量", Category = "其他属性" },
                new PropertyMetadata { EntityType = "Hatch", PropertyName = "PatternAngle", PropertyType = "double", DisplayName = "图案角度", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "Hatch", PropertyName = "PatternScale", PropertyType = "double", DisplayName = "图案比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "Hatch", PropertyName = "PatternSpace", PropertyType = "double", DisplayName = "图案间距", Category = "样式类属性" },

                // ===== Line 属性 =====
                new PropertyMetadata { EntityType = "Line", PropertyName = "Angle", PropertyType = "double", DisplayName = "角度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Line", PropertyName = "Area", PropertyType = "double", DisplayName = "面积", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Line", PropertyName = "EndParam", PropertyType = "double", DisplayName = "终止参数", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Line", PropertyName = "EndPoint", PropertyType = "Point3d", DisplayName = "终止点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Line", PropertyName = "Length", PropertyType = "double", DisplayName = "长度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Line", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "Line", PropertyName = "StartParam", PropertyType = "double", DisplayName = "起始参数", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Line", PropertyName = "StartPoint", PropertyType = "Point3d", DisplayName = "起始点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Line", PropertyName = "Thickness", PropertyType = "double", DisplayName = "厚度", Category = "几何属性" },

                // ===== LineAngularDimension2 属性 =====
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "ArcPoint", PropertyType = "Point3d", DisplayName = "弧点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "CenterMarkSize", PropertyType = "double", DisplayName = "中心标记尺寸", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "Elevation", PropertyType = "double", DisplayName = "高程", Category = "几何属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "HorizontalRotation", PropertyType = "double", DisplayName = "水平旋转", Category = "几何属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "Measurement", PropertyType = "double", DisplayName = "测量值", Category = "几何属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "TextLineSpacingFactor", PropertyType = "double", DisplayName = "文本行间距因子", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "TextPosition", PropertyType = "Point3d", DisplayName = "文本位置", Category = "几何属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "TextRotation", PropertyType = "double", DisplayName = "文本旋转", Category = "几何属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "XLine1End", PropertyType = "Point3d", DisplayName = "第一线终止点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "XLine1Start", PropertyType = "Point3d", DisplayName = "第一线起始点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "XLine2End", PropertyType = "Point3d", DisplayName = "第二线终止点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "LineAngularDimension2", PropertyName = "XLine2Start", PropertyType = "Point3d", DisplayName = "第二线起始点", Category = "几何属性" },

                // ===== MLeader 属性 =====
                new PropertyMetadata { EntityType = "MLeader", PropertyName = "ArrowSize", PropertyType = "double", DisplayName = "箭头尺寸", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "MLeader", PropertyName = "BlockRotation", PropertyType = "double", DisplayName = "块旋转", Category = "几何属性" },
                new PropertyMetadata { EntityType = "MLeader", PropertyName = "DoglegLength", PropertyType = "double", DisplayName = "折线长度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "MLeader", PropertyName = "LandingGap", PropertyType = "double", DisplayName = "着陆间隙", Category = "几何属性" },
                new PropertyMetadata { EntityType = "MLeader", PropertyName = "LeaderCount", PropertyType = "Int32", DisplayName = "引线数量", Category = "其他属性" },
                new PropertyMetadata { EntityType = "MLeader", PropertyName = "LeaderLineCount", PropertyType = "Int32", DisplayName = "引线段数量", Category = "其他属性" },
                new PropertyMetadata { EntityType = "MLeader", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "MLeader", PropertyName = "Scale", PropertyType = "double", DisplayName = "比例", Category = "几何属性" },
                new PropertyMetadata { EntityType = "MLeader", PropertyName = "TextHeight", PropertyType = "double", DisplayName = "文字高度", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "MLeader", PropertyName = "TextLocation", PropertyType = "Point3d", DisplayName = "文字位置", Category = "几何属性" },

                // ===== MText 属性 =====
                new PropertyMetadata { EntityType = "MText", PropertyName = "ActualHeight", PropertyType = "double", DisplayName = "实际高度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "ActualWidth", PropertyType = "double", DisplayName = "实际宽度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "Ascent", PropertyType = "double", DisplayName = "上行高度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "Descent", PropertyType = "double", DisplayName = "下行高度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "Height", PropertyType = "double", DisplayName = "文字高度", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "LineSpaceDistance", PropertyType = "double", DisplayName = "行间距", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "LineSpacingFactor", PropertyType = "double", DisplayName = "行间距因子", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "Location", PropertyType = "Point3d", DisplayName = "位置", Category = "几何属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "Rotation", PropertyType = "double", DisplayName = "旋转角度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "TextHeight", PropertyType = "double", DisplayName = "文本高度", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "MText", PropertyName = "Width", PropertyType = "double", DisplayName = "宽度", Category = "几何属性" },

                // ===== Polyline 属性 =====
                new PropertyMetadata { EntityType = "Polyline", PropertyName = "Area", PropertyType = "double", DisplayName = "面积", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Polyline", PropertyName = "ConstantWidth", PropertyType = "double", DisplayName = "固定宽度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Polyline", PropertyName = "Elevation", PropertyType = "double", DisplayName = "高程", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Polyline", PropertyName = "EndParam", PropertyType = "double", DisplayName = "终止参数", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Polyline", PropertyName = "EndPoint", PropertyType = "Point3d", DisplayName = "终止点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Polyline", PropertyName = "Length", PropertyType = "double", DisplayName = "长度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Polyline", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "Polyline", PropertyName = "NumberOfVertices", PropertyType = "Int32", DisplayName = "顶点数量", Category = "其他属性" },
                new PropertyMetadata { EntityType = "Polyline", PropertyName = "StartParam", PropertyType = "double", DisplayName = "起始参数", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Polyline", PropertyName = "StartPoint", PropertyType = "Point3d", DisplayName = "起始点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Polyline", PropertyName = "Thickness", PropertyType = "double", DisplayName = "厚度", Category = "几何属性" },

                // ===== RotatedDimension 属性 =====
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "Elevation", PropertyType = "double", DisplayName = "高程", Category = "几何属性" },
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "HorizontalRotation", PropertyType = "double", DisplayName = "水平旋转", Category = "几何属性" },
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "Measurement", PropertyType = "double", DisplayName = "测量值", Category = "几何属性" },
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "Oblique", PropertyType = "double", DisplayName = "倾斜角度", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "Rotation", PropertyType = "double", DisplayName = "旋转角度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "TextLineSpacingFactor", PropertyType = "double", DisplayName = "文本行间距因子", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "TextPosition", PropertyType = "Point3d", DisplayName = "文本位置", Category = "几何属性" },
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "TextRotation", PropertyType = "double", DisplayName = "文本旋转", Category = "几何属性" },
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "XLine1Point", PropertyType = "Point3d", DisplayName = "第一线点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "RotatedDimension", PropertyName = "XLine2Point", PropertyType = "Point3d", DisplayName = "第二线点", Category = "几何属性" },

                // ===== Spline 属性 =====
                new PropertyMetadata { EntityType = "Spline", PropertyName = "Area", PropertyType = "double", DisplayName = "面积", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Spline", PropertyName = "Degree", PropertyType = "Int32", DisplayName = "样条曲线度数", Category = "其他属性" },
                new PropertyMetadata { EntityType = "Spline", PropertyName = "EndParam", PropertyType = "double", DisplayName = "终止参数", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Spline", PropertyName = "EndPoint", PropertyType = "Point3d", DisplayName = "终止点", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Spline", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "Spline", PropertyName = "NumControlPoints", PropertyType = "Int32", DisplayName = "控制点数量", Category = "其他属性" },
                new PropertyMetadata { EntityType = "Spline", PropertyName = "NumFitPoints", PropertyType = "Int32", DisplayName = "拟合点数量", Category = "其他属性" },
                new PropertyMetadata { EntityType = "Spline", PropertyName = "StartParam", PropertyType = "double", DisplayName = "起始参数", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Spline", PropertyName = "StartPoint", PropertyType = "Point3d", DisplayName = "起始点", Category = "几何属性" },

                // ===== Wipeout 属性 =====
                new PropertyMetadata { EntityType = "Wipeout", PropertyName = "ColorIndex", PropertyType = "Int32", DisplayName = "颜色索引", Category = "其他属性" },
                new PropertyMetadata { EntityType = "Wipeout", PropertyName = "Height", PropertyType = "double", DisplayName = "高度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Wipeout", PropertyName = "ImageHeight", PropertyType = "double", DisplayName = "图像高度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Wipeout", PropertyName = "ImageWidth", PropertyType = "double", DisplayName = "图像宽度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Wipeout", PropertyName = "LinetypeScale", PropertyType = "double", DisplayName = "线型比例", Category = "样式类属性" },
                new PropertyMetadata { EntityType = "Wipeout", PropertyName = "Position", PropertyType = "Point3d", DisplayName = "位置", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Wipeout", PropertyName = "Rotation", PropertyType = "double", DisplayName = "旋转角度", Category = "几何属性" },
                new PropertyMetadata { EntityType = "Wipeout", PropertyName = "Width", PropertyType = "double", DisplayName = "宽度", Category = "几何属性" }
            };
        }
    }
}
