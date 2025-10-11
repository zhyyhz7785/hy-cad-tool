using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.HelpClass;
using PileSectionType = HyCADTool.HelpClass.PileSectionType;

namespace HyCADTool.Interfaces
{
    /// <summary>
    /// 区域工厂接口（适配原项目）
    /// 用于 PilePanel 的兼容性
    /// </summary>
    public interface IAreaFactory
    {
        StandardAreaBase CreateRectangularArea(Polyline polyline, PileSectionType sectionType, double diameterOrEdge, double scale);
        StandardAreaBase CreateCircularArea(Polyline polyline, PileSectionType sectionType, double diameterOrEdge, double scale);
    }
}

