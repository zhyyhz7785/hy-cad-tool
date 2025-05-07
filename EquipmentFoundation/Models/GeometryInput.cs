using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;
namespace EquipmentFoundation.Models
{
    public class GeometryInput
    {
        public List<Polyline> OuterContours { get; set; } // 外轮廓多边形
        public List<Polyline> InnerPolygons { get; set; } // 内多边形
        public Dictionary<Polyline, double> Elevations { get; set; } // 标高字典
        public List<Circle> Bolts { get; set; } // 螺栓（圆形对象）

        public GeometryInput()
        {
            OuterContours = new List<Polyline>();
            InnerPolygons = new List<Polyline>();
            Elevations = new Dictionary<Polyline, double>();
            Bolts = new List<Circle>();
        }
    }
}