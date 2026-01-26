using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions
{
    /// <summary>
    /// 类型名称转换器
    /// Type Name Converter
    /// </summary>
    public static class TypeNameConverter
    {
        // C#类型到中文名称的映射
        private static readonly Dictionary<string, string> TypeToChinese = new Dictionary<string, string>
        {
            { "Line", "直线" },
            { "Polyline", "多段线" },
            { "Circle", "圆" },
            { "Arc", "圆弧" },
            { "Ellipse", "椭圆" },
            { "Spline", "样条曲线" },
            { "DBText", "文字" },
            { "MText", "多行文字" },
            { "Hatch", "填充图案" },
            { "BlockReference", "块参照" },
            { "DBPoint", "点" },
            { "3DSOLID", "实体" },
            { "3DFACE", "面" },
            { "Ray", "射线" },
            { "Xline", "构造线" },
            { "DIMENSION", "标注" },
            { "AlignedDimension", "标注" },
            { "RotatedDimension", "标注" },
            { "RadialDimension", "标注" },
            { "DiametricDimension", "标注" },
            { "LineAngularDimension2", "标注" },
            { "DIMENSION_ORDINATE", "标注" },
            { "Leader", "引线" },
            { "MLeader", "多重引线" },
            { "Viewport", "视口" },
            { "Attdef", "属性定义" },
            { "Attrib", "属性参照" },
            { "POLYFACE_MESH", "多面体网格" },
            { "SUBD_MESH", "细分网格" },
            { "Region", "区域" },
            { "BODY", "体" },
            { "SURFACE", "曲面" },
            { "NURBS_SURFACE", "NURBS曲面" },
            { "TABLE", "表格" },
            { "WIPEOUT", "擦除" }
        };

        // 中文名称到DXF名称的映射
        private static readonly Dictionary<string, string> ChineseToDxf = new Dictionary<string, string>
        {
            { "直线", "LINE" },
            { "多段线", "LWPOLYLINE" },
            { "圆", "CIRCLE" },
            { "圆弧", "ARC" },
            { "椭圆", "ELLIPSE" },
            { "样条曲线", "SPLINE" },
            { "文字", "TEXT" },
            { "多行文字", "MTEXT" },
            { "填充图案", "HATCH" },
            { "块参照", "INSERT" },
            { "点", "POINT" },
            { "实体", "3DSOLID" },
            { "面", "3DFACE" },
            { "射线", "RAY" },
            { "构造线", "XLINE" },
            { "标注", "DIMENSION" },
            { "对齐标注", "DIMENSION" },
            { "旋转标注", "DIMENSION" },
            { "半径标注", "DIMENSION" },
            { "直径标注", "DIMENSION" },
            { "角度标注", "DIMENSION" },
            { "坐标标注", "DIMENSION" },
            { "引线", "LEADER" },
            { "多重引线", "MULTILEADER" },
            { "视口", "VIEWPORT" },
            { "属性定义", "ATTDEF" },
            { "属性参照", "ATTRIB" },
            { "多面体网格", "POLYFACE_MESH" },
            { "细分网格", "SUBD_MESH" },
            { "区域", "REGION" },
            { "体", "BODY" },
            { "曲面", "SURFACE" },
            { "NURBS曲面", "NURBS_SURFACE" },
            { "表格", "TABLE" },
            { "擦除", "WIPEOUT" }
        };

        /// <summary>
        /// 将C#类型名转换为中文名称
        /// Convert C# type name to Chinese name
        /// </summary>
        public static string ToChinese(string typeName)
        {
            return TypeToChinese.ContainsKey(typeName) ? TypeToChinese[typeName] : typeName;
        }

        /// <summary>
        /// 将中文名称转换为DXF名称
        /// Convert Chinese name to DXF name
        /// </summary>
        public static string ToDxfName(string chineseName)
        {
            return ChineseToDxf.ContainsKey(chineseName) ? ChineseToDxf[chineseName] : chineseName;
        }

        /// <summary>
        /// 直接将C#类型名转换为DXF名称
        /// Convert C# type name to DXF name directly
        /// </summary>
        public static string TypeToDxfName(string typeName)
        {
            var chineseName = ToChinese(typeName);
            return ToDxfName(chineseName);
        }
    }
}

