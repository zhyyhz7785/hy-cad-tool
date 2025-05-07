using System.Collections.Generic;
namespace HyCADTool.HelpClass
{
    public static class TypeConverter
    {
        // DXF标签到中文名称的映射
        private static readonly Dictionary<string, string> typeToChinese = new Dictionary<string, string>
        {
            { "Line", "直线" },   //确认
            { "Polyline", "多段线" }, //确认
            { "Circle", "圆" }, //确认
            { "Arc", "圆弧" },  //确认
            { "Ellipse", "椭圆" }, //确认
            { "Spline", "样条曲线" },//确认
            { "DBText", "文字" },//确认
            { "MText", "多行文字" },//确认
            { "Hatch", "填充图案" },//确认
            { "BlockReference", "块参照" },//确认
            { "DBPoint", "点" },//确认
            { "3DSOLID", "实体" },
            { "3DFACE", "面" },
            { "Ray", "射线" },
            { "Xline", "构造线" },
            { "DIMENSION", "标注" },
            //{ "AlignedDimension", "对齐标注" },
            //{ "RotatedDimension", "旋转标注" },
            //{ "RadialDimension", "半径标注" },
            //{ "DiametricDimension", "直径标注" },
            //{ "", "角度标注" },
            //{ "DIMENSION_ORDINATE", "坐标标注" },
            { "AlignedDimension", "标注" },//确认
            { "RotatedDimension", "标注" },//确认
            { "RadialDimension", "标注" },//确认
            { "DiametricDimension", "标注" },//确认
            { "LineAngularDimension2", "标注" },//确认
            { "DIMENSION_ORDINATE", "标注" },//确认
            { "Leader", "引线" },
            { "MLeader", "多重引线" },//确认
            { "Viewport", "视口" },
            { "Attdef", "属性定义" },
            { "Attrib", "属性参照" },
            { "POLYFACE_MESH", "多面体网格" },
            { "SUBD_MESH", "细分网格" },
            { "Region", "区域" },//确认
            { "BODY", "体" },
            { "SURFACE", "曲面" },
            { "NURBS_SURFACE", "NURBS曲面" },
            { "TABLE", "表格" },
            { "WIPEOUT", "擦除" }
            // 根据需要添加更多类型
        };
        // 中文名称到DXF标签的映射
        private static readonly Dictionary<string, string> chineseToType = new Dictionary<string, string>
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
            // 根据需要添加更多类型
        };
        public static string ToChinese(this string type)
        {
            return typeToChinese.ContainsKey(type) ? typeToChinese[type] : type;
        }
        public static string ToType(this string chinese)
        {
            return chineseToType.ContainsKey(chinese) ? chineseToType[chinese] : chinese;
        }
    }
}
